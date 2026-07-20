using Newtonsoft.Json;
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
        try
        {
            // 載入應用程式設定
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                
                // 確保每個提醒都有唯一識別碼；允許多個 Custom 提醒。
                NormalizeReminderIds();
            }

            // 載入統計資料
            if (File.Exists(StatisticsPath))
            {
                var json = File.ReadAllText(StatisticsPath);
                Statistics = JsonConvert.DeserializeObject<List<DailyStatistics>>(json) ?? new List<DailyStatistics>();
            }

            // 清理超過 30 天的統計資料
            CleanOldStatistics();
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
    private void NormalizeReminderIds()
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var reminder in Settings.Reminders)
        {
            if (string.IsNullOrWhiteSpace(reminder.Id) || !usedIds.Add(reminder.Id))
            {
                reminder.Id = Guid.NewGuid().ToString();
                usedIds.Add(reminder.Id);
                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }
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

    /// <summary>
    /// 設定開機自動啟動
    /// </summary>
    public void SetStartWithWindows(bool enabled)
    {
        Settings.StartWithWindows = enabled;
        
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            
            if (key != null)
            {
                if (enabled)
                {
                    var exePath = Application.ExecutablePath;
                    key.SetValue("HealthReminder", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("HealthReminder", false);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定開機啟動失敗: {ex.Message}");
        }

        Save();
    }
}
