# File: scripts/uninstall-desktop.ps1

param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\PostgresCopy"),
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "The desktop uninstaller is Windows-only."
}

$installDirFull = [System.IO.Path]::GetFullPath($InstallDir)
$localAppDataRoot = [System.IO.Path]::GetFullPath($env:LOCALAPPDATA)
$programsRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "Programs"))
$localAppDataRootWithSlash = $localAppDataRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$programsRootWithSlash = $programsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$roamingAppDataRootWithSlash = [System.IO.Path]::GetFullPath($env:APPDATA).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $installDirFull.StartsWith($programsRootWithSlash, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to uninstall from outside the per-user Programs directory: $installDirFull"
}

$pathsToRemove = @(
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\PostgresCopy.lnk"),
    (Join-Path $env:LOCALAPPDATA "PostgresCopy")
)

$uninstallKeys = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PostgresCopy",
    "HKCU:\Software\PostgresCopy"
)

foreach ($key in $uninstallKeys) {
    if (Test-Path $key) {
        Remove-Item -LiteralPath $key -Recurse -Force
    }
}

Get-Process -Name "PostgresCopy.Desktop" -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

foreach ($path in $pathsToRemove) {
    if (-not (Test-Path $path)) {
        continue
    }

    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($localAppDataRootWithSlash, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $fullPath.StartsWith($roamingAppDataRootWithSlash, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected path: $fullPath"
    }

    Remove-Item -LiteralPath $fullPath -Recurse -Force
}

if (Test-Path $installDirFull) {
    $cleanupScript = Join-Path $env:TEMP "PostgresCopy-uninstall-cleanup.cmd"
    $cmd = @"
@echo off
for /l %%i in (1,1,20) do (
  rmdir /s /q "$installDirFull" 2>nul
  if not exist "$installDirFull" goto done
  timeout /t 1 /nobreak >nul
)
:done
del "%~f0" 2>nul
"@
    Set-Content -LiteralPath $cleanupScript -Value $cmd -Encoding ASCII
    Start-Process -FilePath $cleanupScript -WindowStyle Hidden
}

if (-not $Quiet) {
    Write-Host "PostgresCopy uninstalled. App files, Start Menu shortcut, uninstall entry, and local PostgresCopy data were removed."
}
