# HealthReminder 專案現況、技術與成果

更新日期：2026-07-30  
功能基準 Commit：`dcc56ca`（`fix: stop the big countdown from jittering every second`）  
目前分支：`fix/autostart-path-selfheal`（尚未合回 `master`）

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

一次性提醒觸發後會自動停用並保留歷史；若使用者選擇稍後，會更新一次性期限並重新啟用。

`ReminderType` 目前只保留給舊資料相容、預設 Emoji／托盤圖示及歷史統計讀取使用，不再限制同類提醒只能存在一筆，也不控制排程或完成規則。

## 5. 排程可靠性

- 排程期限在核心層使用 UTC，畫面再轉成本機時間。
- 倒數依絕對截止時間計算，不用 UI Timer 累加秒數。
- 訂閱 Windows 睡眠喚醒與系統時間變更事件，事件發生後重新校準期限。
- 使用 `User`、`DoNotDisturb`、`Temporary` 三種獨立暫停原因，恢復其中一種不會錯誤解除其他暫停。
- 支援快速暫停 30 分鐘、1 小時、今天剩餘時間與立即恢復。
- 一次性本機時間遇到夏令時間不存在區段時會向後尋找有效時間，避免轉換例外導致程式崩潰。

## 6. 通知與 UX 成果

- 自訂無邊框通知從目前螢幕右下角出現，不依賴 Windows Toast 註冊。
- 同一時間只顯示一張通知，其餘進入 Queue。
- 提供完成、開始活動、稍後、略過與逾時狀態。
- 顯示通知不會自動算完成；活動必須倒數結束才寫入 Completed。
- 狀態寫入具防重複保護，同一 Occurrence 只接受一次最終結果。
- 計時活動顯示文字秒數與進度條，完成後先顯示成功回饋再關閉。
- 設定儲存成功改為非阻斷式頁面訊息。
- 一般設定提供「減少動畫」，停用位移與逐格播放但保留內容與操作。

### 2D Sprite 狐狸

狐狸使用內嵌 `Resources/fox-sprites-v3.png`。此資源由 24 姿勢綠幕原圖挑選 8 個適合通知的姿勢，整理成 4×2 透明 Sprite Sheet；`FoxAnimationControl` 以 high-quality bicubic 與 Double Buffer 繪製。

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

- 寫入使用暫存檔後原子取代，降低中途損壞風險。
- `AppSettings.SchemaVersion` 目前為 4。
- 舊設定首次載入時會補齊新欄位。
- JSON 載入會取代預設提醒清單，不會將預設值再次追加；舊資料中設定完全相同的重複提醒會自動合併。
- 舊護眼提醒會遷移為 20 秒、手動開始的計時活動。
- 舊 `Completed`／`Snoozed` 布林欄位仍可遷移至新版狀態。
- 統計以 ReminderId 與實際名稱彙整，不再只看固定 ReminderType。

## 8. UI 現況

主視窗目前為 920×640，包含：

- 今日總覽：下一個提醒、即時倒數卡片、快速暫停。
- 提醒與設定：卡片式提醒管理、通用新增／編輯視窗、一般與勿擾設定、工作時段。
- 健康紀錄：今日成就進度、鼓勵訊息與各提醒統計卡片。

視覺打磨已完成的項目：

- `ElevatedPanel` 在卡片後方繪製多層柔和陰影，卡片不再平貼背景，同時柔化圓角邊緣。
- 原生 `ProgressBar` 換成 `RoundedProgressBar`，圓角漸層並具備 ease-out 補間；極小數值仍維持膠囊外觀。
- 提醒卡片與倒數卡片加上依 `ReminderType` 上色的左側色條，顏色統一由 `AppTheme.AccentFor` 提供。
- 統計頁「今日提醒明細」由原生 `ListView` 改為 `StatRowCard` 卡片列，附完成率小進度條與高亮總計列，預設三個提醒時不需捲動。
- 主視窗標題的靜態狐狸 Emoji 換成 `FoxAnimationControl`，並改用 high-quality bicubic 縮放。
- 按鈕滑鼠停留改用 `HoverGlow` 平滑過渡背景色。
- 「減少動畫」已接上狐狸動畫與大字倒數；`RoundedProgressBar` 與 `HoverGlow` 已具備開關參數，`MainForm` 尚未全部傳入。

