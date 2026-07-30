using System.ComponentModel;
using System.Data;
using System.Media;
using HealthReminder.Models;
using HealthReminder.Forms;
using HealthReminder.Controls;
using HealthReminder.Services;
using HealthReminder.UI;
using HealthReminder.Windows;
using System.IO;

namespace HealthReminder;

/// <summary>
/// 主視窗
/// </summary>
public partial class MainForm : Form
{
    private const string PreviewNotificationOccurrenceId = "__preview_notification__";
    private ReminderService _reminderService;
    private readonly WindowsSystemEventMonitor _systemEventMonitor;
    private readonly NotificationService _notificationService;
    private bool _isReallyClosing = false;
    private readonly List<Button> _navigationButtons = new();
    private Panel? _contentHost;
    private Label? _headerStatusLabel;
    private Button? _todayNavigationButton;
    private Button? _settingsNavigationButton;
    private Button? _statisticsNavigationButton;
    private BindingList<ReminderItem>? _reminderItems;
    private FlowLayoutPanel? _reminderCardsFlow;
    private FlowLayoutPanel? _countdownCardsFlow;
    private FlowLayoutPanel? _statCardsFlow;
    private readonly Dictionary<string, ReminderCountdownCard> _countdownCards = new();
    private Label? _settingsFeedbackLabel;
    private System.Windows.Forms.Timer? _settingsFeedbackTimer;
    private CheckBox? _reduceMotionCheckBox;
    private FoxAnimationControl? _headerFox;
    private DateTimePicker? _workdayStartPicker;
    private DateTimePicker? _workdayEndPicker;

    // 多圖示支援
    private Icon? _mainIcon;      // 主圖示（小狐狸）
    private Icon? _waterIcon;     // 喝水圖示（水滴）
    private Icon? _eyeIcon;       // 眼睛休息圖示
    private Icon? _stretchIcon;   // 起身活動圖示

    public MainForm()
    {
        InitializeComponent();
        _reminderService = new ReminderService();
        _systemEventMonitor = new WindowsSystemEventMonitor();
        _systemEventMonitor.SystemResumed += SystemEventMonitor_ScheduleChanged;
        _systemEventMonitor.SystemTimeChanged += SystemEventMonitor_ScheduleChanged;
        _notificationService = new NotificationService(this);
        _notificationService.ActionSelected += NotificationService_ActionSelected;
        
        // 載入所有圖示
        LoadIcons();

        // 套用可愛風格
        ApplyCuteTheme();
        BuildModernShell();
    }



    /// <summary>
    /// 載入所有圖示
    /// </summary>
    private void LoadIcons()
    {
        // 主圖示（小狐狸）
        _mainIcon = LoadEmbeddedIcon("HealthReminder.Resources.app.ico");
        
        // 喝水圖示（水滴）
        _waterIcon = LoadEmbeddedIcon("HealthReminder.Resources.water.ico");
        
        // 眼睛休息圖示
        _eyeIcon = LoadEmbeddedIcon("HealthReminder.Resources.eye.ico");
        
        // 尚未提供伸展專用圖示時使用主圖示。
        _stretchIcon = _mainIcon == null ? null : (Icon)_mainIcon.Clone();

        // 設定視窗和托盤的主圖示
        if (_mainIcon != null)
        {
            this.Icon = _mainIcon;
            notifyIcon.Icon = _mainIcon;
        }
        else
        {
            notifyIcon.Icon = SystemIcons.Information;
        }
    }

    /// <summary>
    /// 安全載入圖示（檔案不存在時返回 null）
    /// </summary>
    private Icon? LoadEmbeddedIcon(string resourceName)
    {
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            using var icon = new Icon(stream);
            return (Icon)icon.Clone();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"載入內嵌圖示失敗 {resourceName}: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// 根據提醒類型取得對應圖示
    /// </summary>
    private Icon GetIconForReminderType(ReminderType type)
    {
        var icon = type switch
        {
            ReminderType.Water => _waterIcon,
            ReminderType.EyeRest => _eyeIcon,
            ReminderType.Stretch => _stretchIcon,
            _ => _mainIcon
        };

        // 如果對應圖示不存在，使用主圖示；主圖示也不存在則用系統圖示
        return icon ?? _mainIcon ?? SystemIcons.Information;
    }

    #region 表單事件

    private void MainForm_Load(object sender, EventArgs e)
    {
        // 初始化 UI
        InitializeListViews();
        LoadSettingsToUI();

        // 初始化提醒服務
        _reminderService.OnReminderTriggered += ReminderService_OnReminderTriggered;
        _reminderService.OnCountdownTick += ReminderService_OnCountdownTick;
        _reminderService.Initialize();

        // 每次啟動校正開機自啟路徑，避免搬移或重新發佈後登錄項指向舊檔而失效
        SettingsManager.Instance.SyncStartWithWindowsPath();

        // 檢查是否需要最小化啟動
        var settings = SettingsManager.Instance.Settings;
        var forceShow = Environment.GetCommandLineArgs()
            .Any(argument => argument.Equals("--show", StringComparison.OrdinalIgnoreCase));
        if (settings.StartMinimized && !forceShow)
        {
            this.WindowState = FormWindowState.Minimized;
            this.Hide();
            notifyIcon.ShowBalloonTip(2000, "健康提醒小幫手", 
                "程式已在背景執行，點擊此處或右下角圖示開啟設定。", ToolTipIcon.Info);
        }

        UpdateCountdownDisplay();
        UpdateStatisticsDisplay();

        // 可用 --section=settings|stats 直接開到指定分頁（托盤選單也用相同導覽）
        var sectionArg = Environment.GetCommandLineArgs()
            .FirstOrDefault(argument => argument.StartsWith("--section=", StringComparison.OrdinalIgnoreCase));
        if (sectionArg != null)
        {
            var section = sectionArg["--section=".Length..];
            if (section.Equals("settings", StringComparison.OrdinalIgnoreCase))
            {
                ShowSection(panelSettings, _settingsNavigationButton);
            }
            else if (section.Equals("stats", StringComparison.OrdinalIgnoreCase)
                || section.Equals("statistics", StringComparison.OrdinalIgnoreCase))
            {
                ShowSection(panelStatistics, _statisticsNavigationButton);
            }
        }

        if (Environment.GetCommandLineArgs().Any(argument =>
                argument.Equals("--preview-notification", StringComparison.OrdinalIgnoreCase)))
        {
            _notificationService.Enqueue(new ReminderPopupRequest(
                new ReminderItem
                {
                    Id = "preview-reminder",
                    Type = ReminderType.Custom,
                    Name = "小狐狸提醒你",
                    Message = "起身伸展一下，讓肩膀和眼睛都休息一會兒。",
                    Emoji = "🦊",
                    PlaySound = false,
                    ActionKind = ReminderActionKind.TimedActivity,
                    ActivityDurationSeconds = 8
                },
                PreviewNotificationOccurrenceId,
                SettingsManager.Instance.Settings.SnoozeMinutes,
                SettingsManager.Instance.Settings.ReduceMotion));
        }

        // 首次使用引導
        ShowFirstTimeGuide();
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (!_isReallyClosing && e.CloseReason == CloseReason.UserClosing)
        {
            // 點擊 X 按鈕時，最小化到系統托盤而非關閉
            e.Cancel = true;
            this.Hide();
            notifyIcon.ShowBalloonTip(1500, "健康提醒小幫手", 
                "程式已縮小到系統托盤，繼續在背景執行。", ToolTipIcon.Info);
        }
        else
        {
            // 真正關閉時，清理資源
            _notificationService.Dispose();
            _systemEventMonitor.Dispose();
            _reminderService.Dispose();
            _settingsFeedbackTimer?.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            _stretchIcon?.Dispose();
            _eyeIcon?.Dispose();
            _waterIcon?.Dispose();
            _mainIcon?.Dispose();
        }
    }

    private void MainForm_Resize(object sender, EventArgs e)
    {
        if (this.WindowState == FormWindowState.Minimized)
        {
            this.Hide();
        }
        else
        {
            ResizeListViewColumns();
        }
    }

    #endregion

    private void SystemEventMonitor_ScheduleChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;

        BeginInvoke(() =>
        {
            _reminderService.RecalibrateAfterSystemChange();
            UpdateCountdownDisplay();
        });
    }

