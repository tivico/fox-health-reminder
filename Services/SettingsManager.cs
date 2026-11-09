using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HealthReminder.Models;

namespace HealthReminder.Services;

/// <summary>
/// 設定與統計各自讀寫。跨兩個檔案的提醒操作先寫入可重播的提交紀錄。
/// </summary>
public sealed class SettingsManager
{
    private static SettingsManager? _instance;
    private static readonly object _lock = new();
    private static StorageLocationSelection? _configuredStorage;
    private readonly string _settingsPath;
    private readonly string _statisticsPath;
    private readonly string _commitPath;
    private string? _settingsReadError;
    private string? _statisticsReadError;
    private string? _migrationWarning;

    public AppSettings Settings { get; private set; } = new();
    public List<DailyStatistics> Statistics { get; private set; } = new();
    public string? LastError { get; private set; }
    public string? LoadWarning => string.Join(Environment.NewLine,
        new[] { _settingsReadError, _statisticsReadError, _migrationWarning }.OfType<string>()) is { Length: > 0 } warning
        ? warning
        : null;
    public string StorageFolder { get; }
    public bool UsesPortableStorage { get; }
    public string? StorageNotice { get; }

    public static SettingsManager Instance
    {
        get
        {
            lock (_lock)
            {
                return _instance ??= new SettingsManager(_configuredStorage ?? new StorageLocationSelection(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "HealthReminder"), false));
            }
        }
    }

    internal static void ConfigureStorage(StorageLocationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        lock (_lock)
        {
            if (_instance != null)
            {
                throw new InvalidOperationException("設定管理服務初始化後不可再變更資料位置。");
            }
            _configuredStorage = selection;
        }
    }

    internal SettingsManager(StorageLocationSelection storage)
    {
        StorageFolder = Path.GetFullPath(storage.Folder);
        UsesPortableStorage = storage.IsPortable;
        StorageNotice = storage.Notice;
        _settingsPath = Path.Combine(StorageFolder, "settings.json");
        _statisticsPath = Path.Combine(StorageFolder, "statistics.json");
        _commitPath = Path.Combine(StorageFolder, "pending-commit.json");

        Directory.CreateDirectory(StorageFolder);
        try { FlushPendingCommit(); }
        catch (Exception ex) when (IsStorageError(ex))
        {
            throw new IOException($"無法恢復 pending-commit.json。請保留提交檔及兩份 JSON 後再排查。\n{ex.Message}", ex);
        }
        Load();
    }

    private void Load()
    {
        try
        {
            if (ReadIfPresent(_settingsPath) is { } json)
            {
                var version = JObject.Parse(json).Value<int?>(nameof(AppSettings.SchemaVersion)) ?? 1;
                if (version > AppSettings.CurrentSchemaVersion)
                {
                    throw new InvalidDataException("此檔案來自更新版本的程式。");
                }

                var loaded = Deserialize<AppSettings>(json);
                if (loaded.Reminders == null || loaded.Reminders.Any(item => item == null))
                {
                    throw new InvalidDataException("提醒清單含有空值。");
                }
                var normalized = ReminderCollectionNormalizer.Normalize(loaded.Reminders);
                loaded.Reminders = normalized.Reminders;

                if (version < 2)
                {
                    foreach (var item in loaded.Reminders.Where(item => item.Type == ReminderType.EyeRest))
                    {
                        item.ActionKind = ReminderActionKind.TimedActivity;
                        item.ActivityDurationSeconds = 20;
                        item.AutoStartActivity = false;
                    }
                }

                loaded.SchemaVersion = AppSettings.CurrentSchemaVersion;
                var snoozeMinutes = Math.Clamp(loaded.SnoozeMinutes, 1, 60);
                var invalidWorkday = !DailyAchievementCalculator.IsValidWorkday(loaded.WorkdayStart, loaded.WorkdayEnd)
                    || loaded.WorkdayEnd == TimeSpan.FromDays(1);
                var invalidQuietHours = !IsTimeOfDay(loaded.DoNotDisturbStart)
                    || !IsTimeOfDay(loaded.DoNotDisturbEnd);
                var changed = normalized.Changed || version != loaded.SchemaVersion || invalidWorkday
                    || invalidQuietHours || snoozeMinutes != loaded.SnoozeMinutes;
                loaded.SnoozeMinutes = snoozeMinutes;
                if (invalidWorkday)
                {
                    loaded.WorkdayStart = TimeSpan.FromHours(9);
                    loaded.WorkdayEnd = TimeSpan.FromHours(18);
                }
                if (invalidQuietHours)
                {
                    loaded.DoNotDisturbStart = TimeSpan.FromHours(12);
                    loaded.DoNotDisturbEnd = TimeSpan.FromHours(13);
                    loaded.DoNotDisturbEnabled = false;
                }

                Settings = loaded;
                if (changed && !SaveSettings(loaded))
                {
                    _migrationWarning = $"設定已在記憶體修正，但尚未寫回 settings.json。{Environment.NewLine}{LastError}";
                }
            }
        }
        catch (Exception ex) when (IsStorageError(ex) || ex is ArgumentException or FormatException
            or OverflowException)
        {
            _settingsReadError = ReadError(_settingsPath, ex);
            Settings = new AppSettings { Reminders = new() };
        }

        try
        {
            if (ReadIfPresent(_statisticsPath) is { } json)
            {
                var loaded = Deserialize<List<DailyStatistics>>(json);
                if (loaded.Any(day => day == null || day.Records == null
                    || day.Records.Any(record => record == null)))
                {
                    throw new InvalidDataException("統計清單含有空值。");
                }

                var cutoff = DateTime.Today.AddDays(-30);
                Statistics = loaded.Where(day => day.Date >= cutoff).ToList();
            }
        }
        catch (Exception ex) when (IsStorageError(ex) || ex is ArgumentException or FormatException
            or OverflowException)
        {
            _statisticsReadError = ReadError(_statisticsPath, ex);
        }
    }

    internal bool SaveSettings(AppSettings settings) => Commit(settings, null);

    internal bool DeleteReminder(string id)
    {
        if (!Settings.Reminders.Any(item => item.Id == id)) return true;

        var candidate = Settings.Copy();
        candidate.Reminders.RemoveAll(item => item.Id == id);
        return SaveSettings(candidate);
    }

    public int GetDailyGoalTarget()
    {
        var today = GetTodayStatistics();
        return DailyAchievementCalculator.CalculateGoal(
            Settings.Reminders, today.Date, Settings.WorkdayStart, Settings.WorkdayEnd, today.Records);
    }

    public DailyStatistics GetTodayStatistics() =>
        Statistics.FirstOrDefault(day => day.Date.Date == DateTime.Today)
            ?? new DailyStatistics { Date = DateTime.Today };

    internal bool ContainsRecord(string occurrenceId) =>
        Statistics.Any(day => day.Records.Any(record => record.Id == occurrenceId));

    internal bool HasTriggeredRecord(string id, string occurrenceId) =>
        Statistics.Any(day => day.Records.Any(record => record.Id == occurrenceId
            && record.ReminderId == id && record.Status == ReminderStatus.Triggered));

    internal bool RecordShown(ReminderItem item, string occurrenceId)
    {
        var statistics = new List<DailyStatistics>(Statistics);
        var today = GetTodayStatistics();
        statistics.Remove(today);
        statistics.Add(new DailyStatistics
        {
            Date = today.Date,
            Records = new List<ReminderRecord>(today.Records)
            {
                new ReminderRecord
                {
                    Id = occurrenceId,
                    ReminderId = item.Id,
                    Type = item.Type,
                    Name = item.Name,
                    Timestamp = DateTime.Now,
                    OneTimeScheduledAtLocal = item.ScheduleKind == ReminderScheduleKind.OneTime
                        ? item.OneTimeAtLocal : null
                }
            }
        });

        AppSettings? settings = null;
        if (item.ScheduleKind == ReminderScheduleKind.OneTime)
        {
            settings = Settings.Copy();
            settings.Reminders.Single(reminder => reminder.Id == item.Id).IsEnabled = false;
        }
        return Commit(settings, statistics);
    }

    internal bool UpdateRecordStatus(string occurrenceId, ReminderStatus status, AppSettings? settings = null)
    {
        var dayIndex = Statistics.FindIndex(day => day.Records.Any(record => record.Id == occurrenceId));
        if (dayIndex < 0)
        {
            return Fail("找不到這次提醒紀錄，狀態未更新。");
        }

        var day = Statistics[dayIndex];
        var recordIndex = day.Records.FindIndex(record => record.Id == occurrenceId);
        var record = day.Records[recordIndex];
        if (record.Status != ReminderStatus.Triggered)
        {
            if (record.Status == status && settings == null) return true;
            return Fail("這次提醒已經處理過了。");
        }

        var records = new List<ReminderRecord>(day.Records);
        records[recordIndex] = record.WithStatus(status);
        var statistics = new List<DailyStatistics>(Statistics);
        statistics[dayIndex] = new DailyStatistics { Date = day.Date, Records = records };
        return Commit(settings, statistics);
    }

    private bool Commit(AppSettings? settings, List<DailyStatistics>? statistics)
    {
        LastError = null;
        if (settings != null && _settingsReadError != null) return Fail(_settingsReadError);
        if (statistics != null && _statisticsReadError != null) return Fail(_statisticsReadError);

        try
        {
            Directory.CreateDirectory(StorageFolder);
            FlushPendingCommit();

            if (settings != null && statistics != null)
            {
                var commit = new PendingCommit(
                    JsonConvert.SerializeObject(settings, Formatting.Indented),
                    JsonConvert.SerializeObject(statistics, Formatting.Indented));
                WriteAtomically(_commitPath, JsonConvert.SerializeObject(commit), keepBackup: false);

                // 提交紀錄已落盤。展開失敗時保留它，下次寫入或啟動會重播。
                try { FlushPendingCommit(); }
                catch (Exception ex) when (IsStorageError(ex))
                {
                    LastError = $"已保存操作，等待同步 JSON：{ex.Message}";
                    System.Diagnostics.Debug.WriteLine(LastError);
                }
            }
            else if (settings != null)
            {
                WriteAtomically(_settingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            else if (statistics != null)
            {
                WriteAtomically(_statisticsPath, JsonConvert.SerializeObject(statistics, Formatting.Indented));
            }

            if (settings != null) Settings = settings;
            if (statistics != null) Statistics = statistics;
            return true;
        }
        catch (Exception ex) when (IsStorageError(ex))
        {
            return Fail($"無法儲存資料：{ex.Message}");
        }
    }

    private void FlushPendingCommit()
    {
        if (ReadIfPresent(_commitPath) is not { } json) return;

        var pending = Deserialize<PendingCommit>(json);
        if (string.IsNullOrWhiteSpace(pending.SettingsJson) || string.IsNullOrWhiteSpace(pending.StatisticsJson))
        {
            throw new InvalidDataException("待同步提交缺少設定或統計資料。");
        }
        // 驗證兩份內容都可解析後才替換任何檔案。
        Deserialize<AppSettings>(pending.SettingsJson);
        Deserialize<List<DailyStatistics>>(pending.StatisticsJson);
        if (ReadIfPresent(_settingsPath) != pending.SettingsJson)
        {
            WriteAtomically(_settingsPath, pending.SettingsJson);
        }
        if (ReadIfPresent(_statisticsPath) != pending.StatisticsJson)
        {
            WriteAtomically(_statisticsPath, pending.StatisticsJson);
        }
        File.Delete(_commitPath);
    }

    private static void WriteAtomically(string path, string contents, bool keepBackup = true)
    {
        var temporaryPath = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write))
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (keepBackup && File.Exists(path))
            {
                File.Replace(temporaryPath, path, path + ".bak");
            }
            else
            {
                File.Move(temporaryPath, path, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private bool Fail(string message)
    {
        LastError = message;
        System.Diagnostics.Debug.WriteLine(message);
        return false;
    }

    private static T Deserialize<T>(string json) =>
        JsonConvert.DeserializeObject<T>(json) ?? throw new InvalidDataException("JSON 內容為空值。");

    private static string? ReadIfPresent(string path)
    {
        try { return File.ReadAllText(path); }
        catch (FileNotFoundException) { return null; }
    }

    private static bool IsTimeOfDay(TimeSpan value) => value >= TimeSpan.Zero && value < TimeSpan.FromDays(1);

    private static bool IsStorageError(Exception ex) =>
        ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException;

    private static string ReadError(string path, Exception ex) =>
        $"無法讀取 {Path.GetFileName(path)}，原始檔案已保留，請備份並修復後再啟動。{Environment.NewLine}{path}{Environment.NewLine}{ex.Message}";

    private sealed record PendingCommit(string SettingsJson, string StatisticsJson);
}
