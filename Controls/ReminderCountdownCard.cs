using System.Drawing.Drawing2D;
using HealthReminder.Models;
using HealthReminder.Services;
using HealthReminder.UI;

namespace HealthReminder.Controls;

/// <summary>
/// 今日總覽中的單筆提醒狀態卡。
/// </summary>
public sealed class ReminderCountdownCard : UserControl
{
    private readonly Label _emojiLabel;
    private readonly Label _nameLabel;
    private readonly Label _intervalLabel;
    private readonly Label _remainingLabel;
    private readonly Label _nextTimeLabel;
    private Color _accent = AppTheme.Primary;

    public ReminderCountdownCard()
    {
        BackColor = Color.FromArgb(255, 251, 248);
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Height = 64;
        Margin = new Padding(0, 0, 0, 8);

        _emojiLabel = CreateLabel(new Point(14, 12), new Size(42, 36), 17F, FontStyle.Regular);
        _nameLabel = CreateLabel(new Point(58, 10), new Size(170, 22), 9.5F, FontStyle.Bold);
        _intervalLabel = CreateLabel(new Point(58, 33), new Size(160, 18), 8F, FontStyle.Regular);
        _remainingLabel = CreateLabel(new Point(290, 11), new Size(118, 24), 11F, FontStyle.Bold);
        _remainingLabel.ForeColor = AppTheme.Primary;
        _nextTimeLabel = CreateLabel(new Point(420, 15), new Size(170, 20), 8.5F, FontStyle.Regular);
        _nextTimeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _nextTimeLabel.TextAlign = ContentAlignment.MiddleRight;

        Controls.AddRange(new Control[]
        {
            _emojiLabel,
            _nameLabel,
            _intervalLabel,
            _remainingLabel,
            _nextTimeLabel
        });
        void ArrangeContent()
        {
            var scale = DeviceDpi / 96f;
            int Px(int value) => (int)Math.Round(value * scale);
            var rightWidth = Px(150);
            var right = ClientSize.Width - rightWidth - Px(14);
            _remainingLabel.SetBounds(right, Px(8), rightWidth, Px(24));
            _remainingLabel.TextAlign = ContentAlignment.MiddleRight;
            _nextTimeLabel.SetBounds(right, Px(34), rightWidth, Px(20));
            _nameLabel.Width = Math.Max(Px(40), right - _nameLabel.Left - Px(12));
            _intervalLabel.Width = _nameLabel.Width;
        }
        Resize += (_, _) => ArrangeContent();
        DpiChangedAfterParent += (_, _) => ArrangeContent();
        ArrangeContent();
    }

    public string ReminderId { get; private set; } = string.Empty;

    public void UpdateFrom(ReminderTimer timer)
    {
        var item = timer.Item;
        var remaining = timer.RemainingTime;
        ReminderId = item.Id;
        _accent = AppTheme.AccentFor(item.Type);
        _emojiLabel.Text = item.Emoji;
        _nameLabel.Text = item.Name;
        _intervalLabel.Text = !item.IsEnabled
            ? "目前未啟用"
            : item.ScheduleKind == ReminderScheduleKind.OneTime
                ? $"一次性 · {item.OneTimeAtLocal:MM/dd HH:mm}"
                : $"每 {item.IntervalMinutes} 分鐘";

        if (!item.IsEnabled)
        {
            _remainingLabel.Text = "未啟用";
            _remainingLabel.ForeColor = AppTheme.SecondaryText;
            _nextTimeLabel.Text = string.Empty;
        }
        else if (timer.IsPaused)
        {
            _remainingLabel.Text = "已暫停";
            _remainingLabel.ForeColor = AppTheme.Warning;
            _nextTimeLabel.Text = $"剩餘 {FormatRemaining(remaining)}";
        }
        else
        {
            _remainingLabel.Text = FormatRemaining(remaining);
            _remainingLabel.ForeColor = AppTheme.Primary;
            _nextTimeLabel.Text = $"預計 {timer.NextTriggerTime:HH:mm}";
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 10);

        // 依提醒類型上色的左側色條，讓每一列一眼可辨識
        e.Graphics.SetClip(path);
        using (var accentBrush = new SolidBrush(_accent))
        {
            e.Graphics.FillRectangle(accentBrush, new Rectangle(0, 0, 5, Height));
        }
        e.Graphics.ResetClip();

        using var pen = new Pen(AppTheme.Border);
        e.Graphics.DrawPath(pen, path);
    }

    internal static string FormatRemaining(TimeSpan remaining)
    {
        return remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private static Label CreateLabel(Point location, Size size, float fontSize, FontStyle style)
    {
        return new Label
        {
            AutoEllipsis = true,
            Location = location,
            Size = size,
            Font = new Font(AppTheme.FontFamily, fontSize, style),
            ForeColor = AppTheme.PrimaryText,
            TextAlign = ContentAlignment.MiddleLeft
        };
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
