namespace HealthReminder.Models;

/// <summary>
/// 應用程式設定
/// </summary>
public class AppSettings
{
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// 設定結構版本，用於一次性、可追蹤的資料遷移。
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// 所有提醒項目
    /// </summary>
    public List<ReminderItem> Reminders { get; set; } = ReminderItem.GetDefaults();

    /// <summary>
    /// 是否開機自動啟動
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// 是否啟動時最小化
    /// </summary>
    public bool StartMinimized { get; set; } = true;

    /// <summary>
    /// 是否啟用勿擾模式
    /// </summary>
    public bool DoNotDisturbEnabled { get; set; } = false;

    /// <summary>
    /// 勿擾開始時間
    /// </summary>
    public TimeSpan DoNotDisturbStart { get; set; } = new TimeSpan(12, 0, 0); // 中午 12:00

    /// <summary>
    /// 勿擾結束時間
    /// </summary>
    public TimeSpan DoNotDisturbEnd { get; set; } = new TimeSpan(13, 0, 0); // 下午 1:00

    /// <summary>
    /// 稍後提醒的延遲分鐘數
    /// </summary>
    public int SnoozeMinutes { get; set; } = 10;

    /// <summary>
    /// 氣泡通知顯示時間（毫秒）
    /// </summary>
    public int BalloonTipDuration { get; set; } = 5000;

    /// <summary>
    /// 是否顯示倒數計時
    /// </summary>
    public bool ShowCountdown { get; set; } = true;

    /// <summary>
    /// 關閉位移與逐格動畫，但保留文字與操作回饋。
    /// </summary>
    public bool ReduceMotion { get; set; }
}
