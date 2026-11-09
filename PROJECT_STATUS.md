# HealthReminder 專案現況、技術與成果

更新日期：2026-11-04

功能基準：`master`（含橘狐動畫、自啟路徑修正與人工動畫驗收收尾）

目前分支：`master`

## 1. 專案定位

HealthReminder 是給自己、家人與朋友使用的 Windows 桌面健康提醒工具。核心發佈原則是：

- 不需安裝 .NET Runtime。
- 親友取得單一 `HealthReminder.exe` 後即可執行。
- 設定與統計保存在目前 Windows 使用者的 AppData，不要求系統管理員權限。
- UI、通知與任務規則可持續擴充，不把喝水、護眼或伸展寫死成特殊流程。

目前 UI Framework 維持 Windows Forms。現階段的卡片介面、自訂通知、像素角色與倒數動畫都能在 WinForms 穩定完成，因此尚無全面遷移 WPF 的必要。

## 2. 技術棧

| 項目 | 現況 |
| --- | --- |
| 語言 | C# |
| Runtime | .NET 8 |
| UI | Windows Forms |
| 目標平台 | Windows x64 |
| JSON | Newtonsoft.Json 13.0.3 |
| 圖形 | GDI+ 自訂繪製、Double Buffer、透明 PNG Sprite Sheet、bicubic 縮放 |
| 發佈 | Self-contained、Single-file、壓縮、不裁剪 |
| 測試 | 獨立 Console Verification 專案 |
| 版本控制 | Git |

## 3. 目前架構

專案不是傳統 Web MVC；目前採用適合小型桌面程式的「UI + Application Services + Models + Platform Integration」分層。

```text
Program / MainForm
├─ Forms
│  ├─ ReminderEditorForm       通用任務新增與編輯
│  └─ ReminderPopupForm        互動式通知與活動倒數
├─ Controls
│  ├─ ReminderCardControl      設定卡片（含類型色條）
│  ├─ ReminderCountdownCard    首頁倒數卡片（含類型色條）
│  ├─ StatRowCard              統計頁單列卡片與完成率小進度條
│  ├─ RoundedProgressBar       圓角漸層進度條，取代原生 ProgressBar
│  ├─ AnimatedCountdownLabel   固定寬度數字格的大字倒數
│  ├─ ToggleSwitch             鍵盤可操作的開關
│  └─ FoxAnimationControl      狐狸 Sprite 動畫
├─ UI
│  ├─ AppTheme                 顏色、字型與類型強調色 Token
│  ├─ CardPanel                圓角卡片容器
│  ├─ ElevatedPanel            在卡片後方繪製柔和陰影的版面容器
│  └─ HoverGlow                滑鼠進出時平滑過渡背景色
├─ Services
│  ├─ ReminderService          排程、暫停、稍後與狀態轉移
│  ├─ NotificationService      通知佇列，同時只顯示一張
│  ├─ SettingsManager          設定、統計、遷移、原子寫入與開機自啟校正
│  ├─ DailyAchievementCalculator 工作時段目標、完成數與連續達標的純計算
│  └─ ReminderCollectionNormalizer
├─ Models
│  ├─ ReminderItem             通用排程與任務定義
│  ├─ ReminderRecord           單次提醒狀態
│  └─ AppSettings              應用程式設定與 SchemaVersion
└─ Windows
   └─ WindowsSystemEventMonitor 睡眠喚醒與系統時間事件
```

`MainForm` 目前仍偏大，後續可將統計呈現、設定頁組裝與視窗導覽拆成 Presenter／ViewModel 或獨立 UserControl；但排程、通知、Persistence 與 Windows 事件已經有明確服務邊界，未來更換 UI Framework 時不必重寫全部核心功能。

本輪先依責任拆為 `MainForm.Dashboard.cs`、`MainForm.Settings.cs`、`MainForm.Statistics.cs` 三個 partial 檔，仍是同一個視窗類別。`MainForm.cs` 負責視窗生命週期、通知協調與共用外觀。設定草稿和已儲存設定各自持有副本，排程亦保存自己的副本，避免編輯畫面直接改到執行中狀態。Windows 自啟由 `WindowsStartupRegistration` 管理。

## 4. 通用任務模型

每個提醒由「排程方式」與「完成方式」自由組合：

