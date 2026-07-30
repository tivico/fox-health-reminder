using System.Buffers.Binary;
using HealthReminder.Controls;
using HealthReminder.Forms;
using HealthReminder.Models;
using HealthReminder.Services;
using Newtonsoft.Json;

var checks = new List<(string Name, Action Run)>
{
    ("統計只將 Completed 狀態計為完成", VerifyStatisticsStatuses),
    ("舊版 Completed/Snoozed 欄位可遷移", VerifyLegacyStatusCompatibility),
    ("預設提醒具有唯一識別碼", VerifyDefaultReminderIds),
    ("JSON 載入會取代預設清單而非重複追加", VerifyReminderListDeserialization),
    ("提醒只修復重複 Id，不限制範本數量", VerifyReminderNormalization),
    ("完全相同的提醒會去重，不同設定仍會保留", VerifyExactDuplicateNormalization),
    ("四種排程與完成方式組合皆可保存", VerifyGeneralTaskCombinations),
    ("護眼預設為 20 秒計時活動", VerifyDefaultEyeActivity),
    ("每日目標只計算工作時段內的啟用提醒", VerifyDailyGoalWindow),
    ("無效工作時段不會產生錯誤目標", VerifyInvalidWorkday),
    ("工作時段完成數會排除時段外紀錄", VerifyCompletedWithinWorkday),
    ("連續達標會跨日計算且不被未結束的今天中斷", VerifyAchievementStreak),
    ("一次性提醒使用指定的絕對時間", VerifyOneTimeDeadline),
    ("一次性提醒稍後會重新啟用", VerifyOneTimeSnooze),
    ("暫停與恢復會保留剩餘時間", VerifyPausePreservesRemainingTime),
    ("手動、勿擾與暫時暫停互不覆蓋", VerifyIndependentPauseReasons),
    ("單排狐狸圖資會依 512 像素計算影格", VerifyFoxSingleRowFrames),
    ("狐狸動畫可依動作使用不同播放速度", VerifyFoxAnimationIntervals),
    ("提醒類型會持續使用對應的狐狸動畫", VerifyReminderAnimationMappings),
    ("橘狐待機、揮手、喝水、護眼與伸展圖資已按正確名稱及尺寸嵌入", VerifyFoxAssets)
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

static void VerifyReminderListDeserialization()
{
    var source = new AppSettings();
    var json = JsonConvert.SerializeObject(source);
    var restored = JsonConvert.DeserializeObject<AppSettings>(json)
        ?? throw new InvalidOperationException("設定反序列化結果為 null。");

    Equal(source.Reminders.Count, restored.Reminders.Count);
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

static void VerifyExactDuplicateNormalization()
{
    var reminders = new List<ReminderItem>
    {
        new() { Id = "water-a", Type = ReminderType.Water, Name = "喝水", IntervalMinutes = 60 },
        new() { Id = "water-b", Type = ReminderType.Water, Name = "喝水", IntervalMinutes = 60 },
        new() { Id = "water-c", Type = ReminderType.Water, Name = "喝水", IntervalMinutes = 90 }
    };

    var result = ReminderCollectionNormalizer.Normalize(reminders);

    Equal(true, result.Changed);
    Equal(2, result.Reminders.Count);
    Equal("water-a", result.Reminders[0].Id);
    Equal(90, result.Reminders[1].IntervalMinutes);
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

static void VerifyDailyGoalWindow()
{
    var day = new DateTime(2026, 7, 22);
    var reminders = new List<ReminderItem>
    {
        new() { ScheduleKind = ReminderScheduleKind.Interval, IntervalMinutes = 60, IsEnabled = true },
        new() { ScheduleKind = ReminderScheduleKind.Interval, IntervalMinutes = 20, IsEnabled = false },
        new() { ScheduleKind = ReminderScheduleKind.OneTime, OneTimeAtLocal = day.AddHours(12), IsEnabled = true },
        new() { ScheduleKind = ReminderScheduleKind.OneTime, OneTimeAtLocal = day.AddHours(20), IsEnabled = true }
    };

    var target = DailyAchievementCalculator.CalculateGoal(
        reminders,
        day,
        TimeSpan.FromHours(9),
        TimeSpan.FromHours(18));

    Equal(10, target);
}

static void VerifyInvalidWorkday()
{
    Equal(false, DailyAchievementCalculator.IsValidWorkday(
        TimeSpan.FromHours(18),
        TimeSpan.FromHours(9)));

    var target = DailyAchievementCalculator.CalculateGoal(
        ReminderItem.GetDefaults(),
        DateTime.Today,
        TimeSpan.FromHours(18),
        TimeSpan.FromHours(9));
    Equal(0, target);
}

static void VerifyCompletedWithinWorkday()
{
    var day = new DateTime(2026, 7, 22);
    var statistics = new DailyStatistics
    {
        Date = day,
        Records = new List<ReminderRecord>
        {
            NewRecordAt(day.AddHours(8), ReminderStatus.Completed),
            NewRecordAt(day.AddHours(10), ReminderStatus.Completed),
            NewRecordAt(day.AddHours(11), ReminderStatus.Dismissed)
        }
    };

    var completed = DailyAchievementCalculator.CountCompleted(
        statistics,
        TimeSpan.FromHours(9),
        TimeSpan.FromHours(18));
    Equal(1, completed);
}

static void VerifyAchievementStreak()
{
    var today = new DateTime(2026, 7, 22);
    var statistics = new List<DailyStatistics>
    {
        new()
        {
            Date = today,
            Records = new List<ReminderRecord>
            {
                NewRecordAt(today.AddHours(10), ReminderStatus.Dismissed)
            }
        },
        new()
        {
            Date = today.AddDays(-1),
            Records = new List<ReminderRecord>
            {
                NewRecordAt(today.AddDays(-1).AddHours(10), ReminderStatus.Completed),
                NewRecordAt(today.AddDays(-1).AddHours(11), ReminderStatus.Dismissed)
            }
        },
        new()
        {
            Date = today.AddDays(-2),
            Records = new List<ReminderRecord>
            {
                NewRecordAt(today.AddDays(-2).AddHours(10), ReminderStatus.Completed)
            }
        }
    };

    var duringWorkday = DailyAchievementCalculator.CalculateStreak(
        statistics,
        today.AddHours(12),
        TimeSpan.FromHours(9),
        TimeSpan.FromHours(18));
    Equal(2, duringWorkday);

    var afterWorkday = DailyAchievementCalculator.CalculateStreak(
        statistics,
        today.AddHours(20),
        TimeSpan.FromHours(9),
        TimeSpan.FromHours(18));
    Equal(0, afterWorkday);
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

static void VerifyFoxSingleRowFrames()
{
    Equal(6, FoxAnimationControl.GetSingleRowFrameCount(3072, 512));
    Equal(8, FoxAnimationControl.GetSingleRowFrameCount(4096, 512));
    Equal(10, FoxAnimationControl.GetSingleRowFrameCount(5120, 512));
    Equal(0, FoxAnimationControl.GetSingleRowFrameCount(3072, 500));
    Equal(0, FoxAnimationControl.GetSingleRowFrameCount(3000, 512));
}

static void VerifyFoxAnimationIntervals()
{
    Equal(400, FoxAnimationControl.GetFrameInterval(FoxAnimationState.Wait));
    Equal(6, FoxAnimationControl.GetFrameSequenceLength(FoxAnimationState.Wait));
    Equal(2400, FoxAnimationControl.GetCycleDurationMs(FoxAnimationState.Wait));
    Equal(100, FoxAnimationControl.GetFrameInterval(FoxAnimationState.Wave));
    Equal(7, FoxAnimationControl.GetFrameSequenceLength(FoxAnimationState.Wave));
    Equal(700, FoxAnimationControl.GetCycleDurationMs(FoxAnimationState.Wave));
    Equal(120, FoxAnimationControl.GetFrameInterval(FoxAnimationState.Drink));
    Equal(9, FoxAnimationControl.GetFrameSequenceLength(FoxAnimationState.Drink));
    Equal(1080, FoxAnimationControl.GetCycleDurationMs(FoxAnimationState.Drink));
    Equal(140, FoxAnimationControl.GetFrameInterval(FoxAnimationState.EyeRest));
    Equal(9, FoxAnimationControl.GetFrameSequenceLength(FoxAnimationState.EyeRest));
    Equal(1260, FoxAnimationControl.GetCycleDurationMs(FoxAnimationState.EyeRest));
    Equal(160, FoxAnimationControl.GetFrameInterval(FoxAnimationState.Stretch));
    Equal(11, FoxAnimationControl.GetFrameSequenceLength(FoxAnimationState.Stretch));
    Equal(1760, FoxAnimationControl.GetCycleDurationMs(FoxAnimationState.Stretch));
}

static void VerifyReminderAnimationMappings()
{
    Equal(FoxAnimationState.Drink, ReminderPopupForm.GetReminderAnimationState(ReminderType.Water));
    Equal(FoxAnimationState.EyeRest, ReminderPopupForm.GetReminderAnimationState(ReminderType.EyeRest));
    Equal(FoxAnimationState.Stretch, ReminderPopupForm.GetReminderAnimationState(ReminderType.Stretch));
    Equal(FoxAnimationState.Wait, ReminderPopupForm.GetReminderAnimationState(ReminderType.Custom));
}

static void VerifyFoxAssets()
{
    VerifyEmbeddedPng("HealthReminder.Resources.fox_idle.png", 1024, 512);
    VerifyEmbeddedPng("HealthReminder.Resources.fox_wave.png", 2048, 512);
    VerifyEmbeddedPng("HealthReminder.Resources.fox_drink.png", 2048, 512);
    VerifyEmbeddedPng("HealthReminder.Resources.fox_eye_rest.png", 2048, 512);
    VerifyEmbeddedPng("HealthReminder.Resources.fox_stretch.png", 2560, 512);
}

static void VerifyEmbeddedPng(string resourceName, int expectedWidth, int expectedHeight)
{
    using var stream = typeof(FoxAnimationControl).Assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"找不到內嵌圖資：{resourceName}");

    Span<byte> header = stackalloc byte[24];
    stream.ReadExactly(header);
    Equal(expectedWidth, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
    Equal(expectedHeight, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
}

static ReminderRecord NewRecord(ReminderStatus status)
{
    return new ReminderRecord
    {
        Type = ReminderType.Water,
        Status = status
    };
}

static ReminderRecord NewRecordAt(DateTime timestamp, ReminderStatus status)
{
    return new ReminderRecord
    {
        Type = ReminderType.Water,
        Timestamp = timestamp,
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
