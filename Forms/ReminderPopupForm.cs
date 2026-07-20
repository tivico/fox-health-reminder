using System.Diagnostics;
using System.Drawing.Drawing2D;
using HealthReminder.Controls;
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
    int SnoozeMinutes,
    bool ReduceMotion = false);

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
    private readonly System.Windows.Forms.Timer _activityTimer;
    private readonly System.Windows.Forms.Timer _successTimer;
    private readonly System.Windows.Forms.Timer _foxDialogueTimer;
    private readonly Stopwatch _animationWatch = new();
    private FoxAnimationControl? _foxAnimation;
    private Label? _messageLabel;
    private Label? _activityStatusLabel;
    private ProgressBar? _activityProgress;
    private Button? _primaryButton;
    private Button? _snoozeButton;
    private Button? _dismissButton;
    private Rectangle _targetBounds;
    private int _animationStartY;
    private int _animationEndY;
    private bool _isClosing;
    private bool _actionRaised;
    private bool _suppressAction;
    private bool _activityStarted;
    private DateTimeOffset _activityEndsAtUtc;
    private int _foxDialogueStep;
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
        ClientSize = new Size(420, 242);
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
        _activityTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _activityTimer.Tick += ActivityTimer_Tick;
        _successTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _successTimer.Tick += (_, _) =>
        {
            _successTimer.Stop();
            BeginClose(ReminderPopupAction.Completed);
        };
        _foxDialogueTimer = new System.Windows.Forms.Timer { Interval = 700 };
        _foxDialogueTimer.Tick += (_, _) =>
        {
            if (_foxDialogueStep == 0)
            {
                _foxAnimation?.Play(FoxAnimationState.Talk);
                _foxDialogueStep = 1;
            }
            else
            {
                _foxAnimation?.Play(FoxAnimationState.Wait);
                _foxDialogueTimer.Stop();
            }
        };
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
        if (_request.ReduceMotion)
        {
            Bounds = _targetBounds;
            Opacity = 1;
            if (_foxAnimation != null) _foxAnimation.AnimationEnabled = false;
            _autoCloseTimer.Start();
            if (_request.Item.ActionKind == ReminderActionKind.TimedActivity && _request.Item.AutoStartActivity)
            {
                StartActivity();
            }
            return;
        }

        Location = new Point(_targetBounds.Left, _animationStartY);
        _foxAnimation?.Play(FoxAnimationState.RollIn);
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

        _foxAnimation = new FoxAnimationControl
        {
            Location = new Point(14, 24),
            Size = new Size(108, 116)
        };
        var title = new Label
        {
            AutoEllipsis = true,
            Location = new Point(130, 20),
            Size = new Size(262, 28),
            Font = new Font(AppTheme.FontFamily, 12F, FontStyle.Bold),
            ForeColor = AppTheme.PrimaryText,
            Text = $"{_request.Item.Emoji} {_request.Item.Name}"
        };
        _messageLabel = new Label
        {
            AutoEllipsis = true,
            Location = new Point(132, 52),
            Size = new Size(260, 50),
            Font = new Font(AppTheme.FontFamily, 9.5F),
            ForeColor = AppTheme.SecondaryText,
            Text = _request.Item.Message
        };

        _activityStatusLabel = new Label
        {
            AutoEllipsis = true,
            Location = new Point(132, 108),
            Size = new Size(260, 22),
            Font = new Font(AppTheme.FontFamily, 8.5F, FontStyle.Bold),
            ForeColor = AppTheme.Primary,
            Text = _request.Item.ActionKind == ReminderActionKind.TimedActivity
                ? $"準備好後開始 {_request.Item.ActivityDurationSeconds} 秒活動"
                : "完成後告訴小狐狸一聲吧"
        };
        _activityProgress = new ProgressBar
        {
            Location = new Point(132, 134),
            Size = new Size(260, 12),
            Minimum = 0,
            Maximum = Math.Max(1, _request.Item.ActivityDurationSeconds * 10),
            Style = ProgressBarStyle.Continuous,
            Visible = _request.Item.ActionKind == ReminderActionKind.TimedActivity
        };

        _primaryButton = CreateActionButton(
            _request.Item.ActionKind == ReminderActionKind.TimedActivity
                ? $"開始 {_request.Item.ActivityDurationSeconds} 秒"
                : "完成",
            AppTheme.Healthy,
            Color.White,
            (_, _) =>
            {
                if (_request.Item.ActionKind == ReminderActionKind.TimedActivity)
                {
                    StartActivity();
                }
                else
                {
                    CompleteManualAction();
                }
            });
        _primaryButton.Location = new Point(24, 178);
        _primaryButton.Size = new Size(116, 40);

        _snoozeButton = CreateActionButton(
            $"稍後 {_request.SnoozeMinutes} 分",
            AppTheme.SelectedBackground,
            AppTheme.Primary,
            (_, _) => BeginClose(ReminderPopupAction.Snoozed));
        _snoozeButton.Location = new Point(150, 178);
        _snoozeButton.Size = new Size(130, 40);

        _dismissButton = CreateActionButton(
            "略過",
            Color.FromArgb(244, 240, 237),
            AppTheme.SecondaryText,
            (_, _) => BeginClose(ReminderPopupAction.Dismissed));
        _dismissButton.Location = new Point(290, 178);
        _dismissButton.Size = new Size(102, 40);

        Controls.Add(accent);
        Controls.Add(_foxAnimation);
        Controls.Add(title);
        Controls.Add(_messageLabel);
        Controls.Add(_activityStatusLabel);
        Controls.Add(_activityProgress);
        Controls.Add(_primaryButton);
        Controls.Add(_snoozeButton);
        Controls.Add(_dismissButton);
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

    private void StartActivity()
    {
        if (_activityStarted || _isClosing || _request.Item.ActionKind != ReminderActionKind.TimedActivity)
        {
            return;
        }

        _activityStarted = true;
        var durationSeconds = Math.Clamp(_request.Item.ActivityDurationSeconds, 5, 3600);
        _activityEndsAtUtc = DateTimeOffset.UtcNow.AddSeconds(durationSeconds);
        _autoCloseTimer.Stop();
        _successTimer.Stop();
        _foxDialogueTimer.Stop();
        _foxAnimation?.Play(FoxAnimationState.Wait);

        if (_primaryButton != null)
        {
            _primaryButton.Text = "進行中…";
            _primaryButton.Enabled = false;
        }
        if (_snoozeButton != null)
        {
            _snoozeButton.Enabled = false;
        }
        if (_dismissButton != null)
        {
            _dismissButton.Text = "停止並略過";
        }

        UpdateActivityProgress();
        _activityTimer.Start();
    }

    private void CompleteManualAction()
    {
        if (_isClosing) return;

        _autoCloseTimer.Stop();
        _foxDialogueTimer.Stop();
        _foxAnimation?.Play(FoxAnimationState.Celebrate);
        if (_activityStatusLabel != null)
        {
            _activityStatusLabel.Text = "收到！這次任務已記錄完成";
            _activityStatusLabel.ForeColor = AppTheme.Healthy;
        }
        if (_primaryButton != null)
        {
            _primaryButton.Text = "✓ 已完成";
            _primaryButton.Enabled = false;
        }
        if (_snoozeButton != null) _snoozeButton.Enabled = false;
        if (_dismissButton != null) _dismissButton.Enabled = false;
        _successTimer.Start();
    }

    private void ActivityTimer_Tick(object? sender, EventArgs e)
    {
        UpdateActivityProgress();
    }

    private void UpdateActivityProgress()
    {
        if (!_activityStarted || _activityProgress == null || _activityStatusLabel == null) return;

        var durationSeconds = Math.Clamp(_request.Item.ActivityDurationSeconds, 5, 3600);
        var remaining = _activityEndsAtUtc - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            CompleteActivity();
            return;
        }

        var elapsedTenths = (int)Math.Round((durationSeconds - remaining.TotalSeconds) * 10);
        _activityProgress.Value = Math.Clamp(elapsedTenths, 0, _activityProgress.Maximum);
        _activityStatusLabel.Text = $"還有 {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))} 秒 · 放鬆眼睛與肩膀";
    }

    private void CompleteActivity()
    {
        if (!_activityStarted) return;

        _activityStarted = false;
        _activityTimer.Stop();
        _foxDialogueTimer.Stop();
        _foxAnimation?.Play(FoxAnimationState.Celebrate);
        if (_activityProgress != null)
        {
            _activityProgress.Value = _activityProgress.Maximum;
        }
        if (_activityStatusLabel != null)
        {
            _activityStatusLabel.Text = "完成！小狐狸替你拍拍手";
            _activityStatusLabel.ForeColor = AppTheme.Healthy;
        }
        if (_messageLabel != null)
        {
            _messageLabel.Text = "做得很好，這次活動已經記錄完成。";
        }
        if (_primaryButton != null)
        {
            _primaryButton.Text = "✓ 已完成";
        }
        if (_dismissButton != null)
        {
            _dismissButton.Enabled = false;
        }
        _successTimer.Start();
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
            _foxAnimation?.Play(FoxAnimationState.Wave);
            _foxDialogueStep = 0;
            _foxDialogueTimer.Start();
            _autoCloseTimer.Start();
            if (_request.Item.ActionKind == ReminderActionKind.TimedActivity && _request.Item.AutoStartActivity)
            {
                StartActivity();
            }
        }
    }

    private void BeginClose(ReminderPopupAction action)
    {
        if (_isClosing) return;

        _isClosing = true;
        _closingAction = action;
        _autoCloseTimer.Stop();
        _activityTimer.Stop();
        _successTimer.Stop();
        _foxDialogueTimer.Stop();
        if (_request.ReduceMotion)
        {
            RaiseActionAndClose();
            return;
        }
        _foxAnimation?.Play(FoxAnimationState.RollOut);
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
        _activityTimer.Dispose();
        _successTimer.Dispose();
        _foxDialogueTimer.Dispose();
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