    #region 系統托盤事件

    private void notifyIcon_DoubleClick(object sender, EventArgs e)
    {
        ShowMainForm();
    }

    private void notifyIcon_BalloonTipClicked(object sender, EventArgs e)
    {
        // BalloonTip 僅用於啟動／縮小提示，點擊時開啟主視窗。
        ShowMainForm();
    }

    private void menuItemOpen_Click(object sender, EventArgs e)
    {
        ShowMainForm();
    }

    private void menuItemPause_Click(object sender, EventArgs e)
    {
        TogglePause();
    }

    private void menuItemStatistics_Click(object sender, EventArgs e)
    {
        ShowMainForm();
        ShowSection(panelStatistics, _statisticsNavigationButton);
    }

    private void menuItemExit_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "確定要結束程式嗎？\n結束後將不會再收到健康提醒。",
            "確認結束",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _isReallyClosing = true;
            Application.Exit();
        }
    }

    #endregion

    #region 提醒服務事件

    private void ReminderService_OnReminderTriggered(
        object? sender,
        ReminderTriggeredEventArgs e)
    {
        var item = e.Item;

        // 根據提醒類型切換托盤圖示
        var reminderIcon = GetIconForReminderType(item.Type);
        notifyIcon.Icon = reminderIcon;

        // 顯示可操作的自訂動畫通知；多筆提醒由 NotificationService 排隊。
        _notificationService.Enqueue(new ReminderPopupRequest(
            item,
            e.OccurrenceId,
            SettingsManager.Instance.Settings.SnoozeMinutes,
            SettingsManager.Instance.Settings.ReduceMotion));

        if (item.ScheduleKind == ReminderScheduleKind.OneTime && _reminderItems != null)
        {
            var draft = _reminderItems.FirstOrDefault(reminder => reminder.Id == item.Id);
            if (draft != null)
            {
                draft.IsEnabled = false;
                RefreshReminderCards();
            }
        }

        // 播放音效
        if (item.PlaySound)
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch { }
        }

        // 3 秒後恢復主圖示
        var restoreTimer = new System.Windows.Forms.Timer();
        restoreTimer.Interval = 3000;
        restoreTimer.Tick += (s, e) =>
        {
            restoreTimer.Stop();
            restoreTimer.Dispose();
            if (_mainIcon != null)
            {
                notifyIcon.Icon = _mainIcon;
            }
        };
        restoreTimer.Start();

        // 更新統計顯示
        this.Invoke(() => UpdateStatisticsDisplay());
    }

    private void NotificationService_ActionSelected(
        object? sender,
        ReminderPopupActionEventArgs e)
    {
        if (e.Request.OccurrenceId == PreviewNotificationOccurrenceId)
        {
            return;
        }

        switch (e.Action)
        {
            case ReminderPopupAction.Completed:
                _reminderService.MarkCompleted(e.Request.OccurrenceId);
                break;
            case ReminderPopupAction.Snoozed:
                _reminderService.Snooze(e.Request.Item.Id, e.Request.OccurrenceId);
                if (e.Request.Item.ScheduleKind == ReminderScheduleKind.OneTime && _reminderItems != null)
                {
                    var draft = _reminderItems.FirstOrDefault(item => item.Id == e.Request.Item.Id);
                    if (draft != null)
                    {
                        draft.IsEnabled = true;
                        draft.OneTimeAtLocal = e.Request.Item.OneTimeAtLocal;
                        RefreshReminderCards();
                    }
                }
                break;
            case ReminderPopupAction.Dismissed:
                _reminderService.Dismiss(e.Request.OccurrenceId);
                break;
            case ReminderPopupAction.TimedOut:
                _reminderService.MarkMissed(e.Request.OccurrenceId);
                break;
        }

        UpdateStatisticsDisplay();
        UpdateCountdownDisplay();
    }

    private void ReminderService_OnCountdownTick(object? sender, EventArgs e)
    {
        this.Invoke(() => UpdateCountdownDisplay());
    }

    #endregion

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
            btnPauseAll.Text = "⏸ 暫停所有提醒";
            lblPauseStatus.Text = "✓ 運行中";
            lblPauseStatus.ForeColor = Color.FromArgb(76, 175, 80);
            menuItemPause.Text = "暫停所有提醒 (&P)";
        }
        else
        {
            _reminderService.PauseAll();
            btnPauseAll.Text = "▶ 恢復所有提醒";
            lblPauseStatus.Text = "⏸ 已暫停";
            lblPauseStatus.ForeColor = Color.FromArgb(255, 152, 0);
            menuItemPause.Text = "恢復所有提醒 (&P)";
        }

        UpdateHeaderStatus();
        UpdateCountdownDisplay();
    }

    private Button CreateQuickPauseButton(string text, EventHandler clickHandler)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(102, 40),
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
            notifyIcon.Text = $"健康提醒小幫手\n下次提醒：{nextTimer.Item.Name}\n剩餘：{remaining.Minutes}分{remaining.Seconds}秒";
        }
        else
        {
            notifyIcon.Text = "健康提醒小幫手（已暫停）";
        }

        // 更新大字倒數顯示
        if (nextTimer != null)
        {
            var remaining = nextTimer.RemainingTime;
            lblBigCountdown.Text = $"{remaining.Minutes:00}:{remaining.Seconds:00}";
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

        var timers = _reminderService.GetTimers();
        var activeIds = timers.Select(timer => timer.Item.Id).ToHashSet(StringComparer.Ordinal);

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

    #region 設定頁面

    private void btnAddReminder_Click(object sender, EventArgs e)
    {
        if (_reminderItems == null) return;

        using var editor = new ReminderEditorForm();
        if (editor.ShowDialog(this) != DialogResult.OK) return;

        _reminderItems.Add(editor.ResultItem);
        RefreshReminderCards();
    }

    private void InitializeDataGridView()
    {
        dataGridViewReminders.AutoGenerateColumns = false;
        dataGridViewReminders.AllowUserToAddRows = false;
        dataGridViewReminders.AllowUserToDeleteRows = false;
        dataGridViewReminders.MultiSelect = false;
        dataGridViewReminders.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dataGridViewReminders.Columns.Clear();

        // 啟用欄位
        var colEnabled = new DataGridViewCheckBoxColumn
        {
            Name = "colEnabled",
            HeaderText = "啟用",
            DataPropertyName = "IsEnabled",
            Width = 50
        };
        dataGridViewReminders.Columns.Add(colEnabled);

        // 名稱欄位
        var colName = new DataGridViewTextBoxColumn
        {
            Name = "colName",
            HeaderText = "名稱",
            DataPropertyName = "Name",
            Width = 100
        };
        dataGridViewReminders.Columns.Add(colName);

        // 間隔欄位
        var colInterval = new DataGridViewTextBoxColumn
        {
            Name = "colInterval",
            HeaderText = "間隔(分)",
            DataPropertyName = "IntervalMinutes",
            Width = 70
        };
        dataGridViewReminders.Columns.Add(colInterval);

        // 訊息欄位
        var colMessage = new DataGridViewTextBoxColumn
        {
            Name = "colMessage",
            HeaderText = "提醒訊息",
            DataPropertyName = "Message",
            Width = 150
        };
        dataGridViewReminders.Columns.Add(colMessage);

        // 音效欄位
        var colSound = new DataGridViewCheckBoxColumn
        {
            Name = "colSound",
            HeaderText = "音效",
            DataPropertyName = "PlaySound",
            Width = 50
        };
        dataGridViewReminders.Columns.Add(colSound);
    }

    private void LoadSettingsToUI()
    {
        var settings = SettingsManager.Instance.Settings;

        _reminderItems = new BindingList<ReminderItem>(
            settings.Reminders.Select(CloneReminderItem).ToList());
        RefreshReminderCards();

        // 一般設定
        chkStartWithWindows.Checked = settings.StartWithWindows;
        chkStartMinimized.Checked = settings.StartMinimized;
        numSnoozeMinutes.Value = settings.SnoozeMinutes;
        if (_reduceMotionCheckBox != null)
        {
            _reduceMotionCheckBox.Checked = settings.ReduceMotion;
        }
        if (_headerFox != null)
        {
            _headerFox.AnimationEnabled = !settings.ReduceMotion;
        }
        lblBigCountdown.AnimationsEnabled = !settings.ReduceMotion;
        progressBarToday.AnimationsEnabled = !settings.ReduceMotion;

        // 勿擾設定
        chkDoNotDisturb.Checked = settings.DoNotDisturbEnabled;
        dtpDoNotDisturbStart.Value = DateTime.Today.Add(settings.DoNotDisturbStart);
        dtpDoNotDisturbEnd.Value = DateTime.Today.Add(settings.DoNotDisturbEnd);

        // 工作時段（今日成就目標基準）
        if (_workdayStartPicker != null)
        {
            _workdayStartPicker.Value = DateTime.Today.Add(settings.WorkdayStart);
        }
        if (_workdayEndPicker != null)
        {
            _workdayEndPicker.Value = DateTime.Today.Add(settings.WorkdayEnd);
        }
    }

    private void btnSaveSettings_Click(object sender, EventArgs e)
    {
        SaveSettings();
    }

    private void btnResetDefaults_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "確定要重設為預設值嗎？\n\n這會將所有提醒項目和設定恢復成初始狀態。",
            "確認重設",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            SettingsManager.Instance.ResetToDefaults();
            LoadSettingsToUI();
            _reminderService.Reload();
            ShowSettingsFeedback("✓ 已恢復預設值並重新排程", AppTheme.Healthy);
        }
    }

    private void btnDeleteReminder_Click(object sender, EventArgs e)
    {
        MessageBox.Show("請在提醒卡片上點選「刪除」。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private void SaveSettings()
    {
        var settings = SettingsManager.Instance.Settings;
        var workdayStart = _workdayStartPicker?.Value.TimeOfDay ?? settings.WorkdayStart;
        var workdayEnd = _workdayEndPicker?.Value.TimeOfDay ?? settings.WorkdayEnd;

        if (!DailyAchievementCalculator.IsValidWorkday(workdayStart, workdayEnd))
        {
            MessageBox.Show(
                "工作時段的結束時間必須晚於開始時間。\n目前暫不支援跨午夜的工作時段。",
                "工作時段需要修正",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (_reminderItems != null)
        {
            var validationError = ValidateReminders(_reminderItems);
            if (validationError != null)
            {
                MessageBox.Show(
                    validationError,
                    "設定需要修正",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            settings.Reminders = _reminderItems.ToList();
        }

        // 一般設定
        settings.StartMinimized = chkStartMinimized.Checked;
        settings.SnoozeMinutes = (int)numSnoozeMinutes.Value;
        settings.ReduceMotion = _reduceMotionCheckBox?.Checked ?? false;
        if (_headerFox != null)
        {
            _headerFox.AnimationEnabled = !settings.ReduceMotion;
        }
        lblBigCountdown.AnimationsEnabled = !settings.ReduceMotion;
        progressBarToday.AnimationsEnabled = !settings.ReduceMotion;

        // 處理開機自動啟動
        if (chkStartWithWindows.Checked != settings.StartWithWindows)
        {
            SettingsManager.Instance.SetStartWithWindows(chkStartWithWindows.Checked);
        }

        // 勿擾設定
        settings.DoNotDisturbEnabled = chkDoNotDisturb.Checked;
        settings.DoNotDisturbStart = dtpDoNotDisturbStart.Value.TimeOfDay;
        settings.DoNotDisturbEnd = dtpDoNotDisturbEnd.Value.TimeOfDay;

        // 工作時段（今日成就目標基準）
        settings.WorkdayStart = workdayStart;
        settings.WorkdayEnd = workdayEnd;

        // 儲存並重新載入服務
        if (!SettingsManager.Instance.Save())
        {
            MessageBox.Show(
                SettingsManager.Instance.LastError ?? "設定儲存失敗，請稍後再試。",
                "無法儲存設定",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        _reminderService.Reload();

        RefreshReminderCards();
        UpdateCountdownDisplay();
        UpdateStatisticsDisplay();
        ShowSettingsFeedback("✓ 設定已儲存，提醒已重新排程", AppTheme.Healthy);
    }

    private void ShowSettingsFeedback(string message, Color color)
    {
        if (_settingsFeedbackLabel == null) return;

        _settingsFeedbackTimer?.Stop();
        _settingsFeedbackTimer?.Dispose();
        _settingsFeedbackLabel.Text = message;
        _settingsFeedbackLabel.ForeColor = color;
        _settingsFeedbackLabel.Visible = true;
        _settingsFeedbackTimer = new System.Windows.Forms.Timer { Interval = 3500 };
        _settingsFeedbackTimer.Tick += (_, _) =>
        {
            _settingsFeedbackTimer?.Stop();
            _settingsFeedbackLabel.Visible = false;
        };
        _settingsFeedbackTimer.Start();
    }

    private void RefreshReminderCards()
    {
        if (_reminderCardsFlow == null) return;

        _reminderCardsFlow.SuspendLayout();
        while (_reminderCardsFlow.Controls.Count > 0)
        {
            var control = _reminderCardsFlow.Controls[0];
            _reminderCardsFlow.Controls.RemoveAt(0);
            control.Dispose();
        }

        if (_reminderItems != null)
        {
            foreach (var item in _reminderItems)
            {
                var card = new ReminderCardControl(item)
                {
                    Width = GetFlowCardWidth(_reminderCardsFlow)
                };
                card.EditRequested += (_, _) => EditReminder(item);
                card.DeleteRequested += (_, _) => DeleteReminder(item);
                _reminderCardsFlow.Controls.Add(card);
            }
        }

        _reminderCardsFlow.ResumeLayout(true);
    }

    private void EditReminder(ReminderItem item)
    {
        if (_reminderItems == null) return;

        using var editor = new ReminderEditorForm(item);
        if (editor.ShowDialog(this) != DialogResult.OK) return;

        var index = _reminderItems.IndexOf(item);
        if (index >= 0)
        {
            _reminderItems[index] = editor.ResultItem;
            RefreshReminderCards();
        }
    }

    private void DeleteReminder(ReminderItem item)
    {
        if (_reminderItems == null) return;
        if (_reminderItems.Count <= 1)
        {
            MessageBox.Show("至少要保留一個提醒項目。", "無法刪除", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show(
            this,
            $"確定要刪除「{item.Name}」嗎？",
            "確認刪除",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        _reminderItems.Remove(item);
        RefreshReminderCards();
    }

    private static int GetFlowCardWidth(FlowLayoutPanel flow)
    {
        var scrollbarWidth = flow.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
        return Math.Max(320, flow.ClientSize.Width - flow.Padding.Horizontal - scrollbarWidth - 2);
    }

    private static ReminderItem CloneReminderItem(ReminderItem source)
    {
        return new ReminderItem
        {
            Id = source.Id,
            Type = source.Type,
            Name = source.Name,
            Message = source.Message,
            IntervalMinutes = source.IntervalMinutes,
            ScheduleKind = source.ScheduleKind,
            OneTimeAtLocal = source.OneTimeAtLocal,
            ActionKind = source.ActionKind,
            ActivityDurationSeconds = source.ActivityDurationSeconds,
            AutoStartActivity = source.AutoStartActivity,
            IsEnabled = source.IsEnabled,
            PlaySound = source.PlaySound,
            Emoji = source.Emoji
        };
    }

    private static string? ValidateReminders(IEnumerable<ReminderItem> reminders)
    {
        var items = reminders.ToList();
        if (items.Count == 0)
        {
            return "請至少保留一個提醒項目。";
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                return "提醒名稱不可空白。";
            }

            if (item.ScheduleKind == ReminderScheduleKind.Interval && item.IntervalMinutes is < 1 or > 1440)
            {
                return $"「{item.Name}」的提醒間隔必須介於 1 到 1440 分鐘。";
            }

            if (item.ScheduleKind == ReminderScheduleKind.OneTime && item.OneTimeAtLocal == null)
            {
                return $"「{item.Name}」需要設定一次性提醒時間。";
            }

            if (item.ActionKind == ReminderActionKind.TimedActivity && item.ActivityDurationSeconds is < 5 or > 3600)
            {
                return $"「{item.Name}」的活動時間必須介於 5 到 3600 秒。";
            }

            if (string.IsNullOrWhiteSpace(item.Message))
            {
                return $"「{item.Name}」的提醒訊息不可空白。";
            }
        }

        return null;
    }

    #endregion

    #region 統計頁面

    private void UpdateStatisticsDisplay()
    {
        var today = SettingsManager.Instance.GetTodayStatistics();
        var settings = SettingsManager.Instance.Settings;

        int totalCompleted = 0;
        int totalCount = 0;

        var configuredItems = settings.Reminders
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var reminderGroups = today.Records
            .GroupBy(record => string.IsNullOrWhiteSpace(record.ReminderId)
                ? $"legacy:{record.Type}:{record.Name}"
                : record.ReminderId)
            .OrderBy(group => group.First().Name);

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

        foreach (var group in reminderGroups)
        {
            var completed = group.Count(record => record.Status == ReminderStatus.Completed);
            var total = group.Count();
            if (total <= 0) continue;

            var firstRecord = group.First();
            var configured = configuredItems.TryGetValue(firstRecord.ReminderId, out var found) ? found : null;
            var emoji = configured?.Emoji ?? "💡";
            var accent = configured != null ? AppTheme.AccentFor(configured.Type) : AppTheme.Primary;

            if (_statCardsFlow != null)
            {
                var card = new StatRowCard { Width = cardWidth };
                card.SetData(emoji, firstRecord.Name, completed, total, accent, emphasize: false);
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
            ? $"已完成 {goalCompleted} / {goalTarget} 次（整個工作時段目標）"
            : "目前工作時段內沒有預計提醒";

        // 更新今日成就標題
        string achievement = GetAchievementMessage(goalPercent);
        lblTodaySummary.Text = $"📊 今日成就 · {goalPercent}% - {achievement}";

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
    /// 根據今日成就達標百分比取得鼓勵訊息
    /// </summary>
    private string GetAchievementMessage(int percent)
    {
        if (percent <= 0)
        {
            return "加油！今天還沒開始～";
        }
        else if (percent < 50)
        {
            return "不錯，已經開始養成習慣！";
        }
        else if (percent < 100)
        {
            return "很棒，持續努力中！";
        }
        else
        {
            return "太厲害了！今日達標！";
        }
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

    #region 輔助方法

    /// <summary>
    /// 顯示首次使用引導
    /// </summary>
    private void ShowFirstTimeGuide()
    {
        // 檢查是否首次使用
        string guideFlagPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HealthReminder",
            ".guide_shown"
        );

        if (!File.Exists(guideFlagPath))
        {
            string message =
                "🦊 歡迎使用健康提醒小幫手！\n\n" +
                "📌 小提示：\n\n" +
                "• 程式會在背景執行，點擊右下角托盤圖示可開啟\n" +
                "• 按 X 關閉視窗只會縮小到托盤，不會結束程式\n" +
                "• 右鍵點擊托盤圖示可快速操作\n" +
                "• 在「設定」頁面可自訂提醒時間\n\n" +
                "祝你工作愉快，記得照顧好自己！💪";

            MessageBox.Show(
                message,
                "🦊 小狐狸的歡迎訊息",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            // 建立標記檔案
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(guideFlagPath)!);
                File.WriteAllText(guideFlagPath, DateTime.Now.ToString());
            }
            catch { }
        }
    }

    /// <summary>
    /// 建立現代化主框架，沿用既有頁面控制項與事件。
    /// </summary>
    private void BuildModernShell()
    {
        SuspendLayout();

        tabControl.Visible = false;
        ClientSize = new Size(920, 640);
        MinimumSize = new Size(820, 600);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.WindowBackground,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        rootLayout.Controls.Add(CreateHeader(), 0, 0);

        var bodyLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.WindowBackground,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(16, 12, 16, 16),
            Margin = Padding.Empty
        };
        bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        bodyLayout.Controls.Add(CreateSidebar(), 0, 0);

        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.WindowBackground,
            Margin = new Padding(12, 0, 0, 0)
        };
        bodyLayout.Controls.Add(_contentHost, 1, 0);
        rootLayout.Controls.Add(bodyLayout, 0, 1);

        Controls.Add(rootLayout);
        rootLayout.BringToFront();

        BuildStatusPage();
        BuildSettingsPage();
        BuildStatisticsPage();
        ShowSection(panelStatus, _todayNavigationButton);
        UpdateHeaderStatus();

        ResumeLayout(true);
    }

    private Control CreateHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(24, 12, 24, 10),
            Margin = Padding.Empty
        };

        _headerFox = new FoxAnimationControl
        {
            Location = new Point(14, 4),
            Size = new Size(60, 62),
            BackColor = AppTheme.CardBackground
        };
        var title = new Label
        {
            AutoSize = true,
            Location = new Point(80, 13),
            Font = new Font(AppTheme.FontFamily, 15F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = "小狐狸健康助手"
        };
        var subtitle = new Label
        {
            AutoSize = true,
            Location = new Point(82, 42),
            Font = new Font(AppTheme.FontFamily, 8.5F),
            ForeColor = AppTheme.SecondaryText,
            Text = "陪你喝水、休息眼睛，也記得起身走走"
        };

        _headerStatusLabel = new Label
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = false,
            Location = new Point(744, 20),
            Size = new Size(150, 34),
            Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        header.Resize += (_, _) =>
        {
            if (_headerStatusLabel != null)
            {
                _headerStatusLabel.Left = header.ClientSize.Width - _headerStatusLabel.Width - 24;
            }
        };

        header.Controls.Add(_headerFox);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(_headerStatusLabel);
        return header;
    }

    private Control CreateSidebar()
    {
        var sidebar = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(12, 18, 12, 12)
        };

        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppTheme.CardBackground,
            Padding = Padding.Empty
        };

        var sectionLabel = new Label
        {
            AutoSize = false,
            Size = new Size(148, 26),
            Margin = new Padding(8, 0, 0, 8),
            Font = new Font(AppTheme.FontFamily, 8F, FontStyle.Bold),
            ForeColor = AppTheme.SecondaryText,
            Text = "功能選單",
            TextAlign = ContentAlignment.MiddleLeft
        };
        navigation.Controls.Add(sectionLabel);

        _todayNavigationButton = CreateNavigationButton("⌂   今日總覽", panelStatus);
        _settingsNavigationButton = CreateNavigationButton("☷   提醒與設定", panelSettings);
        _statisticsNavigationButton = CreateNavigationButton("▥   健康紀錄", panelStatistics);
        navigation.Controls.Add(_todayNavigationButton);
        navigation.Controls.Add(_settingsNavigationButton);
        navigation.Controls.Add(_statisticsNavigationButton);

        var versionLabel = new Label
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            AutoSize = false,
            Location = new Point(20, 488),
            Size = new Size(136, 28),
            Font = new Font(AppTheme.FontFamily, 8F),
            ForeColor = AppTheme.SecondaryText,
            Text = "版本 1.0.0",
            TextAlign = ContentAlignment.MiddleLeft
        };
        sidebar.Resize += (_, _) => versionLabel.Top = sidebar.ClientSize.Height - 42;

        sidebar.Controls.Add(navigation);
        sidebar.Controls.Add(versionLabel);
        versionLabel.BringToFront();
        return sidebar;
    }

    private Button CreateNavigationButton(string text, Control section)
    {
        var button = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Size = new Size(148, 46),
            Margin = new Padding(0, 2, 0, 4),
            Padding = new Padding(12, 0, 0, 0),
            Font = new Font(AppTheme.FontFamily, 9.5F, FontStyle.Bold),
            ForeColor = AppTheme.SecondaryText,
            BackColor = AppTheme.CardBackground,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand,
            Tag = false
        };
        button.FlatAppearance.BorderSize = 0;
        ApplyRoundedRegion(button, 10);
        button.Resize += (_, _) => ApplyRoundedRegion(button, 10);
        button.Click += (_, _) => ShowSection(section, button);
        button.MouseEnter += (_, _) =>
        {
            if (button.Tag is not true)
            {
                button.BackColor = Color.FromArgb(255, 246, 240);
            }
        };
        button.MouseLeave += (_, _) =>
        {
            if (button.Tag is not true)
            {
                button.BackColor = AppTheme.CardBackground;
            }
        };
        _navigationButtons.Add(button);
        return button;
    }

    private void ShowSection(Control section, Button? activeButton)
    {
        if (_contentHost == null) return;

        foreach (Control page in new Control[] { panelStatus, panelSettings, panelStatistics })
        {
            page.Visible = page == section;
        }

        section.Dock = DockStyle.Fill;
        if (section.Parent != _contentHost)
        {
            _contentHost.Controls.Add(section);
        }
        section.BringToFront();

        foreach (var button in _navigationButtons)
        {
            var selected = button == activeButton;
            button.Tag = selected;
            button.BackColor = selected ? AppTheme.SelectedBackground : AppTheme.CardBackground;
            button.ForeColor = selected ? AppTheme.Primary : AppTheme.SecondaryText;
        }

        // 切到健康紀錄時重新整理，確保資料最新且卡片寬度正確
        if (section == panelStatistics && _statCardsFlow != null)
        {
            UpdateStatisticsDisplay();
        }
    }

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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 166));
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
        lblNextReminder.Location = new Point(24, 18);
        lblBigCountdown.Font = new Font(AppTheme.FontFamily, 38F, FontStyle.Bold);
        lblBigCountdown.Location = new Point(20, 45);
        lblBigCountdown.Size = new Size(270, 76);
        lblBigCountdown.BackColor = AppTheme.CardBackground;
        lblNextReminderName.Font = new Font(AppTheme.FontFamily, 13F, FontStyle.Bold);
        lblNextReminderName.Location = new Point(330, 62);
        lblNextReminderName.Size = new Size(350, 34);
        lblNextReminderName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        heroCard.Controls.Add(lblNextReminder);
        heroCard.Controls.Add(lblBigCountdown);
        heroCard.Controls.Add(lblNextReminderName);

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
        var quickPauseTitle = new Label
        {
            AutoSize = true,
            Location = new Point(16, 12),
            Font = new Font(AppTheme.FontFamily, 9.5F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = "快速暫停"
        };
        lblPauseStatus.AutoSize = true;
        lblPauseStatus.Location = new Point(112, 13);
        lblPauseStatus.Font = new Font(AppTheme.FontFamily, 9.5F, FontStyle.Bold);
        lblPauseStatus.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        var pauseActions = new FlowLayoutPanel
        {
            Location = new Point(12, 42),
            Size = new Size(700, 50),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = AppTheme.CardBackground,
            Margin = Padding.Empty
        };
        btnPauseAll.Size = new Size(142, 40);
        btnPauseAll.Margin = new Padding(4, 0, 6, 0);
        btnPauseAll.Text = "手動暫停";
        pauseActions.Controls.Add(btnPauseAll);
        pauseActions.Controls.Add(CreateQuickPauseButton("30 分鐘", (_, _) => PauseFor(TimeSpan.FromMinutes(30))));
        pauseActions.Controls.Add(CreateQuickPauseButton("1 小時", (_, _) => PauseFor(TimeSpan.FromHours(1))));
        pauseActions.Controls.Add(CreateQuickPauseButton("今天暫停", (_, _) => PauseUntilEndOfDay()));
        pauseActions.Controls.Add(CreateQuickPauseButton("立即恢復", (_, _) => ResumeTemporaryPause()));
        actionCard.Controls.Add(quickPauseTitle);
        actionCard.Controls.Add(lblPauseStatus);
        actionCard.Controls.Add(pauseActions);

        layout.Controls.Add(heroCard, 0, 0);
        layout.Controls.Add(queueCard, 0, 1);
        layout.Controls.Add(actionCard, 0, 2);
        panelStatus.Controls.Add(layout);
        panelStatus.ResumeLayout(true);
    }

    private void BuildSettingsPage()
    {
        panelSettings.SuspendLayout();
        panelSettings.Controls.Clear();
        panelSettings.BackColor = AppTheme.WindowBackground;
        panelSettings.AutoScroll = true;

        var layout = new ElevatedPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 720,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = AppTheme.WindowBackground,
            Padding = new Padding(8, 6, 8, 12),
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 420));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        var reminderSection = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(16, 12, 16, 14)
        };
        var reminderHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = AppTheme.CardBackground
        };
        reminderHeader.Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(0, 8),
            Font = new Font(AppTheme.FontFamily, 11F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = "提醒項目"
        });
        var addReminderButton = new Button
        {
            Text = "+ 新增提醒",
            Size = new Size(112, 36),
            Location = new Point(540, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            UseVisualStyleBackColor = false
        };
        StyleButton(addReminderButton, AppTheme.Healthy, Color.FromArgb(64, 133, 94), Color.White);
        addReminderButton.Click += (_, eventArgs) => btnAddReminder_Click(addReminderButton, eventArgs);
        reminderHeader.Controls.Add(addReminderButton);
        reminderHeader.Resize += (_, _) => addReminderButton.Left = reminderHeader.ClientSize.Width - addReminderButton.Width;
        btnDeleteReminder.Visible = false;

        _reminderCardsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppTheme.CardBackground,
            Padding = new Padding(0, 2, 0, 0),
            Margin = Padding.Empty
        };
        _reminderCardsFlow.Resize += (_, _) =>
        {
            foreach (ReminderCardControl card in _reminderCardsFlow.Controls.OfType<ReminderCardControl>())
            {
                card.Width = GetFlowCardWidth(_reminderCardsFlow);
            }
        };
        var reminderLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = AppTheme.CardBackground,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        reminderLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        reminderLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        reminderLayout.Controls.Add(reminderHeader, 0, 0);
        reminderLayout.Controls.Add(_reminderCardsFlow, 0, 1);
        reminderSection.Controls.Add(reminderLayout);

        var generalCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(16, 10, 16, 10)
        };
        generalCard.Controls.Add(CreateSettingsSectionTitle("一般設定"));
        chkStartWithWindows.Location = new Point(18, 50);
        chkStartMinimized.Location = new Point(164, 50);
        lblSnoozeMinutes.Location = new Point(338, 52);
        numSnoozeMinutes.Location = new Point(446, 48);
        numSnoozeMinutes.Size = new Size(58, 26);
        lblSnoozeUnit.Location = new Point(510, 52);
        _reduceMotionCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(568, 50),
            Text = "減少動畫",
            UseVisualStyleBackColor = true
        };

        var workdayLabel = new Label
        {
            AutoSize = true,
            Location = new Point(18, 90),
            Font = new Font(AppTheme.FontFamily, 9F),
            Text = "工作時段"
        };
        _workdayStartPicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Location = new Point(96, 86),
            Size = new Size(72, 26)
        };
        var workdayTo = new Label
        {
            AutoSize = true,
            Location = new Point(176, 90),
            Font = new Font(AppTheme.FontFamily, 9F),
            Text = "～"
        };
        _workdayEndPicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Location = new Point(200, 86),
            Size = new Size(72, 26)
        };
        var workdayHint = new Label
        {
            AutoSize = true,
            Location = new Point(286, 90),
            Font = new Font(AppTheme.FontFamily, 8.5F),
            ForeColor = AppTheme.SecondaryText,
            Text = "（今日成就 100% 的計算基準）"
        };

        generalCard.Controls.AddRange(new Control[]
        {
            chkStartWithWindows,
            chkStartMinimized,
            lblSnoozeMinutes,
            numSnoozeMinutes,
            lblSnoozeUnit,
            _reduceMotionCheckBox,
            workdayLabel,
            _workdayStartPicker,
            workdayTo,
            _workdayEndPicker,
            workdayHint
        });

        var doNotDisturbCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(16, 10, 16, 10)
        };
        doNotDisturbCard.Controls.Add(CreateSettingsSectionTitle("勿擾時段"));
        chkDoNotDisturb.Location = new Point(18, 45);
        lblDoNotDisturbStart.Location = new Point(104, 48);
        dtpDoNotDisturbStart.Location = new Point(146, 43);
        dtpDoNotDisturbStart.Size = new Size(76, 26);
        lblDoNotDisturbEnd.Location = new Point(252, 48);
        dtpDoNotDisturbEnd.Location = new Point(294, 43);
        dtpDoNotDisturbEnd.Size = new Size(76, 26);
        doNotDisturbCard.Controls.AddRange(new Control[]
        {
            chkDoNotDisturb,
            lblDoNotDisturbStart,
            dtpDoNotDisturbStart,
            lblDoNotDisturbEnd,
            dtpDoNotDisturbEnd
        });

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = AppTheme.WindowBackground,
            Padding = new Padding(0, 10, 0, 0),
            Margin = Padding.Empty
        };
        btnSaveSettings.Size = new Size(128, 40);
        btnResetDefaults.Size = new Size(116, 40);
        btnSaveSettings.Margin = new Padding(8, 0, 0, 0);
        btnResetDefaults.Margin = Padding.Empty;
        _settingsFeedbackLabel = new Label
        {
            AutoSize = false,
            Size = new Size(330, 40),
            Margin = new Padding(0, 0, 12, 0),
            Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold),
            ForeColor = AppTheme.Healthy,
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        actions.Controls.Add(btnSaveSettings);
        actions.Controls.Add(btnResetDefaults);
        actions.Controls.Add(_settingsFeedbackLabel);

        layout.Controls.Add(reminderSection, 0, 0);
        layout.Controls.Add(generalCard, 0, 1);
        layout.Controls.Add(doNotDisturbCard, 0, 2);
        layout.Controls.Add(actions, 0, 3);
        panelSettings.Controls.Add(layout);
        panelSettings.Resize += (_, _) => layout.Width = panelSettings.ClientSize.Width;
        RefreshReminderCards();
        panelSettings.ResumeLayout(true);
    }

    private static Label CreateSettingsSectionTitle(string text)
    {
        return new Label
        {
            AutoSize = true,
            Location = new Point(16, 12),
            Font = new Font(AppTheme.FontFamily, 10F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = text
        };
    }

    private static void ConfigureSettingsGroup(GroupBox groupBox, string title)
    {
        groupBox.Text = title;
        groupBox.Dock = DockStyle.Fill;
        groupBox.Margin = new Padding(0, 0, 0, 10);
        groupBox.Padding = new Padding(16, 10, 16, 12);
        groupBox.BackColor = AppTheme.CardBackground;
        groupBox.ForeColor = AppTheme.PrimaryText;
    }

    private void ArrangeReminderGroup()
    {
        if (groupBoxReminders.ClientSize.Width <= 40) return;

        dataGridViewReminders.Location = new Point(16, 28);
        dataGridViewReminders.Size = new Size(
            groupBoxReminders.ClientSize.Width - 32,
            Math.Max(90, groupBoxReminders.ClientSize.Height - 80));
        btnAddReminder.Location = new Point(16, groupBoxReminders.ClientSize.Height - 42);
        btnDeleteReminder.Location = new Point(108, groupBoxReminders.ClientSize.Height - 42);
        btnAddReminder.Size = new Size(84, 30);
        btnDeleteReminder.Size = new Size(84, 30);
    }

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
        lblTodaySummary.Location = new Point(22, 18);
        lblTodaySummary.Font = new Font(AppTheme.FontFamily, 12F, FontStyle.Bold);
        lblStreakDays.Location = new Point(410, 22);
        lblStreakDays.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        progressBarToday.Location = new Point(22, 58);
        progressBarToday.Size = new Size(580, 24);
        progressBarToday.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblProgressText.Location = new Point(22, 92);
        lblProgressText.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        lblProgressText.ForeColor = AppTheme.SecondaryText;
        summaryCard.Controls.Add(lblTodaySummary);
        summaryCard.Controls.Add(lblStreakDays);
        summaryCard.Controls.Add(progressBarToday);
        summaryCard.Controls.Add(lblProgressText);

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

    private void UpdateHeaderStatus()
    {
        if (_headerStatusLabel == null) return;

        if (_reminderService.IsInDoNotDisturbMode)
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

        if (_reminderService.IsPausedAll)
        {
            lblPauseStatus.Text = "手動暫停中";
            lblPauseStatus.ForeColor = AppTheme.Warning;
            btnPauseAll.Text = "恢復手動暫停";
        }
        else if (_reminderService.IsTemporarilyPaused)
        {
            lblPauseStatus.Text = $"暫停至 {_reminderService.TemporaryPauseUntilLocal:HH:mm}";
            lblPauseStatus.ForeColor = AppTheme.Warning;
            btnPauseAll.Text = "手動暫停";
        }
        else
        {
            lblPauseStatus.Text = "提醒運行中";
            lblPauseStatus.ForeColor = AppTheme.Healthy;
            btnPauseAll.Text = "手動暫停";
        }

        ApplyRoundedRegion(_headerStatusLabel, 12);
    }

    /// <summary>
    /// 套用可愛風格主題
    /// </summary>
    private void ApplyCuteTheme()
    {
        var bgColor = AppTheme.WindowBackground;
        var panelColor = AppTheme.CardBackground;
        var accentColor = AppTheme.Primary;
        var accentHover = AppTheme.PrimaryHover;
        var textColor = AppTheme.PrimaryText;

        // 主視窗
        this.BackColor = bgColor;
        this.Font = new Font(AppTheme.FontFamily, 9F);

        // TabControl 樣式
        tabControl.Font = new Font(AppTheme.FontFamily, 10F, FontStyle.Bold);
        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.SizeMode = TabSizeMode.Fixed;
        tabControl.ItemSize = new Size(132, 38);
        tabControl.Padding = new Point(18, 6);
        tabControl.DrawItem += TabControl_DrawItem;

        // 套用到所有頁籤
        foreach (TabPage page in tabControl.TabPages)
        {
            page.BackColor = bgColor;
            foreach (Control ctrl in page.Controls)
            {
                ApplyThemeToControl(ctrl, bgColor, panelColor, textColor);
            }
        }

        // 美化按鈕
        StyleButton(btnPauseAll, accentColor, accentHover, Color.White);
        StyleButton(btnSaveSettings, accentColor, accentHover, Color.White);
        StyleButton(btnResetDefaults, AppTheme.MutedButton, AppTheme.MutedButtonHover, Color.White);
        StyleButton(btnDeleteReminder, AppTheme.Danger, AppTheme.DangerHover, Color.White);

        // 標題樣式
        lblNextReminder.ForeColor = accentColor;
        lblTodaySummary.ForeColor = accentColor;

        // GroupBox 標題顏色
        groupBoxReminders.ForeColor = accentColor;
        groupBoxGeneral.ForeColor = accentColor;
        groupBoxDoNotDisturb.ForeColor = accentColor;

        // DataGridView 樣式
        StyleDataGridView(dataGridViewReminders, bgColor, accentColor, textColor);

        // ListView 樣式
        StyleListView(listViewCountdown, bgColor, textColor);
        StyleListView(listViewStatistics, bgColor, textColor);

        // 新增按鈕樣式
        StyleButton(btnAddReminder, AppTheme.Healthy, Color.FromArgb(64, 133, 94), Color.White);

        // 大字倒數顏色
        lblBigCountdown.ForeColor = AppTheme.Primary;
        lblNextReminderName.ForeColor = AppTheme.PrimaryText;

        // 統計頁面新控制項
        lblStreakDays.ForeColor = AppTheme.Primary;
        lblFoxMessage.ForeColor = AppTheme.PrimaryText;
        progressBarToday.ForeColor = AppTheme.Primary;
    }

    private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        var tabPage = tabControl.TabPages[e.Index];
        var bounds = e.Bounds;
        var selected = e.Index == tabControl.SelectedIndex;

        using var backgroundBrush = new SolidBrush(
            selected ? AppTheme.SelectedBackground : AppTheme.WindowBackground);
        e.Graphics.FillRectangle(backgroundBrush, bounds);

        if (selected)
        {
            using var accentBrush = new SolidBrush(AppTheme.Primary);
            e.Graphics.FillRectangle(
                accentBrush,
                new Rectangle(bounds.Left, bounds.Bottom - 3, bounds.Width, 3));
        }

        TextRenderer.DrawText(
            e.Graphics,
            tabPage.Text,
            tabControl.Font,
            bounds,
            selected ? AppTheme.Primary : AppTheme.SecondaryText,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
    }

    /// <summary>
    /// 遞迴套用主題到控制項
    /// </summary>
    private void ApplyThemeToControl(Control ctrl, Color bgColor, Color panelColor, Color textColor)
    {
        if (ctrl is Panel panel)
        {
            panel.BackColor = bgColor;
        }
        else if (ctrl is GroupBox groupBox)
        {
            groupBox.BackColor = bgColor;
            groupBox.Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold);
            groupBox.ForeColor = AppTheme.PrimaryText;
        }
        else if (ctrl is Label label && label != lblPauseStatus)
        {
            label.ForeColor = textColor;
        }
        else if (ctrl is CheckBox checkBox)
        {
            checkBox.ForeColor = textColor;
            checkBox.Font = new Font(AppTheme.FontFamily, 9F);
        }

        // 遞迴處理子控制項
        foreach (Control child in ctrl.Controls)
        {
            ApplyThemeToControl(child, bgColor, panelColor, textColor);
        }
    }

    /// <summary>
    /// 美化按鈕
    /// </summary>
    private void StyleButton(Button btn, Color normalColor, Color hoverColor, Color textColor)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = normalColor;
        btn.ForeColor = textColor;
        btn.Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold);
        btn.Cursor = Cursors.Hand;
        ApplyRoundedRegion(btn, AppTheme.CornerRadius);
        btn.Resize += (_, _) => ApplyRoundedRegion(btn, AppTheme.CornerRadius);

        // 滑鼠懸停：平滑過渡背景色
        HoverGlow.Attach(
            btn,
            normalColor,
            hoverColor,
            () => !(_reduceMotionCheckBox?.Checked ?? SettingsManager.Instance.Settings.ReduceMotion));
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0) return;

        var diameter = radius * 2;
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(control.Width - diameter - 1, 0, diameter, diameter, 270, 90);
        path.AddArc(
            control.Width - diameter - 1,
            control.Height - diameter - 1,
            diameter,
            diameter,
            0,
            90);
        path.AddArc(0, control.Height - diameter - 1, diameter, diameter, 90, 90);
        path.CloseFigure();

        var oldRegion = control.Region;
        control.Region = new Region(path);
        oldRegion?.Dispose();
    }

    /// <summary>
    /// 美化 DataGridView
    /// </summary>
    private void StyleDataGridView(DataGridView dgv, Color bgColor, Color headerColor, Color textColor)
    {
        dgv.BackgroundColor = AppTheme.CardBackground;
        dgv.BorderStyle = BorderStyle.None;
        dgv.GridColor = AppTheme.Border;
        dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        dgv.RowTemplate.Height = 32;
        dgv.ColumnHeadersHeight = 36;

        dgv.DefaultCellStyle.BackColor = Color.White;
        dgv.DefaultCellStyle.ForeColor = textColor;
        dgv.DefaultCellStyle.SelectionBackColor = AppTheme.SelectedBackground;
        dgv.DefaultCellStyle.SelectionForeColor = textColor;
        dgv.DefaultCellStyle.Font = new Font(AppTheme.FontFamily, 9F);
        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(255, 251, 248);

        dgv.ColumnHeadersDefaultCellStyle.BackColor = headerColor;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold);
        dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dgv.EnableHeadersVisualStyles = false;

        dgv.RowHeadersVisible = false;
        dgv.AllowUserToResizeRows = false;
    }

    /// <summary>
    /// 美化 ListView
    /// </summary>
    private void StyleListView(ListView lv, Color bgColor, Color textColor)
    {
        lv.BackColor = Color.White;
        lv.ForeColor = textColor;
        lv.BorderStyle = BorderStyle.FixedSingle;
        lv.Font = new Font(AppTheme.FontFamily, 9F);
        lv.GridLines = false;
    }
    private void ShowMainForm()
    {
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.Activate();
        this.BringToFront();
    }

    #endregion
}
