using HealthReminder.Models;

namespace HealthReminder.Services;

public enum ReminderPauseReason
{
    User,
    DoNotDisturb,
    Temporary
}

/// <summary>
/// 單一提醒項目的計時器包裝
/// </summary>
public class ReminderTimer : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private DateTimeOffset _nextTriggerUtc;
    private TimeSpan _remainingWhenPaused = TimeSpan.Zero;
    private readonly HashSet<ReminderPauseReason> _pauseReasons = new();
    
    public ReminderItem Item { get; private set; }
    public bool IsPaused => _pauseReasons.Count > 0;
    
    /// <summary>
    /// 下次觸發時間
    /// </summary>
    public DateTime NextTriggerTime => _nextTriggerUtc.LocalDateTime;
    
    /// <summary>
    /// 剩餘時間
    /// </summary>
    public TimeSpan RemainingTime => IsPaused
        ? _remainingWhenPaused
        : _nextTriggerUtc > DateTimeOffset.UtcNow
            ? _nextTriggerUtc - DateTimeOffset.UtcNow
            : TimeSpan.Zero;

    /// <summary>
    /// 提醒觸發事件
    /// </summary>
    public event EventHandler<ReminderItem>? OnTriggered;

    public ReminderTimer(ReminderItem item)
    {
        Item = item;
        _timer = new System.Windows.Forms.Timer();
        _timer.Interval = 1000; // 每秒檢查一次
        _timer.Tick += Timer_Tick;
        
        ResetTimer();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (IsPaused || !Item.IsEnabled) return;

        if (DateTimeOffset.UtcNow >= _nextTriggerUtc)
        {
            OnTriggered?.Invoke(this, Item);
            if (Item.ScheduleKind == ReminderScheduleKind.OneTime)
            {
                _timer.Stop();
            }
            else
            {
                ResetTimer();
            }
        }
    }

    /// <summary>
    /// 重設計時器到下次觸發時間
    /// </summary>
    public void ResetTimer()
    {
        _nextTriggerUtc = Item.ScheduleKind == ReminderScheduleKind.OneTime
            ? ConvertLocalToUtc(Item.OneTimeAtLocal ?? DateTime.Now.AddMinutes(Item.IntervalMinutes))
            : DateTimeOffset.UtcNow.AddMinutes(Item.IntervalMinutes);
    }

    /// <summary>
    /// 稍後提醒（Snooze）
    /// </summary>
    public void Snooze(int minutes)
    {
        _nextTriggerUtc = DateTimeOffset.UtcNow.AddMinutes(minutes);
        Item.IsEnabled = true;
        _timer.Start();
    }

    /// <summary>
    /// 開始計時
    /// </summary>
    public void Start()
    {
        ResetTimer();
        _timer.Start();
        _pauseReasons.Clear();
    }

    /// <summary>
    /// 暫停計時
    /// </summary>
    public void Pause(ReminderPauseReason reason = ReminderPauseReason.User)
    {
        if (_pauseReasons.Contains(reason)) return;

        if (!IsPaused)
        {
            _remainingWhenPaused = RemainingTime;
        }

        _pauseReasons.Add(reason);
    }

    /// <summary>
    /// 恢復計時
    /// </summary>
    public void Resume(ReminderPauseReason reason = ReminderPauseReason.User)
    {
        if (!_pauseReasons.Remove(reason)) return;
        if (IsPaused) return;

        _nextTriggerUtc = Item.ScheduleKind == ReminderScheduleKind.OneTime
            ? ConvertLocalToUtc(Item.OneTimeAtLocal ?? DateTime.Now)
            : DateTimeOffset.UtcNow.Add(_remainingWhenPaused);
    }

    /// <summary>
    /// 系統喚醒或時鐘變更後重新檢查期限。
    /// 過期提醒保留為立即到期；異常遠的未來期限則限制為一個完整週期。
    /// </summary>
    public void RecalibrateAfterSystemChange()
    {
        if (IsPaused) return;

        var now = DateTimeOffset.UtcNow;
        if (Item.ScheduleKind == ReminderScheduleKind.OneTime)
        {
            _nextTriggerUtc = ConvertLocalToUtc(Item.OneTimeAtLocal ?? DateTime.Now);
            return;
        }

        var maximumRemaining = TimeSpan.FromMinutes(Math.Max(1, Item.IntervalMinutes));
        if (_nextTriggerUtc - now > maximumRemaining)
        {
            _nextTriggerUtc = now.Add(maximumRemaining);
        }
    }

    /// <summary>
    /// 停止計時
    /// </summary>
    public void Stop()
    {
        _timer.Stop();
    }

    /// <summary>
    /// 更新提醒項目設定
    /// </summary>
    public void UpdateItem(ReminderItem item)
    {
        Item = item;
        ResetTimer();
    }

    private static DateTimeOffset ConvertLocalToUtc(DateTime localDateTime)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        for (var minute = 0; minute < 120 && TimeZoneInfo.Local.IsInvalidTime(unspecifiedLocal); minute++)
        {
            unspecifiedLocal = unspecifiedLocal.AddMinutes(1);
        }

        try
        {
            var utc = TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, TimeZoneInfo.Local);
            return new DateTimeOffset(utc, TimeSpan.Zero);
        }
        catch (ArgumentException)
        {
            return DateTimeOffset.UtcNow;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}

