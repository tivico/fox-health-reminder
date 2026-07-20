using HealthReminder.Models;
using HealthReminder.Services;

var checks = new List<(string Name, Action Run)>
{
    ("統計只將 Completed 狀態計為完成", VerifyStatisticsStatuses),
    ("舊版 Completed/Snoozed 欄位可遷移", VerifyLegacyStatusCompatibility),
    ("預設提醒具有唯一識別碼", VerifyDefaultReminderIds),
    ("暫停與恢復會保留剩餘時間", VerifyPausePreservesRemainingTime)
};

var failed = 0;
foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"PASS  {check.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL  {check.Name}");
        Console.WriteLine($"      {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"{checks.Count - failed}/{checks.Count} checks passed.");
return failed == 0 ? 0 : 1;

static void VerifyStatisticsStatuses()
{
    var statistics = new DailyStatistics
    {
        Records = new List<ReminderRecord>
        {
            NewRecord(ReminderStatus.Triggered),
            NewRecord(ReminderStatus.Completed),
            NewRecord(ReminderStatus.Snoozed),
            NewRecord(ReminderStatus.Dismissed)
        }
    };

    Equal(4, statistics.GetTotalCount(ReminderType.Water));
    Equal(1, statistics.GetCompletedCount(ReminderType.Water));
}

static void VerifyLegacyStatusCompatibility()
{
    var completed = new ReminderRecord { Completed = true, Snoozed = true };
    Equal(ReminderStatus.Completed, completed.Status);

    var snoozed = new ReminderRecord { Snoozed = true };
    Equal(ReminderStatus.Snoozed, snoozed.Status);
}

static void VerifyDefaultReminderIds()
{
    var defaults = ReminderItem.GetDefaults();
    Equal(defaults.Count, defaults.Select(item => item.Id).Distinct().Count());

    if (defaults.Any(item => string.IsNullOrWhiteSpace(item.Id)))
    {
        throw new InvalidOperationException("預設提醒包含空白 Id。");
    }
}

static void VerifyPausePreservesRemainingTime()
{
    using var timer = new ReminderTimer(new ReminderItem { IntervalMinutes = 1 });
    timer.Start();
    timer.Pause();

    var paused = timer.RemainingTime;
    Thread.Sleep(150);
    var stillPaused = timer.RemainingTime;

    if (Math.Abs((paused - stillPaused).TotalMilliseconds) > 20)
    {
        throw new InvalidOperationException("暫停期間剩餘時間仍持續減少。");
    }

    timer.Resume();
    var resumed = timer.RemainingTime;
    if (Math.Abs((paused - resumed).TotalMilliseconds) > 100)
    {
        throw new InvalidOperationException("恢復後沒有延續暫停前的剩餘時間。");
    }
}

static ReminderRecord NewRecord(ReminderStatus status)
{
    return new ReminderRecord
    {
        Type = ReminderType.Water,
        Status = status
    };
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected: {expected}; Actual: {actual}");
    }
}
