using HealthReminder.Models;

namespace HealthReminder.Services;

public sealed record ReminderNormalizationResult(
    List<ReminderItem> Reminders,
    bool Changed);

/// <summary>
/// 修復舊版或損壞設定。提醒類型只保留作為歷史分類，不限制使用者建立數量。
/// </summary>
public static class ReminderCollectionNormalizer
{
    public static ReminderNormalizationResult Normalize(IEnumerable<ReminderItem> reminders)
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            normalized.Add(reminder);
        }

        return new ReminderNormalizationResult(normalized, changed);
    }
}
