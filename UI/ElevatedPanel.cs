using System.Drawing.Drawing2D;

namespace HealthReminder.UI;

/// <summary>
/// 會在其 <see cref="CardPanel"/> 子控制項後方繪製柔和陰影的版面容器，帶來立體層次。
/// 陰影只畫在卡片之間與外緣的留白區；子控制項座標與大小完全不受影響。
/// </summary>
public sealed class ElevatedPanel : TableLayoutPanel
{
    public ElevatedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
    }

    /// <summary>陰影往下的位移，模擬由上方而來的光源。</summary>
    public int ShadowOffsetY { get; set; } = 4;

    /// <summary>陰影羽化層數，越大越柔和，也越占用卡片周圍的留白。</summary>
    public int ShadowSpread { get; set; } = 8;

    /// <summary>每一層陰影的不透明度，數值越大陰影越深。</summary>
    public int ShadowLayerAlpha { get; set; } = 9;

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        using (var background = new SolidBrush(BackColor))
        {
            graphics.FillRectangle(background, ClientRectangle);
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (Control control in Controls)
        {
            if (control is CardPanel card && card.Visible)
            {
                DrawCardShadow(graphics, card.Bounds, card.CornerRadius);
            }
        }

        base.OnPaint(e);
    }

    private void DrawCardShadow(Graphics graphics, Rectangle bounds, int radius)
    {
        for (var inset = ShadowSpread; inset >= 1; inset--)
        {
            var layer = new Rectangle(
                bounds.X - inset,
                bounds.Y - inset + ShadowOffsetY,
                bounds.Width + (inset * 2),
                bounds.Height + (inset * 2));

            using var path = CreateRoundedPath(layer, radius + inset);
            using var brush = new SolidBrush(Color.FromArgb(ShadowLayerAlpha, AppTheme.Shadow));
            graphics.FillPath(brush, path);
        }
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
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
