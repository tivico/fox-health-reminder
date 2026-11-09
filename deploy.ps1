<#
.SYNOPSIS
    把目前程式碼發佈成單一 EXE，並更新自己機器上開機自啟所使用的版本。

.DESCRIPTION
    開機自啟指向 %LOCALAPPDATA%\HealthReminder\HealthReminder.exe。
    改完程式碼若沒有同步這個檔案，下次開機跑的仍然是舊版。
    驗證新檔後只更新自用路徑。若新版本啟動失敗，會嘗試還原上一版。

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
$buildDirectory = Join-Path $projectRoot 'artifacts\deploy\build'
$publishBuildDirectory = Join-Path $buildDirectory 'win-x64'
$publishDirectory = Join-Path $projectRoot 'artifacts\deploy\win-x64-single-file'
$publishedExe = Join-Path $publishDirectory 'HealthReminder.exe'
$installDirectory = Join-Path $env:LOCALAPPDATA 'HealthReminder'
$installedExe = Join-Path $installDirectory 'HealthReminder.exe'
$processName = 'HealthReminder'
$backupExe = "$installedExe.bak"

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet 執行失敗，結束碼：$LASTEXITCODE"
    }
}

function Stop-RunningApp {
    $session = [System.Diagnostics.Process]::GetCurrentProcess().SessionId
    $running = @(Get-Process -Name $processName -ErrorAction SilentlyContinue |
        Where-Object { $_.SessionId -eq $session -and $_.Path -eq $installedExe })

    foreach ($process in $running) {
        Write-Host "    停止目標路徑的行程 $($process.Id)..."
        Stop-Process -InputObject $process -ErrorAction Stop
        if (-not $process.WaitForExit(10000)) {
            throw '等待舊版結束逾時，請從系統匣結束後重試。'
        }
    }
    return $running.Count -gt 0
}

function Test-PublishedApp {
    $process = Start-Process -FilePath $publishedExe -ArgumentList '--smoke-test' -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(15000)) {
        Stop-Process -InputObject $process -ErrorAction SilentlyContinue
        throw '新版本的啟動檢查逾時。'
    }
    if ($process.ExitCode -ne 0) {
        throw "新版本的啟動檢查失敗，結束碼：$($process.ExitCode)"
    }
}

function Start-InstalledApp {
    $process = Start-Process -FilePath $installedExe -ArgumentList '--deployment-start' -PassThru
    if ($process.WaitForExit(2000) -or $process.Path -ne $installedExe) {
        throw '啟動後找不到新版本的行程，請確認是否有其他位置的健康提醒正在執行。'
    }
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
try {
    $deploymentLock = [IO.File]::Open((Join-Path $installDirectory 'deploy.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
}
catch [IO.IOException] {
    throw "無法鎖定部署目錄，請確認其他部署已結束。$($_.Exception.Message)"
}

Push-Location $projectRoot
try {
    $totalSteps = if ($SkipVerification) { 3 } else { 4 }
    $step = 0

    if (-not $SkipVerification) {
        $step++
        Write-Host "$step/$totalSteps 執行 Verification..." -ForegroundColor Cyan
        Invoke-DotNet @('build', $verificationProject, '-c', 'Release', '-o', $buildDirectory)
        Invoke-DotNet @((Join-Path $buildDirectory 'HealthReminder.Verification.dll'))
    }

    $step++
    Write-Host "$step/$totalSteps 發佈 self-contained 單一 EXE..." -ForegroundColor Cyan
    Invoke-DotNet @(
        'publish',
        $projectFile,
        '-c',
        'Release',
        '-p:PublishProfile=PortableSingleFile',
        "-p:OutputPath=$publishBuildDirectory\",
        "-p:PublishDir=$publishDirectory"
    )

    if (-not (Test-Path -LiteralPath $publishedExe -PathType Leaf)) {
        throw "發佈完成後找不到執行檔：$publishedExe"
    }

    Test-PublishedApp

    $step++
    Write-Host "$step/$totalSteps 更新 $installDirectory ..." -ForegroundColor Cyan
    $stagedExe = Join-Path $installDirectory ("HealthReminder." + [guid]::NewGuid() + ".tmp")
    $hadPrevious = Test-Path -LiteralPath $installedExe -PathType Leaf
    $wasRunning = $false
    $replaced = $false
    try {
        Copy-Item -LiteralPath $publishedExe -Destination $stagedExe
        $installedHash = (Get-FileHash -LiteralPath $stagedExe -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $publishedExe -Algorithm SHA256).Hash -ne $installedHash) {
            throw '待安裝的檔案與發佈成品不一致。'
        }

        $wasRunning = Stop-RunningApp
        if ($hadPrevious) {
            [IO.File]::Replace($stagedExe, $installedExe, $backupExe)
        }
        else {
            [IO.File]::Move($stagedExe, $installedExe)
        }
        $replaced = $true

        $step++
        if ($NoRestart) {
            Write-Host "$step/$totalSteps 已略過重新啟動（-NoRestart）。" -ForegroundColor Yellow
        }
        else {
            Write-Host "$step/$totalSteps 重新啟動..." -ForegroundColor Cyan
            Start-InstalledApp
        }
    }
    catch {
        if ($replaced -and $hadPrevious) {
            try {
                [IO.File]::Replace($backupExe, $installedExe, [NullString]::Value)
                Write-Warning '啟動失敗，已還原上一版。'
            }
            catch {
                Write-Warning "無法自動還原上一版，請確認備份：$backupExe"
                throw
            }
        }
        elseif ($replaced) {
            Remove-Item -LiteralPath $installedExe -Force
        }
        if ($wasRunning) {
            try { Start-InstalledApp }
            catch { Write-Warning '請手動啟動舊版。' }
        }
        throw
    }
    finally {
        if (Test-Path -LiteralPath $stagedExe) {
            Remove-Item -LiteralPath $stagedExe -Force
        }
    }

    $sizeMb = [math]::Round((Get-Item -LiteralPath $installedExe).Length / 1MB, 1)
    $runKey = Get-Item -LiteralPath 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue
    $autoStart = if ($runKey) { $runKey.GetValue('HealthReminder') }
    if ($autoStart -and $autoStart -ne ('"' + $installedExe + '"')) {
        Write-Warning '開機自啟指向另一個位置，若要改用此版本，請從此版本的設定頁關閉自啟後再重新開啟。'
    }

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
    $deploymentLock.Dispose()
}
