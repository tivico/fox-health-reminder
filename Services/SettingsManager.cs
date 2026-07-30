using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HealthReminder.Models;

namespace HealthReminder.Services;

/// <summary>
/// 設定檔管理服務
/// </summary>
public class SettingsManager
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HealthReminder");
    
    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");
    private static readonly string StatisticsPath = Path.Combine(SettingsFolder, "statistics.json");

    private static SettingsManager? _instance;
    private static readonly object _lock = new object();

    public AppSettings Settings { get; private set; } = new AppSettings();
    public List<DailyStatistics> Statistics { get; private set; } = new List<DailyStatistics>();
    public string? LastError { get; private set; }

    /// <summary>
    /// 單例模式取得實例
    /// </summary>
    public static SettingsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SettingsManager();
                }
            }
            return _instance;
        }
    }

    private SettingsManager()
    {
        EnsureDirectoryExists();
        Load();
    }

    /// <summary>
    /// 確保設定資料夾存在
    /// </summary>
    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SettingsFolder))
        {
            Directory.CreateDirectory(SettingsFolder);
        }
    }

    /// <summary>
    /// 載入設定
    /// </summary>
    public void Load()
    {
        var settingsChanged = false;

        try
        {
            // 載入應用程式設定
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var storedSchemaVersion = JObject.Parse(json).Value<int?>(nameof(AppSettings.SchemaVersion)) ?? 1;
                Settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                settingsChanged = MigrateSettings(storedSchemaVersion);

                // 修復舊設定欄位與重複 Id；不限制任何提醒範本的數量。
                settingsChanged |= NormalizeReminders();
                settingsChanged |= NormalizeWorkday();
            }

            // 載入統計資料
            if (File.Exists(StatisticsPath))
            {
                var json = File.ReadAllText(StatisticsPath);
                Statistics = JsonConvert.DeserializeObject<List<DailyStatistics>>(json) ?? new List<DailyStatistics>();
            }

            // 清理超過 30 天的統計資料
            CleanOldStatistics();

            // 必須等設定與統計都載入後再寫回，避免覆蓋既有統計。
            if (settingsChanged)
            {
                Save();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"載入設定失敗: {ex.Message}");
            Settings = new AppSettings();
            Statistics = new List<DailyStatistics>();
        }
    }

    /// <summary>
    /// 確保提醒項目的 Id 存在且不重複。
    /// </summary>
    private bool NormalizeReminders()
    {
        var result = ReminderCollectionNormalizer.Normalize(Settings.Reminders);
        Settings.Reminders = result.Reminders;
        return result.Changed;
    }

    private bool NormalizeWorkday()
    {
        if (DailyAchievementCalculator.IsValidWorkday(Settings.WorkdayStart, Settings.WorkdayEnd))
        {
            return false;
        }

        Settings.WorkdayStart = new TimeSpan(9, 0, 0);
        Settings.WorkdayEnd = new TimeSpan(18, 0, 0);
        return true;
    }

    private bool MigrateSettings(int storedSchemaVersion)
    {
        var changed = false;
        if (storedSchemaVersion < 2)
        {
            foreach (var item in Settings.Reminders.Where(item => item.Type == ReminderType.EyeRest))
            {
                item.ActionKind = ReminderActionKind.TimedActivity;
                item.ActivityDurationSeconds = 20;
                item.AutoStartActivity = false;
            }
            changed = true;
        }

        if (Settings.SchemaVersion != AppSettings.CurrentSchemaVersion)
        {
            Settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// 重設為預設值
    /// </summary>
    public void ResetToDefaults()
    {
        Settings = new AppSettings();
        Save();
    }

    /// <summary>
    /// 儲存設定
    /// </summary>
    public bool Save()
    {
        LastError = null;

        try
        {
            EnsureDirectoryExists();

            // 儲存應用程式設定
            var settingsJson = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            WriteAllTextAtomically(SettingsPath, settingsJson);

            // 儲存統計資料
            var statsJson = JsonConvert.SerializeObject(Statistics, Formatting.Indented);
            WriteAllTextAtomically(StatisticsPath, statsJson);
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"無法儲存設定：{ex.Message}";
            System.Diagnostics.Debug.WriteLine(LastError);
            return false;
        }
    }

    private static void WriteAllTextAtomically(string path, string contents)
    {
        var temporaryPath = path + ".tmp";

        try
        {
            File.WriteAllText(temporaryPath, contents);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// 計算「今日成就」的目標次數：整個工作時段內，所有啟用提醒預計出現的總次數。
    /// 作為進度條 100% 的基準，讓「完成一整天份」才算達標。
    /// </summary>
    public int GetDailyGoalTarget()
    {
        return DailyAchievementCalculator.CalculateGoal(
            Settings.Reminders,
            DateTime.Today,
            Settings.WorkdayStart,
            Settings.WorkdayEnd);
    }

    /// <summary>
    /// 取得今日統計
    /// </summary>
    public DailyStatistics GetTodayStatistics()
    {
        var today = Statistics.FirstOrDefault(s => s.Date.Date == DateTime.Today);
        if (today == null)
        {
            today = new DailyStatistics { Date = DateTime.Today };
            Statistics.Add(today);
        }
        return today;
    }

    /// <summary>
    /// 新增提醒紀錄
    /// </summary>
    public bool AddRecord(ReminderRecord record)
    {
        var today = GetTodayStatistics();
        today.Records.Add(record);
        return Save();
    }

    /// <summary>
    /// 記錄一筆已實際顯示、尚未由使用者處理的提醒。
    /// </summary>
    public ReminderRecord AddTriggeredRecord(ReminderItem item)
    {
        var record = new ReminderRecord
        {
            ReminderId = item.Id,
            Type = item.Type,
            Name = item.Name,
            Timestamp = DateTime.Now,
            Status = ReminderStatus.Triggered
        };

        AddRecord(record);
        return record;
    }

    /// <summary>
    /// 更新既有提醒事件的狀態，避免完成或稍後動作重複新增紀錄。
    /// </summary>
    public bool UpdateRecordStatus(string occurrenceId, ReminderStatus status)
    {
        var record = Statistics
            .SelectMany(day => day.Records)
            .FirstOrDefault(item => item.Id == occurrenceId);

        if (record == null)
        {
            LastError = "找不到這次提醒紀錄，狀態未更新。";
            return false;
        }

        if (record.Status == status)
        {
            return true;
        }

        // 完成、略過或稍後皆是同一次提醒的最終使用者動作。
        if (record.Status != ReminderStatus.Triggered)
        {
            LastError = "這次提醒已經處理過了。";
            return false;
        }

        record.Status = status;
        return Save();
    }

    /// <summary>
    /// 清理超過 30 天的統計資料
    /// </summary>
    private void CleanOldStatistics()
    {
        var cutoff = DateTime.Today.AddDays(-30);
        Statistics.RemoveAll(s => s.Date < cutoff);
    }

    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "HealthReminder";

    /// <summary>
    /// 設定開機自動啟動
    /// </summary>
    public void SetStartWithWindows(bool enabled)
    {
        Settings.StartWithWindows = enabled;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true);

            if (key != null)
            {
                if (enabled)
                {
                    key.SetValue(RunValueName, $"\"{Application.ExecutablePath}\"");
                }
                else
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定開機啟動失敗: {ex.Message}");
        }

        Save();
    }

    /// <summary>
    /// 校正開機自動啟動的登錄項，讓它永遠指向目前實際執行的執行檔。
    /// 避免搬移資料夾或重新發佈後，登錄項仍指向已不存在的舊路徑而失效。
    /// </summary>
    public void SyncStartWithWindowsPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            var registered = key.GetValue(RunValueName) as string;

            if (!Settings.StartWithWindows)
            {
                // 設定已關閉，但登錄項還留著就順手清掉，兩邊保持一致。
                if (registered != null)
                {
                    key.DeleteValue(RunValueName, false);
                }
                return;
            }

            var expected = $"\"{Application.ExecutablePath}\"";
            if (!string.Equals(registered, expected, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(RunValueName, expected);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"校正開機啟動路徑失敗: {ex.Message}");
        }
    }
}