已完成 920×640 實際畫面檢查。待補項目包括 125%、150%、200% DPI 實機驗證，以及完整鍵盤 Focus 視覺檢查。

## 9. 驗證現況

最後一次正式驗證結果：

```text
Release build: 0 warnings, 0 errors
Verification: 16/16 passed
Published EXE smoke test: passed
```

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

目前約 69.3 MiB，設定如下：

- `RuntimeIdentifier=win-x64`
- `SelfContained=true`
- `PublishSingleFile=true`
- `EnableCompressionInSingleFile=true`
- `PublishTrimmed=false`

暫不啟用 trimming，避免 WinForms、Newtonsoft.Json 或反射路徑被錯誤裁剪。

### 自用機器的部署位置與開機自啟

開機自啟是把執行檔路徑寫進 `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` 的 `HealthReminder` 值。過去這個值是在使用者勾選「開機自動啟動」的當下寫入，因此會指向當時正在執行的建置；一旦該路徑消失（例如 `bin\Debug` 被清掉），Windows 登入時就靜默失敗，程式不會出現在背景。

現在的做法：

- 自用的正式單檔固定放在 `%LOCALAPPDATA%\HealthReminder\HealthReminder.exe`，不隨專案資料夾清理而消失。
- `SettingsManager.SyncStartWithWindowsPath()` 在 `MainForm` 載入時執行，把登錄值改寫為目前實際執行檔路徑；若設定已關閉則清除殘留登錄值，讓設定與登錄兩邊一致。

因此改完程式碼後要讓開機執行的版本跟著更新，需重新發佈並覆蓋該位置：

```powershell
dotnet publish .\HealthReminder.csproj -c Release -p:PublishProfile=PortableSingleFile
Copy-Item .\artifacts\publish\win-x64-single-file\HealthReminder.exe "$env:LOCALAPPDATA\HealthReminder\HealthReminder.exe" -Force
```

分享給親友時仍只需要交付 `artifacts` 內的單一 EXE，對方放在任何位置皆可執行。

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

`artifacts` 原則上只保留目前正式分享用的單一 EXE；其他驗證輸出完成後即可清除。

## 12. 已知限制與下一步

### 待辦（依建議優先順序）

**維運**

1. 把 `fix/autostart-path-selfheal` 合回 `master`。
2. 建立 `deploy.ps1`，一次完成「發佈單檔 → 覆蓋 `%LOCALAPPDATA%`」，避免改完程式忘記同步開機執行的版本。

**收尾與一致性**

3. 將「減少動畫」傳入 `RoundedProgressBar.AnimationsEnabled` 與 `HoverGlow` 的開關參數（兩者已支援，`MainForm` 尚未全部接上）。
4. 工作時段輸入驗證：目前下班早於上班時 `DailyAchievementCalculator` 會回傳 0 並顯示「尚未設定工作時段」，但設定頁未阻止使用者存下這種組合。
5. 提醒項目較多時，統計與提醒清單仍會出現原生捲軸，可評估自繪捲軸。

**功能與體驗**

6. 實測 Windows 125%、150%、200% DPI 與多螢幕。
7. 補完整鍵盤導覽、Focused／Disabled 狀態與螢幕閱讀器標籤。
8. 加入 7 日、30 日趨勢圖。
9. 支援每日指定時間、每週指定星期等進階排程。
10. 讓狐狸對事件反應，例如完成提醒時揮手、今日達標時跳躍慶祝。
11. 依提醒類型顯示對應姿勢的狐狸；原始 24 姿勢圖中已有「記得喝水喔」與「起來走走顧眼睛」牌子等素材可用。
12. 完成 portable.flag 寫入權限檢查與 AppData fallback 提示。
13. 建立親友分享用 README／快速使用說明。
14. 若未來整個應用程式需要大量向量動畫與頁面轉場，再評估完整遷移 WPF；目前不採 WinForms／WPF 混合 UI。

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
Verification: 16/16 passed
```
