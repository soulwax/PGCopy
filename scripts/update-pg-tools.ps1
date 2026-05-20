# File: scripts/update-pg-tools.ps1
#
# Updates the Scoop postgresql package and copies pg_dump.exe, psql.exe, and
# their required DLLs into artifacts\pg-tools\ so that PostgresCopy can use
# them without requiring PostgreSQL on the system PATH.
#
# Usage:
#   .\scripts\update-pg-tools.ps1                  # update scoop + copy tools
#   .\scripts\update-pg-tools.ps1 -SkipScoopUpdate # copy from current Scoop install only
#   .\scripts\update-pg-tools.ps1 -Destination "path\to\dir"  # custom output dir

param(
    [switch]$SkipScoopUpdate,
    [string]$Destination
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")

if (-not $Destination) {
    $Destination = Join-Path $root "artifacts\pg-tools"
}

# --- Step 1: update Scoop and the postgresql package ---

if (-not $SkipScoopUpdate) {
    if (-not (Get-Command scoop -ErrorAction SilentlyContinue)) {
        throw "Scoop is not on PATH. Install it from https://scoop.sh or use -SkipScoopUpdate and point to a manual install."
    }

    Write-Host "Updating Scoop..."
    scoop update
    if ($LASTEXITCODE -ne 0) { throw "scoop update failed." }

    Write-Host "Updating postgresql package..."
    scoop update postgresql
    if ($LASTEXITCODE -ne 0) { throw "scoop update postgresql failed." }
}

# --- Step 2: locate the Scoop postgresql bin directory ---

$scoopPostgres = scoop prefix postgresql 2>$null
if ($LASTEXITCODE -ne 0 -or -not $scoopPostgres) {
    throw "Could not locate the postgresql Scoop package. Run 'scoop install postgresql' first."
}

$srcBin = Join-Path $scoopPostgres "bin"
if (-not (Test-Path $srcBin)) {
    throw "Expected bin directory not found at: $srcBin"
}

Write-Host "Source: $srcBin"

# --- Step 3: copy executables and their non-Windows runtime DLLs ---

# Windows system DLLs (kernel32, ntdll, advapi32, ws2_32, api-ms-win-crt-*, netmsg)
# and vcruntime140 are present on any machine that can run .NET 10, so we skip them.
$filesToCopy = @(
    "pg_dump.exe",
    "psql.exe",
    "libpq.dll",
    "libcrypto-3-x64.dll",
    "libssl-3-x64.dll",
    "libintl-9.dll",
    "libiconv-2.dll",       # runtime dependency of libintl-9.dll
    "libwinpthread-1.dll",  # runtime dependency of libintl-9.dll
    "liblz4.dll",
    "libzstd.dll"
)

$null = New-Item -ItemType Directory -Force -Path $Destination

$copied = 0
foreach ($file in $filesToCopy) {
    $src = Join-Path $srcBin $file
    if (-not (Test-Path $src)) {
        Write-Warning "Expected file not found, skipping: $src"
        continue
    }
    Copy-Item $src $Destination -Force
    $size = [math]::Round((Get-Item $src).Length / 1KB, 0)
    Write-Host ("  Copied {0,-30} {1,6} KB" -f $file, $size)
    $copied++
}

# --- Step 4: report ---

$totalKb = (Get-ChildItem $Destination | Measure-Object Length -Sum).Sum / 1KB
Write-Host ""
Write-Host ("Copied $copied files to: $Destination")
Write-Host ("Total size: {0:N0} KB  ({1:N1} MB)" -f $totalKb, ($totalKb / 1024))

$pgDump = Join-Path $Destination "pg_dump.exe"
$version = & $pgDump --version
Write-Host "pg_dump version: $version"
exit 0
