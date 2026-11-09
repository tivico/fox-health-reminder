[CmdletBinding()]
param(
    [string]$InnoCompiler,
    [switch]$SkipVerification
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = $PSScriptRoot
$projectFile = Join-Path $projectRoot 'HealthReminder.csproj'
$verificationProject = Join-Path $projectRoot 'Verification\HealthReminder.Verification.csproj'
$buildDirectory = Join-Path $projectRoot 'artifacts\build\release'
$publishBuildDirectory = Join-Path $buildDirectory 'win-x64'
$publishProfile = 'PortableSingleFile'
$publishDirectory = Join-Path $projectRoot 'artifacts\publish\win-x64-single-file'
$installerDirectory = Join-Path $projectRoot 'artifacts\installer'
$installerScript = Join-Path $projectRoot 'installer\HealthReminder.iss'
$quickGuide = Join-Path $projectRoot '使用說明.txt'
$releaseVerifier = Join-Path $projectRoot 'Verification\verify-release.ps1'
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))

$publishedFileNames = @('HealthReminder.exe')
$installerFileNames = @('狐狸健康提醒安裝程式.exe', '使用說明.txt')

function Get-ReleaseOutputFiles {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string[]]$AllowedFiles
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $expectedPrefix = $artifactsRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $resolvedPath.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "拒絕清理 artifacts 以外的路徑：$resolvedPath"
    }

    # 防止發布路徑經由 junction 或連結指向別處。
    for ($folder = $resolvedPath; ; $folder = Split-Path -Path $folder -Parent) {
        if (Test-Path -LiteralPath $folder) {
            $directory = Get-Item -LiteralPath $folder -Force
            if (-not $directory.PSIsContainer -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "發布路徑必須是一般資料夾：$folder"
            }
        }
        if ($folder -eq $artifactsRoot) { break }
    }

    if (-not (Test-Path -LiteralPath $resolvedPath)) { return }

    $entries = @(Get-ChildItem -LiteralPath $resolvedPath -Force)
    foreach ($entry in $entries) {
        if ($entry.PSIsContainer -or $entry.Name -notin $AllowedFiles -or
            ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "發布目錄包含非成品項目，請先移至其他位置：$($entry.FullName)"
        }
    }
    return $entries
}

function Clear-ReleaseDirectory {
    param([string]$Path, [string[]]$AllowedFiles)

    # 只刪除已知成品，保留可能仍被執行中程式使用的工作目錄。
    foreach ($file in Get-ReleaseOutputFiles -Path $Path -AllowedFiles $AllowedFiles) {
        Remove-Item -LiteralPath $file.FullName -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet 執行失敗，結束碼：$LASTEXITCODE"
    }
}

function Find-InnoCompiler {
    if ($InnoCompiler) {
        if (-not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
            throw "找不到指定的 Inno Setup 編譯器：$InnoCompiler"
        }

        return [System.IO.Path]::GetFullPath($InnoCompiler)
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )

    if (${env:ProgramFiles(x86)}) {
        $candidates += Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'
        $candidates += Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw @"
找不到 Inno Setup 編譯器 ISCC.exe。
請先從 https://jrsoftware.org/isdl.php 安裝 Inno Setup，
再重新執行 .\build-release.ps1。
"@
}

Push-Location $projectRoot
try {
    $compiler = Find-InnoCompiler
    Get-ReleaseOutputFiles -Path $publishDirectory -AllowedFiles $publishedFileNames | Out-Null
    Get-ReleaseOutputFiles -Path $installerDirectory -AllowedFiles $installerFileNames | Out-Null

    Write-Host '1/5 建置 Release...' -ForegroundColor Cyan
    # 中間建置與使用中的 bin 分開，避免背景程式鎖住 apphost.exe 而阻止發布。
    Invoke-DotNet @('build', $projectFile, '-c', 'Release', '-o', $buildDirectory)

    if (-not $SkipVerification) {
        Write-Host '2/5 執行 Verification...' -ForegroundColor Cyan
        Invoke-DotNet @('build', $verificationProject, '-c', 'Release', '-o', $buildDirectory)
        Invoke-DotNet @((Join-Path $buildDirectory 'HealthReminder.Verification.dll'))
    }
    else {
        Write-Host '2/5 已略過 Verification。' -ForegroundColor Yellow
    }

    Write-Host '3/5 產生 self-contained 單一 EXE...' -ForegroundColor Cyan
    Clear-ReleaseDirectory -Path $publishDirectory -AllowedFiles $publishedFileNames
    Invoke-DotNet @(
        'publish',
        $projectFile,
        '-c',
        'Release',
        "-p:PublishProfile=$publishProfile",
        "-p:OutputPath=$publishBuildDirectory\",
        "-p:PublishDir=$publishDirectory\"
    )

    $publishedExe = Join-Path $publishDirectory 'HealthReminder.exe'
    if (-not (Test-Path -LiteralPath $publishedExe -PathType Leaf)) {
        throw "發布完成後找不到執行檔：$publishedExe"
    }

    $publishedFiles = @(Get-ChildItem -LiteralPath $publishDirectory -File)
    if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].Name -ne 'HealthReminder.exe') {
        throw "正式發布目錄必須只包含 HealthReminder.exe，目前共有 $($publishedFiles.Count) 個檔案。"
    }

    Write-Host '4/5 產生親友安裝包...' -ForegroundColor Cyan
    Clear-ReleaseDirectory -Path $installerDirectory -AllowedFiles $installerFileNames
    & $compiler $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup 編譯失敗，結束碼：$LASTEXITCODE"
    }

    $installers = @(Get-ChildItem -LiteralPath $installerDirectory -File -Filter '*.exe')
    if ($installers.Count -ne 1) {
        throw "預期產生一個安裝程式，但找到 $($installers.Count) 個。"
    }

    Copy-Item -LiteralPath $quickGuide -Destination $installerDirectory

    Write-Host '5/5 驗證正式發布成品...' -ForegroundColor Cyan
    & $releaseVerifier

    Write-Host ''
    Write-Host '發行完成：' -ForegroundColor Green
    Write-Host "  程式：$publishedExe"
    Write-Host "  安裝包：$($installers[0].FullName)"
    Write-Host "  說明：$(Join-Path $installerDirectory '使用說明.txt')"
}
finally {
    Pop-Location
}
