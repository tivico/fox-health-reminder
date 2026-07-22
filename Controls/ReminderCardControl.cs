using System.Drawing.Drawing2D;
using HealthReminder.Models;
using HealthReminder.UI;

namespace HealthReminder.Controls;

/// <summary>
/// 提醒設定摘要卡片。
/// </summary>
public sealed class ReminderCardControl : UserControl
{
    private readonly Label _emojiLabel;
    private readonly Label _nameLabel;
    private readonly Label _detailLabel;
    private readonly Label _messageLabel;
    private readonly ToggleSwitch _enabledSwitch;
    private Color _accent = AppTheme.Primary;

    public ReminderCardControl(ReminderItem item)
    {
        Item = item;
        BackColor = AppTheme.CardBackground;
        DoubleBuffered = true;
        Height = 104;
        Margin = new Padding(0, 0, 0, 10);
        Padding = new Padding(16);

        _emojiLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 20),
            Size = new Size(48, 48),
            Font = new Font(AppTheme.FontFamily, 23F),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _nameLabel = new Label
        {
            AutoEllipsis = true,
            Location = new Point(76, 16),
            Size = new Size(270, 26),
            Font = new Font(AppTheme.FontFamily, 10.5F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText
        };
        _detailLabel = new Label
        {
            AutoEllipsis = true,
            Location = new Point(76, 44),
            Size = new Size(300, 22),
            Font = new Font(AppTheme.FontFamily, 8.5F),
            ForeColor = AppTheme.Primary
        };
        _messageLabel = new Label
        {
            AutoEllipsis = true,
            Location = new Point(76, 70),
            Size = new Size(390, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font(AppTheme.FontFamily, 8.5F),
            ForeColor = AppTheme.SecondaryText
        };
        _enabledSwitch = new ToggleSwitch
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(500, 19)
        };
        _enabledSwitch.CheckedChanged += (_, _) =>
        {
            Item.IsEnabled = _enabledSwitch.Checked;
            EnabledChangedByUser?.Invoke(this, EventArgs.Empty);
        };

        var editButton = CreateTextButton("編輯", AppTheme.SelectedBackground, AppTheme.Primary);
        editButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        editButton.Location = new Point(560, 16);
        editButton.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);

        var deleteButton = CreateTextButton("刪除", Color.FromArgb(250, 235, 235), AppTheme.Danger);
        deleteButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        deleteButton.Location = new Point(634, 16);
        deleteButton.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);

        Controls.Add(_emojiLabel);
        Controls.Add(_nameLabel);
        Controls.Add(_detailLabel);
        Controls.Add(_messageLabel);
        Controls.Add(_enabledSwitch);
        Controls.Add(editButton);
        Controls.Add(deleteButton);

        Resize += (_, _) =>
        {
            _enabledSwitch.Left = ClientSize.Width - 214;
            editButton.Left = ClientSize.Width - 154;
            deleteButton.Left = ClientSize.Width - 80;
        };
        RefreshContent();
    }

    public ReminderItem Item { get; private set; }

    public event EventHandler? EditRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? EnabledChangedByUser;

    public void UpdateItem(ReminderItem item)
    {
        Item = item;
        RefreshContent();
    }

    private void RefreshContent()
    {
        _accent = AppTheme.AccentFor(Item.Type);
        _emojiLabel.Text = Item.Emoji;
        _nameLabel.Text = Item.Name;
        var schedule = Item.ScheduleKind == ReminderScheduleKind.OneTime
            ? $"一次性 · {Item.OneTimeAtLocal:MM/dd HH:mm}"
            : $"每 {Item.IntervalMinutes} 分鐘";
        var action = Item.ActionKind == ReminderActionKind.TimedActivity
            ? $"計時 {Item.ActivityDurationSeconds} 秒{(Item.AutoStartActivity ? "（自動開始）" : string.Empty)}"
            : "手動完成";
        _detailLabel.Text = $"{schedule} · {action} · {(Item.PlaySound ? "有音效" : "靜音")}";
        _messageLabel.Text = Item.Message;
        _enabledSwitch.Checked = Item.IsEnabled;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 12);

        // 依提醒類型上色的左側色條
        e.Graphics.SetClip(path);
        using (var accentBrush = new SolidBrush(_accent))
        {
            e.Graphics.FillRectangle(accentBrush, new Rectangle(0, 0, 5, Height));
        }
        e.Graphics.ResetClip();

        using var pen = new Pen(AppTheme.Border);
        e.Graphics.DrawPath(pen, path);
    }

    private static Button CreateTextButton(string text, Color background, Color foreground)
    {
        var button = new Button
        {
            BackColor = background,
            ForeColor = foreground,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(AppTheme.FontFamily, 8.5F, FontStyle.Bold),
            Size = new Size(66, 34),
            Text = text,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
