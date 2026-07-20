using HealthReminder.Models;
using HealthReminder.Services;

var checks = new List<(string Name, Action Run)>
{
    ("統計只將 Completed 狀態計為完成", VerifyStatisticsStatuses),
    ("舊版 Completed/Snoozed 欄位可遷移", VerifyLegacyStatusCompatibility),
    ("預設提醒具有唯一識別碼", VerifyDefaultReminderIds),
    ("提醒只修復重複 Id，不限制範本數量", VerifyReminderNormalization),
    ("四種排程與完成方式組合皆可保存", VerifyGeneralTaskCombinations),
    ("護眼預設為 20 秒計時活動", VerifyDefaultEyeActivity),
    ("一次性提醒使用指定的絕對時間", VerifyOneTimeDeadline),
    ("一次性提醒稍後會重新啟用", VerifyOneTimeSnooze),
    ("暫停與恢復會保留剩餘時間", VerifyPausePreservesRemainingTime),
    ("手動、勿擾與暫時暫停互不覆蓋", VerifyIndependentPauseReasons)
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

static void VerifyReminderNormalization()
{
    const string duplicateId = "same-id";
    var reminders = new List<ReminderItem>
    {
        new() { Id = duplicateId, Type = ReminderType.Water, Name = "喝水 A" },
        new() { Id = "water-2", Type = ReminderType.Water, Name = "喝水 B" },
        new() { Id = duplicateId, Type = ReminderType.Custom, Name = "自訂 A" },
        new() { Id = "custom-2", Type = ReminderType.Custom, Name = "自訂 B" }
    };

    var result = ReminderCollectionNormalizer.Normalize(reminders);
    Equal(true, result.Changed);
    Equal(4, result.Reminders.Count);
    Equal(2, result.Reminders.Count(item => item.Type == ReminderType.Water));
    Equal(2, result.Reminders.Count(item => item.Type == ReminderType.Custom));
    Equal(4, result.Reminders.Select(item => item.Id).Distinct().Count());
}

static void VerifyGeneralTaskCombinations()
{
    var oneTime = DateTime.Now.AddHours(1);
    var reminders = new List<ReminderItem>
    {
        new() { ScheduleKind = ReminderScheduleKind.Interval, ActionKind = ReminderActionKind.Manual },
        new() { ScheduleKind = ReminderScheduleKind.Interval, ActionKind = ReminderActionKind.TimedActivity, ActivityDurationSeconds = 30 },
        new() { ScheduleKind = ReminderScheduleKind.OneTime, OneTimeAtLocal = oneTime, ActionKind = ReminderActionKind.Manual },
        new() { ScheduleKind = ReminderScheduleKind.OneTime, OneTimeAtLocal = oneTime, ActionKind = ReminderActionKind.TimedActivity, ActivityDurationSeconds = 45, AutoStartActivity = true }
    };

    var result = ReminderCollectionNormalizer.Normalize(reminders);
    Equal(4, result.Reminders.Count);
    Equal(2, result.Reminders.Count(item => item.ScheduleKind == ReminderScheduleKind.OneTime));
    Equal(2, result.Reminders.Count(item => item.ActionKind == ReminderActionKind.TimedActivity));
    Equal(true, result.Reminders.Last().AutoStartActivity);
}

static void VerifyDefaultEyeActivity()
{
    var eyeRest = ReminderItem.GetDefaults().Single(item => item.Type == ReminderType.EyeRest);
    Equal(ReminderActionKind.TimedActivity, eyeRest.ActionKind);
    Equal(20, eyeRest.ActivityDurationSeconds);
    Equal(false, eyeRest.AutoStartActivity);
}

static void VerifyOneTimeDeadline()
{
    var target = DateTime.Now.AddMinutes(5);
    using var timer = new ReminderTimer(new ReminderItem
    {
        ScheduleKind = ReminderScheduleKind.OneTime,
        OneTimeAtLocal = target
    });
    timer.Start();

    if (Math.Abs((timer.NextTriggerTime - target).TotalSeconds) > 2)
    {
        throw new InvalidOperationException("一次性提醒沒有保留指定的本機時間。");
    }
}

static void VerifyOneTimeSnooze()
{
    var item = new ReminderItem
    {
        ScheduleKind = ReminderScheduleKind.OneTime,
        OneTimeAtLocal = DateTime.Now.AddMinutes(1),
        IsEnabled = false
    };
    using var timer = new ReminderTimer(item);
    timer.Snooze(10);

    Equal(true, item.IsEnabled);
    if (timer.RemainingTime < TimeSpan.FromMinutes(9.9))
    {
        throw new InvalidOperationException("稍後提醒沒有建立新的期限。");
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

static void VerifyIndependentPauseReasons()
{
    using var timer = new ReminderTimer(new ReminderItem { IntervalMinutes = 1 });
    timer.Start();
    timer.Pause(ReminderPauseReason.User);
    timer.Pause(ReminderPauseReason.DoNotDisturb);
    timer.Pause(ReminderPauseReason.Temporary);
    timer.Resume(ReminderPauseReason.DoNotDisturb);

    Equal(true, timer.IsPaused);

    timer.Resume(ReminderPauseReason.User);
    Equal(true, timer.IsPaused);

    timer.Resume(ReminderPauseReason.Temporary);
    Equal(false, timer.IsPaused);
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
