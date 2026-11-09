using HealthReminder.Models;

namespace HealthReminder.Services;

public enum ReminderPauseReason
{
    User,
    DoNotDisturb,
    Temporary
}

/// <summary>單一提醒的期限與暫停狀態，由 ReminderService 統一巡檢。</summary>
public sealed class ReminderTimer
{
    private readonly TimeProvider _clock;
    private readonly HashSet<ReminderPauseReason> _pauseReasons = new();
    private DateTimeOffset _nextTriggerUtc;
    private TimeSpan _remainingWhenPaused;
    private TimeSpan _maximumIntervalRemaining;

    public ReminderItem Item { get; private set; }
    public bool IsPaused => _pauseReasons.Count > 0;
    public DateTime NextTriggerTime => TimeZoneInfo.ConvertTime(_nextTriggerUtc, _clock.LocalTimeZone).DateTime;
    public TimeSpan RemainingTime => IsPaused ? _remainingWhenPaused
        : MaxZero(_nextTriggerUtc - _clock.GetUtcNow());

    public ReminderTimer(ReminderItem item, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        Item = item.Copy();
        ResetTimer();
    }

    internal void ResetTimer()
    {
        _maximumIntervalRemaining = TimeSpan.FromMinutes(Item.IntervalMinutes);
        _nextTriggerUtc = Item.ScheduleKind == ReminderScheduleKind.OneTime
            ? GetOneTimeDeadline()
            : _clock.GetUtcNow().Add(_maximumIntervalRemaining);
        _remainingWhenPaused = MaxZero(_nextTriggerUtc - _clock.GetUtcNow());
    }

    internal bool UpdateItem(ReminderItem item)
    {
        var changed = Item.IsEnabled != item.IsEnabled || Item.ScheduleKind != item.ScheduleKind
            || (item.ScheduleKind == ReminderScheduleKind.OneTime
                ? Item.OneTimeAtLocal != item.OneTimeAtLocal
                : Item.IntervalMinutes != item.IntervalMinutes);
        Item = item.Copy();
        if (changed) ResetTimer();
        return changed;
    }

    public void Snooze(int minutes)
    {
        _nextTriggerUtc = _clock.GetUtcNow().AddMinutes(minutes);
        _remainingWhenPaused = TimeSpan.FromMinutes(minutes);
        _maximumIntervalRemaining = _remainingWhenPaused;
        Item.IsEnabled = true;
    }

    internal void RetryPresentation()
    {
        _nextTriggerUtc = _clock.GetUtcNow().AddMinutes(1);
        _remainingWhenPaused = TimeSpan.FromMinutes(1);
    }

    public void Start()
    {
        _pauseReasons.Clear();
        ResetTimer();
    }

    public void Pause(ReminderPauseReason reason = ReminderPauseReason.User)
    {
        if (!IsPaused) _remainingWhenPaused = RemainingTime;
        _pauseReasons.Add(reason);
    }

    public void Resume(ReminderPauseReason reason = ReminderPauseReason.User)
    {
        if (!_pauseReasons.Remove(reason) || IsPaused) return;
        _nextTriggerUtc = Item.ScheduleKind == ReminderScheduleKind.OneTime
            ? GetOneTimeDeadline()
            : _clock.GetUtcNow().Add(_remainingWhenPaused);
    }

    internal void RecalibrateAfterSystemChange()
    {
        if (IsPaused) return;
        if (Item.ScheduleKind == ReminderScheduleKind.OneTime)
        {
            _nextTriggerUtc = GetOneTimeDeadline();
        }
        else if (RemainingTime > _maximumIntervalRemaining)
        {
            _nextTriggerUtc = _clock.GetUtcNow().Add(_maximumIntervalRemaining);
        }
    }

    internal static bool IsInQuietHours(TimeSpan now, TimeSpan start, TimeSpan end) =>
        start < end ? now >= start && now < end : start > end && (now >= start || now < end);

    private DateTimeOffset GetOneTimeDeadline()
    {
        var local = DateTime.SpecifyKind(Item.OneTimeAtLocal ?? _clock.GetLocalNow().DateTime, DateTimeKind.Unspecified);
        var zone = _clock.LocalTimeZone;
        for (var minute = 0; minute < 120 && zone.IsInvalidTime(local); minute++)
        {
            local = local.AddMinutes(1);
        }

        try { return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero); }
        catch (ArgumentException) { return _clock.GetUtcNow(); }
    }

    private static TimeSpan MaxZero(TimeSpan time) => time > TimeSpan.Zero ? time : TimeSpan.Zero;

}