/// <summary>
/// 提醒實際顯示時的事件資料。
/// </summary>
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

/// <summary>
/// 提醒服務 - 管理所有提醒計時器
/// </summary>
public class ReminderService : IDisposable
{
    private readonly List<ReminderTimer> _timers = new();
    private readonly System.Windows.Forms.Timer _doNotDisturbChecker;
    private readonly System.Windows.Forms.Timer _countdownTimer;
    private bool _isInDoNotDisturbMode = false;

    /// <summary>
    /// 提醒觸發事件
    /// </summary>
    public event EventHandler<ReminderTriggeredEventArgs>? OnReminderTriggered;

    /// <summary>
    /// 倒數計時更新事件（每秒觸發）
    /// </summary>
    public event EventHandler? OnCountdownTick;

    /// <summary>
    /// 是否處於勿擾模式
    /// </summary>
    public bool IsInDoNotDisturbMode => _isInDoNotDisturbMode;

    /// <summary>
    /// 是否全部暫停
    /// </summary>
    public bool IsPausedAll { get; private set; } = false;

    public DateTimeOffset? TemporaryPauseUntilUtc { get; private set; }
    public bool IsTemporarilyPaused => TemporaryPauseUntilUtc > DateTimeOffset.UtcNow;
    public DateTime? TemporaryPauseUntilLocal => TemporaryPauseUntilUtc?.LocalDateTime;

    public ReminderService()
    {
        // 勿擾模式檢查器
        _doNotDisturbChecker = new System.Windows.Forms.Timer();
        _doNotDisturbChecker.Interval = 60000; // 每分鐘檢查一次
        _doNotDisturbChecker.Tick += CheckDoNotDisturb;
        _doNotDisturbChecker.Start();

        _countdownTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _countdownTimer.Tick += CountdownTimer_Tick;
        _countdownTimer.Start();
    }

