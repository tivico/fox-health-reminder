using System.ComponentModel;
using HealthReminder.Controls;
using HealthReminder.Forms;
using HealthReminder.Models;
using HealthReminder.Services;
using HealthReminder.UI;
using HealthReminder.Windows;
using Newtonsoft.Json;

namespace HealthReminder;

public partial class MainForm
{
    private Label? _settingsDraftLabel;
    private bool _loadingSettings;

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
        _loadingSettings = true;
        var settings = SettingsManager.Instance.Settings;

        _reminderItems = new BindingList<ReminderItem>(
            settings.Reminders.Select(item => item.Copy()).ToList());
        RefreshReminderCards();

        // 一般設定
        chkStartWithWindows.Checked = settings.StartWithWindows;
        chkStartMinimized.Checked = settings.StartMinimized;
        numSnoozeMinutes.Value = settings.SnoozeMinutes;
        if (_reduceMotionCheckBox != null)
        {
            _reduceMotionCheckBox.Checked = settings.ReduceMotion;
        }
        if (_focusNotificationCheckBox != null)
        {
            _focusNotificationCheckBox.Checked = settings.FocusNotificationForKeyboard;
        }
        UpdateStorageLocationDisplay();
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
        _loadingSettings = false;
        UpdateSettingsDraftStatus();
    }

    private static bool ShouldFocusNotificationForKeyboard()
    {
        return SettingsManager.Instance.Settings.FocusNotificationForKeyboard
            || WindowsAccessibility.IsScreenReaderActive;
    }

    private void UpdateStorageLocationDisplay()
    {
        if (_storageLocationLabel == null)
        {
            return;
        }

        var manager = SettingsManager.Instance;
        var mode = manager.UsesPortableStorage ? "可攜模式" : "AppData";
        var fallback = manager.StorageNotice == null ? string.Empty : "（已確認改用 AppData）";
        _storageLocationLabel.Text =
            $"資料位置：{mode}{fallback}{Environment.NewLine}{manager.StorageFolder}";
        _storageLocationLabel.AccessibleDescription =
            $"目前使用{mode}，設定與健康紀錄儲存在 {manager.StorageFolder}";
    }

    private void btnSaveSettings_Click(object sender, EventArgs e)
    {
        SaveSettings();
    }

    private void btnResetDefaults_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "確定要重設為預設值嗎？\n\n這會將所有提醒項目和設定恢復成初始狀態。",
            "確認重設", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        var manager = SettingsManager.Instance;
        var startupChanged = manager.Settings.StartWithWindows;
        if (!manager.SaveSettings(new AppSettings()))
        {
            ShowSaveError(manager.LastError);
            return;
        }

        LoadSettingsToUI();
        ApplySavedReminders();
        if (ApplyStartupPreference(startupChanged))
        {
            ShowSettingsFeedback("✓ 已恢復預設值並重新排程", AppTheme.Healthy);
        }
    }

    private void btnDeleteReminder_Click(object sender, EventArgs e)
    {
        MessageBox.Show("請在提醒卡片上點選「刪除」。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private void SaveSettings()
    {
        var manager = SettingsManager.Instance;
        var candidate = BuildSettingsDraft();
        if (!DailyAchievementCalculator.IsValidWorkday(candidate.WorkdayStart, candidate.WorkdayEnd))
        {
            ShowSettingsFeedback("每日目標計算時段的結束必須晚於開始時間。", AppTheme.Danger);
            return;
        }

        if (candidate.DoNotDisturbEnabled
            && (int)candidate.DoNotDisturbStart.TotalMinutes == (int)candidate.DoNotDisturbEnd.TotalMinutes)
        {
            ShowSettingsFeedback("勿擾的開始與結束時間必須不同。", AppTheme.Danger);
            return;
        }

        if (_reminderItems != null)
        {
            var error = ValidateReminders(_reminderItems);
            if (error != null)
            {
                ShowSaveError(error);
                return;
            }
        }

        var startupChanged = candidate.StartWithWindows != manager.Settings.StartWithWindows;
        if (!manager.SaveSettings(candidate))
        {
            ShowSaveError(manager.LastError);
            return;
        }

        if (_headerFox != null) _headerFox.AnimationEnabled = !candidate.ReduceMotion;
        lblBigCountdown.AnimationsEnabled = !candidate.ReduceMotion;
        progressBarToday.AnimationsEnabled = !candidate.ReduceMotion;

        ApplySavedReminders();
        if (ApplyStartupPreference(startupChanged))
        {
            ShowSettingsFeedback("✓ 設定已儲存", AppTheme.Healthy);
        }
    }

    private AppSettings BuildSettingsDraft()
    {
        var draft = SettingsManager.Instance.Settings.Copy();
        if (_reminderItems != null) draft.Reminders = _reminderItems.Select(item => item.Copy()).ToList();
        draft.WorkdayStart = _workdayStartPicker?.Value.TimeOfDay ?? draft.WorkdayStart;
        draft.WorkdayEnd = _workdayEndPicker?.Value.TimeOfDay ?? draft.WorkdayEnd;
        draft.DoNotDisturbEnabled = chkDoNotDisturb.Checked;
        draft.DoNotDisturbStart = dtpDoNotDisturbStart.Value.TimeOfDay;
        draft.DoNotDisturbEnd = dtpDoNotDisturbEnd.Value.TimeOfDay;
        draft.StartMinimized = chkStartMinimized.Checked;
        draft.SnoozeMinutes = (int)numSnoozeMinutes.Value;
        draft.ReduceMotion = _reduceMotionCheckBox?.Checked ?? false;
        draft.FocusNotificationForKeyboard = _focusNotificationCheckBox?.Checked ?? false;
        draft.StartWithWindows = chkStartWithWindows.Checked;
        return draft;
    }

    private void UpdateSettingsDraftStatus()
    {
        if (_loadingSettings || _settingsDraftLabel == null) return;

        // 比較草稿內容，可正確處理「改回原值」與只提交一筆刪除的情況。
        var changed = JsonConvert.SerializeObject(BuildSettingsDraft())
            != JsonConvert.SerializeObject(SettingsManager.Instance.Settings);
        _settingsDraftLabel.Text = changed
            ? "有尚未儲存的變更\n按「儲存設定」後生效"
            : "設定已同步\n確認刪除會立即生效";
        _settingsDraftLabel.ForeColor = changed ? AppTheme.Warning : AppTheme.SecondaryText;
        _settingsDraftLabel.AccessibleName = _settingsDraftLabel.Text;
    }

    private void SettingsInputChanged(object? sender, EventArgs e)
    {
        _settingsFeedbackTimer?.Stop();
        if (_settingsFeedbackLabel != null) _settingsFeedbackLabel.Visible = false;
        UpdateSettingsDraftStatus();
    }

    private void ApplySavedReminders()
    {
        _notificationService.RemoveQueued(_reminderService.Reload());
        RefreshReminderCards();
        UpdateCountdownDisplay();
        UpdateStatisticsDisplay();
    }

    private bool ApplyStartupPreference(bool changed)
    {
        if (!changed && !_startupSyncPending) return true;

        var error = WindowsStartupRegistration.Apply(
            SettingsManager.Instance.Settings.StartWithWindows, Application.ExecutablePath);
        _startupSyncPending = error != null;
        if (error == null) return true;

        MessageBox.Show(this, error, "設定已儲存，開機自啟更新失敗",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void ShowSaveError(string? error)
    {
        MessageBox.Show(this, error ?? "設定儲存失敗，請稍後再試。",
            "無法儲存設定", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                card.Changed += SettingsInputChanged;
                _reminderCardsFlow.Controls.Add(card);
            }
        }

        if (_reminderItems == null || _reminderItems.Count == 0)
        {
            _reminderCardsFlow.Controls.Add(CreateSettingsHint("目前沒有提醒，按「新增提醒」建立第一筆。"));
        }
        _reminderCardsFlow.ResumeLayout(true);
        UpdateSettingsDraftStatus();
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

        var result = MessageBox.Show(
            this,
            $"確定要刪除「{item.Name}」嗎？刪除會立即生效。",
            "確認刪除",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (result != DialogResult.Yes) return;

        var manager = SettingsManager.Instance;
        if (!manager.DeleteReminder(item.Id))
        {
            ShowSaveError(manager.LastError);
            return;
        }

        _reminderItems.Remove(item);
        ApplySavedReminders();
        ShowSettingsFeedback("✓ 已刪除提醒", AppTheme.Healthy);
    }

    private static int GetFlowCardWidth(FlowLayoutPanel flow)
    {
        var scrollbarWidth = flow.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
        return Math.Max(320, flow.ClientSize.Width - flow.Padding.Horizontal - scrollbarWidth - 2);
    }


    private static string? ValidateReminders(IEnumerable<ReminderItem> reminders)
    {
        foreach (var item in reminders)
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

    private void BuildSettingsPage()
    {
        panelSettings.SuspendLayout();
        panelSettings.Controls.Clear();
        panelSettings.BackColor = AppTheme.WindowBackground;
        panelSettings.AutoScroll = false;

        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = AppTheme.WindowBackground
        };
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        var scrollContent = new Panel
        {
            Name = "settingsScrollContent",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Margin = Padding.Empty,
            TabIndex = 0
        };

        // 只有外層捲動；各卡片依內容增高，避免清單內外同時出現捲軸。
        var layout = new ElevatedPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.WindowBackground,
            Padding = new Padding(8, 6, 8, 12),
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 3; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var addReminderButton = new Button
        {
            Text = "+ 新增提醒",
            AutoSize = true,
            MinimumSize = new Size(112, 36),
            Margin = new Padding(0, 0, 0, 8),
            UseVisualStyleBackColor = false
        };
        StyleButton(addReminderButton, AppTheme.Healthy, Color.FromArgb(64, 133, 94), Color.White);
        addReminderButton.Click += (_, e) => btnAddReminder_Click(addReminderButton, e);
        btnDeleteReminder.Visible = false;

        _reminderCardsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = AppTheme.CardBackground,
            Margin = Padding.Empty
        };
        _reminderCardsFlow.SizeChanged += (_, _) =>
        {
            foreach (var card in _reminderCardsFlow.Controls.OfType<ReminderCardControl>())
                card.Width = GetFlowCardWidth(_reminderCardsFlow);
        };
        var reminderSection = CreateSettingsCard("提醒管理",
            CreateSettingsHint("新增、編輯與開關會先保留為草稿；按下方「儲存設定」後才生效。"),
            addReminderButton, _reminderCardsFlow);

        _workdayStartPicker = CreateTimePicker();
        _workdayEndPicker = CreateTimePicker();
        dtpDoNotDisturbStart.Size = new Size(84, 28);
        dtpDoNotDisturbEnd.Size = new Size(84, 28);
        lblDoNotDisturbStart.Text = "從";
        lblDoNotDisturbEnd.Text = "至";
        numSnoozeMinutes.Size = new Size(64, 28);
        lblSnoozeMinutes.Text = "稍後提醒延遲";
        lblSnoozeUnit.Text = "分鐘";
        var timingCard = CreateSettingsCard("時間與勿擾",
            CreateSettingsHint("每日目標計算時段"),
            CreateSettingsRow(_workdayStartPicker, CreateSettingsHint("至"), _workdayEndPicker),
            CreateSettingsHint("只用於計算今日目標與連續達標；時段外仍會提醒。"),
            CreateSettingsRow(chkDoNotDisturb, lblDoNotDisturbStart,
                dtpDoNotDisturbStart, lblDoNotDisturbEnd, dtpDoNotDisturbEnd),
            CreateSettingsHint("啟用勿擾後，這段時間會暫停提醒，可跨午夜。"),
            CreateSettingsRow(lblSnoozeMinutes, numSnoozeMinutes, lblSnoozeUnit));
        chkDoNotDisturb.Text = "啟用勿擾";

        _reduceMotionCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "減少動畫",
            AccessibleName = "減少動畫",
            AccessibleDescription = "停用狐狸、倒數、進度條及按鈕的動態補間效果。"
        };
        _focusNotificationCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "通知出現時可直接使用鍵盤操作",
            AccessibleName = "通知出現時取得鍵盤焦點",
            AccessibleDescription = "通知取得焦點後，可直接使用 Enter、Esc 或 Alt 快捷鍵。螢幕閱讀器啟用時會自動套用。"
        };
        _storageLocationLabel = CreateSettingsHint(string.Empty);
        _storageLocationLabel.AccessibleName = "資料儲存位置";
        UpdateStorageLocationDisplay();
        var previewButton = new Button
        {
            Text = "預覽狐狸動畫",
            AutoSize = true,
            MinimumSize = new Size(132, 36),
            Margin = new Padding(0, 8, 0, 0),
            UseVisualStyleBackColor = false
        };
        StyleButton(previewButton, AppTheme.SelectedBackground, AppTheme.Border, AppTheme.Primary);
        previewButton.Click += (_, _) =>
        {
            using var preview = new FoxAnimationPreviewForm();
            preview.ShowDialog(this);
        };
        var preferencesCard = CreateSettingsCard("應用程式偏好",
            CreateSettingsRow(chkStartWithWindows, chkStartMinimized),
            CreateSettingsRow(_reduceMotionCheckBox),
            CreateSettingsRow(_focusNotificationCheckBox),
            CreateSettingsHint("開啟鍵盤操作後，通知出現時會取得焦點，可按 Enter、Esc 或 Alt 快捷鍵。"),
            _storageLocationLabel, previewButton);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = AppTheme.WindowBackground,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(8, 6, 8, 6),
            Margin = Padding.Empty,
            BackColor = AppTheme.WindowBackground,
            TabIndex = 1,
            AccessibleName = "設定儲存操作列"
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        btnSaveSettings.Size = new Size(128, 40);
        btnResetDefaults.Size = new Size(116, 40);
        btnSaveSettings.Margin = new Padding(8, 0, 0, 0);
        btnResetDefaults.Margin = Padding.Empty;
        _settingsFeedbackLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Margin = Padding.Empty,
            Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold),
            ForeColor = AppTheme.Healthy,
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        _settingsDraftLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            Margin = new Padding(0, 0, 8, 0),
            Font = new Font(AppTheme.FontFamily, 9F),
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleRole = AccessibleRole.StaticText
        };
        actions.Controls.Add(btnSaveSettings);
        actions.Controls.Add(btnResetDefaults);
        footer.Controls.Add(_settingsDraftLabel, 0, 0);
        footer.Controls.Add(actions, 1, 0);
        footer.Controls.Add(_settingsFeedbackLabel, 0, 1);
        footer.SetColumnSpan(_settingsFeedbackLabel, 2);

        layout.Controls.Add(reminderSection, 0, 0);
        layout.Controls.Add(timingCard, 0, 1);
        layout.Controls.Add(preferencesCard, 0, 2);
        scrollContent.Controls.Add(layout);
        page.Controls.Add(scrollContent, 0, 0);
        page.Controls.Add(footer, 0, 1);
        panelSettings.Controls.Add(page);
        scrollContent.ClientSizeChanged += (_, _) => layout.Width = scrollContent.ClientSize.Width;

        chkStartWithWindows.CheckedChanged += SettingsInputChanged;
        chkStartMinimized.CheckedChanged += SettingsInputChanged;
        chkDoNotDisturb.CheckedChanged += SettingsInputChanged;
        _reduceMotionCheckBox.CheckedChanged += SettingsInputChanged;
        _focusNotificationCheckBox.CheckedChanged += SettingsInputChanged;
        numSnoozeMinutes.ValueChanged += SettingsInputChanged;
        _workdayStartPicker.ValueChanged += SettingsInputChanged;
        _workdayEndPicker.ValueChanged += SettingsInputChanged;
        dtpDoNotDisturbStart.ValueChanged += SettingsInputChanged;
        dtpDoNotDisturbEnd.ValueChanged += SettingsInputChanged;
        RefreshReminderCards();
        panelSettings.ResumeLayout(true);
    }

    private static DateTimePicker CreateTimePicker() => new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "HH:mm",
        ShowUpDown = true,
        Size = new Size(84, 28)
    };

    private static Label CreateSettingsHint(string text) => new()
    {
        AutoSize = true,
        Text = text,
        ForeColor = AppTheme.SecondaryText,
        Margin = new Padding(0, 4, 0, 8)
    };

    private static FlowLayoutPanel CreateSettingsRow(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 2, 0, 6)
        };
        foreach (var control in controls)
        {
            control.Margin = new Padding(0, 6, 16, 6);
            row.Controls.Add(control);
        }
        return row;
    }

    private static CardPanel CreateSettingsCard(string title, params Control[] controls)
    {
        var card = new CardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(18, 16, 18, 16),
            Margin = new Padding(0, 0, 0, 12)
        };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = controls.Length + 1,
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var heading = new Label
        {
            AutoSize = true,
            Text = title,
            Font = new Font(AppTheme.FontFamily, 11F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Margin = new Padding(0, 0, 0, 10)
        };
        content.Controls.Add(heading, 0, 0);
        for (var i = 0; i < controls.Length; i++)
            content.Controls.Add(controls[i], 0, i + 1);
        for (var i = 0; i < content.RowCount; i++)
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // 提示文字依實際內容寬度換行，長資料路徑也不撐寬整頁。
        content.SizeChanged += (_, _) =>
        {
            foreach (var label in content.Controls.OfType<Label>())
                label.MaximumSize = new Size(Math.Max(1, content.ClientSize.Width), 0);
        };
        card.Controls.Add(content);
        return card;
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
}
