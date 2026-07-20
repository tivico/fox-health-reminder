namespace HealthReminder.Models;

/// <summary>
/// 一次提醒在使用者操作流程中的狀態。
/// </summary>
public enum ReminderStatus
{
    Triggered,
    Completed,
    Snoozed,
    Dismissed,
    Missed
}

/// <summary>
/// 單次提醒的紀錄
/// </summary>
public class ReminderRecord
{
    /// <summary>
    /// 本次提醒事件的唯一識別碼。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 對應的提醒設定識別碼。
    /// </summary>
    public string ReminderId { get; set; } = string.Empty;

    /// <summary>
    /// 提醒類型
    /// </summary>
    public ReminderType Type { get; set; }

    /// <summary>
    /// 提醒名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 發生時間
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 本次提醒目前的狀態。
    /// </summary>
    public ReminderStatus Status { get; set; } = ReminderStatus.Triggered;

    /// <summary>
    /// 舊版資料相容欄位。新程式應使用 Status。
    /// </summary>
    public bool Completed
    {
        get => Status == ReminderStatus.Completed;
        set
        {
            if (value)
            {
                Status = ReminderStatus.Completed;
            }
        }
    }

    /// <summary>
    /// 舊版資料相容欄位。新程式應使用 Status。
    /// </summary>
    public bool Snoozed
    {
        get => Status == ReminderStatus.Snoozed;
        set
        {
            if (value && Status != ReminderStatus.Completed)
            {
                Status = ReminderStatus.Snoozed;
            }
        }
    }
}

/// <summary>
/// 每日統計資料
/// </summary>
public class DailyStatistics
{
    /// <summary>
    /// 日期
    /// </summary>
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>
    /// 當日所有提醒紀錄
    /// </summary>
    public List<ReminderRecord> Records { get; set; } = new List<ReminderRecord>();

    /// <summary>
    /// 取得指定類型的完成次數
    /// </summary>
    public int GetCompletedCount(ReminderType type)
    {
        return Records.Count(r => r.Type == type && r.Status == ReminderStatus.Completed);
    }

    /// <summary>
    /// 取得指定類型的總提醒次數
    /// </summary>
    public int GetTotalCount(ReminderType type)
    {
        return Records.Count(r => r.Type == type);
    }
}
