using System.ComponentModel;
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
    private readonly ReminderService _reminderService;
    private readonly System.Windows.Forms.Timer _restoreIconTimer = new() { Interval = 3000 };
    private bool _startupSyncPending;
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
    private CheckBox? _focusNotificationCheckBox;
    private Label? _storageLocationLabel;
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
        _notificationService = new NotificationService(this, () => _reminderService.CanShowNotifications);
        _notificationService.Presented += NotificationPresented;
        _notificationService.Closed += request =>
            _reminderService.ReleaseUnrecordedPresentation(request.Item.Id, request.OccurrenceId);
        _restoreIconTimer.Tick += (_, _) =>
        {
            _restoreIconTimer.Stop();
            if (_mainIcon != null) notifyIcon.Icon = _mainIcon;
        };
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

        var previewNotificationArgument = Environment.GetCommandLineArgs()
            .FirstOrDefault(argument =>
                argument.StartsWith("--preview-notification", StringComparison.OrdinalIgnoreCase));
        if (previewNotificationArgument != null)
        {
            var manualPreview = previewNotificationArgument.Equals(
                "--preview-notification=manual",
                StringComparison.OrdinalIgnoreCase);
            _notificationService.Enqueue(new ReminderPopupRequest(
                new ReminderItem
                {
                    Id = "preview-reminder",
                    Type = ReminderType.Custom,
                    Name = "小狐狸提醒你",
                    Message = "起身伸展一下，讓肩膀和眼睛都休息一會兒。",
                    Emoji = "🦊",
                    PlaySound = false,
                    ActionKind = manualPreview
                        ? ReminderActionKind.Manual
                        : ReminderActionKind.TimedActivity,
                    ActivityDurationSeconds = 8
                },
                PreviewNotificationOccurrenceId,
                SettingsManager.Instance.Settings.SnoozeMinutes,
                SettingsManager.Instance.Settings.ReduceMotion,
                ShouldFocusNotificationForKeyboard(),
                IsPreview: true));
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
            _restoreIconTimer.Dispose();
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

    private void ReminderService_OnReminderTriggered(object? sender, ReminderTriggeredEventArgs e)
    {
        var settings = SettingsManager.Instance.Settings;
        _notificationService.Enqueue(new ReminderPopupRequest(
            e.Item, e.OccurrenceId, settings.SnoozeMinutes, settings.ReduceMotion,
            ShouldFocusNotificationForKeyboard()));
    }

    private void NotificationPresented(ReminderPopupRequest request)
    {
        if (request.OccurrenceId == PreviewNotificationOccurrenceId) return;

        if (!RecordNotification(request))
        {
            notifyIcon.ShowBalloonTip(5000, "提醒紀錄未儲存",
                _reminderService.LastError ?? "請檢查資料位置。", ToolTipIcon.Warning);
        }

        notifyIcon.Icon = GetIconForReminderType(request.Item.Type);
        _restoreIconTimer.Stop();
        _restoreIconTimer.Start();
        if (request.Item.PlaySound)
        {
            try { SystemSounds.Asterisk.Play(); }
            catch { }
        }
    }

    private bool RecordNotification(ReminderPopupRequest request)
    {
        if (!_reminderService.RecordShown(request.Item, request.OccurrenceId)) return false;
        ShowPendingSyncWarning();

        if (request.Item.ScheduleKind == ReminderScheduleKind.OneTime)
        {
            UpdateOneTimeDraft(request.Item);
        }
        UpdateStatisticsDisplay();
        return true;
    }

    private void UpdateOneTimeDraft(ReminderItem previous)
    {
        var saved = SettingsManager.Instance.Settings.Reminders.Find(item => item.Id == previous.Id);
        var draft = _reminderItems?.FirstOrDefault(item => item.Id == previous.Id);
        if (saved == null || draft == null || draft.ScheduleKind != ReminderScheduleKind.OneTime) return;

        if (draft.OneTimeAtLocal != previous.OneTimeAtLocal || draft.IsEnabled != previous.IsEnabled) return;
        draft.IsEnabled = saved.IsEnabled;
        draft.OneTimeAtLocal = saved.OneTimeAtLocal;
        RefreshReminderCards();
    }

    private void NotificationService_ActionSelected(object? sender, ReminderPopupActionEventArgs e)
    {
        if (e.Request.OccurrenceId == PreviewNotificationOccurrenceId) return;

        var occurrenceId = e.Request.OccurrenceId;
        var manager = SettingsManager.Instance;
        if (!manager.ContainsRecord(occurrenceId) && !RecordNotification(e.Request))
        {
            e.Error = _reminderService.LastError ?? "提醒紀錄未儲存，請重試。";
            return;
        }
        var previous = _reminderService.GetTimerById(e.Request.Item.Id)?.Item;
        var success = e.Action switch
            {
                ReminderPopupAction.Completed => _reminderService.MarkCompleted(occurrenceId),
                ReminderPopupAction.Snoozed => _reminderService.Snooze(e.Request.Item, occurrenceId, e.Request.SnoozeMinutes),
                ReminderPopupAction.Dismissed => _reminderService.Dismiss(occurrenceId),
                ReminderPopupAction.TimedOut => _reminderService.MarkMissed(occurrenceId),
                _ => false
            };

        if (!success)
        {
            e.Error = _reminderService.LastError ?? "提醒紀錄未儲存，請重試。";
            return;
        }

        ShowPendingSyncWarning();

        if (e.Action == ReminderPopupAction.Snoozed
            && previous?.ScheduleKind == ReminderScheduleKind.OneTime)
        {
            UpdateOneTimeDraft(previous);
        }
        UpdateStatisticsDisplay();
        UpdateCountdownDisplay();
    }

    private void ShowPendingSyncWarning()
    {
        if (SettingsManager.Instance.LastError is { } pendingWarning)
        {
            notifyIcon.ShowBalloonTip(5000, "資料等待同步", pendingWarning, ToolTipIcon.Warning);
        }
    }

    private void ReminderService_OnCountdownTick(object? sender, EventArgs e)
    {
        _notificationService.ShowNextIfNeeded();
        UpdateCountdownDisplay();
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
            Text = "版本 1.0.1",
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
