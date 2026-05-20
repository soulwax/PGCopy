# File: scripts/integration-test.ps1

param(
    [switch]$KeepContainers,
    [switch]$Check,

    # Also run the --drop-schema scenario: destination starts with a dirty/wrong
    # schema; PostgresCopy must drop it, rebuild from origin DDL, then copy data.
    # Requires pg_dump and psql on PATH (or in the bundled tools\ directory).
    [switch]$DropSchema
)

$ErrorActionPreference = "Stop"

$root             = Resolve-Path (Join-Path $PSScriptRoot "..")
$composeFile      = Join-Path $root "tests/integration/docker-compose.yml"
$dirtyComposeFile = Join-Path $root "tests/integration/docker-compose-dirty.yml"
$originUrl        = "postgres://postgres:test@localhost:55432/pgcopy"
$destinationUrl   = "postgres://postgres:test@localhost:55433/pgcopy"
$dirtyDestUrl     = "postgres://postgres:test@localhost:55434/pgcopy"

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

function Read-Tables {
    param([string]$ContainerName)
    docker exec $ContainerName psql -U postgres -d pgcopy -Atc `
        "select tablename from pg_tables where schemaname='public' order by tablename;"
}

function Read-Columns {
    param([string]$ContainerName, [string]$Table)
    docker exec $ContainerName psql -U postgres -d pgcopy -Atc `
        "select column_name from information_schema.columns where table_schema='public' and table_name='$Table' order by ordinal_position;"
}

function Test-PgToolsAvailable {
    $pgDump = Get-Command pg_dump -ErrorAction SilentlyContinue
    $psql   = Get-Command psql   -ErrorAction SilentlyContinue
    return ($null -ne $pgDump -and $null -ne $psql)
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

    # ── Drop-schema scenario ────────────────────────────────────────────────────
    if ($DropSchema) {
        Write-Host ""
        Write-Host "Running --drop-schema scenario..."

        if (-not (Test-PgToolsAvailable)) {
            throw "pg_dump and psql are required for the --drop-schema scenario. Add them to PATH or run .\scripts\update-pg-tools.ps1 first."
        }

        # Spin up a third container seeded with the intentionally wrong schema.
        docker compose -f $dirtyComposeFile up -d
        if ($LASTEXITCODE -ne 0) { throw "docker compose up failed for dirty destination." }

        Wait-ForPostgres "pgcopy-destination-dirty"

        # Verify the dirty schema is actually wrong before we fix it.
        $dirtyTables  = Read-Tables  "pgcopy-destination-dirty"
        $dirtyColumns = Read-Columns "pgcopy-destination-dirty" "accounts"

        if ($dirtyTables -notcontains "legacy_junk") {
            throw "Dirty destination is missing legacy_junk table — seed did not apply correctly."
        }
        if ($dirtyColumns -contains "display_name") {
            throw "Dirty destination accounts table already has display_name — seed did not apply correctly."
        }
        Write-Host "Confirmed: dirty destination has wrong schema (missing display_name, has legacy_junk)."

        # Step 1: schema-only with --drop-schema rebuilds the destination schema from origin.
        dotnet run --project src/PostgresCopy -- `
            --origin      $originUrl `
            --destination $dirtyDestUrl `
            --schema-only `
            --drop-schema `
            --yes
        if ($LASTEXITCODE -ne 0) { throw "--schema-only --drop-schema run failed." }

        # Step 2: verify the schema was rebuilt correctly.
        $cleanTables  = Read-Tables  "pgcopy-destination-dirty"
        $cleanColumns = Read-Columns "pgcopy-destination-dirty" "accounts"

        if ($cleanTables -contains "legacy_junk") {
            throw "--drop-schema did not remove legacy_junk table."
        }
        if ($cleanColumns -notcontains "display_name") {
            throw "--drop-schema schema rebuild is missing display_name column on accounts."
        }
        if ($cleanColumns -notcontains "email") {
            throw "--drop-schema schema rebuild is missing email column on accounts."
        }
        Write-Host "Confirmed: destination schema matches origin after --drop-schema (display_name present, legacy_junk gone)."

        # Step 3: data copy into the now-correct schema.
        dotnet run --project src/PostgresCopy -- `
            --origin      $originUrl `
            --destination $dirtyDestUrl `
            --data-only `
            --tables accounts,orders `
            --truncate-destination `
            --yes `
            --verify
        if ($LASTEXITCODE -ne 0) { throw "Data copy into rebuilt schema failed." }

        $originCounts2 = Read-Counts "pgcopy-origin"
        $dirtyCounts   = Read-Counts "pgcopy-destination-dirty"

        if (($originCounts2 -join "`n") -ne ($dirtyCounts -join "`n")) {
            Write-Error "Row counts did not match after --drop-schema copy.`nOrigin:`n$originCounts2`nDirty dest:`n$dirtyCounts"
        }

        Write-Host "Integration --drop-schema scenario passed."
        Write-Host ($dirtyCounts -join "`n")
    }
}
finally {
    if (-not $Check -and -not $KeepContainers) {
        docker compose -f $composeFile down --volumes --remove-orphans
        if (Test-Path $dirtyComposeFile) {
            docker compose -f $dirtyComposeFile down --volumes --remove-orphans
        }
    }

    Pop-Location
}
