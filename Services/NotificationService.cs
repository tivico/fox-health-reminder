using HealthReminder.Forms;

namespace HealthReminder.Services;

/// <summary>
/// 將多筆提醒排隊，確保同一時間只顯示一張自訂通知。
/// </summary>
public sealed class NotificationService : IDisposable
{
    private readonly Control _owner;
    private readonly Queue<ReminderPopupRequest> _queue = new();
    private ReminderPopupForm? _currentPopup;
    private bool _disposed;

    public NotificationService(Control owner)
    {
        _owner = owner;
    }

    public event EventHandler<ReminderPopupActionEventArgs>? ActionSelected;

    public void Enqueue(ReminderPopupRequest request)
    {
        if (_disposed) return;

        if (_owner.InvokeRequired)
        {
            _owner.BeginInvoke(() => Enqueue(request));
            return;
        }

        _queue.Enqueue(request);
        ShowNextIfNeeded();
    }

    private void ShowNextIfNeeded()
    {
        if (_disposed || _currentPopup != null || _queue.Count == 0) return;

        var request = _queue.Dequeue();
        var screen = _owner.IsHandleCreated
            ? Screen.FromHandle(_owner.Handle)
            : Screen.PrimaryScreen ?? Screen.AllScreens[0];
        _currentPopup = new ReminderPopupForm(request, screen);
        _currentPopup.ActionSelected += CurrentPopup_ActionSelected;
        _currentPopup.FormClosed += CurrentPopup_FormClosed;
        _currentPopup.Show();
    }

    private void CurrentPopup_ActionSelected(object? sender, ReminderPopupActionEventArgs e)
    {
        ActionSelected?.Invoke(this, e);
    }

    private void CurrentPopup_FormClosed(object? sender, FormClosedEventArgs e)
    {
        if (_currentPopup != null)
        {
            _currentPopup.ActionSelected -= CurrentPopup_ActionSelected;
            _currentPopup.FormClosed -= CurrentPopup_FormClosed;
            _currentPopup.Dispose();
            _currentPopup = null;
        }

        ShowNextIfNeeded();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _queue.Clear();
        if (_currentPopup != null)
        {
            _currentPopup.ActionSelected -= CurrentPopup_ActionSelected;
            _currentPopup.FormClosed -= CurrentPopup_FormClosed;
            _currentPopup.ForceClose();
            _currentPopup.Dispose();
            _currentPopup = null;
        }
    }
}
