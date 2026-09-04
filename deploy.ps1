<#
.SYNOPSIS
    把目前程式碼發佈成單一 EXE，並更新自己機器上開機自啟所使用的版本。

.DESCRIPTION
    開機自啟指向 %LOCALAPPDATA%\HealthReminder\HealthReminder.exe。
    改完程式碼若沒有同步這個檔案，下次開機跑的仍然是舊版。
    本腳本負責：驗證 → 發佈單檔 → 停掉執行中的舊版 → 覆蓋 → 重新啟動。

    要產生給親友的安裝包請改用 build-release.ps1。

.EXAMPLE
    .\deploy.ps1
    .\deploy.ps1 -SkipVerification -NoRestart
#>
[CmdletBinding()]
param(
    [switch]$SkipVerification,
    [switch]$NoRestart
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot 'HealthReminder.csproj'
$verificationProject = Join-Path $projectRoot 'Verification\HealthReminder.Verification.csproj'
$publishDirectory = Join-Path $projectRoot 'artifacts\publish\win-x64-single-file'
$publishedExe = Join-Path $publishDirectory 'HealthReminder.exe'
$installDirectory = Join-Path $env:LOCALAPPDATA 'HealthReminder'
$installedExe = Join-Path $installDirectory 'HealthReminder.exe'
$processName = 'HealthReminder'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet 執行失敗，結束碼：$LASTEXITCODE"
    }
}

function Stop-RunningApp {
    $running = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
    if ($running.Count -eq 0) {
        Write-Host '    沒有執行中的實例。'
        return $false
    }

    Write-Host "    停止 $($running.Count) 個執行中的實例..."
    $running | Stop-Process -Force
    # 等待行程真正結束，否則接下來的覆蓋會因為檔案被鎖定而失敗。
    if (-not $running.WaitForExit(10000)) {
        throw '等待舊版結束逾時，請手動關閉後重試。'
    }

    return $true
}

function Copy-WithRetry {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    # 行程結束後檔案鎖有時會延遲數百毫秒才釋放，因此重試幾次。
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Copy-Item -LiteralPath $Source -Destination $Destination -Force
            return
        }
        catch [System.IO.IOException] {
            if ($attempt -eq 5) { throw }
            Write-Host "    檔案仍被鎖定，第 $attempt 次重試..."
            Start-Sleep -Milliseconds 600
        }
    }
}

Push-Location $projectRoot
try {
    $totalSteps = if ($SkipVerification) { 3 } else { 4 }
    $step = 0

    if (-not $SkipVerification) {
        $step++
        Write-Host "$step/$totalSteps 執行 Verification..." -ForegroundColor Cyan
        Invoke-DotNet @('run', '--project', $verificationProject, '-c', 'Release')
    }

    $step++
    Write-Host "$step/$totalSteps 發佈 self-contained 單一 EXE..." -ForegroundColor Cyan
    Invoke-DotNet @(
        'publish',
        $projectFile,
        '-c',
        'Release',
        '-p:PublishProfile=PortableSingleFile',
        "-p:PublishDir=$publishDirectory\"
    )

    if (-not (Test-Path -LiteralPath $publishedExe -PathType Leaf)) {
        throw "發佈完成後找不到執行檔：$publishedExe"
    }

    $step++
    Write-Host "$step/$totalSteps 更新 $installDirectory ..." -ForegroundColor Cyan
    $wasRunning = Stop-RunningApp

    if (-not (Test-Path -LiteralPath $installDirectory)) {
        New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
    }

    Copy-WithRetry -Source $publishedExe -Destination $installedExe

    $sourceHash = (Get-FileHash -LiteralPath $publishedExe -Algorithm SHA256).Hash
    $installedHash = (Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash
    if ($sourceHash -ne $installedHash) {
        throw '覆蓋後的檔案與發佈成品不一致，請重新執行。'
    }

    $step++
    if ($NoRestart) {
        Write-Host "$step/$totalSteps 已略過重新啟動（-NoRestart）。" -ForegroundColor Yellow
    }
    else {
        Write-Host "$step/$totalSteps 重新啟動..." -ForegroundColor Cyan
        # 從正式位置啟動，SyncStartWithWindowsPath() 會順便把開機自啟登錄項校正到這個路徑。
        Start-Process -FilePath $installedExe
        Start-Sleep -Seconds 2
        $started = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
        if ($started.Count -eq 0) {
            Write-Warning '啟動後找不到行程，請手動確認。'
        }
        elseif (-not $wasRunning) {
            Write-Host '    已在系統托盤背景執行。'
        }
    }

    $sizeMb = [math]::Round((Get-Item -LiteralPath $installedExe).Length / 1MB, 1)
    $runKey = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
    $autoStart = (Get-ItemProperty -Path $runKey -ErrorAction SilentlyContinue).HealthReminder

    Write-Host ''
    Write-Host '部署完成：' -ForegroundColor Green
    Write-Host "  執行檔：$installedExe ($sizeMb MB)"
    Write-Host "  SHA256：$($installedHash.Substring(0, 16))..."
    if ($autoStart) {
        Write-Host "  開機自啟：$autoStart"
    }
    else {
        Write-Host '  開機自啟：未設定（可在程式的「一般設定」中開啟）' -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}
