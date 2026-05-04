# File: scripts/publish-desktop.ps1

param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$output = Join-Path $root "artifacts/PostgresCopy-desktop-$Runtime"

Push-Location $root
try {
    dotnet publish src/PostgresCopy.Desktop `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        --output $output

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    Write-Host "Desktop app published to $output"
}
finally {
    Pop-Location
}
