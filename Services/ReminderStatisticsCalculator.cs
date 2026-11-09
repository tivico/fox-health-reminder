using HealthReminder.Models;

namespace HealthReminder.Services;

public sealed record ReminderStatisticsRow(
    string ReminderId,
    string DisplayName,
    string Emoji,
    ReminderType Type,
    bool IsHistorical,
    int Completed,
    int Total);

/// <summary>只計算統計列，不變更設定或歷史紀錄。同名的不同 ID 不代表同一個提醒。</summary>
public static class ReminderStatisticsCalculator
{
    public static IReadOnlyList<ReminderStatisticsRow> BuildRows(
        IEnumerable<ReminderRecord> records, IEnumerable<ReminderItem> reminders)
    {
        var configured = reminders.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var rows = records.GroupBy(record => string.IsNullOrWhiteSpace(record.ReminderId)
                ? new StatisticsKey(null, record.Type, record.Name)
                : new StatisticsKey(record.ReminderId, default, string.Empty))
            .Select(group =>
            {
                var first = group.First();
                var current = configured.GetValueOrDefault(first.ReminderId ?? string.Empty);
                var historical = current == null;
                var name = current?.Name ?? first.Name;
                if (string.IsNullOrWhiteSpace(name)) name = "未命名提醒";
                return new ReminderStatisticsRow(
                    first.ReminderId ?? string.Empty,
                    historical ? $"{name}（歷史項目）" : name,
                    current?.Emoji ?? EmojiFor(first.Type),
                    current?.Type ?? first.Type,
                    historical,
                    group.Count(record => record.Status == ReminderStatus.Completed),
                    group.Count());
            })
            .OrderBy(row => row.DisplayName)
            .ThenBy(row => row.Type)
            .ThenBy(row => row.ReminderId, StringComparer.Ordinal)
            .ToList();

        // 多個歷史 ID 可以同名，編號只用於顯示，不將紀錄合併或重新指定 ID。
        foreach (var group in rows.Where(row => row.IsHistorical).GroupBy(row => row.DisplayName).Where(group => group.Count() > 1))
        {
            var index = 1;
            foreach (var row in group)
            {
                var position = rows.IndexOf(row);
                var name = row.DisplayName[..^"（歷史項目）".Length];
                rows[position] = row with { DisplayName = $"{name}（歷史項目 {index++}）" };
            }
        }
        return rows;
    }

    private static string EmojiFor(ReminderType type) => type switch
    {
        ReminderType.Water => "💧",
        ReminderType.EyeRest => "👀",
        ReminderType.Stretch => "🏃",
        _ => "💡"
    };

    private sealed record StatisticsKey(string? ReminderId, ReminderType Type, string Name);
}