public sealed class ReminderTriggeredEventArgs : EventArgs
{
    public ReminderTriggeredEventArgs(ReminderItem item, string occurrenceId)
    {
        Item = item;
        OccurrenceId = occurrenceId;
    }

    public ReminderItem Item { get; }
    public string OccurrenceId { get; }
}

/// <summary>排程與使用者操作都在 WinForms UI 執行緒處理。</summary>
public sealed class ReminderService : IDisposable
{
    private readonly SettingsManager _storage;
    private readonly TimeProvider _clock;
    private readonly List<ReminderTimer> _timers = new();
    private readonly Dictionary<string, string> _pendingOneTime = new();
    private readonly System.Windows.Forms.Timer _timer;

    public event EventHandler<ReminderTriggeredEventArgs>? OnReminderTriggered;
    public event EventHandler? OnCountdownTick;
    public bool IsInDoNotDisturbMode { get; private set; }
    public bool IsPausedAll { get; private set; }
    public DateTimeOffset? TemporaryPauseUntilUtc { get; private set; }
    public bool IsTemporarilyPaused => TemporaryPauseUntilUtc > _clock.GetUtcNow();
    public DateTime? TemporaryPauseUntilLocal => TemporaryPauseUntilUtc is { } until
        ? TimeZoneInfo.ConvertTime(until, _clock.LocalTimeZone).DateTime : null;
    public bool CanShowNotifications => !IsPausedAll && !IsTemporarilyPaused && !IsInDoNotDisturbMode;
    public string? LastError { get; private set; }

    public ReminderService() : this(SettingsManager.Instance, TimeProvider.System) { }

    internal ReminderService(SettingsManager storage, TimeProvider clock)
    {
        _storage = storage;
        _clock = clock;
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => Pulse();
    }

    public void Initialize()
    {
        Reload();
        _timer.Start();
    }

