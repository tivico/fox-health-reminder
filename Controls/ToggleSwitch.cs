using System.Drawing.Drawing2D;
using HealthReminder.UI;

namespace HealthReminder.Controls;

/// <summary>
/// 可用滑鼠及鍵盤操作的輕量開關。
/// </summary>
public sealed class ToggleSwitch : Control
{
    private bool _checked;

    public ToggleSwitch()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        Size = new Size(46, 26);
        TabStop = true;
        AccessibleRole = AccessibleRole.CheckButton;
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            AccessibleName = value ? "已開啟" : "已關閉";
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var trackBounds = new Rectangle(1, 3, Width - 2, Height - 6);
        using var trackPath = RoundedRectangle(trackBounds, trackBounds.Height / 2);
        using var trackBrush = new SolidBrush(
            Checked ? AppTheme.Healthy : Color.FromArgb(213, 205, 200));
        e.Graphics.FillPath(trackBrush, trackPath);

        var thumbSize = trackBounds.Height - 4;
        var thumbX = Checked
            ? trackBounds.Right - thumbSize - 2
            : trackBounds.Left + 2;
        using var thumbBrush = new SolidBrush(Color.White);
        e.Graphics.FillEllipse(
            thumbBrush,
            new Rectangle(thumbX, trackBounds.Top + 2, thumbSize, thumbSize));

        if (Focused)
        {
            using var focusPen = new Pen(AppTheme.Primary) { DashStyle = DashStyle.Dot };
            e.Graphics.DrawRectangle(focusPen, 0, 0, Width - 1, Height - 1);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 90, 180);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 180);
        path.CloseFigure();
        return path;
    }
}
