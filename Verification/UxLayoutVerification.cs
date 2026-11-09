using System.Reflection;
using System.ComponentModel;
using HealthReminder;
using HealthReminder.Controls;
using HealthReminder.Models;
using HealthReminder.Services;
using HealthReminder.Forms;

// 僅在 --ux-check 時啟用：跳過正式進入點，以透明測試視窗與獨立暫存資料驗收。
internal static class UxLayoutVerification
{
    public static int Run(string[] args)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { Check(args); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure == null) return 0;
        Console.WriteLine(failure);
        return 1;
    }

    private static void Check(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        var folder = Path.Combine(Path.GetTempPath(), "HealthReminder-ux-" + Guid.NewGuid().ToString("N"));
        SettingsManager.ConfigureStorage(new StorageLocationSelection(folder, true));
        var output = args.FirstOrDefault(a => a.StartsWith("--output="))?[9..];
        try
        {
            var manager = SettingsManager.Instance;
            manager.Settings.StartMinimized = false;
            manager.Settings.ReduceMotion = true;
            using var form = new MainForm();
            try
            {
                // 跳過正式 Load（歡迎訊息、系統匣與啟動導覽），以透明視窗完成真實 WinForms 排版。
                var load = typeof(MainForm).GetMethod("MainForm_Load", BindingFlags.Instance | BindingFlags.NonPublic)!;
                form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, load);
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.Show();
                Call(form, "LoadSettingsToUI");
                var service = Field<ReminderService>(form, "_reminderService");
                service.Initialize();
                Call(form, "UpdateCountdownDisplay");
                Call(form, "UpdateStatisticsDisplay");
                // DrawToBitmap 擷取真實控制項，不觸發正式啟動流程。
                foreach (var width in new[] { 820, 920 })
                {
                    form.Size = new Size(width, width == 820 ? 600 : 640);
                    foreach (var (page, navigation) in new[] {
                        ("panelStatus", "_todayNavigationButton"),
                        ("panelSettings", "_settingsNavigationButton"),
                        ("panelStatistics", "_statisticsNavigationButton") })
                    {
                        Call(form, "ShowSection", Field<Control>(form, page), Field<Button>(form, navigation));
                        form.PerformLayout();
                        using var bitmap = new Bitmap(form.Width, form.Height);
                        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                        if (output != null)
                        {
                            Directory.CreateDirectory(output);
                            bitmap.Save(Path.Combine(output, $"{page}-{width}.png"));
                        }
                        CheckVisibleLayout(Field<Control>(form, page));
                    }
                }

                var today = manager.GetTodayStatistics();
                if (!manager.Statistics.Contains(today)) manager.Statistics.Add(today);
                foreach (var item in manager.Settings.Reminders)
                    for (var i = 0; i < 3; i++)
                        today.Records.Add(new ReminderRecord
                        {
                            ReminderId = item.Id, Name = item.Name, Type = item.Type,
                            Timestamp = DateTime.Today.AddHours(10),
                            Status = i < 2 ? ReminderStatus.Completed : ReminderStatus.Dismissed
                        });
                Call(form, "UpdateStatisticsDisplay");
                // 正式預設大小以 ClientSize 計算，另保留上面的最小外框大小驗收。
                form.ClientSize = new Size(920, 640);
                foreach (var (page, navigation) in new[] {
                    ("panelStatus", "_todayNavigationButton"),
                    ("panelSettings", "_settingsNavigationButton"),
                    ("panelStatistics", "_statisticsNavigationButton") })
                {
                    Call(form, "ShowSection", Field<Control>(form, page), Field<Button>(form, navigation));
                    Capture(form, output, page + "-default");
                    CheckVisibleLayout(Field<Control>(form, page));
                }
                // 舊 ID 與現行 ID 同名的實際回歸：資料不合併，畫面必須清楚標示歷史。
                foreach (var item in manager.Settings.Reminders)
                    today.Records.Add(new ReminderRecord
                    {
                        ReminderId = "history-" + item.Id, Name = item.Name, Type = item.Type,
                        Timestamp = DateTime.Today.AddHours(9), Status = ReminderStatus.Missed
                    });
                Call(form, "ShowSection", Field<Control>(form, "panelStatistics"), Field<Button>(form, "_statisticsNavigationButton"));
                Call(form, "UpdateStatisticsDisplay");
                var historicalNames = Descendants(Field<Control>(form, "panelStatistics"))
                    .OfType<Label>().Count(label => label.Text.Contains("（歷史項目）"));
                Require(historicalNames == 3, "統計畫面必須標示三個舊 ID 的歷史項目。");
                foreach (var size in new[] { new Size(1720, 940), new Size(820, 600), new Size(1720, 940) })
                {
                    form.Size = size;
                    Capture(form, output, size.Width > 1000 ? "statistics-history-wide" : "statistics-history-small");
                    CheckVisibleLayout(Field<Control>(form, "panelStatistics"));
                }
                form.Size = new Size(820, 600);
                CheckVisibleLayout(Field<Control>(form, "panelStatistics"));
                using (var editor = new ReminderEditorForm(manager.Settings.Reminders[0]))
                {
                    editor.Opacity = 0;
                    editor.ShowInTaskbar = false;
                    editor.Show();
                    Capture(editor, output, "reminder-editor");
                    CheckVisibleLayout(editor);
                    editor.Close();
                }
                VerifyOneTimeEditorSchedule();
                foreach (var action in new[] { ReminderActionKind.Manual, ReminderActionKind.TimedActivity })
                {
                    var request = new ReminderPopupRequest(new ReminderItem
                    {
                        Name = action == ReminderActionKind.Manual ? "喝水提醒" : "眼睛休息",
                        Message = action == ReminderActionKind.Manual ? "喝幾口水，補充水分。" : "看向遠方，讓眼睛休息一下。",
                        ActionKind = action, ActivityDurationSeconds = 20, PlaySound = false
                    }, "__ux_preview__", 10, ReduceMotion: true, IsPreview: true);
                    using var popup = new ReminderPopupForm(request, Screen.PrimaryScreen!);
                    popup.TopMost = false;
                    popup.Show();
                    popup.Opacity = 0;
                    Capture(popup, output, "notification-" + action);
                    CheckVisibleLayout(popup);
                    if (action == ReminderActionKind.TimedActivity)
                    {
                        Field<Button>(popup, "_primaryButton").PerformClick();
                        Require(!Field<Button>(popup, "_primaryButton").Enabled
                            && !Field<Button>(popup, "_snoozeButton").Enabled,
                            "計時開始後主要操作與稍後應停用。");
                        Capture(popup, output, "notification-active");
                        CheckVisibleLayout(popup);
                    }
                    popup.ForceClose();
                }

                var settingsPage = Field<Control>(form, "panelSettings");
                Call(form, "ShowSection", settingsPage, Field<Button>(form, "_settingsNavigationButton"));
                var scroll = (Panel)settingsPage.Controls.Find("settingsScrollContent", true).Single();
                var preview = Descendants(settingsPage).OfType<Button>().Single(b => b.Text == "預覽狐狸動畫");
                scroll.ScrollControlIntoView(preview);
                CheckVisibleLayout(settingsPage);
                if (output != null)
                {
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(Path.Combine(output, "settings-bottom.png"));
                }
                var drafts = Field<BindingList<ReminderItem>>(form, "_reminderItems");
                drafts.Clear();
                Call(form, "RefreshReminderCards");
                CheckVisibleLayout(settingsPage);
                for (var i = 0; i < 8; i++)
                    drafts.Add(new ReminderItem { Name = "長名稱提醒測試，確認名稱與操作不互相遮蓋 " + i });
                Call(form, "RefreshReminderCards");
                CheckVisibleLayout(settingsPage);
                Require(!Field<FlowLayoutPanel>(form, "_reminderCardsFlow").AutoScroll, "提醒清單不應有第二層捲軸。");
                Require(!scroll.HorizontalScroll.Visible, "長清單不應撐出水平捲軸。");

                service.PauseAll();
                service.PauseFor(TimeSpan.FromMinutes(30));
                // 勿擾跨午夜，建立必定包含目前時間、起訖不同的測試時段。
                manager.Settings.DoNotDisturbEnabled = true;
                manager.Settings.DoNotDisturbStart = DateTime.Now.AddHours(-1).TimeOfDay;
                manager.Settings.DoNotDisturbEnd = DateTime.Now.AddHours(1).TimeOfDay;
                service.RecalibrateAfterSystemChange();
                Call(form, "UpdateCountdownDisplay");
                var status = Field<Label>(form, "lblPauseStatus").Text;
                Require(status.Contains("手動暫停") && status.Contains("限時暫停") && status.Contains("勿擾"),
                    "暫停狀態遺漏仍在生效的原因。" );
                Call(form, "ResumeTemporaryPause");
                status = Field<Label>(form, "lblPauseStatus").Text;
                Require(status.Contains("手動暫停") && status.Contains("勿擾") && !status.Contains("限時暫停"),
                    "取消限時暫停後顯示的剩餘原因不正確。");
                Require(!Field<Button>(form, "_resumeTemporaryPauseButton").Enabled, "沒有限時暫停時按鈕應停用。");
                Console.WriteLine("PASS  三頁最小/預設版面、有無統計、0/8 筆提醒、複合暫停、編輯器及通知待機/計時狀態（目前螢幕 DPI）");
            }
            finally
            {
                Call(form, "MainForm_FormClosing", form, new FormClosingEventArgs(CloseReason.ApplicationExitCall, false));
            }
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private static void VerifyOneTimeEditorSchedule()
    {
        var originalTime = DateTime.Now.AddHours(1);
        var item = new ReminderItem
        {
            Name = "一次性提醒", Message = "測試訊息", ScheduleKind = ReminderScheduleKind.OneTime,
            OneTimeAtLocal = originalTime
        };
        using (var editor = new ReminderEditorForm(item) { Opacity = 0 })
        {
            editor.Show();
            Field<TextBox>(editor, "_nameTextBox").Text = "改名";
            item.IsEnabled = false;
            item.OneTimeAtLocal = originalTime.AddMinutes(10);
            editor.AcceptButton!.PerformClick();
            Require(editor.DialogResult == DialogResult.OK && editor.ResultItem.Name == "改名"
                && !editor.ResultItem.IsEnabled && editor.ResultItem.OneTimeAtLocal == item.OneTimeAtLocal,
                "編輯中的提醒應採用主畫面已同步的一次性排程，並保留名稱修改。");
        }

        using var rescheduleEditor = new ReminderEditorForm(item) { Opacity = 0 };
        rescheduleEditor.Show();
        var chosenTime = originalTime.AddHours(2);
        Field<DateTimePicker>(rescheduleEditor, "_oneTimeInput").Value = chosenTime;
        item.OneTimeAtLocal = originalTime.AddMinutes(20);
        rescheduleEditor.AcceptButton!.PerformClick();
        Require(rescheduleEditor.DialogResult == DialogResult.OK && rescheduleEditor.ResultItem.OneTimeAtLocal == chosenTime,
            "使用者明確指定的新時間應優先於背景更新。");
    }

    private static IEnumerable<Control> Descendants(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));

    private static void Capture(Form form, string? output, string name)
    {
        form.PerformLayout();
        using var bitmap = new Bitmap(form.Width, form.Height);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(HealthReminder.UI.AppTheme.WindowBackground);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        if (output != null)
        {
            Directory.CreateDirectory(output);
            bitmap.Save(Path.Combine(output, name + ".png"));
        }
    }

    private static void CheckVisibleLayout(Control parent)
    {
        var children = parent.Controls.Cast<Control>().Where(c => c.Visible).ToArray();
        // 捲動容器允許內容超出 viewport；非捲動容器的操作必須留在邊界內。
        if (parent is not ScrollableControl { AutoScroll: true })
            foreach (var child in children)
                Require(parent.ClientRectangle.Contains(child.Bounds),
                    $"{parent.GetType().Name} 內的 {child.GetType().Name}「{child.Text}」超出邊界：{child.Bounds} / {parent.ClientRectangle}");
        foreach (var child in children) CheckVisibleLayout(child);
        // 特別檢查提醒卡片的文字與操作互相遮蓋問題。
        if (parent is ReminderCardControl or ReminderCountdownCard or StatRowCard)
            for (var i = 0; i < children.Length; i++)
                for (var j = i + 1; j < children.Length; j++)
                    Require(!children[i].Bounds.IntersectsWith(children[j].Bounds),
                        $"卡片內「{children[i].Text}」與「{children[j].Text}」重疊。");
    }

    private static T Field<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
            ?? throw new InvalidOperationException($"找不到欄位 {name}"));

    private static void Call(object target, string name, params object?[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
