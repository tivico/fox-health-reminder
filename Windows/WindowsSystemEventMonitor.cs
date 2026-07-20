using Microsoft.Win32;

namespace HealthReminder.Windows;

/// <summary>
/// 將 Windows 靜態系統事件包裝成可釋放的應用程式服務。
/// </summary>
public sealed class WindowsSystemEventMonitor : IDisposable
{
    private bool _disposed;

    public WindowsSystemEventMonitor()
    {
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        SystemEvents.TimeChanged += SystemEvents_TimeChanged;
    }

    public event EventHandler? SystemResumed;
    public event EventHandler? SystemTimeChanged;

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            SystemResumed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SystemEvents_TimeChanged(object? sender, EventArgs e)
    {
        SystemTimeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;

        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        SystemEvents.TimeChanged -= SystemEvents_TimeChanged;
        _disposed = true;
    }
}
