using System.Drawing.Drawing2D;
using HealthReminder.UI;

namespace HealthReminder.Controls;

/// <summary>
/// 圓角進度條，帶暖色漸層與平滑補間動畫，用來取代外觀老舊的原生 ProgressBar。
/// </summary>
public sealed class RoundedProgressBar : Control
{
    private readonly System.Windows.Forms.Timer _animationTimer;
    private int _value;
    private float _displayValue;
    private bool _animationsEnabled = true;

    public RoundedProgressBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        Height = 24;
        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animationTimer.Tick += (_, _) => AnimateStep();
    }

    public int Minimum { get; set; }
    public int Maximum { get; set; } = 100;
    public Color TrackColor { get; set; } = Color.FromArgb(239, 230, 223);
    public Color FillStart { get; set; } = AppTheme.Primary;
    public Color FillEnd { get; set; } = Color.FromArgb(250, 178, 96);

    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (_animationsEnabled == value) return;
            _animationsEnabled = value;
            if (!value)
            {
                _animationTimer.Stop();
                _displayValue = _value;
                Invalidate();
            }
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, Minimum, Maximum);
            if (_value == clamped) return;
            _value = clamped;
            if (!AnimationsEnabled || !IsHandleCreated)
            {
                _displayValue = clamped;
                Invalidate();
                return;
            }
            _animationTimer.Start();
        }
    }

    private void AnimateStep()
    {
        var target = (float)_value;
        var delta = target - _displayValue;
        if (Math.Abs(delta) < 0.5f)
        {
            _displayValue = target;
            _animationTimer.Stop();
        }
        else
        {
            _displayValue += delta * 0.18f; // ease-out：越接近目標越慢
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var backdrop = new SolidBrush(Parent?.BackColor ?? BackColor))
        {
            graphics.FillRectangle(backdrop, ClientRectangle);
        }

        if (Width <= 2 || Height <= 2) return;

        var radius = Height / 2;
        var track = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var trackPath = Rounded(track, radius))
        using (var trackBrush = new SolidBrush(TrackColor))
        {
            graphics.FillPath(trackBrush, trackPath);
        }

        var range = Math.Max(1, Maximum - Minimum);
        var ratio = Math.Clamp((_displayValue - Minimum) / range, 0f, 1f);
        var fillWidth = (int)Math.Round((Width - 2) * ratio);
        if (fillWidth <= 1) return;

        // 讓極小的進度也維持圓潤的膠囊外觀，不會擠成一條線
        fillWidth = Math.Clamp(fillWidth, Height - 2, Width - 2);
        var fill = new Rectangle(1, 1, fillWidth, Height - 3);
        using var fillPath = Rounded(fill, (Height - 3) / 2);
        using var fillBrush = new LinearGradientBrush(
            new Rectangle(0, 0, fillWidth + 2, Height),
            FillStart,
            FillEnd,
            LinearGradientMode.Horizontal);
        graphics.FillPath(fillBrush, fillPath);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Stop();
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
