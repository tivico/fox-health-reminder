using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace HealthReminder.Controls;

/// <summary>
/// 大字倒數標籤：文字變動時以「上滑＋淡入淡出」補間，讓每秒跳動更平滑生動。
/// 相容原本 Label 的用法（直接設定 Text）；ReduceMotion 時可關閉動畫。
/// </summary>
public sealed class AnimatedCountdownLabel : Control
{
    private const int DurationMs = 170;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _watch = new();
    private string _previousText = string.Empty;
    private bool _animating;

    public AnimatedCountdownLabel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += (_, _) =>
        {
            if (_watch.Elapsed.TotalMilliseconds >= DurationMs)
            {
                _animating = false;
                _watch.Stop();
                _timer.Stop();
            }
            Invalidate();
        };
    }

    public bool AnimationsEnabled { get; set; } = true;

    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            var incoming = value ?? string.Empty;
            if (incoming == base.Text) return;

            if (AnimationsEnabled && IsHandleCreated && !string.IsNullOrEmpty(base.Text))
            {
                _previousText = base.Text;
                _animating = true;
                _watch.Restart();
                _timer.Start();
            }

            base.Text = incoming;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        if (_animating)
        {
            var progress = Math.Clamp(_watch.Elapsed.TotalMilliseconds / DurationMs, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            var shift = (int)Math.Round(Height * 0.22);
            DrawText(g, _previousText, (int)Math.Round(-eased * shift), (float)(1 - eased));
            DrawText(g, base.Text, (int)Math.Round((1 - eased) * shift), (float)eased);
        }
        else
        {
            DrawText(g, base.Text, 0, 1f);
        }
    }

    private void DrawText(Graphics g, string text, int offsetY, float alpha)
    {
        if (string.IsNullOrEmpty(text) || alpha <= 0f) return;

        var value = Math.Clamp(alpha, 0f, 1f);
        using var brush = new SolidBrush(Color.FromArgb((int)(value * 255), ForeColor));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        g.DrawString(text, Font, brush, new RectangleF(0, offsetY, Width, Height), format);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }
}
