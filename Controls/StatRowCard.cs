using System.Drawing.Drawing2D;
using HealthReminder.UI;

namespace HealthReminder.Controls;

/// <summary>
/// 統計頁「今日提醒明細」的單列卡片：左側類型色條、名稱、完成次數，右側附完成率小進度條。
/// 取代外觀老舊的原生 ListView 列。
/// </summary>
public sealed class StatRowCard : UserControl
{
    private readonly Label _emojiLabel;
    private readonly Label _nameLabel;
    private readonly Label _countLabel;
    private Color _accent = AppTheme.Primary;
    private int _percent;

    public StatRowCard()
    {
        BackColor = Color.FromArgb(255, 251, 248);
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Height = 50;
        Margin = new Padding(0, 0, 0, 6);

        _emojiLabel = MakeLabel(new Point(16, 8), new Size(36, 32), 14F, FontStyle.Regular, ContentAlignment.MiddleCenter);
        _nameLabel = MakeLabel(new Point(56, 6), new Size(200, 20), 10F, FontStyle.Bold, ContentAlignment.MiddleLeft);
        _countLabel = MakeLabel(new Point(56, 27), new Size(200, 17), 8.5F, FontStyle.Regular, ContentAlignment.MiddleLeft);
        _countLabel.ForeColor = AppTheme.SecondaryText;

        Controls.Add(_emojiLabel);
        Controls.Add(_nameLabel);
        Controls.Add(_countLabel);
    }

    public void SetData(string emoji, string name, int completed, int total, Color accent, bool emphasize)
    {
        _emojiLabel.Text = emoji;
        _nameLabel.Text = name;
        _countLabel.Text = $"完成 {completed} / {total} 次";
        _accent = accent;
        _percent = total > 0 ? (int)Math.Round(completed * 100.0 / total) : 0;
        BackColor = emphasize ? AppTheme.SelectedBackground : Color.FromArgb(255, 251, 248);
        _nameLabel.ForeColor = emphasize ? AppTheme.Primary : AppTheme.PrimaryText;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 10);

        // 左側類型色條
        g.SetClip(path);
        using (var accentBrush = new SolidBrush(_accent))
        {
            g.FillRectangle(accentBrush, new Rectangle(0, 0, 5, Height));
        }
        g.ResetClip();

        // 右側完成率小進度條 + 百分比
        const int barWidth = 150, barHeight = 10, textWidth = 54;
        int barX = Width - barWidth - textWidth - 12;
        int barY = (Height - barHeight) / 2;
        if (barX > _nameLabel.Right + 10)
        {
            var track = new Rectangle(barX, barY, barWidth, barHeight);
            using (var trackPath = Rounded(track, barHeight / 2))
            using (var trackBrush = new SolidBrush(Color.FromArgb(239, 230, 223)))
            {
                g.FillPath(trackBrush, trackPath);
            }

            var fillWidth = (int)Math.Round(barWidth * (_percent / 100.0));
            if (fillWidth > 1)
            {
                fillWidth = Math.Clamp(fillWidth, barHeight, barWidth);
                using var fillPath = Rounded(new Rectangle(barX, barY, fillWidth, barHeight), barHeight / 2);
                using var fillBrush = new SolidBrush(_accent);
                g.FillPath(fillBrush, fillPath);
            }

            using var font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold);
            var textRect = new Rectangle(barX + barWidth + 6, 0, textWidth, Height);
            TextRenderer.DrawText(g, $"{_percent}%", font, textRect, _accent,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        using var pen = new Pen(AppTheme.Border);
        g.DrawPath(pen, path);
    }

    private static Label MakeLabel(Point location, Size size, float fontSize, FontStyle style, ContentAlignment align)
    {
        return new Label
        {
            AutoEllipsis = true,
            Location = location,
            Size = size,
            Font = new Font(AppTheme.FontFamily, fontSize, style),
            ForeColor = AppTheme.PrimaryText,
            TextAlign = align
        };
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
