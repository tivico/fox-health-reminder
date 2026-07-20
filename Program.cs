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
    static void Main()
    {
        // 確保只有一個執行個體在運行
        bool createdNew;
        using var mutex = new Mutex(true, "HealthReminder_SingleInstance", out createdNew);
        
        if (!createdNew)
        {
            MessageBox.Show("健康提醒小幫手已經在執行中！\n請查看右下角系統托盤。", 
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
