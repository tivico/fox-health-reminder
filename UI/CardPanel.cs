using System.Drawing.Drawing2D;

namespace HealthReminder.UI;

/// <summary>
/// 具有圓角與淡色邊框的輕量 WinForms 卡片容器。
/// </summary>
public sealed class CardPanel : Panel
{
    public CardPanel()
    {
        BackColor = AppTheme.CardBackground;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Padding = new Padding(16);
        Resize += (_, _) => UpdateRegion();
    }

    public int CornerRadius { get; set; } = 14;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width <= 1 || Height <= 1) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedPath(
            new Rectangle(0, 0, Width - 1, Height - 1),
            CornerRadius);
        using var pen = new Pen(AppTheme.Border);
        e.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        if (Width <= 1 || Height <= 1) return;

        using var path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), CornerRadius);
        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
