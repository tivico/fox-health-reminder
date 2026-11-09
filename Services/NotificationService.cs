using HealthReminder.Forms;

namespace HealthReminder.Services;

/// <summary>
/// 將多筆提醒排隊，確保同一時間只顯示一張自訂通知。
/// </summary>
public sealed class NotificationService : IDisposable
{
    private readonly Control _owner;
    private readonly Func<bool> _canShow;
    private readonly Queue<ReminderPopupRequest> _queue = new();
    private ReminderPopupForm? _currentPopup;
    private ReminderPopupRequest? _currentRequest;
    private bool _currentPresented;
    private bool _disposed;

    public NotificationService(Control owner, Func<bool> canShow)
    {
        _owner = owner;
        _canShow = canShow;
    }

    public event EventHandler<ReminderPopupActionEventArgs>? ActionSelected;
    public event Action<ReminderPopupRequest>? Presented;
    public event Action<ReminderPopupRequest>? Closed;

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

    public void RemoveQueued(IReadOnlySet<string> reminderIds)
    {
        if (reminderIds.Count == 0) return;
        var count = _queue.Count;
        for (var index = 0; index < count; index++)
        {
            var request = _queue.Dequeue();
            if (!reminderIds.Contains(request.Item.Id)) _queue.Enqueue(request);
        }
        if (!_currentPresented && _currentRequest != null && reminderIds.Contains(_currentRequest.Item.Id))
        {
            _currentPopup?.ForceClose();
        }
    }

    public void ShowNextIfNeeded()
    {
        if (_disposed || _currentPopup != null || !_queue.TryPeek(out var next)
            || (!next.IsPreview && !_canShow())) return;

        var request = _queue.Dequeue();
        var screen = _owner.IsHandleCreated
            ? Screen.FromHandle(_owner.Handle)
            : Screen.PrimaryScreen ?? Screen.AllScreens[0];
        _currentPopup = new ReminderPopupForm(request, screen);
        _currentRequest = request;
        _currentPresented = false;
        _currentPopup.Presented += (_, _) =>
        {
            _currentPresented = true;
            Presented?.Invoke(request);
        };
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
        var request = _currentRequest;
        _currentRequest = null;
        if (_currentPopup != null)
        {
            _currentPopup.ActionSelected -= CurrentPopup_ActionSelected;
            _currentPopup.FormClosed -= CurrentPopup_FormClosed;
            _currentPopup.Dispose();
            _currentPopup = null;
        }

        if (request != null) Closed?.Invoke(request);
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
