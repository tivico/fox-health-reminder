# 健康提醒小幫手 (HealthReminder)

一個輕量的 Windows 桌面應用程式，定時提醒你喝水、休息眼睛和起身活動，幫助養成健康的工作習慣。

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![Windows](https://img.shields.io/badge/Platform-Windows-lightgrey)
![License](https://img.shields.io/badge/License-MIT-green)

## ✨ 功能特色

- 🔔 **系統托盤常駐** - 最小化後不佔用工具列，安靜地在背景執行
- 💬 **右下角氣泡通知** - 像 LINE 一樣彈出提醒，不會打斷工作
- ⏰ **多重提醒** - 喝水、眼睛休息、起身活動可各自設定間隔
- 😴 **稍後提醒 (Snooze)** - 忙碌時可延後提醒
- 🌙 **勿擾模式** - 設定午休時間自動暫停提醒
- 📊 **今日統計** - 追蹤完成次數，增加成就感
- 🚀 **開機自啟動** - 可選擇是否隨 Windows 啟動
- ⌨️ **鍵盤與螢幕閱讀器** - 通知可用 Tab、Enter、Esc 與 Alt 快捷鍵操作
- 💾 **AppData／可攜模式** - 設定頁顯示實際資料位置，可用 `portable.flag` 切換

## 📋 系統需求

- Windows 10 / 11
- 64 位元 Windows
- 使用正式安裝包或單檔版時，不需另外安裝 .NET Runtime

## 🔧 安裝與執行

### 親友使用：安裝包

1. 雙擊 `狐狸健康提醒安裝程式.exe`。
2. 保持勾選「建立桌面捷徑」，按下一步完成安裝。
3. 安裝完成後可立即啟動；之後從桌面或開始功能表開啟「小狐狸健康助手」。

安裝程式預設安裝到目前 Windows 使用者的 LocalAppData，不需要系統管理員權限。桌面與開始功能表捷徑會直接顯示主視窗；只有開機自動啟動時才會安靜縮到系統匣。解除安裝不會刪除 `%APPDATA%\HealthReminder` 內的設定與健康紀錄，重新安裝或升級後可繼續使用。

### 開發者：從原始碼編譯

1. 確保已安裝 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

2. 開啟終端機，執行：
   ```bash
   git clone <repository-url>
   cd HealthReminder
   dotnet restore
   dotnet build -c Release
   ```

3. 編譯完成後，執行檔位於：
   ```
   bin/Release/net8.0-windows/HealthReminder.exe
   ```

### 開發者：使用 Visual Studio

1. 用 Visual Studio 2022 開啟 `HealthReminder.sln`
2. 按 F5 執行，或 Ctrl+Shift+B 編譯

### 建立正式安裝包

先安裝 [Inno Setup](https://jrsoftware.org/isdl.php)，再於專案根目錄執行：

```powershell
.\build-release.ps1
```

腳本會依序執行 Release 建置、Verification、self-contained 單檔發布、安裝包編譯與發布成品驗證。中間建置使用 `artifacts/build/release`，發布所需的 win-x64 輸入使用其 `win-x64` 子資料夾，避免 `bin/Release` 內正在執行的 EXE 鎖住檔案而阻擋發布。Verification 由同一個隔離資料夾中的 DLL 執行。

請分清楚三種輸出：

| 類型 | 用途與位置 |
| --- | --- |
| 一般建置 | `bin/Release/.../HealthReminder.exe` 或隔離建置資料夾中的 EXE 是啟動器，需要同資料夾的 DLL，且電腦需有 .NET Runtime；只看 EXE 時間不能判斷所有程式內容是否更新 |
| 正式單檔發布 | `artifacts/publish/win-x64-single-file/HealthReminder.exe` 包含應用程式與 Runtime，可單獨提供給他人 |
| 安裝包 | `artifacts/installer/狐狸健康提醒安裝程式.exe` 包含正式單檔與使用說明，執行安裝才會更新安裝目錄及捷徑 |

發布流程依序經過隔離 Release 建置、功能驗證、`PortableSingleFile.pubxml` 打包 Runtime 和 Inno Setup 編譯。最後由 `verify-release.ps1` 檢查單檔、中文路徑與可攜模式，並產生成品雜湊。試新版前先從系統匣結束舊版，再開正式單檔或執行新安裝包。

`build-release.ps1` 會先檢查單檔與安裝包的輸出位置，只清理它負責產生的檔案。如果目錄內有 `portable.flag`、`Data`、其他檔案或連結，發布會在清理前中止。請把可攜程式與資料放在專用資料夾，再從乾淨的輸出目錄發布。`deploy.ps1` 的自用部署則在 `artifacts/deploy/build` 建置及驗證，以免一般 `bin/Release` 的執行檔被占用時阻擋部署。

親友分享資料夾包含：

```text
artifacts\installer\狐狸健康提醒安裝程式.exe
artifacts\installer\使用說明.txt
```

詳細環境、DPI、多螢幕、鍵盤及 SmartScreen 驗收方式請參考 `Verification\RELEASE_CHECKLIST.md`。

若發布失敗，先找輸出中第一個錯誤：`MSB3021`／`MSB3027` 通常是目標 EXE 被占用，檢查是否仍使用自訂的 `bin` 輸出路徑；`NU1301` 表示發布所需套件無法下載，先檢查網路，必要時以 `dotnet restore HealthReminder.csproj -p:PublishProfile=PortableSingleFile --source https://api.nuget.org/v3/index.json` 補齊官方元件，再重跑腳本。`NU1900` 只是安全資訊查詢未成功，不能當作已完成線上弱點檢查。成品檢查結果在 `artifacts/verification/release-verification.txt`。 發布清理只移除成品目錄內的項目、保留目錄本身，避免使用中的工作目錄無法刪除。若正式單檔本身正被使用而顯示 Access denied，先從系統匣結束舊版再重跑；不要刪除 AppData 的健康資料。

例如只改提醒文字或回滾視覺後，要交付單一 EXE，仍需重新執行 `.\build-release.ps1`；單做 `dotnet build` 不會更新正式單檔與安裝包。

## 📖 使用說明

### 首次啟動

從安裝程式建立的桌面或開始功能表捷徑啟動時，主畫面會直接顯示。開機自動啟動時則會安靜縮小到右下角系統托盤。

### 基本操作

| 操作 | 說明 |
|------|------|
| 雙擊托盤圖示 | 開啟主視窗 |
| 右鍵托盤圖示 | 顯示快捷選單 |
| 通知中的按鈕 | 完成、開始活動、稍後或略過 |
| 點擊視窗 X 按鈕 | 縮小到托盤（不會關閉程式） |

### 預設提醒間隔

| 提醒項目 | 預設間隔 | 建議 |
|----------|----------|------|
| 💧 喝水提醒 | 52 分鐘 | 維持身體水分 |
| 👀 眼睛休息 | 20 分鐘 | 遵循 20-20-20 法則 |
| 🏃 起身活動 | 43 分鐘 | 促進血液循環 |

> **20-20-20 法則**：每 20 分鐘，看 20 英尺（約 6 公尺）遠的地方，持續 20 秒。

### 設定說明

在「提醒與設定」頁面，內容分為「提醒管理」「時間與勿擾」「應用程式偏好」，整頁共用一個捲軸。可以調整：

- **提醒項目**：啟用／停用、修改間隔或一次性時間、自訂訊息與活動秒數
- **播放音效**：開啟後，提醒出現及計時活動完成時都會播放 Windows 內建提示音
- **稍後提醒延遲**：預設 10 分鐘
- **每日目標計算時段**：只用於統計目標與連續達標，時段外仍會提醒；需要暫停時請設定勿擾
- **勿擾模式**：設定午休時間（如 12:00-13:00）自動暫停
- **開機自動啟動**：隨 Windows 啟動
- **減少動畫**：停用狐狸逐格、倒數與按鈕補間
- **通知取得鍵盤焦點**：通知出現後可直接以 Enter、Esc 或 Alt 快捷鍵操作
- **資料位置**：顯示目前使用 AppData 或可攜模式的實際資料夾

編輯視窗的「加入草稿／更新草稿」只更新畫面中的草稿；新增、編輯與切換啟用狀態後，請按設定頁下方固定顯示的「儲存設定」才會寫入並套用排程。操作列會提示「有尚未儲存的變更」，捲動內容時仍可操作。確認刪除則會立即儲存該筆刪除並停止對應倒數，其他未儲存的編輯仍是草稿。可以刪到零筆，已存在的健康紀錄會保留。仍在等待顯示的通知會隨排程取消。

循環提醒在暫停中按「稍後」，新的剩餘時間會等所有暫停解除後才開始倒數。一次性提醒會改為新的指定時間，若恢復時已到期，會在下一次檢查時顯示。勿擾時段可以跨午夜，開始和結束時間必須不同。

首頁會依剩餘時間排列提醒，未啟用的項目排在後面。「手動暫停」需按「解除手動暫停」取消；「取消限時暫停」只解除 30 分鐘、1 小時或今天暫停，不會解除手動暫停或勿擾。狀態列會同時列出所有生效原因與限時／勿擾的結束時間。狐狸動畫預覽位於「應用程式偏好」。健康紀錄以「今天已完成幾次」為主，目標時段內的進度與連續達標仍依原有規則計算。

### 今日提醒明細與歷史項目

明細依提醒 ID 彙整。刪除後重建、重設預設值或更換設定檔，都可能讓同名提醒取得新 ID；舊紀錄仍保留。設定中找不到原 ID 的列會標示「歷史項目」，圖示與色條依紀錄保存的類型顯示，總計仍包含它們。停用但仍存在設定中的提醒不算歷史項目；相同 ID 改名後使用目前名稱顯示。多個歷史項目同名時會加顯示編號。

`Services/ReminderStatisticsCalculator.BuildRows()` 接收當日 Records 與目前 Reminders，只建立顯示摘要，不改寫 JSON。`MainForm.Statistics.cs` 將結果交給 `StatRowCard`。不同 ID 不會只因名稱相同就合併，因為使用者可以建立同名、不同間隔的提醒。比如同名喝水提醒的舊 ID 有 12 次、新 ID 有 6 次，畫面會顯示歷史與現行兩列，合計仍是 18 次。

若看見同名列，先確認是否標有「歷史項目」，再只讀檢查設定的 `Reminders[].Id` 與統計的 `Records[].ReminderId`；不要刪掉健康紀錄來消除畫面列數。若需要日後改成分類總覽，應另寫分類規則並驗證所有列完成數與總次數加總不變，不能把不同 ID 直接當成重複資料。圓角容器及提醒／統計卡片已啟用 `ResizeRedraw`（尺寸改變時完整重繪），避免放大縮小時留下舊邊框殘影；重現時請以視窗往返縮放檢查。

### 介面維護與排查

2026-11-04 已依使用者決定回滾「清爽工具」視覺試作，恢復暖橘配色、卡片、陰影、原側欄，以及原本的提醒、統計、編輯器和通知外觀。保留前一輪的儲存草稿說明、暫停原因提示、單一設定頁捲動與版面修正。排程、資料格式及健康紀錄不受此回滾影響。草稿可以先編輯多項再一起儲存；刪除仍是確認後立即生效，避免已刪提醒繼續倒數。

- **設定流程**：`ReminderEditorForm` 建立提醒副本 → 「加入／更新草稿」回傳 `ResultItem` → `MainForm.Settings.cs` 的 `_reminderItems` 保留草稿 → `SaveSettings()` 呼叫 `BuildSettingsDraft()` 與驗證 → `SettingsManager.SaveSettings()` 寫入 `settings.json` → `ApplySavedReminders()` 重載排程、清理失效通知並更新畫面。
- 編輯一次性提醒時，若通知在編輯視窗開著的期間觸發或被延後，`ReminderEditorForm` 在驗證前會同步未改動的排程欄位。例如只改名稱時，保留背景更新後的啟用狀態和時間。使用者明確修改排程時，以編輯視窗的值為準。
- `ReminderService.Snooze()` 以通知顯示的分鐘數延後，並先比較通知原有排程與目前排程。若舊通知顯示排程已變更，可關閉該通知，再從「提醒與設定」確認目前的間隔、啟用狀態或一次性時間。
- **暫停流程**：`MainForm.Dashboard.cs` 的按鈕呼叫 `ReminderService` → 服務分別保留手動、限時與勿擾原因 → `UpdateCountdownDisplay()`／`UpdateHeaderStatus()` 顯示剩餘原因。所有原因解除後才恢復；`ResumeAll()` 目前只解除手動暫停，維護時不要被方法名稱誤導。
- **版面入口**：`MainForm.cs` 的 `BuildModernShell()` 組裝 72 像素標題列、176 像素側欄與內容區；`BuildSettingsPage()` 用 `CreateSettingsCard()` 組裝區塊，`CreateSettingsRow()` 讓欄位隨寬度換行。`ReminderCardControl` 在窄版時將操作移到文字下方，`ReminderCountdownCard` 將倒數與預計時間放在右側上下兩行。`MainForm.Statistics.cs` 控制全天完成次數標題與目標時段進度。
- **視覺規則與元件**：`UI/AppTheme.cs` 管理暖橘配色與類型色；`CardPanel` 提供 14 像素圓角與邊框，`ElevatedPanel` 提供原有陰影。`StatRowCard` 恢復類型色條、完成次數與完成率小進度條。試作新增的 `LineIcons`／`ReminderGlyph` 已移除。
- **修改範例**：若只想微調主色，在 `AppTheme` 一起檢查 `Primary`、`PrimaryHover` 與 `SelectedBackground`，驗證按鈕文字與背景的對比即可；不需調整排程。若要增加「暫停 15 分鐘」，在 `BuildStatusPage()` 以 `CreateQuickPauseButton()` 呼叫 `PauseFor(TimeSpan.FromMinutes(15))`，並檢查最小視窗的按鈕與底部高度。
- **儲存後沒有改變**：先確認已按設定頁「儲存設定」，看是否仍有草稿提示；若顯示錯誤，依畫面指出的資料位置檢查 `settings.json`、資料夾權限與磁碟空間。不要先刪除設定檔。若草稿不是預期內容，結束重開會重新載入已儲存版本。
- **按恢復後仍不倒數**：先看首頁狀態列是否還有手動或勿擾原因，再到「時間與勿擾」查看已儲存的勿擾設定；這通常是原因尚未全部解除，並非計時器壞掉。
- **圖示或樣式沒有更新**：先確認啟動的是新建置的執行檔，而非已安裝舊版；從系統匣完全結束舊版，再以新路徑加 `--show` 啟動。Emoji 若缺字，檢查系統字型與實際縮放比例。
- **截字／按鈕重疊**：先在最小視窗重現，檢查對應卡片的實際寬度、`DeviceDpi`（螢幕縮放）與自動換行，不要只把固定座標加大。需要暫時處理時可先放大視窗；完整 DPI 驗收見 [發布清單](Verification/RELEASE_CHECKLIST.md)。

可在專案根目錄安全執行以下檢查；獨立輸出避免覆蓋正在執行的版本：

```powershell
dotnet build Verification/HealthReminder.Verification.csproj -c Release -o artifacts/ux-build
dotnet artifacts/ux-build/HealthReminder.Verification.dll
dotnet artifacts/ux-build/HealthReminder.Verification.dll --ux-check --output=artifacts/rollback-review
```

`--ux-check` 使用獨立暫存資料，不讀寫正式設定或自啟登錄；透過透明測試視窗完成 WinForms 排版並輸出畫面，結束後清理測試資料。涵蓋 820×600、920×640（含視窗外框）與預設 920×640 內容區三頁邊界、提醒及統計列重疊、設定頁底部、零筆／八筆提醒、空白／有資料統計、複合暫停，以及編輯器與手動／計時通知。產生的 `panelStatus-default.png`、`panelSettings-default.png`、`panelStatistics-default.png` 和 `notification-*.png` 是測試資料預覽，並非正式健康紀錄。它只驗證目前螢幕 DPI，不能替代 125%、150%、200% 與跨螢幕人工操作。若建置出現 `MSB3021`／`MSB3027`，確認是否覆蓋了執行中的 EXE，改用上述獨立輸出；若是 `NU1900`，是套件安全資訊查詢無法連線，需恢復連線後再查詢，不能當作已完成安全檢查。

### 通知鍵盤操作

| 按鍵 | 操作 |
|------|------|
| Tab／Shift+Tab | 切換通知按鈕 |
| Enter | 執行目前按鈕或主要操作 |
| Esc、Alt+K | 略過 |
| Alt+S | 開始計時活動 |
| Alt+C | 完成手動提醒 |
| Alt+L | 稍後提醒 |

## 📁 專案結構

```
HealthReminder/
├── Program.cs              # 程式進入點
├── MainForm.cs             # 主視窗邏輯
├── build-release.ps1       # 一鍵建置、驗證、發布與產生安裝包
├── Controls/               # 卡片、倒數、進度條、Toggle 與狐狸動畫
├── Forms/                  # 提醒編輯、通知與開發預覽視窗
├── Models/
│   ├── ReminderItem.cs     # 提醒項目模型
│   ├── ReminderRecord.cs   # 提醒紀錄模型
│   └── AppSettings.cs      # 應用程式設定
├── Services/
│   ├── ReminderService.cs          # 提醒計時與暫停服務
│   ├── NotificationService.cs      # 通知佇列
│   ├── SettingsManager.cs          # 設定、統計與資料遷移
│   └── StorageLocationResolver.cs  # AppData／可攜模式寫入探測
├── Resources/              # 內嵌圖示與狐狸 Sprite
├── Verification/           # 自動驗證與發布人工驗收清單
└── installer/              # Inno Setup 親友安裝包腳本
```

## ⚙️ 設定檔位置

設定檔儲存在：
```
%APPDATA%\HealthReminder\settings.json
%APPDATA%\HealthReminder\statistics.json
```

若程式旁存在 `portable.flag`，會先在同層 `Data` 建立並刪除唯一測試檔；確認可寫後才使用可攜模式。若無法寫入，程式會明確詢問要改用 AppData 或結束，不會無聲切換。實際使用的位置會顯示在「提醒與設定」頁面。

### 資料復原

設定與統計各自讀寫。啟動時若其中一份讀取失敗，程式會顯示檔名和原因，保留原檔並阻止對該檔寫入。已存在的 `.bak` 是前一份成功寫入的版本。看到錯誤後，先從系統匣結束程式，再備份整個資料資料夾。檢查原檔和對應的 `.bak`，確認備份可讀後才用它還原原檔，最後重新啟動確認資料。

`pending-commit.json` 保存已接受、仍在同步至兩份 JSON 的操作，例如一次性提醒稍後。程式會在下次啟動或寫入時補寫。如果顯示無法恢復此檔，請先保留它和兩份 JSON，檢查資料夾權限、磁碟空間與檔案是否被鎖定，然後重新啟動。通知若顯示「未儲存」，可在排除問題後在同一張通知重試。

### 自用單檔部署與開機自啟

`.\deploy.ps1` 會發佈並檢查新執行檔，再更新 `%LOCALAPPDATA%\HealthReminder\HealthReminder.exe`。若已有舊版，替換時會建立 `HealthReminder.exe.bak`，新版本啟動失敗時腳本會嘗試還原。`-NoRestart` 可只更新檔案。親友安裝包使用 `.\build-release.ps1`。

開機自啟會使用你在設定頁明確啟用自啟時的執行檔路徑。若搬移位置，請從要自啟的版本開啟設定，關閉自啟並儲存，再重新開啟並儲存。執行開發版或可攜版時，單純啟動程式不會改寫登錄路徑。

## 🔑 技術重點

- **NotifyIcon** - 系統托盤圖示與氣泡通知
- **PerMonitorV2** - 高 DPI 與跨螢幕縮放
- **Timer + UTC Deadline** - 可校準的提醒與活動倒數
- **Registry** - 開機自啟動設定
- **JSON** - 設定檔序列化（使用 Newtonsoft.Json）
- **Mutex** - 確保只有一個執行個體

## 📝 授權

本專案採用 MIT License，完整條款見 [LICENSE](LICENSE)。

## 🤝 貢獻

歡迎提出 Issue 或 Pull Request！

---

**💡 小提醒**：定時休息不只對眼睛好，也能提升工作效率喔！