### 排程方式

- `Interval`：每隔 1～1440 分鐘持續提醒。
- `OneTime`：在指定本機日期時間提醒一次。

### 完成方式

- `Manual`：使用者明確按下完成。
- `TimedActivity`：按開始後倒數指定秒數，或依設定自動開始；倒數結束才算完成。

因此目前已支援四種組合：

1. 持續＋手動完成，例如喝水。
2. 持續＋計時活動，例如每 20 分鐘護眼 20 秒。
3. 一次性＋手動完成，例如今天 15:00 吃藥。
4. 一次性＋計時活動，例如晚上做一次 5 分鐘呼吸練習。

一次性提醒實際顯示並記錄後會自動停用；若使用者選擇稍後，會更新一次性期限並重新啟用。排隊中結束程式時，尚未顯示的一次性提醒仍會在下次啟動後排程。循環提醒的待顯示佇列保存在記憶體，隨程式結束。

`ReminderType` 目前只保留給舊資料相容、預設 Emoji／托盤圖示及歷史統計讀取使用，不再限制同類提醒只能存在一筆，也不控制排程或完成規則。

## 5. 排程可靠性

- 排程期限在核心層使用 UTC，畫面再轉成本機時間。
- 倒數依絕對截止時間計算，不用 UI Timer 累加秒數。
- `ReminderService.Pulse()` 使用單一 WinForms 計時器巡檢期限及暫停。`Reload()` 依提醒 ID 更新，只有排程欄位改變才重算期限。刪除只移除對應排程，並回傳 ID 讓通知服務清理等待佇列。
- 訂閱 Windows 睡眠喚醒與系統時間變更事件，事件發生後重新校準期限。
- 使用 `User`、`DoNotDisturb`、`Temporary` 三種獨立暫停原因，恢復其中一種不會錯誤解除其他暫停。
- 支援快速暫停 30 分鐘、1 小時、今天剩餘時間與立即恢復。
- 一次性本機時間遇到夏令時間不存在區段時會向後尋找有效時間，避免轉換例外導致程式崩潰。

## 6. 通知與 UX 成果

- 自訂無邊框通知從目前螢幕右下角出現，不依賴 Windows Toast 註冊。
- 同一時間只顯示一張通知，其餘進入 Queue。
- 通知在顯示完成時才記錄 `Triggered`，顯示後的操作確認寫入才呈現成功。活動倒數使用 `Stopwatch` 的經過時間，避免調整牆上時鐘影響秒數。
- 提供完成、開始活動、稍後、略過與逾時狀態。
- 顯示通知不會自動算完成；活動必須倒數結束才寫入 Completed。
- 狀態寫入具防重複保護，同一 Occurrence 只接受一次最終結果。
- 計時活動顯示文字秒數與進度條，完成後先顯示成功回饋再關閉。
- 設定儲存成功改為非阻斷式頁面訊息。
- 一般設定提供「減少動畫」，停用位移與逐格播放但保留內容與操作。

### 2D Sprite 狐狸

`FoxAnimationControl` 優先載入每個動作各自一張、每格固定 512×512 的單排透明 Sprite Sheet，並由圖片寬度自動計算影格數。各動作可使用不同播放間隔與影格順序。待機使用 `Resources/fox_idle.png`，目前由標準待機與清楚閉眼兩格組成，以每格 400 ms、`睜→睜→睜→閉→閉→閉` 的順序循環；睜眼與閉眼各停留 1.2 秒，完整循環 2.4 秒。喝水、護眼與伸展通知顯示期間會分別持續循環 `fox_drink.png`、`fox_eye_rest.png` 與 `fox_stretch.png`，不再只播一輪便退回待機；自訂提醒才使用待機眨眼。伸展依 `1→2→3→4→5→5→5→4→3→2→1` 播放，每格 160 ms。設定頁「應用程式偏好」提供「預覽狐狸動畫」，可直接切換待機、揮手、喝水、護眼與伸展。新圖資載入失敗時會先沿用 `Wait` 動畫，最後才退回內嵌的 `Resources/fox-sprites-v3.png` 4×2 白狐。繪製使用 high-quality bicubic 與 Double Buffer。

目前橘色狐狸動畫素材進度：

