# File: scripts/integration-test.ps1

param(
    [switch]$KeepContainers,
    [switch]$Check
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$composeFile = Join-Path $root "tests/integration/docker-compose.yml"
$originUrl = "postgres://postgres:test@localhost:55432/pgcopy"
$destinationUrl = "postgres://postgres:test@localhost:55433/pgcopy"

function Test-IntegrationPrerequisites {
    $ok = $true

    Write-Host "Checking integration test prerequisites..."

    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if ($null -eq $docker) {
        Write-Warning "Docker CLI was not found on PATH."
        $ok = $false
    }
    else {
        Write-Host "Docker CLI: $($docker.Source)"
        $dockerVersion = docker --version
        $dockerVersion | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Docker CLI did not report a version."
            $ok = $false
        }

        $composeVersion = docker compose version
        $composeVersion | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "'docker compose' is not available."
            $ok = $false
        }

        $daemonVersion = docker info --format "{{.ServerVersion}}"
        $daemonVersion | ForEach-Object { Write-Host "Docker daemon: $($_)" }
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Docker daemon is not reachable. Start Docker Desktop or a compatible Docker runtime."
            $ok = $false
        }
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        Write-Warning ".NET SDK was not found on PATH."
        $ok = $false
    }
    else {
        Write-Host ".NET CLI: $($dotnet.Source)"
        $dotnetVersion = dotnet --version
        $dotnetVersion | ForEach-Object { Write-Host ".NET SDK: $($_)" }
        if ($LASTEXITCODE -ne 0) {
            Write-Warning ".NET CLI did not report a version."
            $ok = $false
        }
    }

    if ($ok) {
        Write-Host "Integration prerequisites look ready."
    }
    else {
        Write-Warning "Integration prerequisites are incomplete."
    }

    return $ok
}

function Wait-ForPostgres {
    param(
        [string]$ContainerName
    )

    for ($attempt = 1; $attempt -le 30; $attempt++) {
        docker exec $ContainerName pg_isready -U postgres -d pgcopy | Out-Null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "Timed out waiting for $ContainerName."
}

function Read-Counts {
    param(
        [string]$ContainerName
    )

    docker exec $ContainerName psql -U postgres -d pgcopy -Atc @"
select 'accounts=' || count(*) from public.accounts
union all
select 'orders=' || count(*) from public.orders
order by 1;
"@
}

Push-Location $root
try {
    if ($Check) {
        $ok = Test-IntegrationPrerequisites
        if (-not $ok) {
            Write-Host "Integration prerequisites are incomplete." -ForegroundColor Red
            exit 1
        }

        return
    }

    docker compose -f $composeFile down --volumes --remove-orphans
    docker compose -f $composeFile up -d

    Wait-ForPostgres "pgcopy-origin"
    Wait-ForPostgres "pgcopy-destination"

    dotnet run --project src/PostgresCopy -- `
        --origin $originUrl `
        --destination $destinationUrl `
        --tables accounts,orders `
        --truncate-destination `
        --yes `
        --verify

    $originCounts = Read-Counts "pgcopy-origin"
    $destinationCounts = Read-Counts "pgcopy-destination"

    if (($originCounts -join "`n") -ne ($destinationCounts -join "`n")) {
        Write-Error "Row counts did not match.`nOrigin:`n$originCounts`nDestination:`n$destinationCounts"
    }

    Write-Host "Integration copy passed."
    Write-Host ($destinationCounts -join "`n")
}
finally {
    if (-not $Check -and -not $KeepContainers) {
        docker compose -f $composeFile down --volumes --remove-orphans
    }

    Pop-Location
}
