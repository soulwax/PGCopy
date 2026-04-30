param(
    [switch]$KeepContainers
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$composeFile = Join-Path $root "tests/integration/docker-compose.yml"
$originUrl = "postgres://postgres:test@localhost:55432/pgcopy"
$destinationUrl = "postgres://postgres:test@localhost:55433/pgcopy"

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
    if (-not $KeepContainers) {
        docker compose -f $composeFile down --volumes --remove-orphans
    }

    Pop-Location
}
