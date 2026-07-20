using System.Drawing.Drawing2D;

namespace HealthReminder.Controls;

public enum FoxAnimationState
{
    RollIn,
    Wave,
    Talk,
    Wait,
    Celebrate,
    RollOut
}

/// <summary>
/// 使用內嵌 4×2 Sprite Sheet 的像素狐狸動畫；圖資缺失時退回 Emoji。
/// </summary>
public sealed class FoxAnimationControl : Control
{
    private static readonly IReadOnlyDictionary<FoxAnimationState, int[]> FrameSequences =
        new Dictionary<FoxAnimationState, int[]>
        {
            [FoxAnimationState.RollIn] = new[] { 0, 1, 2 },
            [FoxAnimationState.Wave] = new[] { 2, 3, 2, 3 },
            [FoxAnimationState.Talk] = new[] { 3, 2, 3, 2 },
            [FoxAnimationState.Wait] = new[] { 4, 5, 4, 4 },
            [FoxAnimationState.Celebrate] = new[] { 6, 2, 6, 2 },
            [FoxAnimationState.RollOut] = new[] { 2, 1, 7 }
        };

    private readonly System.Windows.Forms.Timer _frameTimer;
    private readonly Bitmap? _spriteSheet;
    private FoxAnimationState _state = FoxAnimationState.Wait;
    private int _sequenceIndex;

    public FoxAnimationControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Size = new Size(104, 104);
        _spriteSheet = LoadSpriteSheet();
        _frameTimer = new System.Windows.Forms.Timer { Interval = 170 };
        _frameTimer.Tick += (_, _) => AdvanceFrame();
        _frameTimer.Start();
    }

    public FoxAnimationState State => _state;

    public bool AnimationEnabled
    {
        get => _frameTimer.Enabled;
        set
        {
            if (value)
            {
                _frameTimer.Start();
            }
            else
            {
                _frameTimer.Stop();
                _state = FoxAnimationState.Wait;
                _sequenceIndex = 0;
                Invalidate();
            }
        }
    }

    public void Play(FoxAnimationState state)
    {
        if (!_frameTimer.Enabled)
        {
            _state = FoxAnimationState.Wait;
            _sequenceIndex = 0;
            Invalidate();
            return;
        }
        _state = state;
        _sequenceIndex = 0;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_spriteSheet == null)
        {
            using var font = new Font("Segoe UI Emoji", Math.Max(24, Height * 0.48F));
            TextRenderer.DrawText(
                e.Graphics,
                "🦊",
                font,
                ClientRectangle,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var sequence = FrameSequences[_state];
        var frame = sequence[Math.Min(_sequenceIndex, sequence.Length - 1)];
        var cellWidth = _spriteSheet.Width / 4;
        var cellHeight = _spriteSheet.Height / 2;
        var source = new Rectangle(
            (frame % 4) * cellWidth,
            (frame / 4) * cellHeight,
            cellWidth,
            cellHeight);

        var destination = FitRectangle(ClientRectangle, cellWidth, cellHeight);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.CompositingMode = CompositingMode.SourceOver;
        e.Graphics.DrawImage(_spriteSheet, destination, source, GraphicsUnit.Pixel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frameTimer.Stop();
            _frameTimer.Dispose();
            _spriteSheet?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void AdvanceFrame()
    {
        var sequence = FrameSequences[_state];
        _sequenceIndex = (_sequenceIndex + 1) % sequence.Length;
        Invalidate();
    }

    private static Bitmap? LoadSpriteSheet()
    {
        try
        {
            using var stream = typeof(FoxAnimationControl).Assembly.GetManifestResourceStream(
                "HealthReminder.Resources.fox-sprites-v2.png");
            if (stream == null) return null;
            using var image = new Bitmap(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    private static Rectangle FitRectangle(Rectangle bounds, int sourceWidth, int sourceHeight)
    {
        var scale = Math.Min(
            bounds.Width / (float)sourceWidth,
            bounds.Height / (float)sourceHeight);
        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        return new Rectangle(
            bounds.Left + ((bounds.Width - width) / 2),
            bounds.Top + ((bounds.Height - height) / 2),
            width,
            height);
    }
}
