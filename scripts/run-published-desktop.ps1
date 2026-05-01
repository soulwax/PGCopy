param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$appPath = Join-Path $root "artifacts/PostgresCopy-desktop-$Runtime/PostgresCopy.Desktop.exe"

if (-not (Test-Path $appPath)) {
    throw "Published desktop app not found at $appPath. Run .\scripts\publish-desktop.ps1 first."
}

Start-Process -FilePath $appPath -WorkingDirectory (Split-Path $appPath -Parent)
