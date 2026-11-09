using HealthReminder.Models;

namespace HealthReminder.Services;

/// <summary>
/// 「今日成就」與連續達標的純計算邏輯，避免 UI 與設定儲存各自使用不同規則。
/// </summary>
public static class DailyAchievementCalculator
{
    public const double StreakCompletionRate = 0.5;

    public static bool IsValidWorkday(TimeSpan start, TimeSpan end)
    {
        return start >= TimeSpan.Zero
            && end <= TimeSpan.FromDays(1)
            && start < end;
    }

    public static int CalculateGoal(
        IEnumerable<ReminderItem> reminders,
        DateTime date,
        TimeSpan workdayStart,
        TimeSpan workdayEnd,
        IEnumerable<ReminderRecord>? shown = null)
    {
        if (!IsValidWorkday(workdayStart, workdayEnd)) return 0;

        var day = date.Date;
        var windowStart = day.Add(workdayStart);
        var windowEnd = day.Add(workdayEnd);
        var windowMinutes = (workdayEnd - workdayStart).TotalMinutes;
        var target = 0;

        var oneTimeGoals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reminder in reminders)
        {
            if (reminder.ScheduleKind == ReminderScheduleKind.OneTime)
            {
                if (reminder.OneTimeAtLocal is { } at
                    && at >= windowStart
                    && at <= windowEnd
                    && (reminder.IsEnabled || shown?.Any(record => record.ReminderId == reminder.Id
                        && record.Timestamp >= at) == true))
                {
                    oneTimeGoals.Add(reminder.Id);
                }
                continue;
            }

            if (reminder.IsEnabled && reminder.IntervalMinutes > 0)
            {
                target += (int)(windowMinutes / reminder.IntervalMinutes);
            }
        }

        if (shown != null)
        {
            foreach (var record in shown)
            {
                if (record.OneTimeScheduledAtLocal is { } at && at >= windowStart && at <= windowEnd)
                {
                    oneTimeGoals.Add(record.ReminderId);
                }
            }
        }
        return target + oneTimeGoals.Count;
    }

    public static int CountCompleted(
        DailyStatistics statistics,
        TimeSpan workdayStart,
        TimeSpan workdayEnd)
    {
        if (!IsValidWorkday(workdayStart, workdayEnd)) return 0;

        var windowStart = statistics.Date.Date.Add(workdayStart);
        var windowEnd = statistics.Date.Date.Add(workdayEnd);
        return statistics.Records.Count(record =>
            record.Status == ReminderStatus.Completed
            && record.Timestamp >= windowStart
            && record.Timestamp <= windowEnd);
    }

    public static int CalculateStreak(
        IEnumerable<DailyStatistics> statistics,
        DateTime now,
        TimeSpan workdayStart,
        TimeSpan workdayEnd)
    {
        if (!IsValidWorkday(workdayStart, workdayEnd)) return 0;

        var recordsByDate = statistics
            .GroupBy(day => day.Date.Date)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(day => day.Records).ToList());

        bool IsAchieved(DateTime date)
        {
            if (!recordsByDate.TryGetValue(date.Date, out var records)) return false;

            var windowStart = date.Date.Add(workdayStart);
            var windowEnd = date.Date.Add(workdayEnd);
            var eligible = records
                .Where(record => record.Timestamp >= windowStart && record.Timestamp <= windowEnd)
                .ToList();
            if (eligible.Count == 0) return false;

            var completed = eligible.Count(record => record.Status == ReminderStatus.Completed);
            return completed / (double)eligible.Count >= StreakCompletionRate;
        }

        var cursor = now.Date;
        if (!IsAchieved(cursor))
        {
            // 今天尚未結束時，不讓仍在進行中的一天中斷昨天以前的紀錄。
            if (now.TimeOfDay <= workdayEnd)
            {
                cursor = cursor.AddDays(-1);
            }
            else
            {
                return 0;
            }
        }

        var streak = 0;
        while (IsAchieved(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }
}
