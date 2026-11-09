namespace HealthReminder.Models;

/// <summary>
/// 提醒類型列舉
/// </summary>
public enum ReminderType
{
    Water,      // 喝水提醒
    EyeRest,    // 眼睛休息
    Stretch,    // 起身活動
    Custom      // 自訂提醒
}

/// <summary>
/// 提醒何時觸發。類型只描述排程，不綁定任何特定健康任務。
/// </summary>
public enum ReminderScheduleKind
{
    Interval,
    OneTime
}

/// <summary>
/// 使用者如何完成一次提醒。
/// </summary>
public enum ReminderActionKind
{
    Manual,
    TimedActivity
}

/// <summary>
/// 單一提醒項目的設定
/// </summary>
public class ReminderItem
{
    /// <summary>
    /// 唯一識別碼
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 提醒類型
    /// </summary>
    public ReminderType Type { get; set; }

    /// <summary>
    /// 顯示名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 提醒訊息內容
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 間隔時間（分鐘）
    /// </summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 持續間隔或一次性排程。
    /// </summary>
    public ReminderScheduleKind ScheduleKind { get; set; } = ReminderScheduleKind.Interval;

    /// <summary>
    /// 一次性排程的本機日期時間；舊設定或間隔排程可為 null。
    /// </summary>
    public DateTime? OneTimeAtLocal { get; set; }

    /// <summary>
    /// 手動完成或計時活動。
    /// </summary>
    public ReminderActionKind ActionKind { get; set; } = ReminderActionKind.Manual;

    /// <summary>
    /// 計時活動長度（秒）。
    /// </summary>
    public int ActivityDurationSeconds { get; set; } = 20;

    /// <summary>
    /// 通知顯示完進場動畫後是否自動開始計時活動。
    /// </summary>
    public bool AutoStartActivity { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否播放音效
    /// </summary>
    public bool PlaySound { get; set; } = true;

    /// <summary>
    /// 圖示表情符號（用於通知顯示）
    /// </summary>
    public string Emoji { get; set; } = "💡";

    internal ReminderItem Copy() => (ReminderItem)MemberwiseClone();

    /// <summary>
    /// 取得預設的提醒項目清單
    /// </summary>
    public static List<ReminderItem> GetDefaults()
    {
        return new List<ReminderItem>
        {
            new ReminderItem
            {
                Type = ReminderType.Water,
                Name = "喝水提醒",
                Message = "該喝水囉！保持水分很重要 💧",
                IntervalMinutes = 52,
                Emoji = "💧"
            },
            new ReminderItem
            {
                Type = ReminderType.EyeRest,
                Name = "眼睛休息",
                Message = "讓眼睛休息一下！看看遠方 20 秒 👀",
                IntervalMinutes = 20,
                Emoji = "👀",
                ActionKind = ReminderActionKind.TimedActivity,
                ActivityDurationSeconds = 20
            },
            new ReminderItem
            {
                Type = ReminderType.Stretch,
                Name = "起身活動",
                Message = "站起來動一動，伸展筋骨！🏃",
                IntervalMinutes = 43,
                Emoji = "🏃"
            }
        };
    }
}
