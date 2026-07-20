namespace HealthReminder.UI;

/// <summary>
/// 應用程式共用的視覺設計 Token，避免顏色與尺寸散落在表單事件中。
/// </summary>
public static class AppTheme
{
    public static Color WindowBackground { get; } = Color.FromArgb(255, 248, 242);
    public static Color CardBackground { get; } = Color.White;
    public static Color Primary { get; } = Color.FromArgb(244, 124, 72);
    public static Color PrimaryHover { get; } = Color.FromArgb(220, 103, 55);
    public static Color Healthy { get; } = Color.FromArgb(79, 157, 114);
    public static Color Warning { get; } = Color.FromArgb(216, 148, 36);
    public static Color Danger { get; } = Color.FromArgb(201, 88, 88);
    public static Color DangerHover { get; } = Color.FromArgb(176, 70, 70);
    public static Color PrimaryText { get; } = Color.FromArgb(61, 51, 47);
    public static Color SecondaryText { get; } = Color.FromArgb(117, 105, 99);
    public static Color Border { get; } = Color.FromArgb(234, 223, 216);
    public static Color SelectedBackground { get; } = Color.FromArgb(255, 232, 218);
    public static Color MutedButton { get; } = Color.FromArgb(139, 127, 120);
    public static Color MutedButtonHover { get; } = Color.FromArgb(112, 101, 95);

    public const string FontFamily = "Microsoft JhengHei UI";
    public const int CornerRadius = 10;
}
