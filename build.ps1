<#
.SYNOPSIS
    Rebuilds NotifyX Payment Reminder WPF single-file executable and syncs installer packages.
#>

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   REBUILDING NOTIFYX PAYMENT REMINDER (.NET 10 WPF)    " -ForegroundColor Magenta
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[-] Publishing Release single-file executable..." -ForegroundColor Yellow
$project = Join-Path $root "src\NotifyX.Wpf\NotifyX.Wpf.csproj"
$publishDir = Join-Path $root "publish"
$installerDir = Join-Path $root "installer"
$installerDistDir = Join-Path $root "installer_dist"

dotnet publish $project -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "[-] Updating installer and distribution packages..." -ForegroundColor Yellow
if (-not (Test-Path $installerDir)) { New-Item -ItemType Directory -Path $installerDir -Force | Out-Null }
if (-not (Test-Path $installerDistDir)) { New-Item -ItemType Directory -Path $installerDistDir -Force | Out-Null }

Copy-Item -Path "$publishDir\*" -Destination $installerDir -Recurse -Force
Copy-Item -Path "$publishDir\*" -Destination $installerDistDir -Recurse -Force

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "   BUILD COMPLETED SUCCESSFULLY!                        " -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host " Output: " -ForegroundColor Gray
Write-Host "  -> $publishDir\NotifyX.exe" -ForegroundColor White
Write-Host "  -> $installerDir\NotifyX.exe" -ForegroundColor White
Write-Host ""
