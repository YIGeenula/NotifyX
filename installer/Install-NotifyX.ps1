<#
.SYNOPSIS
    NotifyX - Payment Reminder - Automated Windows Installer
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Elevate script to Administrator if not already running as Admin
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Requesting Administrator privileges to install NotifyX - Payment Reminder..." -ForegroundColor Cyan
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -NoProfile -File `"$PSCommandPath`""
    exit
}

Clear-Host
Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host "        NOTIFYX - PAYMENT REMINDER - WINDOWS INSTALLER     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host ""

$sourceDir = Join-Path $PSScriptRoot "publish"
if (-not (Test-Path $sourceDir)) {
    $sourceDir = $PSScriptRoot
}

$installDir = "C:\Program Files\NotifyX\PaymentReminder"

Write-Host "[-] Installing to: $installDir" -ForegroundColor Yellow

# 1. Stop any currently running instance
Stop-Process -Name "CodexBlaze.PaymentReminder.Wpf" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "NotifyX.PaymentReminder.Wpf" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# 2. Create Target Directory
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# 3. Copy files
Write-Host "[-] Copying application binaries and assets..." -ForegroundColor Yellow
Copy-Item -Path "$sourceDir\*" -Destination $installDir -Recurse -Force

$mainExe = Join-Path $installDir "NotifyX.exe"
$iconPath = Join-Path $installDir "NotifyX_icon.ico"
if (-not (Test-Path $iconPath)) {
    $sourceIcon = Join-Path $PSScriptRoot "NotifyX_icon.ico"
    if (Test-Path $sourceIcon) {
        Copy-Item -Path $sourceIcon -Destination $iconPath -Force
    }
}
$shortcutIcon = if (Test-Path $iconPath) { "$iconPath,0" } else { "$mainExe,0" }

# 4. Create Desktop Shortcut
Write-Host "[-] Creating Desktop Shortcut with NotifyX Icon..." -ForegroundColor Yellow
$WshShell = New-Object -ComObject WScript.Shell
$desktopPath = [Environment]::GetFolderPath("Desktop")
$shortcut = $WshShell.CreateShortcut("$desktopPath\NotifyX Payment Reminder.lnk")
$shortcut.TargetPath = $mainExe
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = $shortcutIcon
$shortcut.Description = "NotifyX Payment Reminder Desktop Application"
$shortcut.Save()

# Clean legacy desktop shortcut if exists
$oldDesktopShortcut = "$desktopPath\CodexBlaze Payment Reminder.lnk"
if (Test-Path $oldDesktopShortcut) { Remove-Item $oldDesktopShortcut -Force -ErrorAction SilentlyContinue }

# 5. Create Start Menu Shortcut
Write-Host "[-] Creating Start Menu Shortcut with NotifyX Icon..." -ForegroundColor Yellow
$startMenuPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\NotifyX"
if (-not (Test-Path $startMenuPath)) {
    New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
}
$startShortcut = $WshShell.CreateShortcut("$startMenuPath\NotifyX Payment Reminder.lnk")
$startShortcut.TargetPath = $mainExe
$startShortcut.WorkingDirectory = $installDir
$startShortcut.IconLocation = $shortcutIcon
$startShortcut.Description = "NotifyX Payment Reminder Desktop Application"
$startShortcut.Save()

# 6. Configure Windows Startup to run in Background on Boot
Write-Host "[-] Configuring Windows Startup Registry..." -ForegroundColor Yellow
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$startCommand = "`"$mainExe`" --minimized"
Set-ItemProperty -Path $regPath -Name "NotifyXPaymentReminder" -Value $startCommand
Remove-ItemProperty -Path $regPath -Name "CodexBlazePaymentReminder" -ErrorAction SilentlyContinue

# 7. Add to Add/Remove Programs (Control Panel & Windows Settings)
Write-Host "[-] Registering in Windows Add/Remove Programs..." -ForegroundColor Yellow
$uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NotifyXPaymentReminder"
if (-not (Test-Path $uninstallKey)) {
    New-Item -Path $uninstallKey -Force | Out-Null
}
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "NotifyX - Payment Reminder"
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.1"
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "NotifyX"
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $shortcutIcon
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -NoProfile -Command `"Stop-Process -Name 'CodexBlaze.PaymentReminder.Wpf' -Force -ErrorAction SilentlyContinue; Remove-Item 'C:\Program Files\NotifyX' -Recurse -Force; Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'NotifyXPaymentReminder' -ErrorAction SilentlyContinue; Remove-Item 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NotifyXPaymentReminder' -Force -ErrorAction SilentlyContinue; [System.Windows.Forms.MessageBox]::Show('NotifyX Payment Reminder uninstalled successfully!','Uninstall Complete');`""

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   INSTALLATION COMPLETED SUCCESSFULLY!                  " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "1. Desktop shortcut created." -ForegroundColor Gray
Write-Host "2. Start Menu shortcut created." -ForegroundColor Gray
Write-Host "3. Configured to run automatically in background on PC boot." -ForegroundColor Gray
Write-Host ""

# Ask to launch
$launch = Read-Host "Do you want to launch NotifyX Payment Reminder now? (Y/N)"
if ($launch -match "^[Yy]") {
    Start-Process -FilePath $mainExe
}
