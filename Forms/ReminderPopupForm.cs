using System.Diagnostics;
using System.Drawing.Drawing2D;
using HealthReminder.Models;
using HealthReminder.UI;

namespace HealthReminder.Forms;

public enum ReminderPopupAction
{
    Completed,
    Snoozed,
    Dismissed,
    TimedOut
}

public sealed record ReminderPopupRequest(
    ReminderItem Item,
    string OccurrenceId,
    int SnoozeMinutes);

public sealed class ReminderPopupActionEventArgs : EventArgs
{
    public ReminderPopupActionEventArgs(ReminderPopupRequest request, ReminderPopupAction action)
    {
        Request = request;
        Action = action;
    }

    public ReminderPopupRequest Request { get; }
    public ReminderPopupAction Action { get; }
}

/// <summary>
/// 不搶焦點、可操作並具有滑入淡入動畫的提醒視窗。
/// </summary>
public sealed class ReminderPopupForm : Form
{
    private const int AnimationDurationMs = 260;
    private readonly ReminderPopupRequest _request;
    private readonly Screen _screen;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly System.Windows.Forms.Timer _autoCloseTimer;
    private readonly Stopwatch _animationWatch = new();
    private Rectangle _targetBounds;
    private int _animationStartY;
    private int _animationEndY;
    private bool _isClosing;
    private bool _actionRaised;
    private bool _suppressAction;
    private ReminderPopupAction _closingAction;

    public ReminderPopupForm(ReminderPopupRequest request, Screen screen)
    {
        _request = request;
        _screen = screen;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AppTheme.CardBackground;
        ClientSize = new Size(390, 208);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Opacity = 0;

        BuildContent();

        _animationTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _animationTimer.Tick += AnimationTimer_Tick;
        _autoCloseTimer = new System.Windows.Forms.Timer { Interval = 12000 };
        _autoCloseTimer.Tick += (_, _) => BeginClose(ReminderPopupAction.TimedOut);
    }

    public event EventHandler<ReminderPopupActionEventArgs>? ActionSelected;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            var parameters = base.CreateParams;
            parameters.ExStyle |= wsExToolWindow;
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        var workArea = _screen.WorkingArea;
        var scale = DeviceDpi / 96f;
        var margin = (int)Math.Round(18 * scale);
        _targetBounds = new Rectangle(
            workArea.Right - Width - margin,
            workArea.Bottom - Height - margin,
            Width,
            Height);

        _animationStartY = workArea.Bottom + margin;
        _animationEndY = _targetBounds.Top;
        Location = new Point(_targetBounds.Left, _animationStartY);
        BeginAnimation();
    }

    private void BuildContent()
    {
        var accent = new Panel
        {
            BackColor = AppTheme.Primary,
            Dock = DockStyle.Left,
            Width = 6
        };

        var emoji = new Label
        {
            AutoSize = false,
            Location = new Point(24, 20),
            Size = new Size(48, 48),
            Font = new Font(AppTheme.FontFamily, 24F),
            Text = _request.Item.Emoji,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var title = new Label
        {
            AutoEllipsis = true,
            Location = new Point(82, 20),
            Size = new Size(278, 28),
            Font = new Font(AppTheme.FontFamily, 12F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = _request.Item.Name
        };
        var message = new Label
        {
            AutoEllipsis = true,
            Location = new Point(84, 52),
            Size = new Size(278, 48),
            Font = new Font(AppTheme.FontFamily, 9.5F),
            ForeColor = AppTheme.SecondaryText,
            Text = _request.Item.Message
        };

        var completeButton = CreateActionButton(
            "完成",
            AppTheme.Healthy,
            Color.White,
            (_, _) => BeginClose(ReminderPopupAction.Completed));
        completeButton.Location = new Point(24, 142);
        completeButton.Size = new Size(100, 40);

        var snoozeButton = CreateActionButton(
            $"稍後 {_request.SnoozeMinutes} 分",
            AppTheme.SelectedBackground,
            AppTheme.Primary,
            (_, _) => BeginClose(ReminderPopupAction.Snoozed));
        snoozeButton.Location = new Point(134, 142);
        snoozeButton.Size = new Size(128, 40);

        var dismissButton = CreateActionButton(
            "略過",
            Color.FromArgb(244, 240, 237),
            AppTheme.SecondaryText,
            (_, _) => BeginClose(ReminderPopupAction.Dismissed));
        dismissButton.Location = new Point(272, 142);
        dismissButton.Size = new Size(88, 40);

        Controls.Add(accent);
        Controls.Add(emoji);
        Controls.Add(title);
        Controls.Add(message);
        Controls.Add(completeButton);
        Controls.Add(snoozeButton);
        Controls.Add(dismissButton);
    }

    private static Button CreateActionButton(
        string text,
        Color background,
        Color foreground,
        EventHandler onClick)
    {
        var button = new Button
        {
            BackColor = background,
            ForeColor = foreground,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(AppTheme.FontFamily, 9F, FontStyle.Bold),
            Text = text,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += onClick;
        button.Resize += (_, _) => ApplyRoundedRegion(button, 9);
        return button;
    }

    private void BeginAnimation()
    {
        _animationWatch.Restart();
        _animationTimer.Start();
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        var progress = Math.Clamp(
            _animationWatch.Elapsed.TotalMilliseconds / AnimationDurationMs,
            0,
            1);
        var eased = 1 - Math.Pow(1 - progress, 3);

        Top = (int)Math.Round(_animationStartY + ((_animationEndY - _animationStartY) * eased));
        Opacity = _isClosing ? 1 - eased : eased;

        if (progress < 1) return;

        _animationTimer.Stop();
        _animationWatch.Stop();

        if (_isClosing)
        {
            RaiseActionAndClose();
        }
        else
        {
            Top = _targetBounds.Top;
            Opacity = 1;
            _autoCloseTimer.Start();
        }
    }

    private void BeginClose(ReminderPopupAction action)
    {
        if (_isClosing) return;

        _isClosing = true;
        _closingAction = action;
        _autoCloseTimer.Stop();
        _animationStartY = Top;
        _animationEndY = _targetBounds.Top + 36;
        BeginAnimation();
    }

    private void RaiseActionAndClose()
    {
        if (!_suppressAction && !_actionRaised)
        {
            _actionRaised = true;
            ActionSelected?.Invoke(
                this,
                new ReminderPopupActionEventArgs(_request, _closingAction));
        }

        Close();
    }

    public void ForceClose()
    {
        _suppressAction = true;
        _animationTimer.Stop();
        _autoCloseTimer.Stop();
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 16);
        using var border = new Pen(AppTheme.Border);
        e.Graphics.DrawPath(border, path);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width <= 1 || Height <= 1) return;

        using var path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), 16);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _animationTimer.Dispose();
        _autoCloseTimer.Dispose();
        base.OnFormClosed(e);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 1 || control.Height <= 1) return;

        using var path = CreateRoundedPath(
            new Rectangle(0, 0, control.Width, control.Height),
            radius);
        var oldRegion = control.Region;
        control.Region = new Region(path);
        oldRegion?.Dispose();
    }
}
