using HealthReminder.Controls;
using HealthReminder.Models;
using HealthReminder.Services;
using HealthReminder.UI;

namespace HealthReminder;

public partial class MainForm
{
    #region 統計頁面

    private void UpdateStatisticsDisplay()
    {
        var today = SettingsManager.Instance.GetTodayStatistics();
        var settings = SettingsManager.Instance.Settings;

        int totalCompleted = 0;
        int totalCount = 0;

        var reminderRows = ReminderStatisticsCalculator.BuildRows(today.Records, settings.Reminders);

        _statCardsFlow?.SuspendLayout();
        if (_statCardsFlow != null)
        {
            while (_statCardsFlow.Controls.Count > 0)
            {
                var stale = _statCardsFlow.Controls[0];
                _statCardsFlow.Controls.RemoveAt(0);
                stale.Dispose();
            }
        }

        var cardWidth = _statCardsFlow != null ? GetFlowCardWidth(_statCardsFlow) : 320;

        foreach (var row in reminderRows)
        {
            var completed = row.Completed;
            var total = row.Total;
            if (total <= 0) continue;

            var accent = AppTheme.AccentFor(row.Type);

            if (_statCardsFlow != null)
            {
                var card = new StatRowCard { Width = cardWidth };
                card.SetData(row.Emoji, row.DisplayName, completed, total, accent, emphasize: false);
                _statCardsFlow.Controls.Add(card);
            }

            totalCompleted += completed;
            totalCount += total;
        }

        if (_statCardsFlow != null)
        {
            if (totalCount > 0)
            {
                var totalCard = new StatRowCard { Width = cardWidth };
                totalCard.SetData("📊", "總計", totalCompleted, totalCount, AppTheme.Primary, emphasize: true);
                _statCardsFlow.Controls.Add(totalCard);
            }
            else
            {
                _statCardsFlow.Controls.Add(new Label
                {
                    AutoSize = false,
                    Width = cardWidth,
                    Height = 44,
                    Margin = new Padding(2, 10, 0, 0),
                    Font = new Font(AppTheme.FontFamily, 9.5F),
                    ForeColor = AppTheme.SecondaryText,
                    Text = "今天還沒有提醒紀錄，等第一個提醒出現吧 🦊",
                    TextAlign = ContentAlignment.MiddleLeft
                });
            }

            _statCardsFlow.ResumeLayout(true);
        }

        // 今日成就進度：以「整個工作時段預計出現的提醒總數」為 100% 目標
        var goalTarget = SettingsManager.Instance.GetDailyGoalTarget();
        var goalCompleted = DailyAchievementCalculator.CountCompleted(
            today,
            settings.WorkdayStart,
            settings.WorkdayEnd);
        var goalPercent = goalTarget > 0
            ? Math.Min(100, (int)Math.Round(goalCompleted * 100.0 / goalTarget))
            : 0;
        progressBarToday.Value = goalPercent;
        lblProgressText.Text = goalTarget > 0
            ? $"目標時段 {settings.WorkdayStart:hh\\:mm}–{settings.WorkdayEnd:hh\\:mm}：{goalCompleted} / {goalTarget} 次 · {goalPercent}%"
            : "每日目標計算時段內沒有預計提醒";

        // 更新今日成就標題
        lblTodaySummary.Text = totalCompleted > 0
            ? $"今天已完成 {totalCompleted} 次提醒"
            : "今天的健康紀錄，從下一次提醒開始";

        // 工作時段內至少完成一半已觸發提醒，才算該日達標。
        int streakDays = CalculateStreakDays();
        lblStreakDays.Text = $"🔥 連續達標：{streakDays} 天";

        // 更新小狐狸訊息
        lblFoxMessage.Text = GetFoxMessage(goalPercent, streakDays);
    }

    /// <summary>
    /// 計算連續達標天數；今天尚未結束時不會提早中斷既有紀錄。
    /// </summary>
    private int CalculateStreakDays()
    {
        var settings = SettingsManager.Instance.Settings;
        return DailyAchievementCalculator.CalculateStreak(
            SettingsManager.Instance.Statistics,
            DateTime.Now,
            settings.WorkdayStart,
            settings.WorkdayEnd);
    }

