using HealthReminder.Models;

namespace HealthReminder.Services;

public sealed record ReminderNormalizationResult(
    List<ReminderItem> Reminders,
    bool Changed);

/// <summary>
/// 修復舊版或損壞設定中的提醒識別碼與內建類型重複資料。
/// </summary>
public static class ReminderCollectionNormalizer
{
    public static ReminderNormalizationResult Normalize(IEnumerable<ReminderItem> reminders)
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedBuiltInTypes = new HashSet<ReminderType>();
        var normalized = new List<ReminderItem>();
        var changed = false;

        foreach (var reminder in reminders)
        {
            if (reminder.Type != ReminderType.Custom && !usedBuiltInTypes.Add(reminder.Type))
            {
                changed = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(reminder.Id) || !usedIds.Add(reminder.Id))
            {
                reminder.Id = Guid.NewGuid().ToString();
                usedIds.Add(reminder.Id);
                changed = true;
            }

            normalized.Add(reminder);
        }

        return new ReminderNormalizationResult(normalized, changed);
    }
}
