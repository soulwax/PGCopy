# File: scripts/install-desktop.ps1

param(
    [string]$Runtime = "win-x64",
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\PostgresCopy"),
    [string]$AppSource
)

$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "The desktop installer is Windows-only."
}

# -AppSource lets a standalone package (e.g. the NSIS installer, which stages
# this script next to the built exe with no repo/scripts folder around it)
# point directly at the exe to install, skipping the repo-relative lookup
# and self-publish fallback below entirely.
if ($AppSource) {
    $appSource = $AppSource
    if (-not (Test-Path -LiteralPath $appSource)) {
        throw "Specified -AppSource does not exist: $appSource"
    }
}
else {
    $root = Resolve-Path (Join-Path $PSScriptRoot "..")
    $publishDir = Join-Path $root "artifacts\PostgresCopy-desktop-$Runtime"
    $appSource = Join-Path $publishDir "PostgresCopy.Desktop.exe"

    if (-not (Test-Path $appSource)) {
        Write-Host "Published desktop app not found. Publishing first..."
        & (Join-Path $root "scripts\publish-desktop.ps1") -Runtime $Runtime
    }

    if (-not (Test-Path $appSource)) {
        throw "Published desktop app not found at $appSource."
    }
}

$installDirFull = [System.IO.Path]::GetFullPath($InstallDir)
$programsRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "Programs"))
$programsRootWithSlash = $programsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $installDirFull.StartsWith($programsRootWithSlash, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to install outside the per-user Programs directory: $installDirFull"
}

$appTarget = Join-Path $installDirFull "PostgresCopy.Desktop.exe"
$uninstallTarget = Join-Path $installDirFull "Uninstall-PostgresCopy.ps1"
$shortcutPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\PostgresCopy.lnk"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PostgresCopy"
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($appSource).FileVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.0.0"
}

New-Item -ItemType Directory -Path $installDirFull -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path $shortcutPath -Parent) -Force | Out-Null

Get-Process -Name "PostgresCopy.Desktop" -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

Copy-Item -LiteralPath $appSource -Destination $appTarget -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "uninstall-desktop.ps1") -Destination $uninstallTarget -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $appTarget
$shortcut.WorkingDirectory = $installDirFull
$shortcut.IconLocation = "$appTarget,0"
$shortcut.Description = "PostgresCopy"
$shortcut.Save()

New-Item -Path $uninstallKey -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "PostgresCopy" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value $version -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "PostgresCopy contributors" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $appTarget -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $installDirFull -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "InstallDate" -Value (Get-Date -Format "yyyyMMdd") -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallTarget`" -InstallDir `"$installDirFull`"" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name "QuietUninstallString" -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallTarget`" -InstallDir `"$installDirFull`" -Quiet" -PropertyType String -Force | Out-Null

Write-Host "PostgresCopy installed."
Write-Host "App: $appTarget"
Write-Host "Shortcut: $shortcutPath"
Write-Host "Uninstall entry: Settings > Apps > Installed apps > PostgresCopy"
