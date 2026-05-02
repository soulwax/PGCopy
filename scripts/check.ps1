param(
    [switch]$IncludeIntegration
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")

function Test-PowerShellFile {
    param([string]$Path)

    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors) | Out-Null

    if ($errors.Count -gt 0) {
        $messages = $errors | ForEach-Object { "${Path}: $($_.Message)" }
        throw ($messages -join [Environment]::NewLine)
    }
}

function Invoke-Native {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

Push-Location $root
try {
    Write-Host "Checking PowerShell scripts..."
    Get-ChildItem -Path scripts -Filter *.ps1 | ForEach-Object {
        Test-PowerShellFile $_.FullName
    }

    Write-Host "Building solution..."
    Invoke-Native "dotnet" @("build", "PostgresCopy.sln")

    Write-Host "Running unit tests..."
    Invoke-Native "dotnet" @("test", "tests/PostgresCopy.Tests/PostgresCopy.Tests.csproj", "--no-build")

    Write-Host "Checking CLI help..."
    Invoke-Native "dotnet" @("run", "--project", "src/PostgresCopy", "--", "--help")

    if ($IncludeIntegration) {
        if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
            throw "Docker is required for integration checks."
        }

        Write-Host "Running integration check..."
        & (Join-Path $root "scripts/integration-test.ps1")
    }

    Write-Host "All checks passed."
}
finally {
    Pop-Location
}
