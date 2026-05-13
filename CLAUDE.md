# CLAUDE.md

Quick-start for Claude Code in this repo.

## Environment

- Target: `net10.0` only — `net9.0` is not installed. Always pass `-f net10.0` to `dotnet new`.
- Shell: PowerShell 7 on Windows 11. Use PowerShell syntax in scripts.
- `pg_dump` / `psql` are available in interactive PowerShell but may not resolve in non-interactive shells. `SchemaCreator` handles this with an explicit PATH check and a clear error message.

## Project Map

| Path | Purpose |
|------|---------|
| `src/PostgresCopy` | Core library + CLI (Npgsql only, no ORM) |
| `src/PostgresCopy.Desktop` | Native Windows Forms GUI, one window |
| `tests/PostgresCopy.Tests` | Unit tests — no database needed |
| `tests/integration/` | Docker Compose + SQL seeds for manual integration |

## Build & Test

```powershell
dotnet build
dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj
dotnet run --project src\PostgresCopy.Desktop    # GUI
dotnet run --project src\PostgresCopy -- --help  # CLI
```

Run this before any commit:

```powershell
dotnet build PostgresCopy.sln && dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build
```

## Key Conventions

- **No stored credentials.** Connection strings stay in memory only.
- **Destructive actions are explicit.** CLI truncation requires a flag plus `--yes` or an interactive confirmation. Desktop truncation requires the checkbox plus a warning confirmation dialog.
- **Schema copy before data connections.** `MigrationRunner` runs `SchemaCreator` before opening Npgsql connections so PgBouncer poolers are not held open during `pg_dump`.
- **SSH tunneling** via SSH.NET. `SshTunnelConnection` creates `ForwardedPortLocal` with port `0u` (OS-assigned) to avoid TOCTOU races. Dispose calls both `Stop()` and `Dispose()` on each port.
- **`~/.ssh/config` parsing** via `SshConfigReader`. Reads `%USERPROFILE%\.ssh\config`, skips wildcard hosts, expands `~/` paths. Used to pre-populate the SSH Tunnel tab.
- **Identifier quoting** is centralized in `SqlIdentifier`. Do not build raw SQL strings with user-provided table/schema names.
- Keep `src/PostgresCopy.Desktop` thin — shared migration logic belongs in `src/PostgresCopy`.

## Safety Rules

- Origin ≠ destination is enforced before any migration starts.
- Destination schema must match (same tables) for data copy.
- `--truncate-destination` + `--yes` or an interactive CLI confirmation is required for scripted truncation.
- Desktop truncation requires the **Truncate destination** checkbox and a warning confirmation. The default dialog choice must stay non-destructive.
- Credentials are redacted in all log output.

## Known Pitfalls

- Neon pooled connection strings (`*.pooler.neon.tech`) cannot be used with `pg_dump`. The UI shows a note; use a direct (non-pooled) connection string for schema copy.

## Recommended Next Slices

PostgresCopy is close to complete; prefer polish and confidence work over new scope.

- Continue desktop `.exe` polish only when it improves the published-app path beyond the existing icon and smoke-check scripts.
- Keep the Preflight tab focused on local readiness checks only; it should not connect to user databases.
- Keep `--schema-only` / `--data-only` behavior explicit and separate from destructive data operations.
- Keep desktop copy-report export redacted by relying on the existing operations log.
- Improve Docker integration diagnostics beyond the current `-Check` mode only when it reduces real setup friction.
- Keep elapsed completion summaries in shared migration code so CLI and desktop logs stay aligned.
- Consider stronger verification only as an explicit opt-in; row counts should remain the default.

Avoid stored credentials, ORM/provider abstractions, non-PostgreSQL engines, background services, hosted dashboards, localhost web UI revival, and cloud-specific branches.