    /// <summary>
    /// 取得小狐狸鼓勵訊息
    /// </summary>
    private string GetFoxMessage(int percent, int streakDays)
    {
        if (streakDays >= 7)
        {
            return $"🦊 小狐狸說：已經連續達標 {streakDays} 天了，你真的很有毅力！";
        }
        if (percent <= 0)
        {
            string[] messages = {
                "🦊 小狐狸說：新的一天開始囉，一起養成好習慣吧！",
                "🦊 小狐狸說：今天也要記得照顧自己喔～",
                "🦊 小狐狸說：健康是最重要的財富！"
            };
            return messages[DateTime.Now.Second % messages.Length];
        }
        else if (percent < 50)
        {
            string[] messages = {
                "🦊 小狐狸說：不錯喔，繼續保持！",
                "🦊 小狐狸說：你做得很好，再接再厲！",
                "🦊 小狐狸說：每一次提醒都是對自己的關愛～"
            };
            return messages[DateTime.Now.Second % messages.Length];
        }
        else if (percent < 100)
        {
            string[] messages = {
                "🦊 小狐狸說：哇！你好棒，已經完成這麼多了！",
                "🦊 小狐狸說：繼續加油，你是健康小達人！",
                "🦊 小狐狸說：看到你這麼認真，小狐狸好開心！"
            };
            return messages[DateTime.Now.Second % messages.Length];
        }
        else
        {
            string[] messages = {
                "🦊 小狐狸說：太厲害了！你是今日健康冠軍！🏆",
                "🦊 小狐狸說：哇嗚～你超棒的！小狐狸給你一個大擁抱！",
                "🦊 小狐狸說：你已經養成超棒的健康習慣了！"
            };
            return messages[DateTime.Now.Second % messages.Length];
        }
    }

    #endregion

    private void BuildStatisticsPage()
    {
        panelStatistics.SuspendLayout();
        panelStatistics.Controls.Clear();
        panelStatistics.BackColor = AppTheme.WindowBackground;

        var layout = new ElevatedPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.WindowBackground,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 8, 12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var summaryCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(22, 16, 22, 16)
        };
        var summaryLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty
        };
        summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        summaryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        summaryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lblTodaySummary.AutoSize = false;
        lblTodaySummary.AutoEllipsis = true;
        lblTodaySummary.Dock = DockStyle.Fill;
        lblTodaySummary.Font = new Font(AppTheme.FontFamily, 12F, FontStyle.Bold);
        lblStreakDays.Dock = DockStyle.Fill;
        lblStreakDays.AutoSize = false;
        lblStreakDays.Font = new Font(AppTheme.FontFamily, 9F);
        progressBarToday.Dock = DockStyle.Fill;
        progressBarToday.Margin = new Padding(0, 3, 0, 3);
        lblProgressText.Dock = DockStyle.Fill;
        lblProgressText.AutoSize = false;
        lblProgressText.ForeColor = AppTheme.SecondaryText;
        lblProgressText.Font = new Font(AppTheme.FontFamily, 8.5F);
        summaryLayout.Controls.Add(lblTodaySummary, 0, 0);
        summaryLayout.Controls.Add(lblStreakDays, 0, 1);
        summaryLayout.Controls.Add(progressBarToday, 0, 2);
        summaryLayout.Controls.Add(lblProgressText, 0, 3);
        summaryCard.Controls.Add(summaryLayout);

        var foxCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(20, 10, 20, 10)
        };
        lblFoxMessage.Dock = DockStyle.Fill;
        lblFoxMessage.Font = new Font(AppTheme.FontFamily, 10.5F);
        lblFoxMessage.TextAlign = ContentAlignment.MiddleLeft;
        foxCard.Controls.Add(lblFoxMessage);

        var detailCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(18, 14, 18, 16)
        };
        var detailLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = AppTheme.CardBackground,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailLayout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font(AppTheme.FontFamily, 10F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = "今日提醒明細",
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _statCardsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(0, 2, 0, 0),
            Margin = Padding.Empty
        };
        _statCardsFlow.Resize += (_, _) =>
        {
            foreach (var card in _statCardsFlow.Controls.OfType<Control>())
            {
                card.Width = GetFlowCardWidth(_statCardsFlow);
            }
        };
        detailLayout.Controls.Add(_statCardsFlow, 0, 1);
        detailCard.Controls.Add(detailLayout);

        layout.Controls.Add(summaryCard, 0, 0);
        layout.Controls.Add(foxCard, 0, 1);
        layout.Controls.Add(detailCard, 0, 2);
        panelStatistics.Controls.Add(layout);
        panelStatistics.ResumeLayout(true);
    }
}
