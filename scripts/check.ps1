param(
    [switch]$IncludeIntegration,
    [int]$WebPort = 5087
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$webUrl = "http://localhost:$WebPort"

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

function Stop-WebPort {
    param([int]$Port)

    $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    if (-not $connections) {
        return
    }

    $connections |
        Select-Object -ExpandProperty OwningProcess -Unique |
        ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
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

    Write-Host "Checking web app..."
    Stop-WebPort $WebPort
    $serverProcess = Start-Process `
        -FilePath dotnet `
        -ArgumentList @("run", "--project", "src/PostgresCopy.Web", "--urls", $webUrl) `
        -WorkingDirectory $root `
        -WindowStyle Hidden `
        -PassThru

    try {
        Start-Sleep -Seconds 4
        $response = Invoke-WebRequest -UseBasicParsing $webUrl
        $requiredContent = @(
            "Run dry run",
            "ready-mode",
            "postgres://user:password@localhost:5432/source",
            "Cancel",
            "Verify counts"
        )
        $missingContent = $requiredContent | Where-Object {
            $response.Content -notmatch [regex]::Escape($_)
        }

        if ($missingContent.Count -gt 0) {
            throw "Web smoke check missing content: $($missingContent -join ', ')"
        }
    }
    finally {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
        Stop-WebPort $WebPort
    }

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
