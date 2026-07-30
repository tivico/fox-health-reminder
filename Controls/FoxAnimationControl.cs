using System.Drawing.Drawing2D;

namespace HealthReminder.Controls;

public enum FoxAnimationState
{
    RollIn,
    Wave,
    Talk,
    Wait,
    Drink,
    EyeRest,
    Stretch,
    Celebrate,
    RollOut
}

/// <summary>
/// 優先使用每格 512×512 的單排 Sprite Sheet；缺少動作時依序退回橘狐待機與舊版 4×2 圖資。
/// </summary>
public sealed class FoxAnimationControl : Control
{
    private const int SingleRowFrameSize = 512;
    private const string ResourcePrefix = "HealthReminder.Resources.";

    // fox-sprites-v3 的格子：0=端坐 1=眨眼 2=張嘴 3=揮手 4=大笑 5=跳躍 6=伸懶腰 7=蜷睡
    private static readonly IReadOnlyDictionary<FoxAnimationState, int[]> LegacyFrameSequences =
        new Dictionary<FoxAnimationState, int[]>
        {
            [FoxAnimationState.RollIn] = new[] { 6, 0, 0 },        // 伸懶腰登場 → 坐下
            [FoxAnimationState.Wave] = new[] { 3, 0, 3, 0 },       // 揮手 ↔ 坐
            [FoxAnimationState.Talk] = new[] { 2, 0, 2, 0 },       // 張嘴 ↔ 閉嘴
            [FoxAnimationState.Wait] = new[] { 0, 0, 0, 1 },       // 端坐、偶爾眨眼
            [FoxAnimationState.Drink] = new[] { 2, 0, 2, 0 },      // 新圖資缺失時以說話動作代替
            [FoxAnimationState.EyeRest] = new[] { 0, 1, 1, 0 },    // 新圖資缺失時以眨眼動作代替
            [FoxAnimationState.Stretch] = new[] { 6, 0, 6, 0 },    // 新圖資缺失時沿用伸懶腰姿勢
            [FoxAnimationState.Celebrate] = new[] { 5, 4, 5, 4 },  // 跳躍 ↔ 大笑
            [FoxAnimationState.RollOut] = new[] { 0, 1, 7 }        // 坐 → 眨 → 蜷睡
        };

    private static readonly IReadOnlyDictionary<FoxAnimationState, AnimationDefinition> AnimationDefinitions =
        new Dictionary<FoxAnimationState, AnimationDefinition>
        {
            [FoxAnimationState.RollIn] = new(
                "fox_idle.png",
                400,
                new[] { 0, 0, 0, 1, 1, 1 }),
            [FoxAnimationState.Wave] = new(
                "fox_wave.png",
                100,
                new[] { 0, 1, 2, 3, 2, 1, 0 }),
            [FoxAnimationState.Talk] = new("fox_talk.png", 120),
            [FoxAnimationState.Wait] = new(
                "fox_idle.png",
                400,
                new[] { 0, 0, 0, 1, 1, 1 }),
            [FoxAnimationState.Drink] = new(
                "fox_drink.png",
                120,
                new[] { 0, 1, 2, 3, 3, 3, 2, 1, 0 }),
            [FoxAnimationState.EyeRest] = new(
                "fox_eye_rest.png",
                140,
                new[] { 0, 1, 2, 3, 3, 3, 2, 1, 0 }),
            [FoxAnimationState.Stretch] = new(
                "fox_stretch.png",
                160,
                new[] { 0, 1, 2, 3, 4, 4, 4, 3, 2, 1, 0 }),
            [FoxAnimationState.Celebrate] = new("fox_celebrate.png", 100),
            [FoxAnimationState.RollOut] = new("fox_sleep.png", 140)
        };

