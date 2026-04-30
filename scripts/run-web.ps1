param(
    [int]$Port = 5087
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$url = "http://localhost:$Port"
$serverProcess = $null

function Test-AppReady {
    param([string]$AppUrl)

    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $AppUrl -TimeoutSec 2
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

Push-Location $root
try {
    if (-not (Test-AppReady $url)) {
        $serverProcess = Start-Process `
            -FilePath dotnet `
            -ArgumentList @("run", "--project", "src/PostgresCopy.Web", "--urls", $url) `
            -WorkingDirectory $root `
            -WindowStyle Hidden `
            -PassThru

        for ($attempt = 1; $attempt -le 30; $attempt++) {
            if (Test-AppReady $url) {
                break
            }

            if ($serverProcess.HasExited) {
                throw "PostgresCopy.Web exited before it became ready."
            }

            Start-Sleep -Seconds 1
        }
    }

    if (-not (Test-AppReady $url)) {
        throw "Timed out waiting for $url."
    }

    Start-Process $url
    Write-Host "PostgresCopy Web is running at $url"
    Write-Host "Press Enter to stop it."
    Read-Host | Out-Null
}
finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
    }

    Pop-Location
}
