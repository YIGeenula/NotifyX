<#
.SYNOPSIS
    NotifyX Build and Version Release Script
    Usage:
        .\build.ps1                 # Builds with current version
        .\build.ps1 -Version 1.0.2  # Updates version and builds release package
#>
param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

if (![string]::IsNullOrWhiteSpace($Version)) {
    # Remove 'v' if specified (e.g. v1.0.2 -> 1.0.2)
    $cleanVer = $Version.TrimStart('v', 'V').Trim()
    Write-Host "[-] Incrementing project version to $cleanVer..." -ForegroundColor Cyan

    $wpfProj = Join-Path $root "src\NotifyX.Wpf\NotifyX.Wpf.csproj"
    $coreProj = Join-Path $root "src\NotifyX.Core\NotifyX.Core.csproj"

    # Update Version in NotifyX.Wpf.csproj
    $wpfContent = Get-Content $wpfProj -Raw
    $wpfContent = [System.Text.RegularExpressions.Regex]::Replace($wpfContent, '<Version>.*?</Version>', "<Version>$cleanVer</Version>")
    $wpfContent = [System.Text.RegularExpressions.Regex]::Replace($wpfContent, '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$cleanVer.0</AssemblyVersion>")
    $wpfContent = [System.Text.RegularExpressions.Regex]::Replace($wpfContent, '<FileVersion>.*?</FileVersion>', "<FileVersion>$cleanVer.0</FileVersion>")
    Set-Content -Path $wpfProj -Value $wpfContent -NoNewline

    # Update Version in NotifyX.Core.csproj
    $coreContent = Get-Content $coreProj -Raw
    $coreContent = [System.Text.RegularExpressions.Regex]::Replace($coreContent, '<Version>.*?</Version>', "<Version>$cleanVer</Version>")
    $coreContent = [System.Text.RegularExpressions.Regex]::Replace($coreContent, '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$cleanVer.0</AssemblyVersion>")
    $coreContent = [System.Text.RegularExpressions.Regex]::Replace($coreContent, '<FileVersion>.*?</FileVersion>', "<FileVersion>$cleanVer.0</FileVersion>")
    Set-Content -Path $coreProj -Value $coreContent -NoNewline

    Write-Host "[✓] Version updated to $cleanVer in project definitions." -ForegroundColor Green
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   REBUILDING NOTIFYX PAYMENT REMINDER (.NET 10 WPF)    " -ForegroundColor Magenta
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[-] Publishing Release single-file executable..." -ForegroundColor Yellow
$project = Join-Path $root "src\NotifyX.Wpf\NotifyX.Wpf.csproj"
$publishDir = Join-Path $root "publish"
$installerDir = Join-Path $root "installer"

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
dotnet publish $project -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "[-] Updating installer and distribution packages..." -ForegroundColor Yellow
if (-not (Test-Path $installerDir)) { New-Item -ItemType Directory -Path $installerDir -Force | Out-Null }

Copy-Item -Path "$publishDir\*" -Destination $installerDir -Recurse -Force

# Read current version from assembly or project
[xml]$xml = Get-Content $project
$currentVer = $xml.Project.PropertyGroup.Version | Select-Object -First 1

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "   BUILD COMPLETED SUCCESSFULLY! (v$currentVer)         " -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host " Output: " -ForegroundColor Gray
Write-Host "  -> $publishDir\NotifyX.exe" -ForegroundColor White
Write-Host "  -> $installerDir\NotifyX.exe" -ForegroundColor White
Write-Host ""
Write-Host "[TIP] To release this update on GitHub:" -ForegroundColor Cyan
Write-Host "  1. Upload '$publishDir\NotifyX.exe' as a release asset under tag 'v$currentVer'" -ForegroundColor Gray
Write-Host "  2. Users can click 'Check for Updates' inside NotifyX to update directly!" -ForegroundColor Gray
Write-Host ""
