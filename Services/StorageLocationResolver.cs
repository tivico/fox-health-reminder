namespace HealthReminder.Services;

internal sealed record StorageLocationSelection(
    string Folder,
    bool IsPortable,
    string? Notice = null);

internal sealed record StorageLocationProbeResult(
    bool PortableRequested,
    bool PortableAvailable,
    string PortableFolder,
    string AppDataFolder,
    string? FailureReason)
{
    public StorageLocationSelection SelectPreferred()
    {
        return PortableRequested && PortableAvailable
            ? new StorageLocationSelection(PortableFolder, true)
            : new StorageLocationSelection(AppDataFolder, false);
    }

    public StorageLocationSelection SelectAppDataFallback()
    {
        return new StorageLocationSelection(
            AppDataFolder,
            false,
            "偵測到 portable.flag，但程式所在資料夾無法寫入，已由使用者確認改用 AppData。");
    }
}

/// <summary>
/// 決定設定與統計資料的儲存位置，並在可攜模式啟用時先實際驗證寫入權限。
/// </summary>
internal static class StorageLocationResolver
{
    internal static StorageLocationProbeResult Probe(
        string executableDirectory,
        string appDataFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataFolder);

        var normalizedExecutableDirectory = Path.GetFullPath(executableDirectory);
        var normalizedAppDataFolder = Path.GetFullPath(appDataFolder);
        var portableFolder = Path.Combine(normalizedExecutableDirectory, "Data");
        var portableFlagPath = Path.Combine(normalizedExecutableDirectory, "portable.flag");

        if (!File.Exists(portableFlagPath))
        {
            return new StorageLocationProbeResult(
                false,
                false,
                portableFolder,
                normalizedAppDataFolder,
                null);
        }

        string? probePath = null;
        try
        {
            Directory.CreateDirectory(portableFolder);
            probePath = Path.Combine(
                portableFolder,
                $".healthreminder-write-test-{Guid.NewGuid():N}.tmp");

            using (var stream = new FileStream(
                       probePath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 1,
                       FileOptions.WriteThrough))
            {
                stream.WriteByte(0x2A);
                stream.Flush(true);
            }

            File.Delete(probePath);
            probePath = null;

            return new StorageLocationProbeResult(
                true,
                true,
                portableFolder,
                normalizedAppDataFolder,
                null);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or NotSupportedException
                                   or System.Security.SecurityException)
        {
            return new StorageLocationProbeResult(
                true,
                false,
                portableFolder,
                normalizedAppDataFolder,
                ex.Message);
        }
        finally
        {
            if (probePath != null && File.Exists(probePath))
            {
                try
                {
                    File.Delete(probePath);
                }
                catch
                {
                    // 權限測試失敗時不以清理例外蓋過原始原因。
                }
            }
        }
    }
}