| 動作 | 正式資源 | 狀態 |
| --- | --- | --- |
| 待機 | `Resources/fox_idle.png` | 已更新為 2 格版本：既有標準待機＋2026-07-30 18:14 收到的完整閉眼圖；目前先求穩定眨眼，呼吸與擺尾暫緩 |
| 揮手 | `Resources/fox_wave.png` | 已完成並接入，實際觀感明顯比初版眨眼順暢 |
| 喝水 | `Resources/fox_drink.png` | 已完成並依喝水提醒自動播放 |
| 眼睛休息 | `Resources/fox_eye_rest.png` | 已完成並依護眼提醒自動播放 |
| 起身與伸展 | `Resources/fox_stretch.png` | 已完成 5 格並接入；包含既有待機母圖與 4 張新伸展影格，通知期間持續循環 |

2026-07-30 16:29 收到的兩張待機／眨眼候選圖，因眼睛仍與母圖接近相同的上彎弧線而未採用。18:14 收到的新圖具有較清楚、接近水平的閉眼線條，已保留原始 1024×1024 PNG 於 `暫存`，並在不修改原圖的前提下正規化為 512×512 閉眼格，與既有標準待機格組成正式 `fox_idle.png`。舊版 4 格待機 Sprite Sheet 備份於 `artifacts/animation-backups/fox_idle-before-20260730-1814.png`。

2026-07-30 19:15～19:16 收到 4 張逐格製作的伸展圖，已按原始 1024×1024 畫布等比例縮小、校正腳底基準線並與既有待機母圖組成 2560×512 的 `fox_stretch.png`；原始圖保留在 `暫存`。

### 2026-07-30 橘狐動畫階段收尾

- 白狐已由橘粉色狐狸取代，正式素材包含待機眨眼、揮手、喝水、護眼及起身伸展。
- 待機依使用者實測調整為每格 400 ms 的 `睜→睜→睜→閉→閉→閉` 六拍循環。
- 喝水、護眼與伸展通知會在視窗存在期間持續循環專屬動畫；自訂提醒使用待機眨眼。
- 主畫面已提供動畫預覽視窗，可直接切換五種動畫驗收。
- Release 建置為 0 警告、0 錯誤，自動驗證為 20/20 通過。
- 下一階段先以實際使用感受為主，若發現速度、角色基準線或通知切換需要微調，再由本文件待辦繼續。

狀態流程：

```text
RollIn → Wave → Talk → Wait → Celebrate → RollOut
```

圖資載入失敗時會退回靜態狐狸 Emoji，不影響提醒功能。Sprite 已嵌入 Assembly，因此單一 EXE 旁不需額外圖片資料夾。

## 7. 資料與相容性

預設資料位置：

```text
%APPDATA%\HealthReminder\settings.json
%APPDATA%\HealthReminder\statistics.json
```

