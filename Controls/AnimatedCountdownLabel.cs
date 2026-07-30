using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace HealthReminder.Controls;

/// <summary>
/// 大字倒數標籤。每個字元佔用固定寬度的格子（數字一律使用最寬數字的寬度），
/// 因此秒數變動時不會因為字寬不同而左右抖動；並且只有真正改變的字元會做
/// 交叉淡入淡出，未變動的字元保持完全靜止，看起來才會滑順。
/// </summary>
public sealed class AnimatedCountdownLabel : Control
{
    private const int DurationMs = 200;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _watch = new();
    private readonly Dictionary<char, float> _charWidths = new();
    private Font? _metricsFont;
    private float _digitWidth;
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
            // 以實際經過時間推算進度，即使 Timer 觸發不規律，動畫節奏仍然平順。
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
            else
            {
                _animating = false;
            }

            base.Text = incoming;
            Invalidate();
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _metricsFont = null;
        _charWidths.Clear();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var background = new SolidBrush(BackColor))
        {
            g.FillRectangle(background, ClientRectangle);
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        // 半透明文字要用灰階抗鋸齒，ClearType 次像素混色會出現彩邊。
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var current = base.Text;
        if (string.IsNullOrEmpty(current)) return;

        EnsureMetrics(g);

        // 字元位置與寬度只由文字內容決定，數字共用同一個寬度，因此逐秒更新時版面固定。
        var animating = _animating && _previousText.Length == current.Length;
        var progress = animating
            ? Math.Clamp(_watch.Elapsed.TotalMilliseconds / DurationMs, 0, 1)
            : 1;

        // 舊字先快速淡出、新字稍後淡入，減少兩者同時半透明造成的疊影。
        var fadeOut = 1 - Math.Clamp(progress * 1.6, 0, 1);
        var fadeIn = Math.Clamp((progress - 0.25) / 0.75, 0, 1);
        var shift = Height * 0.16f;

        var x = 0f;
        for (var index = 0; index < current.Length; index++)
        {
            var character = current[index];
            var cellWidth = WidthOf(character);
            var cell = new RectangleF(x, 0, cellWidth, Height);

            var changed = animating && _previousText[index] != character;
            if (!changed)
            {
                DrawCharacter(g, character, cell, 0, 1f);
            }
            else
            {
                // 逐格裁切，滑動的字元不會溢出到相鄰格子或控制項外。
                var clip = g.Clip;
                g.SetClip(cell);
                DrawCharacter(g, _previousText[index], cell, -(float)(progress * shift), (float)fadeOut);
                DrawCharacter(g, character, cell, (float)((1 - progress) * shift), (float)fadeIn);
                g.Clip = clip;
            }

            x += cellWidth;
        }
    }

    private void DrawCharacter(Graphics g, char character, RectangleF cell, float offsetY, float alpha)
    {
        if (alpha <= 0.01f) return;

        using var brush = new SolidBrush(
            Color.FromArgb((int)(Math.Clamp(alpha, 0f, 1f) * 255), ForeColor));
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };

        var target = new RectangleF(cell.X, cell.Y + offsetY, cell.Width, cell.Height);
        g.DrawString(character.ToString(), Font, brush, target, format);
    }

    /// <summary>
    /// 量測並快取字元寬度；所有數字統一採用最寬數字的寬度以避免版面跳動。
    /// </summary>
    private void EnsureMetrics(Graphics g)
    {
        if (ReferenceEquals(_metricsFont, Font) && _digitWidth > 0) return;

        _charWidths.Clear();
        _digitWidth = 0;
        for (var digit = '0'; digit <= '9'; digit++)
        {
            _digitWidth = Math.Max(_digitWidth, Measure(g, digit));
        }

        _metricsFont = Font;
    }

    private float WidthOf(char character)
    {
        if (char.IsDigit(character)) return _digitWidth;
        if (_charWidths.TryGetValue(character, out var cached)) return cached;

        using var graphics = CreateGraphics();
        var width = Measure(graphics, character);
        _charWidths[character] = width;
        return width;
    }

    private float Measure(Graphics g, char character)
    {
        var size = g.MeasureString(
            character.ToString(),
            Font,
            PointF.Empty,
            StringFormat.GenericTypographic);

        // 標點（例如冒號）過窄時給一點呼吸空間，整體節奏比較舒服。
        return Math.Max(size.Width, char.IsDigit(character) ? 0 : Font.Size * 0.42f);
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
