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

## 📋 系統需求

- Windows 10 / 11
- .NET 8.0 Runtime ([下載連結](https://dotnet.microsoft.com/download/dotnet/8.0))

## 🔧 安裝與執行

### 方法一：從原始碼編譯

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

### 方法二：使用 Visual Studio

1. 用 Visual Studio 2022 開啟 `HealthReminder.sln`
2. 按 F5 執行，或 Ctrl+Shift+B 編譯

## 📖 使用說明

### 首次啟動

程式啟動後會自動縮小到右下角系統托盤（時鐘旁邊的小圖示區域）。

### 基本操作

| 操作 | 說明 |
|------|------|
| 雙擊托盤圖示 | 開啟主視窗 |
| 右鍵托盤圖示 | 顯示快捷選單 |
| 點擊通知氣泡 | 標記完成或稍後提醒 |
| 點擊視窗 X 按鈕 | 縮小到托盤（不會關閉程式） |

### 預設提醒間隔

| 提醒項目 | 預設間隔 | 建議 |
|----------|----------|------|
| 💧 喝水提醒 | 60 分鐘 | 維持身體水分 |
| 👀 眼睛休息 | 20 分鐘 | 遵循 20-20-20 法則 |
| 🏃 起身活動 | 45 分鐘 | 促進血液循環 |

> **20-20-20 法則**：每 20 分鐘，看 20 英尺（約 6 公尺）遠的地方，持續 20 秒。

### 設定說明

在「設定」頁籤可以調整：

- **提醒項目**：啟用/停用、修改間隔時間、自訂訊息
- **稍後提醒延遲**：預設 10 分鐘
- **勿擾模式**：設定午休時間（如 12:00-13:00）自動暫停
- **開機自動啟動**：隨 Windows 啟動

## 📁 專案結構

```
HealthReminder/
├── Program.cs              # 程式進入點
├── MainForm.cs             # 主視窗邏輯
├── MainForm.Designer.cs    # 主視窗 UI 設計
├── Models/
│   ├── ReminderItem.cs     # 提醒項目模型
│   ├── ReminderRecord.cs   # 提醒紀錄模型
│   └── AppSettings.cs      # 應用程式設定
├── Services/
│   ├── ReminderService.cs  # 提醒計時服務
│   └── SettingsManager.cs  # 設定檔管理
└── Resources/
    └── app.ico             # 應用程式圖示（需自行準備）
```

## ⚙️ 設定檔位置

設定檔儲存在：
```
%APPDATA%\HealthReminder\settings.json
%APPDATA%\HealthReminder\statistics.json
```

## 🎨 自訂圖示

請在 `Resources` 資料夾放置 `app.ico` 檔案作為應用程式圖示。

建議圖示規格：
- 格式：ICO
- 尺寸：包含 16x16, 32x32, 48x48, 256x256

可使用免費工具如 [IcoFX](https://icofx.ro/) 或線上轉換工具製作。

## 🔑 技術重點

- **NotifyIcon** - 系統托盤圖示與氣泡通知
- **Timer** - 多重計時器管理
- **Registry** - 開機自啟動設定
- **JSON** - 設定檔序列化（使用 Newtonsoft.Json）
- **Singleton** - 單例模式確保只有一個執行個體

## 📝 授權

MIT License

## 🤝 貢獻

歡迎提出 Issue 或 Pull Request！

---

**💡 小提醒**：定時休息不只對眼睛好，也能提升工作效率喔！
