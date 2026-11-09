using Microsoft.Win32;

namespace HealthReminder.Windows;

internal static class WindowsStartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "HealthReminder";

    // 只有使用者明確調整開機自啟時才更新登錄，不在應用程式載入時改寫路徑。
    internal static string? Apply(bool enabled, string executable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(RunValue, $"\"{executable}\"");
            else key.DeleteValue(RunValue, throwOnMissingValue: false);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return $"無法更新開機自啟登錄項：{ex.Message}\n可再次按「儲存設定」重試。";
        }
    }
}
