using HealthReminder.Models;

namespace HealthReminder.Services;

public sealed record ReminderNormalizationResult(
    List<ReminderItem> Reminders,
    bool Changed);

/// <summary>
/// 修復舊版或損壞設定。提醒類型只保留作為歷史分類，不限制使用者建立數量；
/// 但會移除所有設定欄位皆相同、只有 Id 不同的重複資料。
/// </summary>
public static class ReminderCollectionNormalizer
{
    public static ReminderNormalizationResult Normalize(IEnumerable<ReminderItem> reminders)
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedDefinitions = new HashSet<ReminderDefinition>();
        var normalized = new List<ReminderItem>();
        var changed = false;

        foreach (var reminder in reminders)
        {
            if (string.IsNullOrWhiteSpace(reminder.Id) || !usedIds.Add(reminder.Id))
            {
                reminder.Id = Guid.NewGuid().ToString();
                usedIds.Add(reminder.Id);
                changed = true;
            }

            if (!Enum.IsDefined(reminder.ScheduleKind))
            {
                reminder.ScheduleKind = ReminderScheduleKind.Interval;
                changed = true;
            }

            if (!Enum.IsDefined(reminder.ActionKind))
            {
                reminder.ActionKind = ReminderActionKind.Manual;
                changed = true;
            }

            var interval = Math.Clamp(reminder.IntervalMinutes, 1, 1440);
            if (interval != reminder.IntervalMinutes)
            {
                reminder.IntervalMinutes = interval;
                changed = true;
            }

            var activitySeconds = Math.Clamp(reminder.ActivityDurationSeconds, 5, 3600);
            if (activitySeconds != reminder.ActivityDurationSeconds)
            {
                reminder.ActivityDurationSeconds = activitySeconds;
                changed = true;
            }

            if (reminder.ScheduleKind == ReminderScheduleKind.OneTime && reminder.OneTimeAtLocal == null)
            {
                reminder.OneTimeAtLocal = DateTime.Now.AddMinutes(reminder.IntervalMinutes);
                changed = true;
            }

            if (!usedDefinitions.Add(ReminderDefinition.From(reminder)))
            {
                changed = true;
                continue;
            }

            normalized.Add(reminder);
        }

        return new ReminderNormalizationResult(normalized, changed);
    }

    private sealed record ReminderDefinition(
        ReminderType Type,
        string Name,
        string Message,
        int IntervalMinutes,
        ReminderScheduleKind ScheduleKind,
        DateTime? OneTimeAtLocal,
        ReminderActionKind ActionKind,
        int ActivityDurationSeconds,
        bool AutoStartActivity,
        bool IsEnabled,
        bool PlaySound,
        string Emoji)
    {
        public static ReminderDefinition From(ReminderItem item)
        {
            return new ReminderDefinition(
                item.Type,
                item.Name,
                item.Message,
                item.IntervalMinutes,
                item.ScheduleKind,
                item.OneTimeAtLocal,
                item.ActionKind,
                item.ActivityDurationSeconds,
                item.AutoStartActivity,
                item.IsEnabled,
                item.PlaySound,
                item.Emoji);
        }
    }
}
