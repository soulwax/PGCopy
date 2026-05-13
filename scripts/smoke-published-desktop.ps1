# File: scripts/smoke-published-desktop.ps1

param(
    [string]$Runtime = "win-x64",
    [switch]$Launch
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $root "artifacts/PostgresCopy-desktop-$Runtime"
$appPath = Join-Path $publishDir "PostgresCopy.Desktop.exe"

if (-not (Test-Path $appPath)) {
    throw "Published desktop app not found at $appPath. Run .\scripts\publish-desktop.ps1 first."
}

$app = Get-Item $appPath
if ($app.Length -lt 10MB) {
    throw "Published desktop app looks too small for a self-contained single-file build: $([Math]::Round($app.Length / 1MB, 2)) MB."
}

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($appPath)
if ($version.ProductName -ne "PostgresCopy Desktop") {
    throw "Unexpected ProductName '$($version.ProductName)' in published exe metadata."
}

if ([string]::IsNullOrWhiteSpace($version.FileVersion)) {
    throw "Published exe is missing FileVersion metadata."
}

Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($appPath)
try {
    if ($null -eq $icon) {
        throw "Published exe has no extractable app icon."
    }

    if ($icon.Width -le 0 -or $icon.Height -le 0) {
        throw "Published exe app icon has invalid dimensions."
    }
}
finally {
    if ($null -ne $icon) {
        $icon.Dispose()
    }
}

Write-Host "Published desktop smoke check passed."
Write-Host "Path: $appPath"
Write-Host "Size: $([Math]::Round($app.Length / 1MB, 2)) MB"
Write-Host "Version: $($version.FileVersion)"

if ($Launch) {
    Write-Host "Launching published desktop app for visual smoke check..."
    $process = Start-Process -FilePath $appPath -WorkingDirectory $publishDir -PassThru
    Start-Sleep -Seconds 2

    if ($process.HasExited) {
        throw "Published desktop app exited immediately with code $($process.ExitCode)."
    }

    Write-Host "Published desktop app launched. Process ID: $($process.Id)"
    Write-Host "Verify the header/logo, app icon, Connection tab, Preflight tab, Peek tab, SSH tab, Save log button, and operations log."
}
