# AGENTS.md

Guidance for coding agents working on PostgresCopy.

PostgresCopy has one job: copy PostgreSQL data from an origin database to a destination database in a way that is predictable, visible, and safe. Keep every change in service of that job.

## Start Here

Read these files before making design or behavior changes:

- `README.md` for current user-facing behavior.
- `TODO.md` for the lightweight backlog.
- `docs/superpowers/specs/2026-04-30-pgcopy-design.md` for original product intent.
- `docs/agents/DECISIONS.md` for decisions already made.
- `docs/agents/RUNBOOK.md` for verification commands and environment notes.

## Current Shape

- `src/PostgresCopy` is the core CLI and migration engine.
- `src/PostgresCopy.Desktop` is the native one-window C# GUI.
- `src/PostgresCopy.Web` is an interim local web prototype for no-terminal use.
- `tests/PostgresCopy.Tests` contains unit tests for parsing, validation, planning, and safety helpers.
- `tests/integration` contains Docker-backed PostgreSQL seed files.
- `scripts/integration-test.ps1` is the manual integration check.

## Product Boundaries

Do:

- Keep the app PostgreSQL-only.
- Prefer Npgsql directly.
- Keep the CLI scriptable and stable.
- Prefer a one-window native C# desktop GUI for no-terminal use.
- Keep any interim web UI one-window, practical, and local while it exists.
- Make destructive behavior explicit and confirmed.
- Print or stream human-readable progress.
- Fail before copying when preflight detects unsafe shape.
- Redact credentials everywhere.

Do not:

- Add a general ETL framework.
- Add ORM usage.
- Add non-PostgreSQL engines.
- Add background services.
- Add cloud-specific behavior.
- Turn the local web prototype into the primary long-term UI.
- Store credentials.
- Print raw connection strings.
- Add destructive defaults.

## Safety Rules

- Origin and destination must not normalize to the same database.
- Destination schema/table mismatch must fail before data copy.
- Destructive actions require an explicit flag or UI checkbox.
- CLI destructive actions require `--yes` or an interactive confirmation.
- GUI destructive actions require typing `TRUNCATE`.
- Never silently skip a failed table.
- Keep stack traces behind `--verbose` in CLI paths.

## Architecture Rules

- Keep schema handling separate from data transfer.
- Keep UI thin; shared behavior belongs in `src/PostgresCopy`.
- Do not require a localhost web server for the final no-terminal experience.
- Keep `Program.cs` readable. If orchestration grows, extract a small service.
- Keep table/column SQL identifier quoting centralized in `SqlIdentifier`.
- Treat user-provided schema/table names as untrusted unless quoted as identifiers.
- Prefer async APIs and dispose connections/transactions/copy streams.
- Add abstractions only when they remove real duplication or clarify a safety boundary.

## Verification Baseline

Run these before finalizing normal code changes:

```powershell
dotnet build PostgresCopy.sln
dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build
dotnet run --project src\PostgresCopy -- --help
```

For native GUI changes:

```powershell
dotnet run --project src\PostgresCopy.Desktop
```

Then verify the origin field, destination field, run button, cancel path, and operations log.

For interim web UI changes:

```powershell
dotnet run --project src\PostgresCopy.Web --urls http://localhost:5087
```

Then verify the page loads and renders the origin field, destination field, run button, and operations log. Use `agent-browser` when available.

For end-to-end database behavior, use:

```powershell
.\scripts\integration-test.ps1
```

This requires Docker.

## Known Environment Notes

- This repo targets `net10.0`.
- `NuGet.config` clears user package sources and uses nuget.org because a missing user-level source has caused restores to fail.
- Docker was not available in the current Codex environment when the integration script was added.
- A running `PostgresCopy.Web` process can lock build outputs while the interim prototype exists. Stop it before rebuilding if MSBuild reports locked DLLs.
- The user reported `pg_dump.exe` is available in PowerShell 7, but non-interactive checks did not resolve `pg_dump` by name in this session. Do not build schema-copy behavior on that assumption without verifying the path in the current shell.

## Agile Working Style

- Prefer one small productive slice at a time.
- Update `TODO.md` when a task becomes true.
- Keep tests close to changed behavior.
- When behavior touches database reality, add or update integration seeds/scripts.
- For user-facing behavior, update `README.md`.
- When something is intentionally not supported, make the error clear.

## Good Next Slices

Pick from `TODO.md`, but the most useful remaining slices are:

- Add a clear partial-failure summary.
- Improve dry-run with row counts and destination readiness.
- De-emphasize or remove the interim web prototype once the desktop app is comfortable.
- Make integration testing easier when Docker is available.
