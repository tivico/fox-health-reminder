using HealthReminder.Services;

namespace HealthReminder;

/// <summary>
/// 程式進入點
/// </summary>
static class Program
{
    /// <summary>
    /// 應用程式的主要進入點
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HealthReminder");
        var storageProbe = StorageLocationResolver.Probe(
            AppContext.BaseDirectory,
            appDataFolder);

        if (args.Any(argument =>
                argument.Equals("--smoke-test", StringComparison.OrdinalIgnoreCase)))
        {
            return RunSmokeTest(storageProbe);
        }

        // 先確認單一執行個體，再載入或寫入任何使用者設定。
        bool createdNew;
        using var mutex = new Mutex(true, "HealthReminder_SingleInstance", out createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "健康提醒小幫手已經在執行中！\n請查看右下角系統托盤。",
                "提示",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }

        StorageLocationSelection storageSelection;
        if (storageProbe.PortableRequested && !storageProbe.PortableAvailable)
        {
            var result = MessageBox.Show(
                "偵測到 portable.flag，但程式所在資料夾無法寫入可攜資料。\n\n" +
                $"可攜資料夾：\n{storageProbe.PortableFolder}\n\n" +
                $"原因：{storageProbe.FailureReason}\n\n" +
                "是否改用目前 Windows 使用者的 AppData 儲存設定與健康紀錄？\n" +
                "選擇「否」將直接結束程式，不會無聲切換資料位置。",
                "可攜模式無法使用",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1);

            if (result != DialogResult.Yes)
            {
                return 2;
            }

            storageSelection = storageProbe.SelectAppDataFallback();
        }
        else
        {
            storageSelection = storageProbe.SelectPreferred();
        }

        try
        {
            SettingsManager.ConfigureStorage(storageSelection);
            _ = SettingsManager.Instance;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"無法準備資料儲存位置，程式將結束。\n\n{storageSelection.Folder}\n\n{ex.Message}",
                "無法開啟資料位置",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 3;
        }

        Application.Run(new MainForm());
        return 0;
    }

    private static int RunSmokeTest(StorageLocationProbeResult storageProbe)
    {
        try
        {
            if (Application.HighDpiMode != HighDpiMode.PerMonitorV2)
            {
                return 7;
            }

            if (storageProbe.PortableRequested && !storageProbe.PortableAvailable)
            {
                return 4;
            }

            var requiredResources = new[]
            {
                "HealthReminder.Resources.app.ico",
                "HealthReminder.Resources.fox_idle.png",
                "HealthReminder.Resources.fox_wave.png",
                "HealthReminder.Resources.fox_drink.png",
                "HealthReminder.Resources.fox_eye_rest.png",
                "HealthReminder.Resources.fox_stretch.png"
            };

            var assembly = typeof(Program).Assembly;
            foreach (var resourceName in requiredResources)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null || stream.Length == 0)
                {
                    return 5;
                }
            }

            return 0;
        }
        catch
        {
            return 6;
        }
    }
}
