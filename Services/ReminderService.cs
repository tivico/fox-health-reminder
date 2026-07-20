using HealthReminder.Models;

namespace HealthReminder.Services;

/// <summary>
/// 單一提醒項目的計時器包裝
/// </summary>
public class ReminderTimer : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private DateTime _nextTriggerTime;
    private TimeSpan _remainingWhenPaused = TimeSpan.Zero;
    
    public ReminderItem Item { get; private set; }
    public bool IsPaused { get; private set; } = false;
    
    /// <summary>
    /// 下次觸發時間
    /// </summary>
    public DateTime NextTriggerTime => _nextTriggerTime;
    
    /// <summary>
    /// 剩餘時間
    /// </summary>
    public TimeSpan RemainingTime => IsPaused
        ? _remainingWhenPaused
        : _nextTriggerTime > DateTime.Now
            ? _nextTriggerTime - DateTime.Now
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

        if (DateTime.Now >= _nextTriggerTime)
        {
            OnTriggered?.Invoke(this, Item);
            ResetTimer();
        }
    }

    /// <summary>
    /// 重設計時器到下次觸發時間
    /// </summary>
    public void ResetTimer()
    {
        _nextTriggerTime = DateTime.Now.AddMinutes(Item.IntervalMinutes);
    }

    /// <summary>
    /// 稍後提醒（Snooze）
    /// </summary>
    public void Snooze(int minutes)
    {
        _nextTriggerTime = DateTime.Now.AddMinutes(minutes);
    }

    /// <summary>
    /// 開始計時
    /// </summary>
    public void Start()
    {
        ResetTimer();
        _timer.Start();
        IsPaused = false;
    }

    /// <summary>
    /// 暫停計時
    /// </summary>
    public void Pause()
    {
        if (IsPaused) return;

        _remainingWhenPaused = RemainingTime;
        IsPaused = true;
    }

    /// <summary>
    /// 恢復計時
    /// </summary>
    public void Resume()
    {
        if (!IsPaused) return;

        _nextTriggerTime = DateTime.Now.Add(_remainingWhenPaused);
        IsPaused = false;
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
                    OnReminderTriggered?.Invoke(
                        this,
                        new ReminderTriggeredEventArgs(item, occurrence.Id));
                }
            };
            _timers.Add(timer);
            
            if (item.IsEnabled)
            {
                timer.Start();
            }
        }

        CheckDoNotDisturb(null, EventArgs.Empty);
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
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
    /// 暫停所有提醒
    /// </summary>
    public void PauseAll()
    {
        IsPausedAll = true;
        foreach (var timer in _timers)
        {
            timer.Pause();
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
                timer.Resume();
            }
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
    /// 檢查勿擾模式
    /// </summary>
    private void CheckDoNotDisturb(object? sender, EventArgs e)
    {
        var settings = SettingsManager.Instance.Settings;
        
        if (!settings.DoNotDisturbEnabled)
        {
            _isInDoNotDisturbMode = false;
            return;
        }

        var now = DateTime.Now.TimeOfDay;
        var start = settings.DoNotDisturbStart;
        var end = settings.DoNotDisturbEnd;

        // 處理跨午夜的情況
        if (start < end)
        {
            _isInDoNotDisturbMode = now >= start && now < end;
        }
        else
        {
            _isInDoNotDisturbMode = now >= start || now < end;
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
