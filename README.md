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

腳本會依序執行 Release 建置、Verification、self-contained 單檔發布、安裝包編譯與發布成品驗證。親友分享資料夾包含：

```text
artifacts\installer\狐狸健康提醒安裝程式.exe
artifacts\installer\使用說明.txt
```

詳細環境、DPI、多螢幕、鍵盤及 SmartScreen 驗收方式請參考 `Verification\RELEASE_CHECKLIST.md`。

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
| 💧 喝水提醒 | 60 分鐘 | 維持身體水分 |
| 👀 眼睛休息 | 20 分鐘 | 遵循 20-20-20 法則 |
| 🏃 起身活動 | 45 分鐘 | 促進血液循環 |

> **20-20-20 法則**：每 20 分鐘，看 20 英尺（約 6 公尺）遠的地方，持續 20 秒。

### 設定說明

在「提醒與設定」頁面可以調整：

- **提醒項目**：啟用／停用、修改間隔或一次性時間、自訂訊息與活動秒數
- **播放音效**：開啟後，提醒出現及計時活動完成時都會播放 Windows 內建提示音
- **稍後提醒延遲**：預設 10 分鐘
- **勿擾模式**：設定午休時間（如 12:00-13:00）自動暫停
- **開機自動啟動**：隨 Windows 啟動
- **減少動畫**：停用狐狸逐格、倒數與按鈕補間
- **通知取得鍵盤焦點**：通知出現後可直接以 Enter、Esc 或 Alt 快捷鍵操作
- **資料位置**：顯示目前使用 AppData 或可攜模式的實際資料夾

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

## 🔑 技術重點

- **NotifyIcon** - 系統托盤圖示與氣泡通知
- **PerMonitorV2** - 高 DPI 與跨螢幕縮放
- **Timer + UTC Deadline** - 可校準的提醒與活動倒數
- **Registry** - 開機自啟動設定
- **JSON** - 設定檔序列化（使用 Newtonsoft.Json）
- **Mutex** - 確保只有一個執行個體

## 📝 授權

MIT License

## 🤝 貢獻

歡迎提出 Issue 或 Pull Request！

---

**💡 小提醒**：定時休息不只對眼睛好，也能提升工作效率喔！