    private readonly System.Windows.Forms.Timer _frameTimer;
    private readonly Bitmap? _legacySpriteSheet;
    private readonly Dictionary<string, SpriteSheetClip> _clipCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingClipResources = new(StringComparer.Ordinal);
    private SpriteSheetClip? _activeClip;
    private int[]? _activeFrameSequence;
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
        _legacySpriteSheet = LoadEmbeddedBitmap($"{ResourcePrefix}fox-sprites-v3.png");
        _activeClip = GetOrLoadSingleRowClip(_state);
        _activeFrameSequence = AnimationDefinitions[_state].FrameSequence;
        _frameTimer = new System.Windows.Forms.Timer { Interval = GetFrameInterval(_state) };
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
                _sequenceIndex = 0;
                Invalidate();
            }
        }
    }

    public void Play(FoxAnimationState state)
    {
        if (!_frameTimer.Enabled)
        {
            ApplyState(state, forceRestart: true);
            return;
        }

        ApplyState(state);
    }

    public void Restart(FoxAnimationState state)
    {
        if (!_frameTimer.Enabled)
        {
            _frameTimer.Start();
        }

        ApplyState(state, forceRestart: true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_activeClip != null)
        {
            var requestedFrame = _activeFrameSequence == null
                ? _sequenceIndex
                : _activeFrameSequence[Math.Min(_sequenceIndex, _activeFrameSequence.Length - 1)];
            var activeFrame = Math.Clamp(requestedFrame, 0, _activeClip.FrameCount - 1);
            var activeSource = new Rectangle(
                activeFrame * SingleRowFrameSize,
                0,
                SingleRowFrameSize,
                SingleRowFrameSize);
            DrawFrame(e.Graphics, _activeClip.Bitmap, activeSource);
            return;
        }

        if (_legacySpriteSheet == null)
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

        var sequence = LegacyFrameSequences[_state];
        var frame = sequence[Math.Min(_sequenceIndex, sequence.Length - 1)];
        var cellWidth = _legacySpriteSheet.Width / 4;
        var cellHeight = _legacySpriteSheet.Height / 2;
        var source = new Rectangle(
            (frame % 4) * cellWidth,
            (frame / 4) * cellHeight,
            cellWidth,
            cellHeight);

        DrawFrame(e.Graphics, _legacySpriteSheet, source);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frameTimer.Stop();
            _frameTimer.Dispose();
            foreach (var clip in _clipCache.Values)
            {
                clip.Bitmap.Dispose();
            }
            _clipCache.Clear();
            _legacySpriteSheet?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void AdvanceFrame()
    {
        var frameCount = _activeFrameSequence?.Length
            ?? _activeClip?.FrameCount
            ?? LegacyFrameSequences[_state].Length;
        _sequenceIndex = (_sequenceIndex + 1) % frameCount;
        Invalidate();
    }

    private void ApplyState(FoxAnimationState state, bool forceRestart = false)
    {
        var previousClip = _activeClip;
        var previousSequence = _activeFrameSequence;
        _state = state;
        var nextClip = GetOrLoadSingleRowClip(state);
        var intervalState = state;
        if (nextClip == null && state != FoxAnimationState.Wait)
        {
            nextClip = GetOrLoadSingleRowClip(FoxAnimationState.Wait);
            if (nextClip != null)
            {
                intervalState = FoxAnimationState.Wait;
            }
        }
        var nextSequence = nextClip == null
            ? null
            : AnimationDefinitions[intervalState].FrameSequence;

        // 多個尚未提供的動作會共用橘狐待機圖；此時保持目前影格，
        // 避免提醒流程切換狀態時反覆從第 1 格重新開始。
        if (forceRestart ||
            nextClip == null ||
            !ReferenceEquals(previousClip, nextClip) ||
            !ReferenceEquals(previousSequence, nextSequence))
        {
            _sequenceIndex = 0;
        }

        _activeClip = nextClip;
        _activeFrameSequence = nextSequence;
        _frameTimer.Interval = GetFrameInterval(intervalState);
        Invalidate();
    }

    private SpriteSheetClip? GetOrLoadSingleRowClip(FoxAnimationState state)
    {
        var definition = AnimationDefinitions[state];
        var resourceName = $"{ResourcePrefix}{definition.ResourceFileName}";

        if (_clipCache.TryGetValue(resourceName, out var cached))
        {
            return cached;
        }
        if (_missingClipResources.Contains(resourceName))
        {
            return null;
        }

        var bitmap = LoadEmbeddedBitmap(resourceName);
        if (bitmap == null)
        {
            _missingClipResources.Add(resourceName);
            return null;
        }

        var frameCount = GetSingleRowFrameCount(bitmap.Width, bitmap.Height);
        if (frameCount == 0)
        {
            bitmap.Dispose();
            _missingClipResources.Add(resourceName);
            return null;
        }

        var clip = new SpriteSheetClip(bitmap, frameCount);
        _clipCache.Add(resourceName, clip);
        return clip;
    }

    private static Bitmap? LoadEmbeddedBitmap(string resourceName)
    {
        try
        {
            using var stream = typeof(FoxAnimationControl).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            using var image = new Bitmap(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    private void DrawFrame(Graphics graphics, Bitmap spriteSheet, Rectangle source)
    {
        var destination = FitRectangle(ClientRectangle, source.Width, source.Height);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.DrawImage(spriteSheet, destination, source, GraphicsUnit.Pixel);
    }

    internal static int GetSingleRowFrameCount(int sheetWidth, int sheetHeight)
    {
        if (sheetHeight != SingleRowFrameSize ||
            sheetWidth < SingleRowFrameSize ||
            sheetWidth % SingleRowFrameSize != 0)
        {
            return 0;
        }

        return sheetWidth / SingleRowFrameSize;
    }

    internal static int GetFrameInterval(FoxAnimationState state)
    {
        return AnimationDefinitions[state].FrameIntervalMs;
    }

    internal static int GetFrameSequenceLength(FoxAnimationState state)
    {
        return AnimationDefinitions[state].FrameSequence?.Length ?? 0;
    }

    internal static int GetCycleDurationMs(FoxAnimationState state)
    {
        var definition = AnimationDefinitions[state];
        return definition.FrameSequence == null
            ? 0
            : definition.FrameIntervalMs * definition.FrameSequence.Length;
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

    private sealed record AnimationDefinition(
        string ResourceFileName,
        int FrameIntervalMs,
        int[]? FrameSequence = null);

    private sealed record SpriteSheetClip(Bitmap Bitmap, int FrameCount);
}
