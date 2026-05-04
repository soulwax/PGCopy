# File: scripts/run-desktop.ps1

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")

Push-Location $root
try {
    dotnet run --project src/PostgresCopy.Desktop

    if ($LASTEXITCODE -ne 0) {
        throw "PostgresCopy.Desktop failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
