<#
.SYNOPSIS
    Removes AutoTint for the current user.

.DESCRIPTION
    Stops the app, removes the start-with-Windows entry, the Start Menu shortcut, the
    Installed apps registration, and the program folder.

    Your settings in %APPDATA%\AutoTint are kept unless -RemoveSettings is given, so
    reinstalling puts the panel back where you had it.

.PARAMETER RemoveSettings
    Also delete saved settings (position, size, strength, colour, toggles).

.NOTES
    Written for Windows PowerShell 5.1, since Windows invokes this via UninstallString.
#>
[CmdletBinding()]
param(
    [switch]$RemoveSettings
)

$ErrorActionPreference = 'Stop'

$AppName      = 'AutoTint'
$InstallDir   = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
$Shortcut     = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$AppName.lnk"
$UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
$RunKey       = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$SettingsDir  = Join-Path $env:APPDATA $AppName

function Step($text) { Write-Host "  $text" }

Write-Host ""
Write-Host "Uninstalling $AppName" -ForegroundColor Cyan
Write-Host ""

$running = Get-Process -Name $AppName -ErrorAction SilentlyContinue
if ($running) {
    Step 'Stopping AutoTint...'
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

Step 'Removing the start-with-Windows entry'
if (Get-ItemProperty -Path $RunKey -Name $AppName -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $RunKey -Name $AppName -Force -ErrorAction SilentlyContinue
}

Step 'Removing the Start Menu shortcut'
if (Test-Path $Shortcut) { Remove-Item $Shortcut -Force -ErrorAction SilentlyContinue }

Step 'Removing the Installed apps registration'
if (Test-Path $UninstallKey) { Remove-Item $UninstallKey -Recurse -Force -ErrorAction SilentlyContinue }

if ($RemoveSettings) {
    Step 'Removing saved settings'
    if (Test-Path $SettingsDir) { Remove-Item $SettingsDir -Recurse -Force -ErrorAction SilentlyContinue }
} else {
    Step "Keeping saved settings in $SettingsDir (use -RemoveSettings to delete them)"
}

Write-Host ""
Write-Host "$AppName has been removed." -ForegroundColor Green
Write-Host ""

# This script lives inside the folder being deleted, so the folder is removed by a
# detached process once this one has exited.
if (Test-Path $InstallDir) {
    $command = "Start-Sleep -Seconds 2; Remove-Item -LiteralPath '$InstallDir' -Recurse -Force -ErrorAction SilentlyContinue"
    Start-Process powershell.exe `
        -ArgumentList '-NoProfile', '-WindowStyle', 'Hidden', '-Command', $command `
        -WindowStyle Hidden
}
