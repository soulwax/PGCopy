# Runbook

Commands assume PowerShell from the repo root.

## Build and Test

```powershell
dotnet restore PostgresCopy.sln
dotnet build PostgresCopy.sln
dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build
```

## CLI Smoke Checks

```powershell
dotnet run --project src\PostgresCopy -- --help
dotnet run --project src\PostgresCopy -- --origin "postgres://user:secret@localhost:5432/app" --destination "postgres://user:secret@localhost:5432/app" --dry-run
```

The second command should fail because origin and destination are identical.

## Web App

```powershell
dotnet run --project src\PostgresCopy.Web --urls http://localhost:5087
```

Open:

```text
http://localhost:5087
```

Expected controls:

- origin database URL
- destination database URL
- schema
- tables
- dry run
- truncate destination
- run copy
- clear log
- operations log

If the build fails with locked DLLs, check for the running web process:

```powershell
Get-NetTCPConnection -LocalPort 5087 -ErrorAction SilentlyContinue
```

Then stop the owning process only if it is the local dev server you started.

## Integration Check

Requires Docker.

```powershell
.\scripts\integration-test.ps1
```

Keep containers for inspection:

```powershell
.\scripts\integration-test.ps1 -KeepContainers
```

The script should:

1. Start origin and destination PostgreSQL containers.
2. Seed origin with data.
3. Seed destination with matching empty tables.
4. Run PostgresCopy.
5. Compare row counts.

## PostgreSQL Client Tools

The original design allows future schema copy via `pg_dump` and `psql`, but the current app primarily uses Npgsql binary COPY for data.

Before adding schema-copy behavior, verify tool paths in the shell that will launch the app:

```powershell
pwsh -NoProfile -Command "Get-Command pg_dump, psql"
```

If that does not resolve, find explicit paths and make the failure message clear.

## Useful Git Checks

```powershell
git status --short --untracked-files=all
git diff --stat
```

Do not revert changes you did not make.
