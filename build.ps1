[CmdletBinding()]
param(
    [switch]$Installer,
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

function Stop-Build([string]$Message) {
    Write-Host "ОШИБКА: $Message" -ForegroundColor Red
    exit 1
}

function Invoke-External([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) {
        Stop-Build "$Program завершился с кодом $LASTEXITCODE"
    }
}

if ($env:OS -ne 'Windows_NT') {
    Stop-Build 'Сборка доступна только на Windows.'
}

$dotnet = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
if (-not $dotnet) {
    $dotnet = Get-Command 'dotnet' -ErrorAction SilentlyContinue
}
if (-not $dotnet) {
    Stop-Build 'Не найден .NET SDK 8 или новее. Установи .NET SDK и повтори запуск.'
}

$sdkVersion = (& $dotnet.Source --version).Trim()
if ($sdkVersion -notmatch '^(\d+)\.' -or [int]$Matches[1] -lt 8) {
    Stop-Build "Нужен .NET SDK версии 8 или новее. Обнаружена версия: $sdkVersion"
}
Write-Host "Используется .NET SDK $sdkVersion; проект собирается под .NET 8." -ForegroundColor DarkGray
Write-Host "WPF XAML-файлы App.xaml и MainWindow.xaml подключаются явно, без автоматического поиска." -ForegroundColor DarkGray

$project = Join-Path $PSScriptRoot 'StageCue.csproj'
if (-not (Test-Path -LiteralPath $project)) {
    Stop-Build 'Не найден StageCue.csproj.'
}

$artifacts = Join-Path $PSScriptRoot 'artifacts'
$portable = Join-Path $artifacts 'portable'
$installerDir = Join-Path $artifacts 'installer'

if (Test-Path -LiteralPath $portable) {
    Remove-Item -LiteralPath $portable -Recurse -Force
}
New-Item -Path $portable -ItemType Directory -Force | Out-Null
New-Item -Path $installerDir -ItemType Directory -Force | Out-Null

Write-Host ''
Write-Host '[1/3] Сборка автономного StageCue.exe' -ForegroundColor Cyan

$publishArgs = @(
    'publish',
    $project,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '--output', $portable
)

if ($SkipRestore) {
    $publishArgs += '--no-restore'
}

Invoke-External $dotnet.Source $publishArgs

$exe = Join-Path $portable 'StageCue.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    Stop-Build 'После публикации StageCue.exe не найден.'
}

$readme = Join-Path $PSScriptRoot 'README_RU.md'
$logo = Join-Path $PSScriptRoot 'Assets/StageCueLogo.svg'
if (Test-Path -LiteralPath $readme) {
    Copy-Item -LiteralPath $readme -Destination $portable -Force
}
if (Test-Path -LiteralPath $logo) {
    Copy-Item -LiteralPath $logo -Destination $portable -Force
}

Write-Host 'Автономный EXE создан.' -ForegroundColor Green

if ($Installer) {
    Write-Host ''
    Write-Host '[2/3] Поиск Inno Setup 6' -ForegroundColor Cyan

    $iscc = $null
    $programFiles = [Environment]::GetFolderPath('ProgramFiles')
    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
    $possiblePaths = @()

    if ($programFiles) {
        $possiblePaths += (Join-Path $programFiles 'Inno Setup 6/ISCC.exe')
    }
    if ($programFilesX86) {
        $possiblePaths += (Join-Path $programFilesX86 'Inno Setup 6/ISCC.exe')
    }

    foreach ($candidate in $possiblePaths) {
        if (Test-Path -LiteralPath $candidate) {
            $iscc = $candidate
            break
        }
    }

    if (-not $iscc) {
        $isccCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
        if ($isccCommand) {
            $iscc = $isccCommand.Source
        }
    }

    if (-not $iscc) {
        Write-Host 'Inno Setup 6 не найден. Пробую установить через winget...' -ForegroundColor Yellow
        $winget = Get-Command 'winget.exe' -ErrorAction SilentlyContinue

        if ($winget) {
            & $winget.Source install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
            if ($LASTEXITCODE -eq 0) {
                foreach ($candidate in $possiblePaths) {
                    if (Test-Path -LiteralPath $candidate) {
                        $iscc = $candidate
                        break
                    }
                }
            }
        }
    }

    if ($iscc) {
        Write-Host ''
        Write-Host '[3/3] Создание установщика StageCue-Setup-x64.exe' -ForegroundColor Cyan
        $iss = Join-Path $PSScriptRoot 'installer/StageCue.iss'
        Invoke-External $iscc @('/Qp', $iss)

        $setup = Join-Path $installerDir 'StageCue-Setup-x64.exe'
        if (-not (Test-Path -LiteralPath $setup)) {
            Stop-Build 'Inno Setup завершился, но установщик не найден.'
        }

        $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
        Set-Content -LiteralPath (Join-Path $installerDir 'SHA256.txt') -Value $hash -Encoding ASCII
        Write-Host "Установщик: $setup" -ForegroundColor Green
        Write-Host "SHA-256: $hash"
    }
    else {
        Write-Host 'Inno Setup 6 не установлен. Автономный EXE готов, установщик пока пропущен.' -ForegroundColor Yellow
        Write-Host 'Установи Inno Setup 6 с https://jrsoftware.org/isdl.php и запусти build.cmd повторно.'
    }
}
else {
    Write-Host 'Создание установщика пропущено: параметр -Installer не указан.'
}

Write-Host ''
Write-Host 'СБОРКА ЗАВЕРШЕНА.' -ForegroundColor Green
Write-Host "Автономное приложение: $exe"
Write-Host "Папка результатов: $artifacts"