    internal void Pulse()
    {
        RefreshPauseState();
        foreach (var timer in _timers.ToArray())
        {
            if (timer.IsPaused || !timer.Item.IsEnabled || timer.RemainingTime > TimeSpan.Zero) continue;
            var occurrenceId = Guid.NewGuid().ToString();
            if (timer.Item.ScheduleKind == ReminderScheduleKind.OneTime)
            {
                if (!_pendingOneTime.TryAdd(timer.Item.Id, occurrenceId)) continue;
            }
            else
            {
                timer.ResetTimer();
            }

            OnReminderTriggered?.Invoke(this, new ReminderTriggeredEventArgs(timer.Item.Copy(), occurrenceId));
        }
        OnCountdownTick?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<ReminderTimer> GetTimers() => _timers.AsReadOnly();

    public ReminderTimer? GetTimerById(string id) => _timers.Find(timer => timer.Item.Id == id);

    internal bool RecordShown(ReminderItem item, string occurrenceId)
    {
        LastError = null;
        if (_storage.ContainsRecord(occurrenceId)) return true;

        var timer = GetTimerById(item.Id);
        if (timer == null || !timer.Item.IsEnabled ||
            (item.ScheduleKind == ReminderScheduleKind.OneTime
                && _pendingOneTime.GetValueOrDefault(item.Id) != occurrenceId))
        {
            LastError = "此提醒的排程已變更。";
            return false;
        }
        if (!_storage.RecordShown(item, occurrenceId)) return StorageFailed();

        if (item.ScheduleKind == ReminderScheduleKind.OneTime)
        {
            _pendingOneTime.Remove(item.Id);
            timer.UpdateItem(_storage.Settings.Reminders.Single(reminder => reminder.Id == item.Id));
        }
        return true;
    }

    internal void ReleaseUnrecordedPresentation(string id, string occurrenceId)
    {
        if (_pendingOneTime.GetValueOrDefault(id) != occurrenceId) return;
        _pendingOneTime.Remove(id);
        GetTimerById(id)?.RetryPresentation();
    }

    internal bool Snooze(ReminderItem presentedItem, string occurrenceId, int minutes)
    {
        LastError = null;
        var id = presentedItem.Id;
        var timer = GetTimerById(id);
        if (timer == null || !_storage.HasTriggeredRecord(id, occurrenceId))
        {
            LastError = "找不到可稍後的提醒，排程未變更。";
            return false;
        }

        // 已顯示的通知可能比目前設定舊。只延後它原本指向的排程。
        var current = timer.Item;
        if (current.ScheduleKind != presentedItem.ScheduleKind ||
            (current.ScheduleKind == ReminderScheduleKind.OneTime
                ? current.OneTimeAtLocal != presentedItem.OneTimeAtLocal
                : !current.IsEnabled || current.IntervalMinutes != presentedItem.IntervalMinutes))
        {
            LastError = "此提醒的排程已變更，無法從這張通知延後。";
            return false;
        }

        AppSettings? candidate = null;
        if (timer.Item.ScheduleKind == ReminderScheduleKind.OneTime)
        {
            candidate = _storage.Settings.Copy();
            var item = candidate.Reminders.Single(reminder => reminder.Id == id);
            item.OneTimeAtLocal = TimeZoneInfo.ConvertTime(_clock.GetUtcNow().AddMinutes(minutes),
                _clock.LocalTimeZone).DateTime;
            item.IsEnabled = true;
        }
        if (!_storage.UpdateRecordStatus(occurrenceId, ReminderStatus.Snoozed, candidate))
        {
            return StorageFailed();
        }

        if (candidate != null)
        {
            timer.UpdateItem(candidate.Reminders.Single(reminder => reminder.Id == id));
        }
        RefreshPauseState();
        timer.Snooze(minutes);
        return true;
    }

    public bool MarkCompleted(string occurrenceId) => SetStatus(occurrenceId, ReminderStatus.Completed);
    public bool Dismiss(string occurrenceId) => SetStatus(occurrenceId, ReminderStatus.Dismissed);
    public bool MarkMissed(string occurrenceId) => SetStatus(occurrenceId, ReminderStatus.Missed);

    private bool SetStatus(string occurrenceId, ReminderStatus status)
    {
        LastError = null;
        return _storage.UpdateRecordStatus(occurrenceId, status) || StorageFailed();
    }

    private bool StorageFailed()
    {
        LastError = _storage.LastError;
        return false;
    }

    public void PauseAll()
    {
        IsPausedAll = true;
        RefreshPauseState();
    }

    public void ResumeAll()
    {
        IsPausedAll = false;
        RefreshPauseState();
    }

    public void PauseUntil(DateTimeOffset resumeAtUtc)
    {
        TemporaryPauseUntilUtc = resumeAtUtc;
        RefreshPauseState();
        OnCountdownTick?.Invoke(this, EventArgs.Empty);
    }

    public void PauseFor(TimeSpan duration) => PauseUntil(_clock.GetUtcNow().Add(duration));

    public void PauseUntilEndOfDay()
    {
        var tomorrow = _clock.GetLocalNow().Date.AddDays(1);
        PauseUntil(TimeZoneInfo.ConvertTimeToUtc(tomorrow, _clock.LocalTimeZone));
    }

    public void ResumeTemporaryPause()
    {
        TemporaryPauseUntilUtc = null;
        RefreshPauseState();
        OnCountdownTick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>回傳因排程變更或刪除而需取消等待通知的提醒 ID。</summary>
    public IReadOnlySet<string> Reload()
    {
        var previous = _timers.ToDictionary(timer => timer.Item.Id);
        var invalidated = new HashSet<string>();
        _timers.Clear();
        foreach (var item in _storage.Settings.Reminders)
        {
            if (previous.Remove(item.Id, out var existing))
            {
                if (existing.UpdateItem(item)) invalidated.Add(item.Id);
                _timers.Add(existing);
            }
            else
            {
                _timers.Add(new ReminderTimer(item, _clock));
            }
        }
        invalidated.UnionWith(previous.Keys);
        foreach (var id in invalidated) _pendingOneTime.Remove(id);
        RefreshPauseState();
        return invalidated;
    }

    public void RecalibrateAfterSystemChange()
    {
        RefreshPauseState();
        foreach (var timer in _timers) timer.RecalibrateAfterSystemChange();
        OnCountdownTick?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshPauseState()
    {
        if (!IsTemporarilyPaused) TemporaryPauseUntilUtc = null;
        var settings = _storage.Settings;
        IsInDoNotDisturbMode = settings.DoNotDisturbEnabled
            && ReminderTimer.IsInQuietHours(_clock.GetLocalNow().TimeOfDay,
                settings.DoNotDisturbStart, settings.DoNotDisturbEnd);

        foreach (var timer in _timers)
        {
            // 先加入正在生效的原因，再解除過期原因，避免中間短暫恢復。
            if (IsPausedAll) timer.Pause(ReminderPauseReason.User);
            if (IsTemporarilyPaused) timer.Pause(ReminderPauseReason.Temporary);
            if (IsInDoNotDisturbMode) timer.Pause(ReminderPauseReason.DoNotDisturb);
            if (!IsPausedAll) timer.Resume(ReminderPauseReason.User);
            if (!IsTemporarilyPaused) timer.Resume(ReminderPauseReason.Temporary);
            if (!IsInDoNotDisturbMode) timer.Resume(ReminderPauseReason.DoNotDisturb);
        }
    }

    public void Dispose() => _timer.Dispose();
}
