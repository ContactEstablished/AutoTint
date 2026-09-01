<#
.SYNOPSIS
    Installs AutoTint for the current user.

.DESCRIPTION
    Publishes a fresh build, copies it to %LOCALAPPDATA%\Programs\AutoTint, adds a Start
    Menu shortcut, registers it so it appears in Settings > Installed apps with a working
    uninstall, and (by default) sets it to start with Windows.

    Per-user by design: no administrator rights, nothing written outside your profile, and
    no external installer toolchain required.

.PARAMETER NoAutostart
    Install without adding the start-with-Windows entry.

.PARAMETER NoLaunch
    Install without starting the app afterwards.
#>
[CmdletBinding()]
param(
    [switch]$NoAutostart,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

$AppName     = 'AutoTint'
$RepoRoot    = Split-Path $PSScriptRoot -Parent
$ProjectPath = Join-Path $RepoRoot 'src\AutoTint\AutoTint.csproj'
$InstallDir  = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
$ExePath     = Join-Path $InstallDir "$AppName.exe"
$ShortcutDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$Shortcut    = Join-Path $ShortcutDir "$AppName.lnk"
$UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
$RunKey      = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

function Step($text) { Write-Host "  $text" }

if (-not (Test-Path $ProjectPath)) {
    throw "Cannot find $ProjectPath. Run this from a checkout of the AutoTint repository."
}

# Version comes from the project file so the two can never disagree.
$csproj = Get-Content $ProjectPath -Raw
if ($csproj -notmatch '<Version>([^<]+)</Version>') { throw 'No <Version> found in the project file.' }
$Version = $Matches[1].Trim()

Write-Host ""
Write-Host "Installing $AppName $Version for $env:USERNAME" -ForegroundColor Cyan
Write-Host ""

# The published build is framework-dependent, so the runtime has to be present.
$hasRuntime = (& dotnet --list-runtimes 2>$null) -match 'Microsoft\.WindowsDesktop\.App 10\.'
if (-not $hasRuntime) {
    Write-Warning 'The .NET 10 Desktop Runtime was not found. AutoTint will not start without it.'
    Write-Warning 'Get it from https://dotnet.microsoft.com/download/dotnet/10.0 (Desktop Runtime, x64).'
}

Step 'Building a fresh release...'
$staging = Join-Path ([IO.Path]::GetTempPath()) "autotint-install-$([guid]::NewGuid().ToString('N'))"
& dotnet publish $ProjectPath -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -o $staging --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

$stagedExe = Join-Path $staging "$AppName.exe"
if (-not (Test-Path $stagedExe)) { throw "Build did not produce $stagedExe." }

# A running copy holds its own executable open.
$running = Get-Process -Name $AppName -ErrorAction SilentlyContinue
if ($running) {
    Step 'Stopping the running copy...'
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

Step "Copying to $InstallDir"
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item $stagedExe $ExePath -Force
Copy-Item (Join-Path $PSScriptRoot 'Uninstall.ps1') (Join-Path $InstallDir 'Uninstall.ps1') -Force
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

Step 'Adding a Start Menu shortcut'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($Shortcut)
$link.TargetPath       = $ExePath
$link.WorkingDirectory = $InstallDir
$link.IconLocation     = $ExePath
$link.Description      = 'Dim a too-bright window during video calls'
$link.Save()

Step 'Registering with Installed apps'
$sizeKb = [int]((Get-Item $ExePath).Length / 1KB)
New-Item -Path $UninstallKey -Force | Out-Null
$uninstallCommand =
    "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $InstallDir 'Uninstall.ps1')`""
Set-ItemProperty $UninstallKey 'DisplayName'     "$AppName"
Set-ItemProperty $UninstallKey 'DisplayVersion'  "$Version"
Set-ItemProperty $UninstallKey 'Publisher'       'Matthew Wilson'
Set-ItemProperty $UninstallKey 'DisplayIcon'     "$ExePath"
Set-ItemProperty $UninstallKey 'InstallLocation' "$InstallDir"
Set-ItemProperty $UninstallKey 'UninstallString' $uninstallCommand
Set-ItemProperty $UninstallKey 'EstimatedSize'   $sizeKb -Type DWord
Set-ItemProperty $UninstallKey 'NoModify'        1 -Type DWord
Set-ItemProperty $UninstallKey 'NoRepair'        1 -Type DWord

if ($NoAutostart) {
    Step 'Skipping start-with-Windows'
} else {
    Step 'Setting it to start with Windows'
    Set-ItemProperty $RunKey $AppName "`"$ExePath`""
}

Write-Host ""
Write-Host "Installed to $InstallDir" -ForegroundColor Green
Write-Host "  Start Menu   : $AppName"
Write-Host "  Installed apps: $AppName $Version (uninstall from Settings, or run Uninstall.ps1)"
if (-not $NoAutostart) {
    Write-Host "  Startup      : enabled -- appears in Task Manager > Startup apps"
}
Write-Host ""

if (-not $NoLaunch) {
    Step 'Starting AutoTint...'
    Start-Process $ExePath
}