- `SettingsManager` 分別驗證設定與統計。寫入先建立暫存檔再取代原檔，舊版保存在 `.bak`。一次性提醒的跨檔操作使用 `pending-commit.json`，下次啟動或寫入會先補同步。資料復原步驟見 [README](README.md#資料復原)。
- `AppSettings.SchemaVersion` 目前為 5。
- 舊設定首次載入時會補齊新欄位。
- JSON 載入會取代預設提醒清單，不會將預設值再次追加；舊資料中設定完全相同的重複提醒會自動合併。
- 舊護眼提醒會遷移為 20 秒、手動開始的計時活動。
- 舊 `Completed`／`Snoozed` 布林欄位仍可遷移至新版狀態。
- 統計以 ReminderId 與實際名稱彙整。一次性通知保留原排程時間，已顯示的提醒即使停用仍可列入當天目標。

## 8. UI 現況

主視窗預設內容區為 920×640，已恢復原本暖橘卡片視覺、72 像素標題列與 176 像素側欄，包含：

- 今日總覽：下一個提醒、依剩餘時間排序的卡片、手動與快速暫停。
- 提醒與設定：提醒管理、時間與勿擾、應用程式偏好；新增／編輯先加入草稿，固定操作列儲存後套用。
- 健康紀錄：今日完成次數、目標時段進度、鼓勵訊息與各提醒統計卡片。

2026-11-04 介面與操作優化：設定頁改為單一外層捲動，卡片隨內容增高；提醒卡片在窄版時將操作移至下方。編輯器明示草稿，首頁區分「解除手動暫停」與「取消限時暫停」，列出同時生效的原因，提醒清單依剩餘時間排序。每日目標計算時段明示只影響統計；狐狸動畫預覽移至應用程式偏好。排程服務與資料格式沿用既有設計。維護位置、操作流程、診斷方法與修改範例見 [README 的介面維護與排查](README.md#介面維護與排查)。 本輪已驗證：Release 建置成功、既有 Verification 32/32 通過；新增隔離介面檢查在目前螢幕 DPI 通過 820×600／920×640 三頁、設定頁底部、0／8 筆提醒與複合暫停，並已目視檢查輸出。建置出現 NU1900（套件安全資訊來源無法連線），因此沒有宣稱完成弱點查詢；其他 DPI、多螢幕、實際鍵盤及安裝包尚未重新驗收。

2026-11-04 視覺回滾：使用者認為清爽工具版不如原本外觀，因此以 `artifacts/modern-before/*.before` 還原主視窗、三分頁、提醒卡片、統計卡片、編輯器與通知；共用主題、卡片、陰影、開關和進度條恢復視覺試作前內容。試作新增的 `LineIcons`／`ReminderGlyph` 已移除。前一輪的操作說明與版面改善保留。擴充介面檢查保留，移除只屬於試作首頁的完成數標籤斷言。正式設定、健康紀錄與安裝版不需遷移。 回滾後 Release 建置成功、32/32 功能檢查與擴充版面檢查通過；已核對 9 份改版前備份及 5 份原視覺元件一致，並更新先前 `artifacts/modern-build` 測試入口的主程式組件。建置仍有 4 個套件安全資訊連線警告 NU1900，未宣稱完成線上弱點查詢。

2026-11-04 統計辨識修正：截圖同名列來自目前設定已找不到的舊提醒 ID 與現行 ID；原本以 ID 分組卻未顯示歷史身分，並將舊圖示全部退回燈泡，容易被誤認為重複計算。新增 `ReminderStatisticsCalculator.BuildRows()`，讓統計列明示「歷史項目」，依紀錄類型補回圖示與色條，同 ID 改名使用目前名稱，停用但存在設定的項目仍屬現行；不同 ID 保持獨立，原始 JSON 不變。以截圖的 70 次、完成 2 次重現並驗證合計不變。圓角容器、提醒與統計卡片補上 `ResizeRedraw`，避免尺寸改變後殘留舊邊框；自動版面驗證新增寬窄往返，實際螢幕殘影及其他 DPI 仍需人工確認。計算與維護規則見 [README](README.md#今日提醒明細與歷史項目)。 本次 36/36 功能驗證與擴充版面檢查通過，正式單檔於台北時間 2026-11-04 16:42:06、安裝包於 16:42:18 更新，發布啟動與中文路徑／可攜模式檢查通過。發布時原 EXE 與工作目錄被使用，已將舊成品保留至 `artifacts/release-backups/running-before-statistics-fix-*`，並改成只清理成品目錄內容、保留目錄本身；執行中的舊版仍須由使用者結束後重開新版。

視覺打磨已完成的項目：

- `ElevatedPanel` 恢復原有多層陰影；`CardPanel` 恢復 14 像素圓角與細邊框。
- `RoundedProgressBar` 恢復暖色漸層，保留補間及減少動畫設定。
- 提醒及統計恢復類型色條與 Emoji 卡片；共用顏色仍由 `AppTheme` 管理。
- 統計頁 `StatRowCard` 恢復完成次數、右側完成率小進度條與總計列。
- 主視窗標題的靜態狐狸 Emoji 換成 `FoxAnimationControl`，並改用 high-quality bicubic 縮放。
- 按鈕滑鼠停留改用 `HoverGlow` 平滑過渡背景色。
- 「減少動畫」已同步接上狐狸動畫、大字倒數、`RoundedProgressBar` 與 `HoverGlow`。
- 通知按鈕具備明確 Focus／Disabled 視覺、AccessibleName／Description、Tab 順序、Enter／Esc，以及 Alt+S／C／L／K 快捷鍵。
- 一般設定新增「通知出現時取得鍵盤焦點」；Windows 螢幕閱讀器啟用時會自動取得焦點。
- 專案設定為 `ApplicationHighDpiMode=PerMonitorV2`，主視窗與通知使用 DPI AutoScale。

已完成 920×640 實際畫面檢查。通知鍵盤與 Focus／Disabled 程式收尾已完成；125%、150%、200% DPI、多螢幕及 Narrator 仍需依 `Verification/RELEASE_CHECKLIST.md` 在對應設備人工驗收。

## 9. 驗證現況

2026-11-04 回滾版本已完成正式發布：`build-release.ps1` 全流程成功，32/32 功能驗證、中文與空白路徑、可寫及不可寫可攜模式檢查通過；成品為 `artifacts/publish/win-x64-single-file/HealthReminder.exe` 與 `artifacts/installer/狐狸健康提醒安裝程式.exe`。完整時間、SHA256 及驗收限制在 `artifacts/verification/release-verification.txt`。安裝包未簽章；安裝生命週期、乾淨 Windows、其他 DPI 與多螢幕未重新驗收。

本次另修正發布腳本：中間建置及 Verification 使用 `artifacts/build/release`，發布的 win-x64 輸入使用其子資料夾，避開使用中 `bin/Release` EXE 的檔案鎖。發布初次因缺少 .NET Runtime 發布元件而失敗，已從官方 NuGet 補齊後重新執行成功；安全資訊查詢在一般執行環境仍有 NU1900 連線警告。只做一般建置不等於更新單檔與安裝包，實際交付需重新執行發布腳本；目前執行中的程式與安裝版不會自動切換。舊成品已備份於 `artifacts/release-backups`，操作與排查說明已更新到 README。

先前正式發布驗證結果：

```text
Release build: 0 warnings, 0 errors
Verification: 24/24 passed
Published EXE smoke test: passed
```

本輪修復的本機 Release 建置為零警告、零錯誤。Verification 擴充為 32 項，包括壞檔保護、重試、補寫、暫停交錯、刪除和長時間倒數。新版安裝包與登入自啟仍依發布清單驗收。

目前自動驗證包含：

- Completed 統計規則。
- 舊狀態欄位相容性。
- 預設提醒唯一 Id。
- JSON 載入取代提醒清單，不會重複追加預設項目。
- 只修復重複 Id，不限制同類範本數量。
- 完全相同提醒自動去重，同類但不同設定仍可並存。
- 四種排程／完成組合。
- 護眼 20 秒預設活動。
- 工作時段每日目標與時段外提醒排除規則。
- 工作時段完成數與跨日連續達標規則。
- 一次性絕對期限。
- 一次性稍後重新啟用。
- 暫停保留剩餘時間。
- 多種暫停原因互不覆蓋。
- 通知 Alt+S／C／L／K 與 Esc 對應正確操作。
- 沒有 `portable.flag` 時使用 AppData。
- 可攜模式會先實際測試 `Data` 寫入權限並清除測試檔。
- `Data` 無法建立時回報明確 failure，交由 UI 決定 fallback 或結束。

## 10. 建置與發布

一般 Release 建置：

```powershell
dotnet build .\HealthReminder.csproj -c Release
```

執行驗證：

```powershell
dotnet run --project .\Verification\HealthReminder.Verification.csproj -c Release
```

建立正式單一 EXE：

```powershell
dotnet publish .\HealthReminder.csproj -c Release -p:PublishProfile=PortableSingleFile
```

正式成品：

```text
artifacts\publish\win-x64-single-file\HealthReminder.exe
```

目前約 72.7 MiB，設定如下：

- `RuntimeIdentifier=win-x64`
- `SelfContained=true`
- `PublishSingleFile=true`
- `EnableCompressionInSingleFile=true`
- `PublishTrimmed=false`

暫不啟用 trimming，避免 WinForms、Newtonsoft.Json 或反射路徑被錯誤裁剪。

### 親友安裝包

親友版採 Inno Setup 的每位使用者安裝模式。開發者安裝 Inno Setup 後，在專案根目錄執行：

```powershell
.\build-release.ps1
```

腳本會依序執行 Release 建置、24 項 Verification、self-contained 單檔發布、安裝包編譯與發布成品驗證，並驗證正式發布目錄只包含單一 `HealthReminder.exe`。

正式分享成品：

```text
artifacts\installer\狐狸健康提醒安裝程式.exe
```

安裝包使用繁體中文介面，安裝至 `%LOCALAPPDATA%\Programs\HealthReminder`，不要求系統管理員權限，並建立開始功能表與預設勾選的桌面捷徑。解除安裝會清除程式與開機自啟登錄值，但保留 `%APPDATA%\HealthReminder` 的設定及健康紀錄。`使用說明.txt` 同時放在分享資料夾、安裝目錄與開始功能表。

`Verification/verify-release.ps1` 會額外驗證中文／空白路徑、self-contained 啟動煙霧測試、portable 可寫與不可寫情境、SHA256、版本與簽章狀態，並輸出 `artifacts/verification/release-verification.txt`。

2026-07-31 已在目前 Windows 主機完成一次靜默安裝生命週期實測：程式、親友說明、桌面捷徑、開始功能表捷徑與安裝後 `--smoke-test` 均通過；解除安裝後程式資料夾、捷徑與解除安裝登錄項皆已清除。乾淨 Windows 10／11 無 Runtime 仍需外部設備或啟用 Windows Sandbox 後驗收。

### 自用機器的部署位置與開機自啟

開機自啟使用 `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` 的 `HealthReminder` 值。請從正式路徑開啟程式再啟用自啟，讓登錄項指向可長期使用的版本。

現在的做法：

- 自用的正式單檔固定放在 `%LOCALAPPDATA%\HealthReminder\HealthReminder.exe`，不隨專案資料夾清理而消失。
- `WindowsStartupRegistration.Apply()` 在使用者儲存自啟變更時更新登錄。單純啟動其他版本會保留原先選定的路徑。

因此改完程式碼後要讓開機執行的版本跟著更新，直接執行 `deploy.ps1`：

```powershell
.\deploy.ps1
```

流程為驗證 → 發佈單檔與啟動檢查 → 核對待安裝檔的 SHA256 → 結束目標路徑的舊版 → 替換並保留備份 → 重新啟動。啟動失敗時會嘗試還原。若登錄指向另一個路徑，腳本會顯示提示，可依 README 重新指定。

可用參數：`-SkipVerification` 略過測試，`-NoRestart` 只更新檔案不重新啟動。

`deploy.ps1` 只處理自己這台機器；要產生給親友的安裝包仍使用 `build-release.ps1`。

分享給親友時優先交付 `artifacts\installer` 內的安裝程式；`artifacts\publish` 內的單一 EXE 保留作為免安裝備用版。

## 11. 專案檔案政策

Git 應追蹤：

- C# 原始碼、專案與 Solution。
- `Resources` 內的正式圖示與狐狸 Sprite。
- `PortableSingleFile.pubxml`。
- README、重構計畫、現況紀錄與原始需求文件。

不應追蹤、可隨時重建：

- `.vs/`
- `bin/`
- `obj/`
- 臨時建置輸出與視覺測試截圖。
- 舊式多 DLL self-contained 發布資料夾。
- `*.csproj.user` 與 `*.pubxml.user`。

`artifacts` 原則上只保留目前正式分享用的安裝程式與免安裝單一 EXE；其他驗證輸出完成後即可清除。

## 12. 已知限制與下一步

### 待辦（依建議優先順序）

**狐狸動畫素材**

人工動畫驗收已完成；主畫面的「動畫預覽」入口已隱藏，2026-11-04 改由設定頁的「預覽狐狸動畫」提供入口。合併前 Release 建置為 0 警告、0 錯誤，20 項 Verification 全數通過。

1. 呼吸與尾巴擺動暫緩；等能取得真正保持母圖一致的連續影格後，再加入待機動畫，避免為了湊四格產生晃動。

**收尾與一致性**

2. 提醒項目較多時，統計與提醒清單仍會出現原生捲軸，可評估自繪捲軸。

**功能與體驗**

3. 在乾淨 Windows 10／11（無 .NET Runtime）完成安裝、提醒、解除安裝與重裝實測。
4. 實測 Windows 125%、150%、200% DPI、不同縮放比例的多螢幕與 Narrator 朗讀。
5. 決定是否購買程式碼簽章；目前親友測試版維持未簽章並提供 SmartScreen 說明及 SHA256。
6. 加入 7 日、30 日趨勢圖。
7. 支援每日指定時間、每週指定星期等進階排程。
8. 讓狐狸對事件反應，例如完成提醒時揮手、今日達標時跳躍慶祝。
9. 依提醒類型顯示對應姿勢的狐狸；原始 24 姿勢圖中已有「記得喝水喔」與「起來走走顧眼睛」牌子等素材可用。
10. 視親友實際安裝回饋補充圖文版快速使用說明。
11. 若未來整個應用程式需要大量向量動畫與頁面轉場，再評估完整遷移 WPF；目前不採 WinForms／WPF 混合 UI。

更細的階段工作與驗收清單請參考 `REFACTORING_PLAN.md`。

## 13. 2026-07-21 專案整理紀錄

本次整理已移除超過 500 MiB 的可重建或過時內容，包括：

- 兩份舊式多 DLL self-contained 發布資料夾。
- `.vs`、所有 `bin`／`obj` 與臨時建置輸出。
- UI 預覽、測試截圖及狐狸圖資產生過程的中間檔。
- 指向開發者本機絕對路徑的兩份舊 Publish Profile。
- `*.csproj.user`、`*.pubxml.user` 等個人環境設定。

已保留原始需求文件、正式資源、原始碼、驗證專案、重構計畫、`PortableSingleFile.pubxml`，以及目前可直接分享的正式單檔 EXE。清理後重新執行 Release 建置為 0 警告、0 錯誤，驗證為 10/10 通過。

## 14. 2026-07-30 UI 打磨與問題修正紀錄

本次以「不動排程與資料邏輯、只改 UI 層」為原則進行視覺打磨，並修掉兩個實際問題。

### 視覺打磨

依序完成卡片陰影、圓角漸層進度條、卡片類型色條、狐狸素材更新、狐狸上主視窗、統計頁卡片化，以及大字倒數與按鈕 hover 的動畫。細節見第 8 節。

狐狸素材改用使用者提供的 24 姿勢綠幕原圖：以 chroma-key 去背並抑制綠色溢色，自動偵測每隻的邊界，挑選 8 個姿勢後統一縮放、水平置中、置底對齊，輸出為 `Resources/fox-sprites-v3.png`（4×2，每格 240×240）。置底對齊可避免逐格播放時角色忽高忽低。

### 今日成就改為工作時段目標型

原本進度條的分母是「到目前為止已觸發的提醒數」，因此一早完成單一提醒就會顯示 100%，失去意義。改為以整個工作時段預計出現的提醒總數為 100% 目標：

- 新增 `AppSettings.WorkdayStart` 與 `WorkdayEnd`，預設 09:00–18:00，可在設定頁調整，因為每個人下班時間不同。
- 計算集中在 `DailyAchievementCalculator`，同時處理工作時段驗證、時段外紀錄排除與跨日連續達標。
- 畫面同時顯示百分比與「已完成 N / M 次」，避免只有百分比而看不出實際數量。

### 修正：開機自啟指向已不存在的執行檔

登錄值原本停留在啟用當下的建置路徑，`bin\Debug` 被清除後 Windows 登入時靜默失敗。新增 `SettingsManager.SyncStartWithWindowsPath()` 於啟動時校正路徑，並把自用正式版固定放在 `%LOCALAPPDATA%`。詳見第 10 節。

驗證方式：故意把登錄值改成不存在的路徑後啟動程式，確認登錄值被自動改寫回實際執行檔路徑。

### 修正：大字倒數每秒抖動

`AnimatedCountdownLabel` 初版每秒重新排版整串文字，因為字型數字並非等寬，冒號與後續數字會隨數字寬度左右位移；同時整串一起垂直滑動，且半透明文字使用 ClearType 造成彩邊。

改為每個字元佔用固定寬度格子（數字統一採用最寬數字的寬度）、只有實際改變的字元做交叉淡入、逐格裁切避免溢出，並改用灰階抗鋸齒。

驗證方式：擷取連續影格後以像素分析比對字元墨水區塊位置，確認前四個字元位置完全一致、冒號固定不動，變動數字的墨水中心維持在同一像素位置。

### 本次驗證結果

```text
Release build: 0 warnings, 0 errors
Verification: 20/20 passed
```
