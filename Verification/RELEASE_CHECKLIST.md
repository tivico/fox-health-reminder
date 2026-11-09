# HealthReminder 發布與無障礙驗收

## 自動驗證

在專案根目錄執行：

```powershell
.\build-release.ps1
```

腳本會在 `artifacts/build/release` 建置並執行 Verification，然後產生正式單檔、安裝包及執行 `verify-release.ps1`。這不會覆寫使用中的 `bin/Release` EXE；交付時應取用 `artifacts/publish/win-x64-single-file` 與 `artifacts/installer`。自動驗證包含：

- 正式發布資料夾只包含一個 self-contained `HealthReminder.exe`。
- 單檔可從含中文及空白的路徑啟動內建 `--smoke-test`。
- `portable.flag` 位於可寫位置時使用 `Data`。
- `portable.flag` 位於不可寫位置時能偵測失敗，不會無聲改用 AppData。
- 安裝包、快速使用說明、版本資訊、SHA256 與 Authenticode 狀態。

發布前會檢查輸出目錄。若要驗收可攜資料保護，請在隔離副本的發布目錄放入 `portable.flag` 與 `Data`，執行發布後確認腳本在清理前中止，原有成品和測試資料均可讀取。

報告輸出至：

```text
artifacts\verification\release-verification.txt
```

平時可單獨執行 `dotnet run --project Verification/HealthReminder.Verification.csproj -c Release`。資料復原、寫入重試、跨檔案提交與排程驗證使用隔離資料夾和可控制的時鐘。

## 提醒管理與資料回歸

用測試資料驗收下列情境。要和正式資料隔離，可將測試執行檔複製到獨立資料夾，再建立 `portable.flag`。

1. 建立兩筆提醒，記下第二筆剩餘時間。確認刪除第一筆後直接回首頁，第一筆倒數消失，第二筆延續原期限。刪除最後一筆、重啟，確認仍為空清單，統計保留。
   在設定頁捲動到最上與最下，確認固定操作列的儲存按鈕均完整可見。修改一般設定或提醒開關後，確認「有尚未儲存的變更」持續顯示，改回原值或儲存成功後清除。
2. 讓一張通知保持開啟，等另一筆到期排隊，再刪除排隊中的提醒。關閉第一張通知，確認第二張沒有顯示或新增統計。
3. 修改提醒名稱或「減少動畫」後儲存，確認倒數延續。將間隔設為 90 分鐘，檢查首頁、托盤和卡片都有小時，並用長名稱確認托盤更新正常。
4. 啟動計時通知，結束時確認先寫入 `Completed` 再顯示成功。用隔離資料阻擋 `statistics.json.tmp` 的寫入，確認失敗會顯示重試訊息，解除阻擋後在同一張通知重試成功。
5. 在獨立測試帳戶檢查開機自啟。從自用正式路徑啟用並儲存後，關閉並啟動開發版，確認登錄項仍指向先前選定的路徑。執行自用部署時，確認只替換目標路徑，並保留上一版 EXE。
6. 編輯即將到期的一次性提醒，只修改名稱。等通知顯示後更新草稿並儲存，確認提醒維持已停用。再測試視窗開啟期間按「稍後」，確認只改名稱仍保留延後的時間。若刻意在編輯器指定新時間，確認使用者指定的排程生效。
7. 通知顯示後變更原提醒的間隔、啟用狀態或排程方式，再按原通知的「稍後」，應提示排程已變更。若只改全域稍後分鐘數，原通知應依按鈕顯示的時間延後。

若目前電腦尚未安裝 HealthReminder 且程式沒有執行，可額外測試實際安裝、桌面／開始功能表捷徑、安裝後 smoke test 與完整解除安裝：

```powershell
.\Verification\verify-release.ps1 -InstallLifecycle
```

## 介面與操作語意回歸（2026-11-04）

自動檢查指令與資料隔離方式見 [README](../README.md#介面維護與排查)。`--ux-check` 會以目前螢幕 DPI 產生畫面，確認最小／預設三頁邊界與提醒、統計列重疊、設定頁底部、0／8 筆提醒、空白／有資料統計、複合暫停，以及編輯器與手動／計時通知。請再人工確認：

1. 新增或編輯提醒：按「加入草稿／更新草稿」後，原排程不變，底部出現未儲存提示；按「儲存設定」後才套用。刪除仍會在確認後立即生效。
2. 同時設定手動、限時與勿擾暫停：狀態列列出三種原因；按「取消限時暫停」後，其他兩個仍顯示並繼續暫停。沒有限時暫停時對應按鈕停用；首頁仍有 30 分鐘、1 小時與今天暫停按鈕。跨日限時暫停需顯示日期。
3. 設定頁只有整頁捲軸；新增多筆、刪到零筆、長名稱、長資料路徑、捲動到底部時，儲存按鈕可用且沒有水平捲軸。「預覽狐狸動畫」位於應用程式偏好。
4. 調整每日目標計算時段，時段外仍有提醒；健康紀錄主標題是全天完成次數，目標進度只計指定時段，兩者可能不同。
5. 確認三頁已恢復暖橘主題、卡片及 Emoji，保留前一輪操作改善；通知開始計時後，開始／稍後停用，停止並略過仍可操作。
6. 依下方 DPI 清單驗收 125%、150%、200%，尤其檢查最小視窗的快速暫停按鈕、長倒數與設定欄位換行。畫面擷取與邊界檢查不能代替實際滑鼠、鍵盤與跨螢幕操作。

## 歷史統計與縮放回歸

1. 用相同名稱、不同新舊 ID 建立測試紀錄，確認找不到設定的 ID 標成「歷史項目」，喝水／護眼／伸展圖示及類型色正確。
2. 截圖案例的五組 15、6、31、12、6 次應保留總計 70 次、完成 2 次；不能為了消除同名列而刪除或合併資料。
3. 同 ID 改名、停用仍維持一列；同名不同 ID 的兩個現行提醒仍分開。無 ID 舊紀錄依類型與名稱彙整。
4. 統計頁在最小視窗與最大化之間往返切換，確認原圓角邊框不殘留；其他卡片也檢查縮放重繪。自動 `--ux-check` 額外驗證歷史標籤與 1720×940／820×600 往返排版，實際螢幕殘影仍需人工確認。

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
