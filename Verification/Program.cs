using System.Buffers.Binary;
using HealthReminder.Controls;
using HealthReminder.Forms;
using HealthReminder.Models;
using HealthReminder.Services;
using Newtonsoft.Json;

if (args.Contains("--ux-check")) return UxLayoutVerification.Run(args);

var checks = new List<(string Name, Action Run)>
{
    ("統計只將 Completed 狀態計為完成", VerifyStatisticsStatuses),
    ("同名歷史與現行提醒分開標示，保留原計數與類型圖示", VerifyHistoricalStatisticsRows),
    ("提醒改名與停用後仍依相同 ID 彙整", VerifyRenamedStatisticsRows),
    ("同名不同 ID 的現行提醒不會被誤合併", VerifyDistinctStatisticsRows),
    ("無 ID 舊紀錄依類型名稱彙整，同名歷史列可辨識", VerifyLegacyStatisticsRows),
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
    ("損壞設定與統計分開處理，原始檔保持完整", VerifyIndependentFileRecovery),
    ("寫入失敗後可用同一次提醒重試", VerifyStatusWriteRetry),
    ("跨檔案提交可在下次啟動補寫", VerifyPendingCommitReplay),
    ("循環提醒暫停中稍後，恢復後從新期限倒數", VerifyPausedIntervalSnooze),
    ("舊通知不會延後已變更的排程，仍使用按鈕顯示的分鐘數", VerifyStaleNotificationSnooze),
    ("一次性提醒顯示後才記帳，稍後仍受暫停約束", VerifyOneTimeDisplayAndSnooze),
    ("設定重載保留其他期限，最後一筆可刪除", VerifyReloadAndDelete),
    ("勿擾相同起訖不啟用並支援跨午夜", VerifyQuietHours),
    ("長時間倒數包含小時", VerifyLongCountdown),
    ("單排狐狸圖資會依 512 像素計算影格", VerifyFoxSingleRowFrames),
    ("狐狸動畫可依動作使用不同播放速度", VerifyFoxAnimationIntervals),
    ("提醒類型會持續使用對應的狐狸動畫", VerifyReminderAnimationMappings),
    ("通知 Alt 快捷鍵與 Esc 對應正確操作", VerifyReminderKeyboardMappings),
    ("橘狐待機、揮手、喝水、護眼與伸展圖資已按正確名稱及尺寸嵌入", VerifyFoxAssets),
    ("沒有 portable.flag 時使用 AppData", VerifyDefaultStorageLocation),
    ("portable.flag 會先驗證 Data 寫入權限", VerifyWritablePortableStorage),
    ("可攜 Data 無法寫入時要求明確 fallback", VerifyBlockedPortableStorage)
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

static void VerifyHistoricalStatisticsRows()
{
    var reminders = new[]
    {
        new ReminderItem { Id = "water", Name = "喝水提醒", Type = ReminderType.Water, Emoji = "💧" },
        new ReminderItem { Id = "eye", Name = "眼睛休息", Type = ReminderType.EyeRest, Emoji = "👀", IsEnabled = false },
        new ReminderItem { Id = "stretch", Name = "起身活動", Type = ReminderType.Stretch, Emoji = "🏃" }
    };
    var records = new List<ReminderRecord>();
    void Add(string id, string name, ReminderType type, int total, int completed)
    {
        for (var i = 0; i < total; i++)
            records.Add(new ReminderRecord
            {
                ReminderId = id, Name = name, Type = type,
                Status = i < completed ? ReminderStatus.Completed : ReminderStatus.Missed
            });
    }
    Add("old-stretch", "起身活動", ReminderType.Stretch, 15, 0);
    Add("stretch", "起身活動", ReminderType.Stretch, 6, 1);
    Add("old-eye", "眼睛休息", ReminderType.EyeRest, 31, 0);
    Add("old-water", "喝水提醒", ReminderType.Water, 12, 1);
    Add("water", "喝水提醒", ReminderType.Water, 6, 0);
    var original = JsonConvert.SerializeObject(records);
    var rows = ReminderStatisticsCalculator.BuildRows(records, reminders);
    Equal(5, rows.Count);
    Equal(70, rows.Sum(row => row.Total));
    Equal(2, rows.Sum(row => row.Completed));
    Equal(5, rows.Select(row => row.DisplayName).Distinct().Count());
    Equal(3, rows.Count(row => row.IsHistorical));
    Equal("👀", rows.Single(row => row.ReminderId == "old-eye").Emoji);
    Equal("💧", rows.Single(row => row.ReminderId == "old-water").Emoji);
    Equal("🏃", rows.Single(row => row.ReminderId == "old-stretch").Emoji);
    Equal(true, rows.Single(row => row.ReminderId == "old-water").DisplayName.Contains("歷史項目"));
    Equal(original, JsonConvert.SerializeObject(records));
}

static void VerifyRenamedStatisticsRows()
{
    var records = new[]
    {
        new ReminderRecord { ReminderId = "same", Name = "舊名稱", Status = ReminderStatus.Completed },
        new ReminderRecord { ReminderId = "same", Name = "新名稱", Status = ReminderStatus.Snoozed }
    };
    var rows = ReminderStatisticsCalculator.BuildRows(records,
        [new ReminderItem { Id = "same", Name = "目前名稱", Emoji = "☕", IsEnabled = false }]);
    Equal(1, rows.Count);
    Equal("目前名稱", rows[0].DisplayName);
    Equal("☕", rows[0].Emoji);
    Equal(false, rows[0].IsHistorical);
    Equal(2, rows[0].Total);
    Equal(1, rows[0].Completed);
}

static void VerifyDistinctStatisticsRows()
{
    var reminders = new[]
    {
        new ReminderItem { Id = "a", Name = "喝水", IntervalMinutes = 30 },
        new ReminderItem { Id = "b", Name = "喝水", IntervalMinutes = 60 }
    };
    var rows = ReminderStatisticsCalculator.BuildRows(
        reminders.Select(item => new ReminderRecord { ReminderId = item.Id, Name = item.Name }), reminders);
    Equal(2, rows.Count);
    Equal(2, rows.Select(row => row.ReminderId).Distinct().Count());
    Equal(false, rows.Any(row => row.IsHistorical));
    Equal(2, rows.Sum(row => row.Total));
}

static void VerifyLegacyStatisticsRows()
{
    var records = new[]
    {
        new ReminderRecord { ReminderId = null!, Type = ReminderType.Water, Name = "習慣", Status = ReminderStatus.Completed },
        new ReminderRecord { ReminderId = "", Type = ReminderType.Water, Name = "習慣" },
        new ReminderRecord { ReminderId = "old", Type = ReminderType.Water, Name = "習慣" },
        new ReminderRecord { ReminderId = "other", Type = ReminderType.Custom, Name = "" }
    };
    var rows = ReminderStatisticsCalculator.BuildRows(records, []);
    Equal(3, rows.Count);
    Equal(4, rows.Sum(row => row.Total));
    Equal(1, rows.Sum(row => row.Completed));
    Equal(3, rows.Select(row => row.DisplayName).Distinct().Count());
    Equal(true, rows.All(row => row.IsHistorical));
    Equal(true, rows.Any(row => row.DisplayName.Contains("未命名提醒")));
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
    var timer = new ReminderTimer(new ReminderItem
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
    var timer = new ReminderTimer(item);
    timer.Snooze(10);

    Equal(true, timer.Item.IsEnabled);
    Equal(false, item.IsEnabled);
    if (timer.RemainingTime < TimeSpan.FromMinutes(9.9))
    {
        throw new InvalidOperationException("稍後提醒沒有建立新的期限。");
    }
}

static void VerifyPausePreservesRemainingTime()
{
    var timer = new ReminderTimer(new ReminderItem { IntervalMinutes = 1 });
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
    var timer = new ReminderTimer(new ReminderItem { IntervalMinutes = 1 });
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

static void VerifyIndependentFileRecovery()
{
    WithTemporaryDirectory(folder =>
    {
        var settingsPath = Path.Combine(folder, "settings.json");
        var statisticsPath = Path.Combine(folder, "statistics.json");
        const string damaged = "{broken";
        var goodStats = JsonConvert.SerializeObject(new[]
        {
            new DailyStatistics { Records = [new ReminderRecord { Id = "original" }] }
        });
        File.WriteAllText(settingsPath, damaged);
        File.WriteAllText(statisticsPath, goodStats);

        var manager = new SettingsManager(new StorageLocationSelection(folder, true));
        Equal(true, manager.LoadWarning?.Contains("settings.json") == true);
        Equal(1, manager.GetTodayStatistics().Records.Count);
        Equal(false, manager.SaveSettings(new AppSettings()));
        Equal(damaged, File.ReadAllText(settingsPath));
        Equal(goodStats, File.ReadAllText(statisticsPath));

        File.WriteAllText(settingsPath, JsonConvert.SerializeObject(new AppSettings
        {
            Reminders = [new ReminderItem { Name = "既有自訂提醒" }]
        }));
        File.WriteAllText(statisticsPath, damaged);
        manager = new SettingsManager(new StorageLocationSelection(folder, true));
        Equal(true, manager.LoadWarning?.Contains("statistics.json") == true);
        Equal("既有自訂提醒", manager.Settings.Reminders.Single().Name);
        Equal(false, manager.RecordShown(manager.Settings.Reminders.Single(), "new"));
        Equal(true, manager.SaveSettings(manager.Settings.Copy()));
        Equal(damaged, File.ReadAllText(statisticsPath));
    });
}

static void VerifyStatusWriteRetry()
{
    WithTemporaryDirectory(folder =>
    {
        var manager = new SettingsManager(new StorageLocationSelection(folder, true));
        var item = manager.Settings.Reminders.First();
        Equal(true, manager.RecordShown(item, "retry"));
        var temporaryPath = Path.Combine(folder, "statistics.json.tmp");
        Directory.CreateDirectory(temporaryPath);
        Equal(false, manager.UpdateRecordStatus("retry", ReminderStatus.Completed));
        Equal(ReminderStatus.Triggered, manager.GetTodayStatistics().Records.Single().Status);
        Directory.Delete(temporaryPath);

        Equal(true, manager.UpdateRecordStatus("retry", ReminderStatus.Completed));
        Equal(true, manager.UpdateRecordStatus("retry", ReminderStatus.Completed));
        var reloaded = new SettingsManager(new StorageLocationSelection(folder, true));
        Equal(ReminderStatus.Completed, reloaded.GetTodayStatistics().Records.Single().Status);
    });
}

static void VerifyPendingCommitReplay()
{
    WithTemporaryDirectory(folder =>
    {
        var location = new StorageLocationSelection(folder, true);
        var manager = new SettingsManager(location);
        var item = new ReminderItem
        {
            ScheduleKind = ReminderScheduleKind.OneTime,
            OneTimeAtLocal = DateTime.Today.AddHours(12)
        };
        Equal(true, manager.SaveSettings(new AppSettings { Reminders = [item] }));

        var temporaryPath = Path.Combine(folder, "statistics.json.tmp");
        Directory.CreateDirectory(temporaryPath);
        Equal(true, manager.RecordShown(item, "shown"));
        var journal = Path.Combine(folder, "pending-commit.json");
        Equal(true, File.Exists(journal));
        Equal(false, manager.UpdateRecordStatus("shown", ReminderStatus.Completed));
        Directory.Delete(temporaryPath);

        var reloaded = new SettingsManager(location);
        Equal(false, File.Exists(journal));
        Equal(false, reloaded.Settings.Reminders.Single().IsEnabled);
        Equal(1, reloaded.GetTodayStatistics().Records.Count);
        Equal(1, reloaded.GetDailyGoalTarget());
    });
}

static void VerifyPausedIntervalSnooze()
{
    WithTemporaryDirectory(folder =>
    {
        var clock = new VerificationClock();
        var manager = new SettingsManager(new StorageLocationSelection(folder, true));
        var item = new ReminderItem { IntervalMinutes = 60 };
        Equal(true, manager.SaveSettings(new AppSettings { Reminders = [item] }));
        using var service = new ReminderService(manager, clock);
        service.Initialize();
        Equal(true, manager.RecordShown(item, "interval"));
        service.PauseAll();
        service.PauseFor(TimeSpan.FromMinutes(30));
        Equal(true, service.Snooze(item, "interval", 10));
        var timer = service.GetTimers().Single();
        Equal(TimeSpan.FromMinutes(10), timer.RemainingTime);
        var deadline = timer.NextTriggerTime;
        clock.Advance(TimeSpan.FromMinutes(2));
        Equal(false, service.Snooze(item, "interval", 10));
        Equal(deadline, timer.NextTriggerTime);
        service.ResumeAll();
        Equal(true, timer.IsPaused);
        service.ResumeTemporaryPause();
        Equal(TimeSpan.FromMinutes(10), timer.RemainingTime);
    });
}

static void VerifyStaleNotificationSnooze()
{
    WithTemporaryDirectory(folder =>
    {
        var clock = new VerificationClock();
        var manager = new SettingsManager(new StorageLocationSelection(folder, true));
        var shownItem = new ReminderItem { IntervalMinutes = 20 };
        Equal(true, manager.SaveSettings(new AppSettings { Reminders = [shownItem.Copy()] }));
        using var service = new ReminderService(manager, clock);
        service.Initialize();
        Equal(true, manager.RecordShown(shownItem, "already-visible"));

        var updated = manager.Settings.Copy();
        updated.Reminders[0].IsEnabled = false;
        Equal(true, manager.SaveSettings(updated));
        service.Reload();
        Equal(false, service.Snooze(shownItem, "already-visible", 10));
        Equal(false, service.GetTimers().Single().Item.IsEnabled);

        updated = manager.Settings.Copy();
        updated.Reminders[0].ScheduleKind = ReminderScheduleKind.OneTime;
        updated.Reminders[0].OneTimeAtLocal = clock.GetLocalNow().DateTime.AddHours(1);
        Equal(true, manager.SaveSettings(updated));
        service.Reload();
        var newDeadline = service.GetTimers().Single().NextTriggerTime;
        Equal(false, service.Snooze(shownItem, "already-visible", 10));
        Equal(newDeadline, service.GetTimers().Single().NextTriggerTime);
        Equal(ReminderStatus.Triggered, manager.GetTodayStatistics().Records.Single().Status);

        Equal(true, manager.SaveSettings(new AppSettings { Reminders = [shownItem.Copy()], SnoozeMinutes = 30 }));
        service.Reload();
        Equal(true, service.Snooze(shownItem, "already-visible", 10));
        Equal(TimeSpan.FromMinutes(10), service.GetTimers().Single().RemainingTime);
    });
}

static void VerifyOneTimeDisplayAndSnooze()
{
    WithTemporaryDirectory(folder =>
    {
        var clock = new VerificationClock();
        var manager = new SettingsManager(new StorageLocationSelection(folder, true));
        var item = new ReminderItem
        {
            ScheduleKind = ReminderScheduleKind.OneTime,
            OneTimeAtLocal = clock.GetLocalNow().DateTime.AddSeconds(-1)
        };
        Equal(true, manager.SaveSettings(new AppSettings { Reminders = [item] }));
        using var service = new ReminderService(manager, clock);
        service.Initialize();
        var events = new List<ReminderTriggeredEventArgs>();
        service.OnReminderTriggered += (_, args) => events.Add(args);
        service.Pulse();
        service.Pulse();
        Equal(1, events.Count);
        Equal(0, manager.GetTodayStatistics().Records.Count);
        Equal(true, manager.Settings.Reminders.Single().IsEnabled);

        var due = events.Single();
        Equal(true, service.RecordShown(due.Item, due.OccurrenceId));
        Equal(false, manager.Settings.Reminders.Single().IsEnabled);
        Equal(1, manager.GetTodayStatistics().Records.Count);
        service.PauseFor(TimeSpan.FromMinutes(30));
        Equal(true, service.Snooze(due.Item, due.OccurrenceId, 10));
        Equal(true, service.GetTimers().Single().IsPaused);
        clock.Advance(TimeSpan.FromMinutes(11));
        service.Pulse();
        Equal(1, events.Count);
        service.ResumeTemporaryPause();
        service.Pulse();
        Equal(2, events.Count);
        Equal(ReminderStatus.Snoozed, manager.GetTodayStatistics().Records.Single().Status);
    });
}

static void VerifyReloadAndDelete()
{
    WithTemporaryDirectory(folder =>
    {
        var clock = new VerificationClock();
        var location = new StorageLocationSelection(folder, true);
        var manager = new SettingsManager(location);
        var first = new ReminderItem { Name = "第一筆" };
        var second = new ReminderItem { Name = "第二筆", IntervalMinutes = 30 };
        Equal(true, manager.SaveSettings(new AppSettings { Reminders = [first, second] }));
        using var service = new ReminderService(manager, clock);
        service.Initialize();

        clock.Advance(TimeSpan.FromMinutes(5));
        var candidate = manager.Settings.Copy();
        candidate.Reminders[1].Name = "改名";
        candidate.ReduceMotion = true;
        Equal(true, manager.SaveSettings(candidate));
        Equal(0, service.Reload().Count);
        Equal(TimeSpan.FromMinutes(25), service.GetTimerById(second.Id)!.RemainingTime);

        Equal(true, manager.DeleteReminder(first.Id));
        Equal(true, service.Reload().SetEquals([first.Id]));
        Equal(TimeSpan.FromMinutes(25), service.GetTimers().Single().RemainingTime);

        Equal(true, manager.DeleteReminder(second.Id));
        Equal(true, service.Reload().SetEquals([second.Id]));
        Equal(0, service.GetTimers().Count);
        Equal(0, new SettingsManager(location).Settings.Reminders.Count);
    });
}

static void VerifyQuietHours()
{
    Equal(false, ReminderTimer.IsInQuietHours(TimeSpan.FromHours(12), TimeSpan.FromHours(12), TimeSpan.FromHours(12)));
    Equal(true, ReminderTimer.IsInQuietHours(TimeSpan.FromHours(23), TimeSpan.FromHours(22), TimeSpan.FromHours(7)));
    Equal(true, ReminderTimer.IsInQuietHours(TimeSpan.FromHours(2), TimeSpan.FromHours(22), TimeSpan.FromHours(7)));
    Equal(false, ReminderTimer.IsInQuietHours(TimeSpan.FromHours(7), TimeSpan.FromHours(22), TimeSpan.FromHours(7)));
}

static void VerifyLongCountdown()
{
    Equal("01:30:00", ReminderCountdownCard.FormatRemaining(TimeSpan.FromMinutes(90)));
    Equal("24:00:00", ReminderCountdownCard.FormatRemaining(TimeSpan.FromDays(1)));
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

static void VerifyReminderKeyboardMappings()
{
    Equal<ReminderPopupKeyboardCommand?>(
        ReminderPopupKeyboardCommand.Primary,
        ReminderPopupForm.GetKeyboardCommand(Keys.Alt | Keys.S, ReminderActionKind.TimedActivity));
    Equal<ReminderPopupKeyboardCommand?>(
        ReminderPopupKeyboardCommand.Primary,
        ReminderPopupForm.GetKeyboardCommand(Keys.Alt | Keys.C, ReminderActionKind.Manual));
    Equal<ReminderPopupKeyboardCommand?>(
        ReminderPopupKeyboardCommand.Snooze,
        ReminderPopupForm.GetKeyboardCommand(Keys.Alt | Keys.L, ReminderActionKind.Manual));
    Equal<ReminderPopupKeyboardCommand?>(
        ReminderPopupKeyboardCommand.Dismiss,
        ReminderPopupForm.GetKeyboardCommand(Keys.Alt | Keys.K, ReminderActionKind.Manual));
    Equal<ReminderPopupKeyboardCommand?>(
        ReminderPopupKeyboardCommand.Dismiss,
        ReminderPopupForm.GetKeyboardCommand(Keys.Escape, ReminderActionKind.TimedActivity));
    Equal<ReminderPopupKeyboardCommand?>(
        null,
        ReminderPopupForm.GetKeyboardCommand(Keys.Alt | Keys.C, ReminderActionKind.TimedActivity));
    Equal<ReminderPopupKeyboardCommand?>(
        null,
        ReminderPopupForm.GetKeyboardCommand(Keys.Alt | Keys.S, ReminderActionKind.Manual));
}

static void VerifyFoxAssets()
{
    VerifyEmbeddedPng("HealthReminder.Resources.fox_idle.png", 1024, 512);
    VerifyEmbeddedPng("HealthReminder.Resources.fox_wave.png", 2048, 512);
    VerifyEmbeddedPng("HealthReminder.Resources.fox_drink.png", 2048, 512);
    VerifyEmbeddedPng("HealthReminder.Resources.fox_eye_rest.png", 2048, 512);
    VerifyEmbeddedPng("HealthReminder.Resources.fox_stretch.png", 2560, 512);
}

static void VerifyDefaultStorageLocation()
{
    WithTemporaryDirectory(root =>
    {
        var appData = Path.Combine(root, "AppData");
        var result = StorageLocationResolver.Probe(root, appData);

        Equal(false, result.PortableRequested);
        Equal(false, result.PortableAvailable);
        Equal(Path.GetFullPath(appData), result.SelectPreferred().Folder);
        Equal(false, result.SelectPreferred().IsPortable);
    });
}

static void VerifyWritablePortableStorage()
{
    WithTemporaryDirectory(root =>
    {
        File.WriteAllText(Path.Combine(root, "portable.flag"), string.Empty);
        var result = StorageLocationResolver.Probe(root, Path.Combine(root, "AppData"));

        Equal(true, result.PortableRequested);
        Equal(true, result.PortableAvailable);
        Equal(true, result.SelectPreferred().IsPortable);
        Equal(Path.Combine(Path.GetFullPath(root), "Data"), result.SelectPreferred().Folder);
        Equal(0, Directory.GetFiles(result.PortableFolder).Length);
    });
}

static void VerifyBlockedPortableStorage()
{
    WithTemporaryDirectory(root =>
    {
        File.WriteAllText(Path.Combine(root, "portable.flag"), string.Empty);
        File.WriteAllText(Path.Combine(root, "Data"), "阻止建立同名資料夾");
        var result = StorageLocationResolver.Probe(root, Path.Combine(root, "AppData"));

        Equal(true, result.PortableRequested);
        Equal(false, result.PortableAvailable);
        Equal(false, result.SelectAppDataFallback().IsPortable);
        Equal(true, !string.IsNullOrWhiteSpace(result.FailureReason));
        Equal(true, !string.IsNullOrWhiteSpace(result.SelectAppDataFallback().Notice));
    });
}

static void WithTemporaryDirectory(Action<string> action)
{
    var root = Path.Combine(
        Path.GetTempPath(),
        "HealthReminderVerification",
        Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);

    try
    {
        action(root);
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
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

sealed class VerificationClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    public void Advance(TimeSpan amount) => _now += amount;
}
