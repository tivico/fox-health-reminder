namespace HealthReminder;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        
        // NotifyIcon (系統托盤圖示)
        this.notifyIcon = new NotifyIcon(this.components);
        
        // ContextMenuStrip (右鍵選單)
        this.contextMenuStrip = new ContextMenuStrip(this.components);
        this.menuItemOpen = new ToolStripMenuItem();
        this.menuItemPause = new ToolStripMenuItem();
        this.menuItemSeparator1 = new ToolStripSeparator();
        this.menuItemStatistics = new ToolStripMenuItem();
        this.menuItemSeparator2 = new ToolStripSeparator();
        this.menuItemExit = new ToolStripMenuItem();

        // TabControl
        this.tabControl = new TabControl();
        this.tabPageStatus = new TabPage();
        this.tabPageSettings = new TabPage();
        this.tabPageStatistics = new TabPage();

        // 狀態頁面控制項
        this.panelStatus = new Panel();
        this.lblNextReminder = new Label();
        this.lblBigCountdown = new HealthReminder.Controls.AnimatedCountdownLabel();
        this.lblNextReminderName = new Label();
        this.listViewCountdown = new ListView();
        this.btnPauseAll = new Button();
        this.lblPauseStatus = new Label();

        // 設定頁面控制項
        this.panelSettings = new Panel();
        this.groupBoxReminders = new GroupBox();
        this.dataGridViewReminders = new DataGridView();
        this.groupBoxGeneral = new GroupBox();
        this.chkStartWithWindows = new CheckBox();
        this.chkStartMinimized = new CheckBox();
        this.groupBoxDoNotDisturb = new GroupBox();
        this.chkDoNotDisturb = new CheckBox();
        this.lblDoNotDisturbStart = new Label();
        this.dtpDoNotDisturbStart = new DateTimePicker();
        this.lblDoNotDisturbEnd = new Label();
        this.dtpDoNotDisturbEnd = new DateTimePicker();
        this.lblSnoozeMinutes = new Label();
        this.numSnoozeMinutes = new NumericUpDown();
        this.btnSaveSettings = new Button();
        this.lblSnoozeUnit = new Label();

        // 統計頁面控制項
        this.panelStatistics = new Panel();
        this.lblTodaySummary = new Label();
        this.listViewStatistics = new ListView();
        this.lblStreakDays = new Label();
        this.progressBarToday = new HealthReminder.Controls.RoundedProgressBar();
        this.lblProgressText = new Label();
        this.lblFoxMessage = new Label();

        // 
        // contextMenuStrip
        // 
        this.contextMenuStrip.Items.AddRange(new ToolStripItem[] {
            this.menuItemOpen,
            this.menuItemPause,
            this.menuItemSeparator1,
            this.menuItemStatistics,
            this.menuItemSeparator2,
            this.menuItemExit
        });
        this.contextMenuStrip.Size = new Size(150, 120);

        // 
        // menuItemOpen
        // 
        this.menuItemOpen.Text = "開啟主視窗 (&O)";
        this.menuItemOpen.Font = new Font(this.menuItemOpen.Font, FontStyle.Bold);
        this.menuItemOpen.Click += new EventHandler(this.menuItemOpen_Click);

        // 
        // menuItemPause
        // 
        this.menuItemPause.Text = "暫停所有提醒 (&P)";
        this.menuItemPause.Click += new EventHandler(this.menuItemPause_Click);

        // 
        // menuItemStatistics
        // 
        this.menuItemStatistics.Text = "今日統計 (&S)";
        this.menuItemStatistics.Click += new EventHandler(this.menuItemStatistics_Click);

        // 
        // menuItemExit
        // 
        this.menuItemExit.Text = "結束程式 (&X)";
        this.menuItemExit.Click += new EventHandler(this.menuItemExit_Click);

        // 
        // notifyIcon
        // 
        this.notifyIcon.ContextMenuStrip = this.contextMenuStrip;
        this.notifyIcon.Text = "健康提醒小幫手";
        this.notifyIcon.Visible = true;
        this.notifyIcon.DoubleClick += new EventHandler(this.notifyIcon_DoubleClick);
        this.notifyIcon.BalloonTipClicked += new EventHandler(this.notifyIcon_BalloonTipClicked);

        // 
        // tabControl
        // 
        this.tabControl.Controls.Add(this.tabPageStatus);
        this.tabControl.Controls.Add(this.tabPageSettings);
        this.tabControl.Controls.Add(this.tabPageStatistics);
        this.tabControl.Dock = DockStyle.Fill;
        this.tabControl.Location = new Point(0, 0);
        this.tabControl.Name = "tabControl";
        this.tabControl.SelectedIndex = 0;
        this.tabControl.Size = new Size(784, 581);
        this.tabControl.TabIndex = 0;

        // 
        // tabPageStatus
        // 
        this.tabPageStatus.Controls.Add(this.panelStatus);
        this.tabPageStatus.Location = new Point(4, 24);
        this.tabPageStatus.Name = "tabPageStatus";
        this.tabPageStatus.Padding = new Padding(10);
        this.tabPageStatus.Size = new Size(476, 433);
        this.tabPageStatus.TabIndex = 0;
        this.tabPageStatus.Text = "狀態";
        this.tabPageStatus.UseVisualStyleBackColor = true;

        // 
        // lblBigCountdown
        // 
        this.lblBigCountdown = new HealthReminder.Controls.AnimatedCountdownLabel();
        this.lblBigCountdown.Font = new Font("Microsoft JhengHei UI", 36F, FontStyle.Bold);
        this.lblBigCountdown.Location = new Point(10, 35);
        this.lblBigCountdown.Name = "lblBigCountdown";
        this.lblBigCountdown.Size = new Size(200, 60);
        this.lblBigCountdown.Text = "00:00";

        // 
        // lblNextReminderName
        // 
        this.lblNextReminderName = new Label();
        this.lblNextReminderName.Font = new Font("Microsoft JhengHei UI", 12F);
        this.lblNextReminderName.Location = new Point(220, 55);
        this.lblNextReminderName.Name = "lblNextReminderName";
        this.lblNextReminderName.Size = new Size(220, 25);
        this.lblNextReminderName.Text = "下一個：--";

        // 
        // panelStatus
        // 
        this.panelStatus.Controls.Add(this.lblNextReminder);
        this.panelStatus.Controls.Add(this.lblBigCountdown);
        this.panelStatus.Controls.Add(this.lblNextReminderName);
        this.panelStatus.Controls.Add(this.listViewCountdown);
        this.panelStatus.Controls.Add(this.btnPauseAll);
        this.panelStatus.Controls.Add(this.lblPauseStatus);
        this.panelStatus.Dock = DockStyle.Fill;
        this.panelStatus.Location = new Point(10, 10);
        this.panelStatus.Name = "panelStatus";
        this.panelStatus.Size = new Size(456, 413);

        // 
        // lblNextReminder
        // 
        this.lblNextReminder.AutoSize = true;
        this.lblNextReminder.Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold);
        this.lblNextReminder.Location = new Point(10, 10);
        this.lblNextReminder.Name = "lblNextReminder";
        this.lblNextReminder.Size = new Size(200, 21);
        this.lblNextReminder.Text = "⏰ 提醒倒數計時";

        // 
        // listViewCountdown
        // 
        this.listViewCountdown.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.listViewCountdown.FullRowSelect = true;
        this.listViewCountdown.GridLines = true;
        this.listViewCountdown.Location = new Point(10, 105);
        this.listViewCountdown.Name = "listViewCountdown";
        this.listViewCountdown.Size = new Size(436, 215);
        this.listViewCountdown.TabIndex = 1;
        this.listViewCountdown.UseCompatibleStateImageBehavior = false;
        this.listViewCountdown.View = View.Details;

        // 
        // btnPauseAll
        // 
        this.btnPauseAll.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.btnPauseAll.Location = new Point(10, 340);
        this.btnPauseAll.Name = "btnPauseAll";
        this.btnPauseAll.Size = new Size(150, 35);
        this.btnPauseAll.TabIndex = 2;
        this.btnPauseAll.Text = "⏸ 暫停所有提醒";
        this.btnPauseAll.UseVisualStyleBackColor = true;
        this.btnPauseAll.Click += new EventHandler(this.btnPauseAll_Click);

        // 
        // lblPauseStatus
        // 
        this.lblPauseStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.lblPauseStatus.AutoSize = true;
        this.lblPauseStatus.ForeColor = Color.Green;
        this.lblPauseStatus.Location = new Point(170, 348);
        this.lblPauseStatus.Name = "lblPauseStatus";
        this.lblPauseStatus.Size = new Size(80, 15);
        this.lblPauseStatus.Text = "✓ 運行中";

        // 
        // tabPageSettings
        // 
        this.tabPageSettings.Controls.Add(this.panelSettings);
        this.tabPageSettings.Location = new Point(4, 24);
        this.tabPageSettings.Name = "tabPageSettings";
        this.tabPageSettings.Padding = new Padding(10);
        this.tabPageSettings.Size = new Size(476, 433);
        this.tabPageSettings.TabIndex = 1;
        this.tabPageSettings.Text = "設定";
        this.tabPageSettings.UseVisualStyleBackColor = true;


        // 
        // btnResetDefaults
        // 
        this.btnResetDefaults = new Button();
        this.btnResetDefaults.Location = new Point(10, 355);
        this.btnResetDefaults.Name = "btnResetDefaults";
        this.btnResetDefaults.Size = new Size(100, 30);
        this.btnResetDefaults.TabIndex = 4;
        this.btnResetDefaults.Text = "🔄 重設預設";
        this.btnResetDefaults.UseVisualStyleBackColor = true;
        this.btnResetDefaults.Click += new EventHandler(this.btnResetDefaults_Click);

        // 
        // btnSaveSettings
        // 
        this.btnSaveSettings.Location = new Point(346, 355);
        this.btnSaveSettings.Name = "btnSaveSettings";
        this.btnSaveSettings.Size = new Size(100, 30);
        this.btnSaveSettings.TabIndex = 3;
        this.btnSaveSettings.Text = "💾 儲存設定";
        this.btnSaveSettings.UseVisualStyleBackColor = true;
        this.btnSaveSettings.Click += new EventHandler(this.btnSaveSettings_Click);

        // 
        // panelSettings
        // 
        this.panelSettings.AutoScroll = true;
        this.panelSettings.Controls.Add(this.groupBoxReminders);
        this.panelSettings.Controls.Add(this.groupBoxGeneral);
        this.panelSettings.Controls.Add(this.groupBoxDoNotDisturb);
        this.panelSettings.Controls.Add(this.btnSaveSettings);
        this.panelSettings.Controls.Add(this.btnResetDefaults);
        this.panelSettings.Dock = DockStyle.Fill;
        this.panelSettings.Location = new Point(10, 10);
        this.panelSettings.Name = "panelSettings";
        this.panelSettings.Size = new Size(456, 413);

        // 
        // btnDeleteReminder
        // 
        this.btnDeleteReminder = new Button();
        this.btnDeleteReminder.Location = new Point(10, 145);
        this.btnDeleteReminder.Name = "btnDeleteReminder";
        this.btnDeleteReminder.Size = new Size(80, 25);
        this.btnDeleteReminder.Text = "🗑 刪除";
        this.btnDeleteReminder.UseVisualStyleBackColor = true;
        this.btnDeleteReminder.Click += new EventHandler(this.btnDeleteReminder_Click);

        // 
        // btnAddReminder
        // 
        this.btnAddReminder = new Button();
        this.btnAddReminder.Location = new Point(100, 145);
        this.btnAddReminder.Name = "btnAddReminder";
        this.btnAddReminder.Size = new Size(80, 25);
        this.btnAddReminder.Text = "➕ 新增";
        this.btnAddReminder.UseVisualStyleBackColor = true;
        this.btnAddReminder.Click += new EventHandler(this.btnAddReminder_Click);

        // 
        // groupBoxReminders
        // 
        this.groupBoxReminders.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.groupBoxReminders.Controls.Add(this.dataGridViewReminders);
        this.groupBoxReminders.Controls.Add(this.btnDeleteReminder);
        this.groupBoxReminders.Controls.Add(this.btnAddReminder);
        this.groupBoxReminders.Location = new Point(10, 10);
        this.groupBoxReminders.Name = "groupBoxReminders";
        this.groupBoxReminders.Size = new Size(436, 175);
        this.groupBoxReminders.TabIndex = 0;
        this.groupBoxReminders.TabStop = false;
        this.groupBoxReminders.Text = "提醒項目設定";

        // 
        // dataGridViewReminders
        // 
        this.dataGridViewReminders.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.dataGridViewReminders.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        this.dataGridViewReminders.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.dataGridViewReminders.Location = new Point(10, 22);
        this.dataGridViewReminders.Name = "dataGridViewReminders";
        this.dataGridViewReminders.Size = new Size(416, 115);
        this.dataGridViewReminders.TabIndex = 0;

        // 
        // groupBoxGeneral
        // 
        this.groupBoxGeneral.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.groupBoxGeneral.Controls.Add(this.chkStartWithWindows);
        this.groupBoxGeneral.Controls.Add(this.chkStartMinimized);
        this.groupBoxGeneral.Controls.Add(this.lblSnoozeMinutes);
        this.groupBoxGeneral.Controls.Add(this.numSnoozeMinutes);
        this.groupBoxGeneral.Controls.Add(this.lblSnoozeUnit);
        this.groupBoxGeneral.Location = new Point(10, 195);
        this.groupBoxGeneral.Name = "groupBoxGeneral";
        this.groupBoxGeneral.Size = new Size(436, 80);
        this.groupBoxGeneral.TabIndex = 1;
        this.groupBoxGeneral.TabStop = false;
        this.groupBoxGeneral.Text = "一般設定";

        // 
        // chkStartWithWindows
        // 
        this.chkStartWithWindows.AutoSize = true;
        this.chkStartWithWindows.Location = new Point(10, 22);
        this.chkStartWithWindows.Name = "chkStartWithWindows";
        this.chkStartWithWindows.Size = new Size(110, 19);
        this.chkStartWithWindows.TabIndex = 0;
        this.chkStartWithWindows.Text = "開機自動啟動";
        this.chkStartWithWindows.UseVisualStyleBackColor = true;

        // 
        // chkStartMinimized
        // 
        this.chkStartMinimized.AutoSize = true;
        this.chkStartMinimized.Location = new Point(150, 22);
        this.chkStartMinimized.Name = "chkStartMinimized";
        this.chkStartMinimized.Size = new Size(110, 19);
        this.chkStartMinimized.TabIndex = 1;
        this.chkStartMinimized.Text = "啟動時最小化";
        this.chkStartMinimized.UseVisualStyleBackColor = true;

        // 
        // lblSnoozeMinutes
        // 
        this.lblSnoozeMinutes.AutoSize = true;
        this.lblSnoozeMinutes.Location = new Point(10, 52);
        this.lblSnoozeMinutes.Name = "lblSnoozeMinutes";
        this.lblSnoozeMinutes.Size = new Size(91, 15);
        this.lblSnoozeMinutes.Text = "稍後提醒延遲：";

        // 
        // numSnoozeMinutes
        // 
        this.numSnoozeMinutes.Location = new Point(145, 50);
        this.numSnoozeMinutes.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
        this.numSnoozeMinutes.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        this.numSnoozeMinutes.Name = "numSnoozeMinutes";
        this.numSnoozeMinutes.Size = new Size(50, 23);
        this.numSnoozeMinutes.TabIndex = 2;
        this.numSnoozeMinutes.Value = new decimal(new int[] { 10, 0, 0, 0 });

        // 
        // lblSnoozeUnit
        // 
        this.lblSnoozeUnit = new Label();
        this.lblSnoozeUnit.AutoSize = true;
        this.lblSnoozeUnit.Location = new Point(180, 52);
        this.lblSnoozeUnit.Name = "lblSnoozeUnit";
        this.lblSnoozeUnit.Text = "分鐘";

        // 
        // groupBoxDoNotDisturb
        // 
        this.groupBoxDoNotDisturb.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.groupBoxDoNotDisturb.Controls.Add(this.chkDoNotDisturb);
        this.groupBoxDoNotDisturb.Controls.Add(this.lblDoNotDisturbStart);
        this.groupBoxDoNotDisturb.Controls.Add(this.dtpDoNotDisturbStart);
        this.groupBoxDoNotDisturb.Controls.Add(this.lblDoNotDisturbEnd);
        this.groupBoxDoNotDisturb.Controls.Add(this.dtpDoNotDisturbEnd);
        this.groupBoxDoNotDisturb.Location = new Point(10, 285);
        this.groupBoxDoNotDisturb.Name = "groupBoxDoNotDisturb";
        this.groupBoxDoNotDisturb.Size = new Size(436, 60);
        this.groupBoxDoNotDisturb.TabIndex = 2;
        this.groupBoxDoNotDisturb.TabStop = false;
        this.groupBoxDoNotDisturb.Text = "勿擾模式（午休時間）";

        // 
        // chkDoNotDisturb
        // 
        this.chkDoNotDisturb.AutoSize = true;
        this.chkDoNotDisturb.Location = new Point(10, 25);
        this.chkDoNotDisturb.Name = "chkDoNotDisturb";
        this.chkDoNotDisturb.Size = new Size(50, 19);
        this.chkDoNotDisturb.TabIndex = 0;
        this.chkDoNotDisturb.Text = "啟用";
        this.chkDoNotDisturb.UseVisualStyleBackColor = true;

        // 
        // lblDoNotDisturbStart
        // 
        this.lblDoNotDisturbStart.AutoSize = true;
        this.lblDoNotDisturbStart.Location = new Point(70, 26);
        this.lblDoNotDisturbStart.Name = "lblDoNotDisturbStart";
        this.lblDoNotDisturbStart.Size = new Size(25, 15);
        this.lblDoNotDisturbStart.Text = "從：";

        // 
        // dtpDoNotDisturbStart
        // 
        this.dtpDoNotDisturbStart.CustomFormat = "HH:mm";
        this.dtpDoNotDisturbStart.Format = DateTimePickerFormat.Custom;
        this.dtpDoNotDisturbStart.Location = new Point(120, 22);
        this.dtpDoNotDisturbStart.Name = "dtpDoNotDisturbStart";
        this.dtpDoNotDisturbStart.ShowUpDown = true;
        this.dtpDoNotDisturbStart.Size = new Size(70, 23);
        this.dtpDoNotDisturbStart.TabIndex = 1;

        // 
        // lblDoNotDisturbEnd
        // 
        this.lblDoNotDisturbEnd.AutoSize = true;
        this.lblDoNotDisturbEnd.Location = new Point(200, 26);
        this.lblDoNotDisturbEnd.Name = "lblDoNotDisturbEnd";
        this.lblDoNotDisturbEnd.Size = new Size(25, 15);
        this.lblDoNotDisturbEnd.Text = "到：";

        // 
        // dtpDoNotDisturbEnd
        // 
        this.dtpDoNotDisturbEnd.CustomFormat = "HH:mm";
        this.dtpDoNotDisturbEnd.Format = DateTimePickerFormat.Custom;
        this.dtpDoNotDisturbEnd.Location = new Point(250, 22);
        this.dtpDoNotDisturbEnd.Name = "dtpDoNotDisturbEnd";
        this.dtpDoNotDisturbEnd.ShowUpDown = true;
        this.dtpDoNotDisturbEnd.Size = new Size(70, 23);
        this.dtpDoNotDisturbEnd.TabIndex = 2;

        

        // 
        // tabPageStatistics
        // 
        this.tabPageStatistics.Controls.Add(this.panelStatistics);
        this.tabPageStatistics.Location = new Point(4, 24);
        this.tabPageStatistics.Name = "tabPageStatistics";
        this.tabPageStatistics.Padding = new Padding(10);
        this.tabPageStatistics.Size = new Size(476, 433);
        this.tabPageStatistics.TabIndex = 2;
        this.tabPageStatistics.Text = "統計";
        this.tabPageStatistics.UseVisualStyleBackColor = true;

        // 
        // lblStreakDays
        // 
        this.lblStreakDays = new Label();
        this.lblStreakDays.AutoSize = true;
        this.lblStreakDays.Font = new Font("Microsoft JhengHei UI", 10F);
        this.lblStreakDays.Location = new Point(10, 35);
        this.lblStreakDays.Name = "lblStreakDays";
        this.lblStreakDays.Text = "🔥 連續達標：0 天";

        // 
        // progressBarToday
        // 
        this.progressBarToday = new HealthReminder.Controls.RoundedProgressBar();
        this.progressBarToday.Location = new Point(10, 65);
        this.progressBarToday.Name = "progressBarToday";
        this.progressBarToday.Size = new Size(350, 25);

        // 
        // lblProgressText
        // 
        this.lblProgressText = new Label();
        this.lblProgressText.AutoSize = true;
        this.lblProgressText.Font = new Font("Microsoft JhengHei UI", 9F);
        this.lblProgressText.Location = new Point(365, 70);
        this.lblProgressText.Name = "lblProgressText";
        this.lblProgressText.Text = "0%";

        // 
        // lblFoxMessage
        // 
        this.lblFoxMessage = new Label();
        this.lblFoxMessage.Font = new Font("Microsoft JhengHei UI", 11F);
        this.lblFoxMessage.Location = new Point(10, 100);
        this.lblFoxMessage.Name = "lblFoxMessage";
        this.lblFoxMessage.Size = new Size(420, 30);
        this.lblFoxMessage.Text = "🦊 小狐狸說：今天也要好好照顧自己喔！";

        // 
        // panelStatistics
        // 
        this.panelStatistics.Controls.Add(this.lblTodaySummary);
        this.panelStatistics.Controls.Add(this.lblStreakDays);
        this.panelStatistics.Controls.Add(this.progressBarToday);
        this.panelStatistics.Controls.Add(this.lblProgressText);
        this.panelStatistics.Controls.Add(this.lblFoxMessage);
        this.panelStatistics.Controls.Add(this.listViewStatistics);
        this.panelStatistics.Dock = DockStyle.Fill;
        this.panelStatistics.Location = new Point(10, 10);
        this.panelStatistics.Name = "panelStatistics";
        this.panelStatistics.Size = new Size(456, 413);

        // 
        // lblTodaySummary
        // 
        this.lblTodaySummary.AutoSize = true;
        this.lblTodaySummary.Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold);
        this.lblTodaySummary.Location = new Point(10, 10);
        this.lblTodaySummary.Name = "lblTodaySummary";
        this.lblTodaySummary.Size = new Size(150, 21);
        this.lblTodaySummary.Text = "📊 今日成就";

        // 
        // listViewStatistics
        // 
        this.listViewStatistics.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        this.listViewStatistics.FullRowSelect = true;
        this.listViewStatistics.GridLines = true;
        this.listViewStatistics.Location = new Point(10, 140);
        this.listViewStatistics.Name = "listViewStatistics";
        this.listViewStatistics.Size = new Size(436, 260);
        this.listViewStatistics.TabIndex = 1;
        this.listViewStatistics.UseCompatibleStateImageBehavior = false;
        this.listViewStatistics.View = View.Details;

        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new SizeF(96F, 96F);
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.ClientSize = new Size(784, 581);
        this.Controls.Add(this.tabControl);
        this.MinimumSize = new Size(720, 560);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "健康提醒小幫手";
        this.FormClosing += new FormClosingEventHandler(this.MainForm_FormClosing);
        this.Load += new EventHandler(this.MainForm_Load);
        this.Resize += new EventHandler(this.MainForm_Resize);

        this.contextMenuStrip.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this.dataGridViewReminders)).EndInit();
        ((System.ComponentModel.ISupportInitialize)(this.numSnoozeMinutes)).EndInit();
        this.ResumeLayout(false);
    }

    #endregion

    private NotifyIcon notifyIcon;
    private ContextMenuStrip contextMenuStrip;
    private ToolStripMenuItem menuItemOpen;
    private ToolStripMenuItem menuItemPause;
    private ToolStripSeparator menuItemSeparator1;
    private ToolStripMenuItem menuItemStatistics;
    private ToolStripSeparator menuItemSeparator2;
    private ToolStripMenuItem menuItemExit;

    private TabControl tabControl;
    private TabPage tabPageStatus;
    private TabPage tabPageSettings;
    private TabPage tabPageStatistics;

    // 狀態頁面
    private Panel panelStatus;
    private Label lblNextReminder;
    private HealthReminder.Controls.AnimatedCountdownLabel lblBigCountdown;
    private Label lblNextReminderName;
    private ListView listViewCountdown;
    private Button btnPauseAll;
    private Label lblPauseStatus;


    // 設定頁面
    private Panel panelSettings;
    private GroupBox groupBoxReminders;
    private DataGridView dataGridViewReminders;
    private GroupBox groupBoxGeneral;
    private CheckBox chkStartWithWindows;
    private CheckBox chkStartMinimized;
    private Label lblSnoozeMinutes;
    private NumericUpDown numSnoozeMinutes;
    private GroupBox groupBoxDoNotDisturb;
    private CheckBox chkDoNotDisturb;
    private Label lblDoNotDisturbStart;
    private DateTimePicker dtpDoNotDisturbStart;
    private Label lblDoNotDisturbEnd;
    private DateTimePicker dtpDoNotDisturbEnd;
    private Button btnSaveSettings;
    private Button btnResetDefaults;
    private Button btnAddReminder;
    private Button btnDeleteReminder;
    private Label lblSnoozeUnit;

    // 統計頁面
    private Panel panelStatistics;
    private Label lblTodaySummary;
    private ListView listViewStatistics;
    private Label lblStreakDays;
    private HealthReminder.Controls.RoundedProgressBar progressBarToday;
    private Label lblProgressText;
    private Label lblFoxMessage;
}
