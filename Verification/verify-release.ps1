[CmdletBinding()]
param(
    [switch]$RequireSignature,
    [switch]$InstallLifecycle
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path $PSScriptRoot -Parent
$artifactsRoot = Join-Path $projectRoot 'artifacts'
$publishDirectory = Join-Path $artifactsRoot 'publish\win-x64-single-file'
$installerDirectory = Join-Path $artifactsRoot 'installer'
$publishedExe = Join-Path $publishDirectory 'HealthReminder.exe'
$installerExe = Join-Path $installerDirectory '狐狸健康提醒安裝程式.exe'
$quickGuide = Join-Path $installerDirectory '使用說明.txt'
$verificationRoot = Join-Path $artifactsRoot 'verification'
$pathTestRoot = Join-Path $verificationRoot '路徑 測試'
$reportPath = Join-Path $verificationRoot 'release-verification.txt'

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Clear-VerificationPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsRoot).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $expectedPrefix = $resolvedArtifacts + [System.IO.Path]::DirectorySeparatorChar

    if (-not $resolvedPath.StartsWith(
            $expectedPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "拒絕清理 artifacts 以外的驗證路徑：$resolvedPath"
    }

    if (Test-Path -LiteralPath $resolvedPath) {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }
}

function Invoke-SmokeTest {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][int]$ExpectedExitCode
    )

    $process = Start-Process `
        -FilePath $Executable `
        -ArgumentList '--smoke-test' `
        -Wait `
        -PassThru `
        -WindowStyle Hidden

    if ($process.ExitCode -ne $ExpectedExitCode) {
        throw "啟動煙霧測試失敗：$Executable；預期 $ExpectedExitCode，實際 $($process.ExitCode)"
    }
}

function Invoke-InstallLifecycleTest {
    $installDir = Join-Path $env:LOCALAPPDATA 'Programs\HealthReminder'
    $desktopShortcut = Join-Path (
        [Environment]::GetFolderPath('Desktop')) '小狐狸健康助手.lnk'
    $programGroup = Join-Path (
        [Environment]::GetFolderPath('Programs')) '小狐狸健康助手'
    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{F2CA4965-5479-4927-9AD5-44E74809A183}_is1'

    Assert-Condition `
        (-not (Test-Path -LiteralPath $uninstallKey)) `
        '已偵測到安裝版 HealthReminder；為避免覆蓋使用中的版本，拒絕執行安裝生命週期測試。'
    Assert-Condition `
        ($null -eq (Get-Process -Name HealthReminder -ErrorAction SilentlyContinue)) `
        'HealthReminder 正在執行；請先結束程式再執行安裝生命週期測試。'

    $installed = $false
    try {
        $setup = Start-Process `
            -FilePath $installerExe `
            -ArgumentList '/VERYSILENT', '/CURRENTUSER', '/NORESTART', '/SUPPRESSMSGBOXES' `
            -Wait `
            -PassThru `
            -WindowStyle Hidden
        Assert-Condition ($setup.ExitCode -eq 0) "安裝程式結束碼為 $($setup.ExitCode)。"
        $installed = $true

        $appExe = Join-Path $installDir 'HealthReminder.exe'
        $installedGuide = Join-Path $installDir '使用說明.txt'
        $uninstaller = Join-Path $installDir 'unins000.exe'
        $requiredPaths = @(
            $appExe,
            $installedGuide,
            $uninstaller,
            $desktopShortcut,
            (Join-Path $programGroup '小狐狸健康助手.lnk'),
            (Join-Path $programGroup '使用說明.lnk')
        )
        foreach ($requiredPath in $requiredPaths) {
            Assert-Condition `
                (Test-Path -LiteralPath $requiredPath) `
                "安裝後缺少：$requiredPath"
        }

        Invoke-SmokeTest -Executable $appExe -ExpectedExitCode 0
    }
    finally {
        $uninstaller = Join-Path $installDir 'unins000.exe'
        if ($installed -and (Test-Path -LiteralPath $uninstaller)) {
            $uninstall = Start-Process `
                -FilePath $uninstaller `
                -ArgumentList '/VERYSILENT', '/NORESTART', '/SUPPRESSMSGBOXES' `
                -Wait `
                -PassThru `
                -WindowStyle Hidden
            Assert-Condition `
                ($uninstall.ExitCode -eq 0) `
                "解除安裝程式結束碼為 $($uninstall.ExitCode)。"
        }
    }

    Assert-Condition (-not (Test-Path -LiteralPath $installDir)) '解除安裝後程式資料夾仍存在。'
    Assert-Condition (-not (Test-Path -LiteralPath $desktopShortcut)) '解除安裝後桌面捷徑仍存在。'
    Assert-Condition (-not (Test-Path -LiteralPath $programGroup)) '解除安裝後開始功能表群組仍存在。'
    Assert-Condition (-not (Test-Path -LiteralPath $uninstallKey)) '解除安裝後登錄項仍存在。'
}

$publishFiles = @(Get-ChildItem -LiteralPath $publishDirectory -File)
Assert-Condition ($publishFiles.Count -eq 1) '正式發布目錄必須只包含一個檔案。'
Assert-Condition ($publishFiles[0].Name -eq 'HealthReminder.exe') '正式發布檔名必須是 HealthReminder.exe。'
Assert-Condition (Test-Path -LiteralPath $installerExe -PathType Leaf) '找不到狐狸健康提醒安裝程式.exe。'
Assert-Condition (Test-Path -LiteralPath $quickGuide -PathType Leaf) '找不到親友分享用使用說明.txt。'

New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
Clear-VerificationPath -Path $pathTestRoot

try {
    $writablePortableRoot = Join-Path $pathTestRoot '含 中文與空白 可寫'
    New-Item -ItemType Directory -Path $writablePortableRoot -Force | Out-Null
    $pathTestExe = Join-Path $writablePortableRoot '小狐狸 健康提醒.exe'
    Copy-Item -LiteralPath $publishedExe -Destination $pathTestExe

    Invoke-SmokeTest -Executable $pathTestExe -ExpectedExitCode 0

    New-Item -ItemType File -Path (Join-Path $writablePortableRoot 'portable.flag') -Force | Out-Null
    Invoke-SmokeTest -Executable $pathTestExe -ExpectedExitCode 0
    Assert-Condition `
        (Test-Path -LiteralPath (Join-Path $writablePortableRoot 'Data') -PathType Container) `
        'portable.flag 未建立可寫的 Data 資料夾。'

    $blockedPortableRoot = Join-Path $pathTestRoot '含 中文與空白 不可寫'
    New-Item -ItemType Directory -Path $blockedPortableRoot -Force | Out-Null
    $blockedTestExe = Join-Path $blockedPortableRoot '小狐狸 健康提醒.exe'
    Copy-Item -LiteralPath $publishedExe -Destination $blockedTestExe
    New-Item -ItemType File -Path (Join-Path $blockedPortableRoot 'portable.flag') -Force | Out-Null
    New-Item -ItemType File -Path (Join-Path $blockedPortableRoot 'Data') -Force | Out-Null
    Invoke-SmokeTest -Executable $blockedTestExe -ExpectedExitCode 4
}
finally {
    Clear-VerificationPath -Path $pathTestRoot
}

$signature = Get-AuthenticodeSignature -LiteralPath $installerExe
if ($RequireSignature -and $signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
    throw "安裝程式需要有效程式碼簽章，目前狀態：$($signature.Status)"
}

$installLifecycleStatus = '未執行；可加上 -InstallLifecycle 在未安裝且程式未執行時測試。'
if ($InstallLifecycle) {
    Invoke-InstallLifecycleTest
    $installLifecycleStatus = 'PASS'
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing.Common
$screens = @([System.Windows.Forms.Screen]::AllScreens)
$bitmap = [System.Drawing.Bitmap]::new(1, 1)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $currentDpi = [Math]::Round($graphics.DpiX)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

$installerHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installerExe).Hash
$publishedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $publishedExe).Hash
$monitorStatus = if ($screens.Count -ge 2) {
    "目前偵測到 $($screens.Count) 台螢幕；仍需人工拖曳與通知定位驗收。"
}
else {
    '目前只有一台螢幕；多螢幕項目待外部設備驗收。'
}

$report = @(
    "HealthReminder 發布驗證報告",
    "產生時間：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')",
    "作業系統：$([System.Environment]::OSVersion.VersionString)",
    "目前程序 DPI：$currentDpi",
    "螢幕數量：$($screens.Count)",
    "多螢幕狀態：$monitorStatus",
    "發布 EXE：$publishedExe",
    "發布 EXE SHA256：$publishedHash",
    "安裝程式：$installerExe",
    "安裝程式 SHA256：$installerHash",
    "安裝程式簽章：$($signature.Status)",
    "安裝／捷徑／解除安裝生命週期：$installLifecycleStatus",
    "中文與空白路徑：PASS",
    "可寫 portable.flag：PASS",
    "不可寫 portable.flag 偵測：PASS",
    "乾淨 Windows 10／11 無 Runtime：待 Windows Sandbox 或外部乾淨設備人工驗收",
    "125%／150%／200% DPI：已啟用 PerMonitorV2；待各縮放比例人工視覺驗收"
)
$report | Set-Content -LiteralPath $reportPath -Encoding UTF8
$report | ForEach-Object { Write-Host $_ }
