# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Environment

- Targets `net10.0` exclusively. `net9.0` and below are not installed; `dotnet new` must be invoked with `-f net10.0`.
- Shell: PowerShell 7 on Windows 11. Use PowerShell syntax in scripts.
- `pg_dump` / `psql` resolve in interactive PowerShell but may not resolve in non-interactive shells. `SchemaCreator` performs an explicit PATH/bundled-tools check and surfaces a clear error. The desktop Preflight tab can run `Get pg tools` to download client tools into `tools\` beside the executable.

## Build, Test, Run

```powershell
dotnet build PostgresCopy.sln
dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build   # full unit suite, no DB
dotnet run --project src\PostgresCopy.Desktop                                # GUI
dotnet run --project src\PostgresCopy -- --help                              # CLI
```

Pre-commit check (build + unit + CLI smoke):

```powershell
.\scripts\check.ps1
.\scripts\check.ps1 -IncludeIntegration   # also runs the Docker integration script
```

Run a single test by filter:

```powershell
dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~MigrationPlannerTests"
```

Integration tests require Docker:

```powershell
.\scripts\integration-test.ps1 -Check          # local prerequisites only, no containers
.\scripts\integration-test.ps1                  # spin up two Postgres containers, seed, copy, verify
.\scripts\integration-test.ps1 -KeepContainers  # leave containers running for inspection
```

CLI smoke check (same-database rejection should fail fast):

```powershell
dotnet run --project src\PostgresCopy -- --origin "postgres://user:secret@localhost:5432/app" --destination "postgres://user:secret@localhost:5432/app" --dry-run
```

Publish self-contained single-file executables:

```powershell
.\scripts\dist.ps1                       # checks + desktop/CLI publish + zips + checksums
.\scripts\publish-desktop.ps1            # → artifacts\PostgresCopy-desktop-win-x64\
.\scripts\publish-cli.ps1               # → artifacts\PostgresCopy-cli-win-x64\
.\scripts\publish-desktop.ps1 -SmokeCheck  # publish + metadata/icon checks
```

## Architecture

PostgresCopy has **two front-ends and one shared migration core**.

- `src/PostgresCopy` — shared core *plus* the scriptable CLI (a single `Exe` project). `Program.cs` is the CLI entry; everything under `Cli/`, `Config/`, `Database/`, `Migration/`, `Logging/` is reused by the desktop.
- `src/PostgresCopy.Desktop` — Windows Forms `WinExe` (`net10.0-windows`). One window, five tabs (Connection, Preflight, Peek into Database, History, SSH Tunnel). Translates UI state into `MigrationSettings` and hands them to the same `MigrationRunner` the CLI uses. Keep this layer thin — any logic worth testing belongs in the core.
- `DatabasePeekInspector` (in `Database/`) is the read-only inspector used by the Peek tab — distinct from `PostgresSchemaInspector`, which is used only during migration planning.
- `DesktopRunHistoryStore` stores local redacted run history under `%LOCALAPPDATA%\PostgresCopy\history.json`, capped at 200 entries. It is history, not a credential store or runnable recipe system.

### Migration pipeline

`MigrationRunner.RunAsync` is the canonical ordering. Reading it is the fastest way to understand the system:

1. Validate connection strings (origin ≠ destination is enforced upstream by `MigrationSettingsValidator`).
2. *(If `--create-schema`)* Run `SchemaCreator` **before opening Npgsql connections** — this prevents PgBouncer/pooled connections from being held open during `pg_dump`. `--schema-only` exits after this step.
3. Open Npgsql connections on origin + destination.
4. `PostgresSchemaInspector` discovers origin tables and FK dependencies → `OriginTableSelectionValidator` confirms every requested table exists.
5. `MigrationPlanner` builds a topologically sorted `MigrationPlan` (parents before children).
6. `DestinationPreflightValidator` confirms every planned destination table exists with matching columns in matching order. Fails *before* any data copy.
7. *(Dry run?)* `DryRunReporter` prints counts and exits.
8. `DestinationDataPreflight` refuses to append into non-empty destination tables unless `TruncateDestination` is set.
9. *(Truncate?)* `DestinationTableCleaner` empties planned tables. Requires `destructiveActionsConfirmed=true` from the caller — CLI gets this via `--yes` or `InteractiveCliPrompt`; the desktop gets it via the truncate checkbox plus a warning dialog.
10. `CopyDataMigrator` streams each table via binary `COPY TO STDOUT` → `COPY FROM STDIN`.
11. `SequenceSynchronizer` realigns identity/serial sequences on destination.
12. *(Verify?)* `RowCountVerifier` compares row counts and throws on mismatch.

### Logging

`IMigrationLogger` is the abstraction. `ConsoleMigrationLogger` is CLI; `UiMigrationLogger` is desktop and applies per-line severity coloring. Both share the same step/info/success/error/plan vocabulary, which is why CLI and desktop logs stay aligned.

### SSH tunnel (desktop only)

`SshTunnelConnection` uses SSH.NET's `ForwardedPortLocal` with port `0u` (OS-assigned) to avoid TOCTOU races; `Dispose` calls both `Stop()` and `Dispose()` on each port. `SshConfigReader` parses `%USERPROFILE%\.ssh\config`, skips wildcard hosts, and expands `~/` paths to pre-populate the SSH tab. The tunnel is established before the migration starts and torn down in `finally`.

## Conventions

- **No stored credentials.** Connection strings stay in memory; never written to disk. Password/passphrase hashes are not reusable credentials; if credential convenience is ever approved, use an explicit OS vault design.
- **Credentials redacted in all log output.** Use `RedactedConnectionString` on `PostgresConnectionInfo`, never the raw string.
- **Identifier quoting is centralized.** `SqlIdentifier.Quote` is the only correct way to embed user-provided schema/table names in SQL. Do not concatenate raw strings.
- **Schema copy is separate from data copy.** `--schema-only` and `--data-only` exist for a reason; do not collapse them.
- **Destructive paths are explicit.** CLI: `--truncate-destination` + (`--yes` or interactive confirmation). Desktop: truncate checkbox + warning dialog whose default choice is non-destructive.
- **Keep `src/PostgresCopy.Desktop` thin.** Shared migration logic belongs in `src/PostgresCopy` so CLI and desktop stay consistent.
- **Prefer async + dispose.** All Npgsql connections, transactions, and copy streams use `await using`.

## Known Pitfalls

- Neon pooled connection strings (`*.pooler.neon.tech`) cannot be used with `pg_dump`. The UI shows a note; switch to a direct (non-pooled) connection string for schema copy.
- `NuGet.config` clears user package sources and uses nuget.org only — a missing user-level source has caused restores to fail in the past. Do not remove the `<clear/>`.
- The desktop targets `net10.0-windows` (Windows Forms). The CLI in `src/PostgresCopy` is OS-agnostic.

## Out of Scope

Per `AGENTS.md`, do not introduce: an ORM, non-PostgreSQL engines, a local web UI (the prototype was removed deliberately — the desktop `.exe` is the no-terminal path), background services, stored credentials, cloud-provider-specific branches, a general ETL framework, or upsert/conflict-resolution modes. `TODO_POLISHING.md` contains exploratory ideas, but it does not override these boundaries.

## Reference

- `README.md` — user-facing behavior and CLI flag table.
- `AGENTS.md` — long-form agent guidance and product boundaries.
- `docs/agents/RUNBOOK.md` — verification commands, smoke-check scripts, expected GUI controls.
- `docs/agents/DECISIONS.md` — design decisions already settled.
- `TODO.md` — current backlog (most items are checked).
- `TODO_POLISHING.md` — optional polish backlog for history, recipes, verification, and convenience ideas.
