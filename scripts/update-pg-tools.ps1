# File: scripts/update-pg-tools.ps1
#
# Downloads the PostgreSQL Windows binaries zip from the EDB CDN, extracts
# pg_dump.exe, psql.exe, and their required DLLs into the destination
# directory, then deletes the zip. No installation, no registry changes,
# no admin rights required, no side effects on the host machine.
#
# Usage:
#   .\scripts\update-pg-tools.ps1                       # fetch latest PG17 tools
#   .\scripts\update-pg-tools.ps1 -PgVersion 18         # use PG18 instead
#   .\scripts\update-pg-tools.ps1 -Destination "path\"  # custom output directory

param(
    [int]    $PgVersion   = 17,
    [string] $Destination
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")

if (-not $Destination) {
    $Destination = Join-Path $root "artifacts\pg-tools"
}

# --- Step 1: resolve the latest patch release URL from the winget manifest ---
#
# winget manifests live at:
#   https://raw.githubusercontent.com/microsoft/winget-pkgs/master/manifests/p/PostgreSQL/PostgreSQL/<ver>/...
#
# Rather than parsing YAML, we ask winget for the installer URL directly,
# then swap "-windows-x64.exe" for "-windows-x64-binaries.zip" — EDB
# publishes both artefacts under the same version string.

Write-Host "Resolving PostgreSQL $PgVersion installer URL via winget..."

$winget = Get-Command winget -ErrorAction SilentlyContinue
if ($null -eq $winget) {
    throw "winget is not available. PostgresCopy requires Windows 10 1809+ or Windows 11."
}

$wingetOut = winget show "PostgreSQL.PostgreSQL.$PgVersion" --source winget 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "winget could not find PostgreSQL.PostgreSQL.$PgVersion. Ensure winget sources are up to date (winget source update)."
}

# Extract the installer URL line: "  Installer-URL: https://get.enterprisedb.com/.../postgresql-X.Y-Z-windows-x64.exe"
$installerLine = $wingetOut | Where-Object { $_ -match 'Installer-URL:' } | Select-Object -First 1
if (-not $installerLine) {
    throw "Could not extract installer URL from winget output. Raw output:`n$($wingetOut -join "`n")"
}

$installerUrl = ($installerLine -split 'Installer-URL:\s*')[1].Trim()
Write-Host "Installer URL : $installerUrl"

# Swap the exe installer for the binaries zip published alongside it.
$zipUrl = $installerUrl -replace '-windows-x64\.exe$', '-windows-x64-binaries.zip'
Write-Host "Binaries zip  : $zipUrl"

# --- Step 2: download the zip to a temporary file ---

$tmpZip = [System.IO.Path]::GetTempFileName() + ".zip"
Write-Host ""
Write-Host "Downloading binaries zip..."
Write-Host "  -> $tmpZip"

try {
    # Use HttpClient for streaming progress — Invoke-WebRequest buffers everything first.
    $httpClient = [System.Net.Http.HttpClient]::new()
    $httpClient.Timeout = [System.TimeSpan]::FromMinutes(10)
    try {
        $response = $httpClient.GetAsync($zipUrl, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        $response.EnsureSuccessStatusCode() | Out-Null

        $totalBytes = $response.Content.Headers.ContentLength
        $totalMb = if ($totalBytes) { [math]::Round($totalBytes / 1MB, 1) } else { "?" }
        Write-Host "  Size: ${totalMb} MB"

        $stream   = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $outStream = [System.IO.File]::Create($tmpZip)
        try {
            $buffer    = [byte[]]::new(131072)  # 128 KB chunks
            $received  = 0L
            $lastReport = 0L
            $read = $stream.Read($buffer, 0, $buffer.Length)
            while ($read -gt 0) {
                $outStream.Write($buffer, 0, $read)
                $received += $read
                # Emit a progress line every ~2 MB so the GUI log stays live.
                if (($received - $lastReport) -ge 2MB) {
                    $mb = [math]::Round($received / 1MB, 1)
                    if ($totalBytes) {
                        $pct = [math]::Round($received * 100 / $totalBytes)
                        Write-Host "  Downloading... ${mb} / ${totalMb} MB  (${pct}%)"
                    } else {
                        Write-Host "  Downloading... ${mb} MB"
                    }
                    $lastReport = $received
                }
                $read = $stream.Read($buffer, 0, $buffer.Length)
            }
        }
        finally {
            $outStream.Dispose()
            $stream.Dispose()
        }
    }
    finally {
        $httpClient.Dispose()
    }

    $sizeMb = [math]::Round((Get-Item $tmpZip).Length / 1MB, 1)
    Write-Host "Downloaded ${sizeMb} MB — OK"
}
catch {
    Remove-Item $tmpZip -ErrorAction SilentlyContinue
    throw "Download failed: $_"
}

# --- Step 3: extract only the files we need ---

$filesToExtract = @(
    "pgsql/bin/pg_dump.exe",
    "pgsql/bin/psql.exe",
    "pgsql/bin/libpq.dll",
    "pgsql/bin/libcrypto-3-x64.dll",
    "pgsql/bin/libssl-3-x64.dll",
    "pgsql/bin/libintl-9.dll",
    "pgsql/bin/libiconv-2.dll",       # runtime dependency of libintl-9.dll
    "pgsql/bin/libwinpthread-1.dll",  # runtime dependency of libintl-9.dll
    "pgsql/bin/liblz4.dll",
    "pgsql/bin/libzstd.dll"
)

$null = New-Item -ItemType Directory -Force -Path $Destination

Write-Host ""
Write-Host "Extracting to: $Destination"

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($tmpZip)
    try {
        $copied = 0
        foreach ($entryPath in $filesToExtract) {
            $entry = $archive.GetEntry($entryPath)
            if ($null -eq $entry) {
                Write-Warning "  Not found in zip: $entryPath (skipped)"
                continue
            }
            $outFile = Join-Path $Destination ([System.IO.Path]::GetFileName($entry.FullName))
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $outFile, $true)
            $sizekb = [math]::Round($entry.Length / 1KB, 0)
            Write-Host ("  Extracted {0,-30} {1,6} KB" -f [System.IO.Path]::GetFileName($entry.FullName), $sizekb)
            $copied++
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    # Always remove the zip — no traces on the host.
    Remove-Item $tmpZip -Force -ErrorAction SilentlyContinue
    Write-Host "Zip deleted."
}

# --- Step 4: smoke-check ---

$totalKb = (Get-ChildItem $Destination | Measure-Object Length -Sum).Sum / 1KB
Write-Host ""
Write-Host "Copied $copied files to: $Destination"
Write-Host ("Total size: {0:N0} KB  ({1:N1} MB)" -f $totalKb, ($totalKb / 1024))

$pgDump = Join-Path $Destination "pg_dump.exe"
$version = & $pgDump --version
Write-Host "pg_dump version: $version"
exit 0