    /// <summary>
    /// 初始化所有提醒計時器
    /// </summary>
    public void Initialize()
    {
        var settings = SettingsManager.Instance.Settings;
        
        // 清除舊的計時器
        foreach (var timer in _timers)
        {
            timer.Dispose();
        }
        _timers.Clear();

        // 建立新的計時器
        foreach (var item in settings.Reminders)
        {
            var timer = new ReminderTimer(item);
            timer.OnTriggered += (s, item) =>
            {
                if (!_isInDoNotDisturbMode && !IsPausedAll)
                {
                    var occurrence = SettingsManager.Instance.AddTriggeredRecord(item);
                    if (item.ScheduleKind == ReminderScheduleKind.OneTime)
                    {
                        item.IsEnabled = false;
                        SettingsManager.Instance.Save();
                    }
                    OnReminderTriggered?.Invoke(
                        this,
                        new ReminderTriggeredEventArgs(item, occurrence.Id));
                }
            };
            _timers.Add(timer);
            
            if (item.IsEnabled)
            {
                timer.Start();
                if (IsPausedAll)
                {
                    timer.Pause(ReminderPauseReason.User);
                }
                if (IsTemporarilyPaused)
                {
                    timer.Pause(ReminderPauseReason.Temporary);
                }
            }
        }

        CheckDoNotDisturb(null, EventArgs.Empty);
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        CheckTemporaryPause();
        OnCountdownTick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 取得所有計時器狀態
    /// </summary>
    public IReadOnlyList<ReminderTimer> GetTimers() => _timers.AsReadOnly();

    /// <summary>
    /// 根據 ID 取得計時器
    /// </summary>
    public ReminderTimer? GetTimerById(string id)
    {
        return _timers.FirstOrDefault(t => t.Item.Id == id);
    }

    /// <summary>
    /// 稍後提醒指定項目
    /// </summary>
    public bool Snooze(string id, string occurrenceId)
    {
        var timer = GetTimerById(id);
        if (timer == null)
        {
            return false;
        }

        var snoozeMinutes = SettingsManager.Instance.Settings.SnoozeMinutes;
        if (timer.Item.ScheduleKind == ReminderScheduleKind.OneTime)
        {
            timer.Item.OneTimeAtLocal = DateTime.Now.AddMinutes(snoozeMinutes);
        }
        timer.Snooze(snoozeMinutes);
        return SettingsManager.Instance.UpdateRecordStatus(
            occurrenceId,
            ReminderStatus.Snoozed);
    }

    /// <summary>
    /// 標記提醒為已完成
    /// </summary>
    public bool MarkCompleted(string occurrenceId)
    {
        return SettingsManager.Instance.UpdateRecordStatus(
            occurrenceId,
            ReminderStatus.Completed);
    }

    /// <summary>
    /// 將本次提醒標記為略過。
    /// </summary>
    public bool Dismiss(string occurrenceId)
    {
        return SettingsManager.Instance.UpdateRecordStatus(
            occurrenceId,
            ReminderStatus.Dismissed);
    }

    /// <summary>
    /// 將逾時未操作的通知標記為錯過。
    /// </summary>
    public bool MarkMissed(string occurrenceId)
    {
        return SettingsManager.Instance.UpdateRecordStatus(
            occurrenceId,
            ReminderStatus.Missed);
    }

    /// <summary>
    /// 暫停所有提醒
    /// </summary>
    public void PauseAll()
    {
        IsPausedAll = true;
        foreach (var timer in _timers)
        {
            timer.Pause(ReminderPauseReason.User);
        }
    }

    /// <summary>
    /// 恢復所有提醒
    /// </summary>
    public void ResumeAll()
    {
        IsPausedAll = false;
        foreach (var timer in _timers)
        {
            if (timer.Item.IsEnabled)
            {
                timer.Resume(ReminderPauseReason.User);
            }
        }
    }

    /// <summary>
    /// 暫停所有提醒至指定時間，但不覆蓋使用者手動暫停或勿擾狀態。
    /// </summary>
    public void PauseUntil(DateTimeOffset resumeAtUtc)
    {
        TemporaryPauseUntilUtc = resumeAtUtc;
        foreach (var timer in _timers.Where(timer => timer.Item.IsEnabled))
        {
            timer.Pause(ReminderPauseReason.Temporary);
        }

        OnCountdownTick?.Invoke(this, EventArgs.Empty);
    }

    public void PauseFor(TimeSpan duration)
    {
        PauseUntil(DateTimeOffset.UtcNow.Add(duration));
    }

    public void PauseUntilEndOfDay()
    {
        var tomorrowLocal = DateTime.Today.AddDays(1);
        PauseUntil(new DateTimeOffset(tomorrowLocal).ToUniversalTime());
    }

    public void ResumeTemporaryPause()
    {
        TemporaryPauseUntilUtc = null;
        foreach (var timer in _timers.Where(timer => timer.Item.IsEnabled))
        {
            timer.Resume(ReminderPauseReason.Temporary);
        }

        OnCountdownTick?.Invoke(this, EventArgs.Empty);
    }

    private void CheckTemporaryPause()
    {
        if (TemporaryPauseUntilUtc is { } resumeAt && DateTimeOffset.UtcNow >= resumeAt)
        {
            ResumeTemporaryPause();
        }
    }

    /// <summary>
    /// 重新載入設定
    /// </summary>
    public void Reload()
    {
        Initialize();
    }

    /// <summary>
    /// 在系統喚醒或時間調整後校準所有計時器。
    /// </summary>
    public void RecalibrateAfterSystemChange()
    {
        CheckDoNotDisturb(null, EventArgs.Empty);
        foreach (var timer in _timers)
        {
            timer.RecalibrateAfterSystemChange();
        }

        OnCountdownTick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 檢查勿擾模式
    /// </summary>
    private void CheckDoNotDisturb(object? sender, EventArgs e)
    {
        var settings = SettingsManager.Instance.Settings;
        
        var newMode = false;
        if (settings.DoNotDisturbEnabled)
        {
            var now = DateTime.Now.TimeOfDay;
            var start = settings.DoNotDisturbStart;
            var end = settings.DoNotDisturbEnd;

            // 處理跨午夜的情況
            newMode = start < end
                ? now >= start && now < end
                : now >= start || now < end;
        }

        _isInDoNotDisturbMode = newMode;
        foreach (var timer in _timers.Where(timer => timer.Item.IsEnabled))
        {
            if (newMode)
            {
                timer.Pause(ReminderPauseReason.DoNotDisturb);
            }
            else
            {
                timer.Resume(ReminderPauseReason.DoNotDisturb);
            }
        }
    }

    public void Dispose()
    {
        _doNotDisturbChecker.Stop();
        _doNotDisturbChecker.Dispose();

        _countdownTimer.Stop();
        _countdownTimer.Dispose();

        foreach (var timer in _timers)
        {
            timer.Dispose();
        }
        _timers.Clear();
    }
}
