# File: scripts/build-installer.ps1
#
# Builds a per-user, no-admin NSIS installer for the PostgresCopy desktop
# app: PostgresCopy-Setup-<version>.exe. Requires NSIS (makensis) to be
# installed separately — this script does not fetch or install NSIS itself.
#
# Usage:
#   .\scripts\build-installer.ps1                  # publish (if needed) + build installer
#   .\scripts\build-installer.ps1 -Runtime win-arm64
#   .\scripts\build-installer.ps1 -SkipPublish     # reuse an existing published exe

param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "The NSIS installer build is Windows-only."
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $root "artifacts\PostgresCopy-desktop-$Runtime"
$appSource = Join-Path $publishDir "PostgresCopy.Desktop.exe"
$installerSourceDir = Join-Path $root "installer"
$stageDir = Join-Path $root "artifacts\installer-stage"
$outputDir = Join-Path $root "artifacts\dist"

# --- Step 1: locate makensis ---

$makensisPath = $null
$onPath = Get-Command makensis -ErrorAction SilentlyContinue
if ($onPath) {
    $makensisPath = $onPath.Source
}
else {
    $candidates = @(
        "$env:ProgramFiles\NSIS\makensis.exe",
        "${env:ProgramFiles(x86)}\NSIS\makensis.exe"
    )
    $makensisPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $makensisPath) {
    throw @"
NSIS (makensis) was not found on PATH or in the usual install locations.

Install it first, then re-run this script:
  winget install NSIS.NSIS
or download from https://nsis.sourceforge.io/Download

This script does not install NSIS automatically, since (unlike the pg-tools
zip-extraction helper) NSIS ships as a real installer that would run on this
machine as a side effect of a build script.
"@
}

Write-Host "makensis: $makensisPath"

# --- Step 2: publish the desktop app if needed ---

if (-not $SkipPublish -or -not (Test-Path -LiteralPath $appSource)) {
    Write-Host "Publishing desktop app ($Runtime, $Configuration)..."
    & (Join-Path $root "scripts\publish-desktop.ps1") -Runtime $Runtime -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "publish-desktop.ps1 failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $appSource)) {
    throw "Published desktop app not found at $appSource."
}

# --- Step 3: resolve the version to embed in the filename/metadata ---

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($appSource).FileVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.0.0"
}
# Trim a trailing ".0" fourth segment (FileVersion is X.Y.Z.0) to match the
# csproj's three-part <Version> for a cleaner installer filename.
if ($version -match '^(\d+\.\d+\.\d+)\.0$') {
    $version = $Matches[1]
}
Write-Host "App version: $version"

# --- Step 4: stage installer inputs ---

Write-Host "Staging installer inputs..."
Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

Copy-Item -LiteralPath $appSource -Destination (Join-Path $stageDir "PostgresCopy.Desktop.exe") -Force
Copy-Item -LiteralPath (Join-Path $root "scripts\install-desktop.ps1") -Destination (Join-Path $stageDir "install-desktop.ps1") -Force
Copy-Item -LiteralPath (Join-Path $root "scripts\uninstall-desktop.ps1") -Destination (Join-Path $stageDir "uninstall-desktop.ps1") -Force
Copy-Item -LiteralPath (Join-Path $root "src\PostgresCopy.Desktop\Assets\icon.ico") -Destination (Join-Path $stageDir "icon.ico") -Force
Copy-Item -LiteralPath (Join-Path $root "LICENSE.md") -Destination (Join-Path $stageDir "LICENSE.md") -Force
Copy-Item -LiteralPath (Join-Path $installerSourceDir "PostgresCopy.nsi") -Destination (Join-Path $stageDir "PostgresCopy.nsi") -Force

# --- Step 5: compile ---

Write-Host "Compiling installer..."
Push-Location $stageDir
try {
    & $makensisPath "/DAPP_VERSION=$version" "PostgresCopy.nsi"
    if ($LASTEXITCODE -ne 0) {
        throw "makensis failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$builtInstaller = Join-Path $stageDir "PostgresCopy-Setup-$version.exe"
if (-not (Test-Path -LiteralPath $builtInstaller)) {
    throw "Expected installer was not produced: $builtInstaller"
}

$finalInstaller = Join-Path $outputDir "PostgresCopy-Setup-$version.exe"
Move-Item -LiteralPath $builtInstaller -Destination $finalInstaller -Force

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $finalInstaller
Write-Host ""
Write-Host "Installer built: $finalInstaller"
Write-Host "SHA256: $($hash.Hash.ToLowerInvariant())"
