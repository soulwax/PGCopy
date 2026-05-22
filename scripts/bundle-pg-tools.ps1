# File: scripts/bundle-pg-tools.ps1
#
# Copies the pg-tools staging directory (artifacts\pg-tools\) into the tools\
# subdirectory of one or more published artifacts so that pg_dump and psql are
# available next to the executable without requiring a system PostgreSQL install.
#
# Run update-pg-tools.ps1 first to populate artifacts\pg-tools\, or pass
# -UpdateFirst to do both in one step.
#
# Usage:
#   .\scripts\bundle-pg-tools.ps1                        # bundle into desktop + CLI artifacts
#   .\scripts\bundle-pg-tools.ps1 -Target desktop        # desktop only
#   .\scripts\bundle-pg-tools.ps1 -Target cli            # CLI only
#   .\scripts\bundle-pg-tools.ps1 -Runtime win-arm64     # non-default runtime
#   .\scripts\bundle-pg-tools.ps1 -UpdateFirst           # download tools first, then bundle
#   .\scripts\bundle-pg-tools.ps1 -UpdateFirst -PgVersion 18  # use PG18 binaries

param(
    [ValidateSet("desktop", "cli", "both")]
    [string]$Target = "both",

    [string]$Runtime = "win-x64",

    # Download fresh pg-tools via update-pg-tools.ps1 before bundling.
    [switch]$UpdateFirst,

    # Passed through to update-pg-tools.ps1 when -UpdateFirst is set.
    [int]$PgVersion = 17
)

$ErrorActionPreference = "Stop"

$root       = Resolve-Path (Join-Path $PSScriptRoot "..")
$pgToolsSrc = Join-Path $root "artifacts\pg-tools"

# --- Step 1: optionally refresh the pg-tools staging area ---

if ($UpdateFirst) {
    $updateScript = Join-Path $root "scripts\update-pg-tools.ps1"
    Write-Host "Running update-pg-tools.ps1 -PgVersion $PgVersion..."
    & $updateScript -PgVersion $PgVersion
    if ($LASTEXITCODE -ne 0) { throw "update-pg-tools.ps1 failed." }
    Write-Host ""
}

# --- Step 2: verify the staging area exists and contains the required files ---

if (-not (Test-Path $pgToolsSrc)) {
    throw @"
artifacts\pg-tools\ does not exist.
Run  .\scripts\update-pg-tools.ps1  first to download it from EDB,
or re-run this script with  -UpdateFirst  to do both in one step.
"@
}

$required = @("pg_dump.exe", "psql.exe", "libpq.dll")
$missing  = $required | Where-Object { -not (Test-Path (Join-Path $pgToolsSrc $_)) }
if ($missing) {
    throw "artifacts\pg-tools\ is missing required files: $($missing -join ', '). Re-run update-pg-tools.ps1."
}

$srcFiles  = Get-ChildItem $pgToolsSrc -File
$srcSizeKb = [math]::Round(($srcFiles | Measure-Object Length -Sum).Sum / 1KB, 0)
Write-Host ("pg-tools staging: {0} files, {1:N0} KB" -f $srcFiles.Count, $srcSizeKb)

# --- Step 3: resolve which artifact directories to target ---

$artifactDirs = @()
if ($Target -in @("desktop", "both")) {
    $artifactDirs += Join-Path $root "artifacts\PostgresCopy-desktop-$Runtime"
}
if ($Target -in @("cli", "both")) {
    $artifactDirs += Join-Path $root "artifacts\PostgresCopy-cli-$Runtime"
}

foreach ($artifactDir in $artifactDirs) {
    if (-not (Test-Path $artifactDir)) {
        $label = if ($artifactDir -like "*desktop*") { "desktop" } else { "cli" }
        throw @"
Artifact directory not found: $artifactDir
Run  .\scripts\publish-$label.ps1  first, then re-run this script.
"@
    }
}

# --- Step 4: copy into each artifact's tools\ subdirectory ---

foreach ($artifactDir in $artifactDirs) {
    $toolsDest = Join-Path $artifactDir "tools"
    $label     = Split-Path $artifactDir -Leaf

    $isUpdate = Test-Path $toolsDest
    $null     = New-Item -ItemType Directory -Force -Path $toolsDest

    $copied   = 0
    $replaced = 0

    foreach ($file in $srcFiles) {
        $dest    = Join-Path $toolsDest $file.Name
        $existed = Test-Path $dest

        # Skip if the destination is already identical (same size + same last-write time).
        if ($existed) {
            $destItem = Get-Item $dest
            if ($destItem.Length -eq $file.Length -and
                $destItem.LastWriteTimeUtc -eq $file.LastWriteTimeUtc) {
                continue
            }
            $replaced++
        }

        Copy-Item $file.FullName $dest -Force
        $copied++
    }

    $destFiles  = Get-ChildItem $toolsDest -File
    $destSizeKb = [math]::Round(($destFiles | Measure-Object Length -Sum).Sum / 1KB, 0)

    $action = if ($isUpdate) { "Updated" } else { "Bundled" }
    Write-Host ("{0} tools\ in {1}  ({2} copied, {3} already current, {4:N0} KB total)" -f `
        $action, $label, $copied, ($srcFiles.Count - $copied), $destSizeKb)
}

# --- Step 5: smoke-check — the bundled pg_dump must run ---

$checkDir = $artifactDirs[0]   # verify against the first target
$pgDump  = Join-Path $checkDir "tools\pg_dump.exe"
$version = & $pgDump --version
if ($LASTEXITCODE -ne 0) {
    throw "Bundled pg_dump.exe failed version check in $checkDir\tools\."
}

Write-Host ""
Write-Host "Bundled pg_dump: $version"
Write-Host "Done. PostgresCopy will use the bundled tools\ directory automatically."
