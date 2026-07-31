using System.Runtime.InteropServices;

namespace HealthReminder.Windows;

internal static class WindowsAccessibility
{
    private const uint SpiGetScreenReader = 0x0046;

    internal static bool IsScreenReaderActive
    {
        get
        {
            try
            {
                return SystemParametersInfo(
                    SpiGetScreenReader,
                    0,
                    out var active,
                    0) && active;
            }
            catch
            {
                return false;
            }
        }
    }

    [DllImport("user32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        [MarshalAs(UnmanagedType.Bool)] out bool pvParam,
        uint fWinIni);
}
