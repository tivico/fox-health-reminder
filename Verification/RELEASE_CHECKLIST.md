# HealthReminder 發布與無障礙驗收

## 自動驗證

在專案根目錄執行：

```powershell
.\build-release.ps1
```

腳本會執行 Release 建置、Verification、單檔發布、安裝包編譯及 `verify-release.ps1`。自動驗證包含：

- 正式發布資料夾只包含一個 self-contained `HealthReminder.exe`。
- 單檔可從含中文及空白的路徑啟動內建 `--smoke-test`。
- `portable.flag` 位於可寫位置時使用 `Data`。
- `portable.flag` 位於不可寫位置時能偵測失敗，不會無聲改用 AppData。
- 安裝包、快速使用說明、版本資訊、SHA256 與 Authenticode 狀態。

報告輸出至：

```text
artifacts\verification\release-verification.txt
```

若目前電腦尚未安裝 HealthReminder 且程式沒有執行，可額外測試實際安裝、桌面／開始功能表捷徑、安裝後 smoke test 與完整解除安裝：

```powershell
.\Verification\verify-release.ps1 -InstallLifecycle
```

## Windows 10／11 乾淨環境

以下項目必須在 Windows Sandbox、虛擬機或另一台沒有安裝 .NET Runtime／SDK 的電腦執行，不能由開發機結果取代：

1. 將 `artifacts\installer` 複製到乾淨環境。
2. 確認系統沒有 `dotnet` 命令或已安裝的 .NET Desktop Runtime。
3. 雙擊 `狐狸健康提醒安裝程式.exe`。
4. 確認不要求系統管理員權限。
5. 確認桌面與開始功能表出現「小狐狸健康助手」。
6. 從捷徑啟動，確認主畫面直接顯示。
7. 開啟通知、完成／稍後／略過各一次，確認設定與統計可寫入。
8. 解除安裝後重新安裝，確認原設定與統計保留。

Windows Sandbox 可使用唯讀 mapped folder 並停用網路；設定方式參考 [Microsoft Windows Sandbox 文件](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file)。

## DPI 與多螢幕

程式已設定 `ApplicationHighDpiMode=PerMonitorV2`，所有主要 Form 使用 DPI AutoScale。仍需依序人工確認：

1. Windows 顯示縮放設為 125%、150%、200%，每次登出／登入或重啟程式。
2. 主畫面三個頁面不截字、不重疊，設定頁可完整捲動。
3. 新增／編輯提醒視窗按鈕完整可見。
4. 通知視窗三個按鈕、倒數文字及狐狸不超出邊界。
5. 多螢幕使用不同縮放比例，將主視窗移到各螢幕後觸發通知。
6. 通知應出現在主視窗所在螢幕的工作區右下角，不被工作列遮住。

Per-monitor DPI 相關說明參考 [Microsoft WinForms High DPI 文件](https://learn.microsoft.com/dotnet/desktop/winforms/high-dpi-support-in-windows-forms)。

## 鍵盤與螢幕閱讀器

先在「提醒與設定」勾選「通知出現時取得鍵盤焦點」，或啟用 Windows Narrator。計時通知可用 `HealthReminder.exe --show --preview-notification`；手動完成通知可用 `HealthReminder.exe --show --preview-notification=manual`（兩次測試之間先從系統匣完全結束程式）。

- Tab／Shift+Tab 依序移動「開始／完成 → 稍後 → 略過」。
- Enter 執行主要操作。
- Esc 或 Alt+K 略過。
- Alt+S 開始計時活動。
- Alt+C 完成手動提醒。
- Alt+L 稍後提醒。
- 活動開始後，「開始」及「稍後」呈 Disabled 狀態，焦點移到「停止並略過」。
- Focus 外框在三個按鈕上均清楚可見。
- Narrator 朗讀提醒名稱、訊息、活動狀態、按鈕名稱及 Disabled 狀態。

## SmartScreen 與程式碼簽章

目前安裝包未簽章，少量親友測試可透過可信任管道傳送，並搭配 `使用說明.txt` 與 SHA256；第一次下載仍可能出現 SmartScreen「Windows 已保護您的電腦」。

- 僅親友、小量測試：可暫不購買簽章，但必須清楚核對來源。
- 對外公開或頻繁更新：建議使用同一個受信任的程式碼簽章身分。
- 希望完全避免一般下載的 SmartScreen 警告：評估 Microsoft Store。

Microsoft 說明：未簽章檔案每次更新都必須重新累積檔案信譽；自簽憑證無法取代受信任簽章。[SmartScreen reputation](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation)
