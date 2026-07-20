using HealthReminder.Controls;
using HealthReminder.Models;
using HealthReminder.UI;

namespace HealthReminder.Forms;

/// <summary>
/// 新增與編輯提醒的專用對話框，避免直接操作表格儲存格。
/// </summary>
public sealed class ReminderEditorForm : Form
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _messageTextBox;
    private readonly ComboBox _emojiComboBox;
    private readonly NumericUpDown _intervalInput;
    private readonly ToggleSwitch _enabledSwitch;
    private readonly ToggleSwitch _soundSwitch;
    private readonly ReminderItem _workingItem;

    public ReminderEditorForm(ReminderItem? source = null)
    {
        _workingItem = source == null ? CreateDefaultItem() : CloneItem(source);

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AppTheme.WindowBackground;
        ClientSize = new Size(540, 520);
        Font = new Font(AppTheme.FontFamily, 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = source == null ? "新增提醒" : "編輯提醒";

        var title = new Label
        {
            AutoSize = false,
            Location = new Point(28, 22),
            Size = new Size(470, 34),
            Font = new Font(AppTheme.FontFamily, 15F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = source == null ? "建立新的健康提醒" : $"編輯 {_workingItem.Name}"
        };
        var subtitle = new Label
        {
            AutoSize = false,
            Location = new Point(30, 58),
            Size = new Size(470, 24),
            ForeColor = AppTheme.SecondaryText,
            Text = "設定名稱、提醒內容與間隔，儲存後會重新開始倒數。"
        };

        _nameTextBox = CreateTextBox(new Point(30, 118), new Size(310, 30));
        _messageTextBox = CreateTextBox(new Point(30, 196), new Size(476, 72));
        _messageTextBox.Multiline = true;
        _messageTextBox.ScrollBars = ScrollBars.Vertical;

        _emojiComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(360, 118),
            Size = new Size(146, 30),
            Font = new Font(AppTheme.FontFamily, 11F)
        };
        _emojiComboBox.Items.AddRange(new object[] { "💧", "👀", "🏃", "💡", "🦊", "☕", "🧘", "🍎" });

        _intervalInput = new NumericUpDown
        {
            Location = new Point(30, 314),
            Size = new Size(116, 30),
            Minimum = 1,
            Maximum = 1440,
            Value = Math.Clamp(_workingItem.IntervalMinutes, 1, 1440),
            Font = new Font(AppTheme.FontFamily, 10F)
        };

        _enabledSwitch = new ToggleSwitch { Location = new Point(30, 392) };
        _soundSwitch = new ToggleSwitch { Location = new Point(260, 392) };

        var cancelButton = CreateButton("取消", Color.FromArgb(235, 229, 225), AppTheme.SecondaryText);
        cancelButton.Location = new Point(286, 452);
        cancelButton.DialogResult = DialogResult.Cancel;

        var saveButton = CreateButton("儲存提醒", AppTheme.Primary, Color.White);
        saveButton.Location = new Point(396, 452);
        saveButton.Click += SaveButton_Click;

        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(CreateFieldLabel("提醒名稱", new Point(30, 94)));
        Controls.Add(CreateFieldLabel("圖示", new Point(360, 94)));
        Controls.Add(_nameTextBox);
        Controls.Add(_emojiComboBox);
        Controls.Add(CreateFieldLabel("提醒訊息", new Point(30, 172)));
        Controls.Add(_messageTextBox);
        Controls.Add(CreateFieldLabel("提醒間隔", new Point(30, 290)));
        Controls.Add(_intervalInput);
        Controls.Add(new Label
        {
            AutoSize = true,
            Location = new Point(154, 319),
            ForeColor = AppTheme.SecondaryText,
            Text = "分鐘"
        });
        Controls.Add(CreateFieldLabel("啟用提醒", new Point(30, 366)));
        Controls.Add(_enabledSwitch);
        Controls.Add(CreateFieldLabel("播放音效", new Point(260, 366)));
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

        var emojiIndex = _emojiComboBox.Items.IndexOf(_workingItem.Emoji);
        _emojiComboBox.SelectedIndex = emojiIndex >= 0 ? emojiIndex : 3;
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

        _workingItem.Name = name;
        _workingItem.Message = message;
        _workingItem.Emoji = _emojiComboBox.SelectedItem?.ToString() ?? "💡";
        _workingItem.IntervalMinutes = (int)_intervalInput.Value;
        _workingItem.IsEnabled = _enabledSwitch.Checked;
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
            Emoji = "💡",
            IsEnabled = true,
            PlaySound = true
        };
    }

    private static ReminderItem CloneItem(ReminderItem source)
    {
        return new ReminderItem
        {
            Id = source.Id,
            Type = source.Type,
            Name = source.Name,
            Message = source.Message,
            IntervalMinutes = source.IntervalMinutes,
            IsEnabled = source.IsEnabled,
            PlaySound = source.PlaySound,
            Emoji = source.Emoji
        };
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
