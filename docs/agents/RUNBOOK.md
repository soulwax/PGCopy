# Runbook

Commands assume PowerShell from the repo root.

## Build and Test

```powershell
dotnet restore PostgresCopy.sln
dotnet build PostgresCopy.sln
dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build
```

Or run the standard non-Docker check suite:

```powershell
.\scripts\check.ps1
```

## Native Desktop App

The main user path is the native Windows desktop `.exe` over the shared migration core.

Run from source:

```powershell
dotnet run --project src\PostgresCopy.Desktop
```

Or use the launcher:

```powershell
.\Start-PostgresCopy-Desktop.cmd
```

Publish the self-contained `.exe`:

```powershell
.\scripts\publish-desktop.ps1
```

Publish and run metadata/icon smoke checks:

```powershell
.\scripts\publish-desktop.ps1 -SmokeCheck
.\scripts\smoke-published-desktop.ps1
```

Launch the published app for visual verification:

```powershell
.\scripts\smoke-published-desktop.ps1 -Launch
```

Run the published `.exe`:

```powershell
.\Start-PostgresCopy-Desktop-Published.cmd
```

Expected native GUI controls:

- origin database URL
- destination database URL
- preflight check button
- get pg tools button and pg tools status
- peek database URL
- peek database button
- history tab with successful and failed/cancelled runs separated
- schema
- tables
- dry run button
- truncate destination with warning confirmation
- drop destination schema first with warning confirmation
- verify and repair counts
- create destination schema
- SSH tunnel configuration
- copy button
- cancel
- save log
- operations log

For GUI changes, verify the header/logo, app/window icon, origin field, destination field, preflight tab, get-pg-tools state, peek tab, history tab, dry run button, copy button, cancel path, SSH tab, save-log path, and operations log.
The operations log uses colored whole-line severity for errors, warnings, steps, successes, active work, table data, and guidance lines; saved logs remain plain text.

## CLI Smoke Checks

The CLI is the scriptable automation companion. Run these after CLI changes and as part of normal checks:

```powershell
dotnet run --project src\PostgresCopy -- --help
dotnet run --project src\PostgresCopy -- --origin "postgres://user:secret@localhost:5432/app" --destination "postgres://user:secret@localhost:5432/app" --dry-run
```

The second command should fail because origin and destination are identical.

## Integration Check

Requires Docker.

```powershell
.\scripts\integration-test.ps1
```

Check local prerequisites without starting containers:

```powershell
.\scripts\integration-test.ps1 -Check
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

## Publish

```powershell
.\scripts\dist.ps1
.\scripts\publish-desktop.ps1
.\scripts\publish-cli.ps1
```

Use `dist.ps1` for binary distribution: it runs the standard non-Docker checks, publishes desktop and CLI artifacts, smoke-checks the desktop artifact, creates zip archives under `artifacts\dist`, and writes `SHA256SUMS.txt`.

The lower-level publish scripts are still useful when iterating on one artifact. Outputs go under `artifacts/`, which is ignored by git.

## PostgreSQL Client Tools

Schema copy uses `pg_dump` and `psql`; data copy uses Npgsql binary COPY.

When changing schema-copy behavior, verify tool paths in the shell that will launch the app:

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
