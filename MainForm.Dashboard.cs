using HealthReminder.Controls;
using HealthReminder.UI;

namespace HealthReminder;

public partial class MainForm
{
    private Button? _resumeTemporaryPauseButton;
    #region 狀態頁面

    private void btnPauseAll_Click(object sender, EventArgs e)
    {
        TogglePause();
    }

    private void TogglePause()
    {
        if (_reminderService.IsPausedAll)
        {
            _reminderService.ResumeAll();
        }
        else
        {
            _reminderService.PauseAll();
        }

        UpdateCountdownDisplay();
    }

    private Button CreateQuickPauseButton(string text, EventHandler clickHandler)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(74, 38),
            Margin = new Padding(0, 0, 6, 0),
            UseVisualStyleBackColor = false
        };
        StyleButton(button, AppTheme.SelectedBackground, Color.FromArgb(225, 235, 230), AppTheme.Primary);
        button.Click += clickHandler;
        return button;
    }

    private void PauseFor(TimeSpan duration)
    {
        _reminderService.PauseFor(duration);
        UpdateCountdownDisplay();
    }

    private void PauseUntilEndOfDay()
    {
        _reminderService.PauseUntilEndOfDay();
        UpdateCountdownDisplay();
    }

    private void ResumeTemporaryPause()
    {
        _reminderService.ResumeTemporaryPause();
        UpdateCountdownDisplay();
    }

    private void InitializeListViews()
    {
        // 倒數計時列表
        listViewCountdown.Columns.Clear();
        listViewCountdown.Columns.Add("狀態", 50);
        listViewCountdown.Columns.Add("提醒項目", 120);
        listViewCountdown.Columns.Add("間隔", 80);
        listViewCountdown.Columns.Add("剩餘時間", 100);
        listViewCountdown.Columns.Add("下次提醒", 120);

        // 統計列表
        listViewStatistics.Columns.Clear();
        listViewStatistics.Columns.Add("提醒項目", 150);
        listViewStatistics.Columns.Add("完成次數", 100);
        listViewStatistics.Columns.Add("總提醒次數", 100);
        listViewStatistics.Columns.Add("完成率", 80);

        ResizeListViewColumns();
    }

    private void ResizeListViewColumns()
    {
        if (listViewCountdown.Columns.Count == 5 && listViewCountdown.ClientSize.Width > 500)
        {
            listViewCountdown.Columns[0].Width = 64;
            listViewCountdown.Columns[1].Width = Math.Max(180, listViewCountdown.ClientSize.Width - 440);
            listViewCountdown.Columns[2].Width = 90;
            listViewCountdown.Columns[3].Width = 120;
            listViewCountdown.Columns[4].Width = 130;
        }

        if (listViewStatistics.Columns.Count == 4 && listViewStatistics.ClientSize.Width > 440)
        {
            listViewStatistics.Columns[0].Width = Math.Max(180, listViewStatistics.ClientSize.Width - 320);
            listViewStatistics.Columns[1].Width = 100;
            listViewStatistics.Columns[2].Width = 110;
            listViewStatistics.Columns[3].Width = 90;
        }
    }

    private void UpdateCountdownDisplay()
    {
        UpdateCountdownCards();

        // 更新托盤圖示提示文字
        var nextTimer = _reminderService.GetTimers()
            .Where(t => t.Item.IsEnabled && !t.IsPaused)
            .OrderBy(t => t.RemainingTime)
            .FirstOrDefault();

        if (nextTimer != null)
        {
            var remaining = nextTimer.RemainingTime;
            notifyIcon.Text = $"健康提醒小幫手\n下次提醒剩餘：{ReminderCountdownCard.FormatRemaining(remaining)}";
        }
        else
        {
            notifyIcon.Text = "健康提醒小幫手（目前沒有倒數）";
        }

        // 更新大字倒數顯示
        if (nextTimer != null)
        {
            var remaining = nextTimer.RemainingTime;
            lblBigCountdown.Text = ReminderCountdownCard.FormatRemaining(remaining);
            lblNextReminderName.Text = $"下一個：{nextTimer.Item.Emoji} {nextTimer.Item.Name}";
        }
        else
        {
            lblBigCountdown.Text = "--:--";
            lblNextReminderName.Text = "下一個：--";
        }

        UpdateHeaderStatus();
    }

    private void UpdateCountdownCards()
    {
        if (_countdownCardsFlow == null) return;

        var timers = _reminderService.GetTimers()
            .OrderBy(timer => !timer.Item.IsEnabled)
            .ThenBy(timer => timer.RemainingTime)
            .ToList();
        var activeIds = timers.Select(timer => timer.Item.Id).ToHashSet(StringComparer.Ordinal);

        var cardIndex = 0;
        foreach (var timer in timers)
        {
            if (!_countdownCards.TryGetValue(timer.Item.Id, out var card))
            {
                card = new ReminderCountdownCard();
                _countdownCards[timer.Item.Id] = card;
                _countdownCardsFlow.Controls.Add(card);
            }

            card.Width = GetFlowCardWidth(_countdownCardsFlow);
            card.UpdateFrom(timer);
            if (_countdownCardsFlow.Controls.GetChildIndex(card) != cardIndex)
                _countdownCardsFlow.Controls.SetChildIndex(card, cardIndex);
            cardIndex++;
        }

        foreach (var staleId in _countdownCards.Keys.Where(id => !activeIds.Contains(id)).ToList())
        {
            var card = _countdownCards[staleId];
            _countdownCardsFlow.Controls.Remove(card);
            card.Dispose();
            _countdownCards.Remove(staleId);
        }
    }

    #endregion

    private void BuildStatusPage()
    {
        panelStatus.SuspendLayout();
        panelStatus.Controls.Clear();
        panelStatus.BackColor = AppTheme.WindowBackground;
        panelStatus.Padding = Padding.Empty;

        var layout = new ElevatedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.WindowBackground,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 8, 12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));

        var heroCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(24, 18, 24, 18)
        };
        lblNextReminder.Text = "下一個提醒";
        lblNextReminder.Font = new Font(AppTheme.FontFamily, 10F, FontStyle.Bold);
        lblNextReminder.ForeColor = AppTheme.SecondaryText;
        var heroLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty
        };
        heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        heroLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lblNextReminder.Dock = DockStyle.Fill;
        lblNextReminder.Margin = Padding.Empty;
        lblBigCountdown.Font = new Font(AppTheme.FontFamily, 34F, FontStyle.Bold);
        lblBigCountdown.Dock = DockStyle.Fill;
        lblBigCountdown.Margin = Padding.Empty;
        lblBigCountdown.BackColor = AppTheme.CardBackground;
        lblNextReminderName.AutoSize = false;
        lblNextReminderName.AutoEllipsis = true;
        lblNextReminderName.Font = new Font(AppTheme.FontFamily, 11F, FontStyle.Bold);
        lblNextReminderName.Dock = DockStyle.Fill;
        lblNextReminderName.Margin = Padding.Empty;
        lblNextReminderName.TextAlign = ContentAlignment.MiddleLeft;
        heroLayout.Controls.Add(lblNextReminder, 0, 0);
        heroLayout.SetColumnSpan(lblNextReminder, 2);
        heroLayout.Controls.Add(lblBigCountdown, 0, 1);
        heroLayout.Controls.Add(lblNextReminderName, 1, 1);
        heroCard.Controls.Add(heroLayout);

        var queueCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(18, 14, 18, 16)
        };
        var queueLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = AppTheme.CardBackground
        };
        queueLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        queueLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        queueLayout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font(AppTheme.FontFamily, 10F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = "今天接下來的提醒",
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _countdownCardsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(0, 2, 0, 0),
            Margin = Padding.Empty
        };
        _countdownCardsFlow.Resize += (_, _) =>
        {
            foreach (ReminderCountdownCard card in _countdownCardsFlow.Controls.OfType<ReminderCountdownCard>())
            {
                card.Width = GetFlowCardWidth(_countdownCardsFlow);
            }
        };
        queueLayout.Controls.Add(_countdownCardsFlow, 0, 1);
        queueCard.Controls.Add(queueLayout);

        var actionCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(16, 10, 16, 10)
        };
        var pauseLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        pauseLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        pauseLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lblPauseStatus.AutoSize = false;
        lblPauseStatus.Dock = DockStyle.Fill;
        lblPauseStatus.Margin = Padding.Empty;
        lblPauseStatus.Font = new Font(AppTheme.FontFamily, 9F);
        lblPauseStatus.TextAlign = ContentAlignment.MiddleLeft;
        var pauseActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = AppTheme.CardBackground,
            Margin = Padding.Empty
        };
        btnPauseAll.Size = new Size(104, 38);
        btnPauseAll.Margin = new Padding(0, 0, 6, 4);
        btnPauseAll.Text = "手動暫停";
        pauseActions.Controls.Add(btnPauseAll);
        pauseActions.Controls.Add(CreateQuickPauseButton("30 分鐘", (_, _) => PauseFor(TimeSpan.FromMinutes(30))));
        pauseActions.Controls.Add(CreateQuickPauseButton("1 小時", (_, _) => PauseFor(TimeSpan.FromHours(1))));
        pauseActions.Controls.Add(CreateQuickPauseButton("今天暫停", (_, _) => PauseUntilEndOfDay()));
        _resumeTemporaryPauseButton = CreateQuickPauseButton("取消限時暫停", (_, _) => ResumeTemporaryPause());
        pauseActions.Controls.Add(_resumeTemporaryPauseButton);
        pauseLayout.Controls.Add(lblPauseStatus, 0, 0);
        pauseLayout.Controls.Add(pauseActions, 0, 1);
        actionCard.Controls.Add(pauseLayout);

        layout.Controls.Add(heroCard, 0, 0);
        layout.Controls.Add(queueCard, 0, 1);
        layout.Controls.Add(actionCard, 0, 2);
        panelStatus.Controls.Add(layout);
        panelStatus.ResumeLayout(true);
    }

    private void UpdateHeaderStatus()
    {
        if (_headerStatusLabel == null) return;

        var hasEnabledReminders = _reminderService.GetTimers().Any(timer => timer.Item.IsEnabled);
        if (!hasEnabledReminders)
        {
            _headerStatusLabel.Text = "目前沒有啟用的提醒";
            _headerStatusLabel.ForeColor = AppTheme.SecondaryText;
            _headerStatusLabel.BackColor = AppTheme.CardBackground;
        }
        else if (_reminderService.IsInDoNotDisturbMode)
        {
            _headerStatusLabel.Text = "☾  勿擾模式中";
            _headerStatusLabel.ForeColor = AppTheme.Warning;
            _headerStatusLabel.BackColor = Color.FromArgb(255, 247, 226);
        }
        else if (_reminderService.IsPausedAll)
        {
            _headerStatusLabel.Text = "Ⅱ  提醒已暫停";
            _headerStatusLabel.ForeColor = AppTheme.Warning;
            _headerStatusLabel.BackColor = Color.FromArgb(255, 247, 226);
        }
        else if (_reminderService.IsTemporarilyPaused)
        {
            var resumeAt = _reminderService.TemporaryPauseUntilLocal;
            _headerStatusLabel.Text = resumeAt?.Date == DateTime.Today
                ? $"Ⅱ  暫停至 {resumeAt:HH:mm}"
                : $"Ⅱ  暫停至 {resumeAt:MM/dd HH:mm}";
            _headerStatusLabel.ForeColor = AppTheme.Warning;
            _headerStatusLabel.BackColor = Color.FromArgb(255, 247, 226);
        }
        else
        {
            _headerStatusLabel.Text = "●  提醒運行中";
            _headerStatusLabel.ForeColor = AppTheme.Healthy;
            _headerStatusLabel.BackColor = Color.FromArgb(237, 248, 241);
        }

        // 多個原因可同時生效；分開解除時，畫面仍需顯示尚未解除的原因。
        var reasons = new List<string>();
        if (_reminderService.IsPausedAll) reasons.Add("手動暫停（需自行恢復）");
        if (_reminderService.IsTemporarilyPaused)
        {
            var resumeAt = _reminderService.TemporaryPauseUntilLocal;
            reasons.Add(resumeAt?.Date == DateTime.Today
                ? $"限時暫停至 {resumeAt:HH:mm}"
                : $"限時暫停至 {resumeAt:MM/dd HH:mm}");
        }
        if (_reminderService.IsInDoNotDisturbMode)
        {
            var quietEnd = HealthReminder.Services.SettingsManager.Instance.Settings.DoNotDisturbEnd;
            reasons.Add($"勿擾至 {quietEnd:hh\\:mm}");
        }
        lblPauseStatus.Text = reasons.Count > 0
            ? string.Join(" · ", reasons)
            : hasEnabledReminders ? "提醒運行中 · 忙碌時可以在這裡暫停" : "目前沒有啟用的提醒，請到「提醒與設定」新增或啟用。";
        lblPauseStatus.AccessibleName = lblPauseStatus.Text;
        lblPauseStatus.ForeColor = reasons.Count > 0 ? AppTheme.Warning : AppTheme.SecondaryText;
        btnPauseAll.Text = _reminderService.IsPausedAll ? "解除手動暫停" : "手動暫停";
        menuItemPause.Text = _reminderService.IsPausedAll ? "解除手動暫停 (&P)" : "手動暫停 (&P)";
        if (_resumeTemporaryPauseButton != null)
            _resumeTemporaryPauseButton.Enabled = _reminderService.IsTemporarilyPaused;

        ApplyRoundedRegion(_headerStatusLabel, 12);
    }
}
