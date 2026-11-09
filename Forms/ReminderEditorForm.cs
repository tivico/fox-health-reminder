using HealthReminder.Controls;
using HealthReminder.Models;
using HealthReminder.UI;

namespace HealthReminder.Forms;

/// <summary>
/// 通用提醒編輯器：排程方式與完成方式彼此獨立，不綁定特定健康任務。
/// </summary>
public sealed class ReminderEditorForm : Form
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _messageTextBox;
    private readonly ComboBox _emojiComboBox;
    private readonly ComboBox _scheduleKindComboBox;
    private readonly NumericUpDown _intervalInput;
    private readonly DateTimePicker _oneTimeInput;
    private readonly Label _intervalUnitLabel;
    private readonly ComboBox _actionKindComboBox;
    private readonly NumericUpDown _activitySecondsInput;
    private readonly Label _activitySecondsLabel;
    private readonly Label _activitySecondsUnitLabel;
    private readonly ToggleSwitch _autoStartSwitch;
    private readonly Label _autoStartLabel;
    private readonly ToggleSwitch _enabledSwitch;
    private readonly ToggleSwitch _soundSwitch;
    private readonly ReminderItem? _sourceItem;
    private readonly ReminderItem _workingItem;

    public ReminderEditorForm(ReminderItem? source = null)
    {
        _sourceItem = source;
        _workingItem = source?.Copy() ?? CreateDefaultItem();

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AppTheme.WindowBackground;
        ClientSize = new Size(560, 630);
        Font = new Font(AppTheme.FontFamily, 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = source == null ? "新增提醒" : "編輯提醒";

        Controls.Add(new Label
        {
            AutoSize = false,
            Location = new Point(28, 22),
            Size = new Size(500, 34),
            Font = new Font(AppTheme.FontFamily, 15F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = source == null ? "建立新的提醒任務" : $"編輯 {_workingItem.Name}"
        });
        Controls.Add(new Label
        {
            AutoSize = false,
            Location = new Point(30, 58),
            Size = new Size(500, 24),
            ForeColor = AppTheme.SecondaryText,
            Text = "完成後先保留為草稿，回設定頁按「儲存設定」才會生效。"
        });

        _nameTextBox = CreateTextBox(new Point(30, 118), new Size(326, 30));
        _messageTextBox = CreateTextBox(new Point(30, 196), new Size(500, 72));
        _messageTextBox.Multiline = true;
        _messageTextBox.ScrollBars = ScrollBars.Vertical;

        _emojiComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(376, 118),
            Size = new Size(154, 30),
            Font = new Font(AppTheme.FontFamily, 11F)
        };
        _emojiComboBox.Items.AddRange(new object[] { "💧", "👀", "🏃", "💡", "🦊", "☕", "🧘", "🍎", "💊", "📌" });

        _scheduleKindComboBox = CreateComboBox(new Point(30, 316), new Size(200, 30));
        _scheduleKindComboBox.Items.AddRange(new object[] { "持續：每隔一段時間", "一次性：指定日期時間" });
        _scheduleKindComboBox.SelectedIndexChanged += (_, _) => UpdateConditionalFields();

        _intervalInput = new NumericUpDown
        {
            Location = new Point(260, 316),
            Size = new Size(116, 30),
            Minimum = 1,
            Maximum = 1440,
            Font = new Font(AppTheme.FontFamily, 10F)
        };
        _intervalUnitLabel = CreateMutedLabel("分鐘", new Point(384, 321));
        _oneTimeInput = new DateTimePicker
        {
            CustomFormat = "yyyy/MM/dd HH:mm",
            Format = DateTimePickerFormat.Custom,
            Location = new Point(260, 316),
            ShowUpDown = true,
            Size = new Size(270, 30),
            Font = new Font(AppTheme.FontFamily, 10F)
        };

        _actionKindComboBox = CreateComboBox(new Point(30, 398), new Size(200, 30));
        _actionKindComboBox.Items.AddRange(new object[] { "手動按下完成", "計時活動完成" });
        _actionKindComboBox.SelectedIndexChanged += (_, _) => UpdateConditionalFields();

        _activitySecondsInput = new NumericUpDown
        {
            Location = new Point(260, 398),
            Size = new Size(86, 30),
            Minimum = 5,
            Maximum = 3600,
            Font = new Font(AppTheme.FontFamily, 10F)
        };
        _activitySecondsLabel = CreateFieldLabel("活動秒數", new Point(260, 374));
        _activitySecondsUnitLabel = CreateMutedLabel("秒", new Point(354, 403));
        _autoStartLabel = CreateFieldLabel("通知後自動開始", new Point(398, 374));
        _autoStartSwitch = new ToggleSwitch { Location = new Point(400, 398) };

        _enabledSwitch = new ToggleSwitch { Location = new Point(30, 500) };
        _soundSwitch = new ToggleSwitch { Location = new Point(260, 500) };

        var cancelButton = CreateButton("取消", Color.FromArgb(235, 229, 225), AppTheme.SecondaryText);
        cancelButton.Location = new Point(306, 566);
        cancelButton.DialogResult = DialogResult.Cancel;

        var saveButton = CreateButton(source == null ? "加入草稿" : "更新草稿", AppTheme.Primary, Color.White);
        saveButton.Location = new Point(416, 566);
        saveButton.Click += SaveButton_Click;

        Controls.Add(CreateFieldLabel("提醒名稱", new Point(30, 94)));
        Controls.Add(CreateFieldLabel("圖示", new Point(376, 94)));
        Controls.Add(_nameTextBox);
        Controls.Add(_emojiComboBox);
        Controls.Add(CreateFieldLabel("提醒訊息", new Point(30, 172)));
        Controls.Add(_messageTextBox);
        Controls.Add(CreateFieldLabel("排程方式", new Point(30, 292)));
        Controls.Add(CreateFieldLabel("排程設定", new Point(260, 292)));
        Controls.Add(_scheduleKindComboBox);
        Controls.Add(_intervalInput);
        Controls.Add(_intervalUnitLabel);
        Controls.Add(_oneTimeInput);
        Controls.Add(CreateFieldLabel("完成方式", new Point(30, 374)));
        Controls.Add(_actionKindComboBox);
        Controls.Add(_activitySecondsLabel);
        Controls.Add(_activitySecondsInput);
        Controls.Add(_activitySecondsUnitLabel);
        Controls.Add(_autoStartLabel);
        Controls.Add(_autoStartSwitch);
        Controls.Add(CreateFieldLabel("啟用提醒", new Point(30, 474)));
        Controls.Add(_enabledSwitch);
        Controls.Add(CreateFieldLabel("播放音效", new Point(260, 474)));
        Controls.Add(_soundSwitch);
        Controls.Add(cancelButton);
        Controls.Add(saveButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        LoadValues();
    }

    public ReminderItem ResultItem => _workingItem;

    private void LoadValues()
    {
        _nameTextBox.Text = _workingItem.Name;
        _messageTextBox.Text = _workingItem.Message;
        _enabledSwitch.Checked = _workingItem.IsEnabled;
        _soundSwitch.Checked = _workingItem.PlaySound;
        _autoStartSwitch.Checked = _workingItem.AutoStartActivity;
        _intervalInput.Value = Math.Clamp(_workingItem.IntervalMinutes, 1, 1440);
        _activitySecondsInput.Value = Math.Clamp(_workingItem.ActivityDurationSeconds, 5, 3600);
        _oneTimeInput.Value = ClampDateTime(_workingItem.OneTimeAtLocal ?? DateTime.Now.AddHours(1));
        _scheduleKindComboBox.SelectedIndex = (int)_workingItem.ScheduleKind;
        _actionKindComboBox.SelectedIndex = (int)_workingItem.ActionKind;

        var emojiIndex = _emojiComboBox.Items.IndexOf(_workingItem.Emoji);
        _emojiComboBox.SelectedIndex = emojiIndex >= 0 ? emojiIndex : 3;
        UpdateConditionalFields();
    }

    private void UpdateConditionalFields()
    {
        var isOneTime = _scheduleKindComboBox.SelectedIndex == (int)ReminderScheduleKind.OneTime;
        _intervalInput.Visible = !isOneTime;
        _intervalUnitLabel.Visible = !isOneTime;
        _oneTimeInput.Visible = isOneTime;

        var isTimed = _actionKindComboBox.SelectedIndex == (int)ReminderActionKind.TimedActivity;
        _activitySecondsLabel.Visible = isTimed;
        _activitySecondsInput.Visible = isTimed;
        _activitySecondsUnitLabel.Visible = isTimed;
        _autoStartLabel.Visible = isTimed;
        _autoStartSwitch.Visible = isTimed;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        var name = _nameTextBox.Text.Trim();
        var message = _messageTextBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowValidationMessage("請輸入提醒名稱。", _nameTextBox);
            return;
        }

        if (message.Length == 0)
        {
            ShowValidationMessage("請輸入提醒訊息。", _messageTextBox);
            return;
        }

        var scheduleKind = (ReminderScheduleKind)Math.Max(0, _scheduleKindComboBox.SelectedIndex);
        var isEnabled = _enabledSwitch.Checked;
        DateTime? oneTimeAt = scheduleKind == ReminderScheduleKind.OneTime ? _oneTimeInput.Value : null;

        // 編輯視窗開啟期間，通知仍可能停用一次性提醒或將它延後。
        // 排程欄位未被編輯時，採用主畫面已同步的草稿值。
        if (_sourceItem?.ScheduleKind == ReminderScheduleKind.OneTime
            && _workingItem.ScheduleKind == ReminderScheduleKind.OneTime
            && _workingItem.OneTimeAtLocal is { } originalTime
            && oneTimeAt == ClampDateTime(originalTime)
            && isEnabled == _workingItem.IsEnabled)
        {
            oneTimeAt = _sourceItem.OneTimeAtLocal;
            isEnabled = _sourceItem.IsEnabled;
        }

        if (scheduleKind == ReminderScheduleKind.OneTime && isEnabled && oneTimeAt <= DateTime.Now)
        {
            ShowValidationMessage("一次性提醒時間必須晚於現在。", _oneTimeInput);
            return;
        }

        _workingItem.Name = name;
        _workingItem.Message = message;
        _workingItem.Emoji = _emojiComboBox.SelectedItem?.ToString() ?? "💡";
        _workingItem.ScheduleKind = scheduleKind;
        _workingItem.IntervalMinutes = (int)_intervalInput.Value;
        _workingItem.OneTimeAtLocal = oneTimeAt;
        _workingItem.ActionKind = (ReminderActionKind)Math.Max(0, _actionKindComboBox.SelectedIndex);
        _workingItem.ActivityDurationSeconds = (int)_activitySecondsInput.Value;
        _workingItem.AutoStartActivity = _workingItem.ActionKind == ReminderActionKind.TimedActivity && _autoStartSwitch.Checked;
        _workingItem.IsEnabled = isEnabled;
        _workingItem.PlaySound = _soundSwitch.Checked;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowValidationMessage(string message, Control control)
    {
        MessageBox.Show(this, message, "設定需要修正", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        control.Focus();
    }

    private static ReminderItem CreateDefaultItem()
    {
        return new ReminderItem
        {
            Type = ReminderType.Custom,
            Name = "新提醒",
            Message = "該休息一下囉！",
            IntervalMinutes = 30,
            ScheduleKind = ReminderScheduleKind.Interval,
            ActionKind = ReminderActionKind.Manual,
            ActivityDurationSeconds = 20,
            Emoji = "💡",
            IsEnabled = true,
            PlaySound = true
        };
    }

    private static DateTime ClampDateTime(DateTime value)
    {
        if (value < DateTimePicker.MinimumDateTime) return DateTimePicker.MinimumDateTime;
        if (value > DateTimePicker.MaximumDateTime) return DateTimePicker.MaximumDateTime;
        return value;
    }

    private static Label CreateFieldLabel(string text, Point location)
    {
        return new Label
        {
            AutoSize = true,
            Location = location,
            Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = text
        };
    }

    private static Label CreateMutedLabel(string text, Point location)
    {
        return new Label
        {
            AutoSize = true,
            Location = location,
            ForeColor = AppTheme.SecondaryText,
            Text = text
        };
    }

    private static ComboBox CreateComboBox(Point location, Size size)
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = location,
            Size = size,
            Font = new Font(AppTheme.FontFamily, 9.5F)
        };
    }

    private static TextBox CreateTextBox(Point location, Size size)
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Location = location,
            Size = size,
            Font = new Font(AppTheme.FontFamily, 10F)
        };
    }

    private static Button CreateButton(string text, Color background, Color foreground)
    {
        var button = new Button
        {
            BackColor = background,
            ForeColor = foreground,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold),
            Size = new Size(102, 40),
            Text = text,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
