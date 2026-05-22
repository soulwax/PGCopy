# Decisions

This is a lightweight decision log for future agents.

## 2026-04-30: Keep the Migration Core Shared

The migration engine in `src/PostgresCopy` remains shared. The CLI stays scriptable and stable, and any no-terminal UI must stay a thin wrapper around the same core migration code.

Reason: PostgresCopy should work in automation and should also be easy to use without a terminal. The core behavior must not fork by surface.

## 2026-04-30: Target .NET 10

Projects target `net10.0`.

Reason: the user requested the latest .NET framework available in this environment.

## 2026-04-30: Use a Native C# Desktop GUI

The no-terminal experience is a small native C# desktop GUI in `src/PostgresCopy.Desktop`. The desktop app is the primary human-facing product surface. The old `src/PostgresCopy.Web` project was an interim prototype and should not become the long-term product UI.

Reason: PostgresCopy only needs a few inputs, clear options, and live output. A localhost web server is unnecessary machinery for that job, while a native desktop shell keeps the app closer to a simple C# utility.

## 2026-04-30: Destination Schema Must Match for Now

The data-copy path assumes destination schema and tables already exist.

Reason: schema generation is a separate problem. Keeping it separate makes data transfer safer and easier to verify.

## 2026-04-30: Destructive Actions Are Explicit

Destination truncation is allowed only through `--truncate-destination` or an explicit GUI checkbox. CLI uses `--yes` or an interactive `TRUNCATE` confirmation. GUI paths require explicit confirmation.

Reason: productive repeated migrations need a way to empty destination tables, but destructive behavior must never be surprising.

## 2026-04-30: Use Npgsql Binary COPY for Data

Data transfer uses PostgreSQL binary COPY streams through Npgsql.

Reason: it is fast, PostgreSQL-native, and avoids loading full tables into memory.

## 2026-04-30: Docker Integration Is Manual

The integration setup is a PowerShell script plus Docker Compose files, not an always-on test project.

Reason: database integration tests are valuable but should not make normal unit test runs heavy or fragile.

## 2026-04-30: Verification Starts With Row Counts

`--verify` compares origin and destination row counts after copying.

Reason: row counts are cheap, understandable, and useful as a first trust check. Checksums can come later if needed.

## 2026-04-30: NuGet Sources Are Repo-Local

`NuGet.config` clears inherited package sources and uses nuget.org.

Reason: a missing user-level NuGet source caused restore failures in this environment.

## 2026-04-30: Unit Tests Are Kept From the Start

The original brainstorming sketch suggested skipping unit tests in v1. This was reversed: `tests/PostgresCopy.Tests` exists with 9 test suites covering parsing, validation, planning, safety gates, and redaction.

Reason: the logic in `Cli`, `Config`, and `Migration` is testable without a real database, catches regressions without Docker, and builds quickly. Skipping them created more risk than the overhead cost.

## 2026-04-30: Do Not Assume pg_dump Is on PATH

Schema-copy behavior via `pg_dump` and `psql` depends on external PostgreSQL client tools. Any code that shells out to these tools must verify their path in the shell that will actually launch the app. Non-interactive sessions in this environment did not resolve `pg_dump` by name even when it was available in an interactive shell.

Reason: silent tool-not-found failures during copy are worse than a clear preflight failure. Verify the path explicitly and fail with a useful message if it is missing.

## 2026-05-01: This Environment Has .NET 10 Only

Projects target `net10.0`. The framework `net9.0` is not installed in this environment and scaffolding commands using `-f net9.0` will fail with an invalid option error.

Reason: confirmed during initial scaffolding attempt. Always use `-f net10.0` for `dotnet new` commands in this repo.

## 2026-05-01: Schema Copy via pg_dump Is Implemented

`SchemaCreator.CreateAsync` shells out to `pg_dump --schema-only --no-owner --no-acl` and pipes its stdout to `psql`. It checks both tools on PATH at startup and returns a descriptive error if either is missing or times out. The desktop GUI exposes this as "Create schema (requires pg_dump)".

Reason: destination tables must exist before data copy can run, and `pg_dump` is the safest way to reproduce the schema exactly including sequences, indexes, and constraints.

## 2026-05-01: SSH Tunnel via SSH.NET

SSH tunneling is implemented in `SshTunnelConnection` (Desktop project only). It wraps SSH.NET's `SshClient` and `ForwardedPortLocal`. Port `0u` is passed to `ForwardedPortLocal` — the OS assigns a free port, avoiding any TOCTOU race. `Dispose` calls `Stop()` then `Dispose()` on each forwarded port.

Reason: some PostgreSQL instances are only reachable via an SSH jump host. The tunnel is established before Npgsql connections are opened and torn down in the `finally` block of the run handler.

## 2026-05-01: Credentials as Connection Strings Are Accepted

The app accepts Npgsql connection strings which contain passwords in plaintext. This is an accepted risk for a desktop utility — the user supplies the string and it lives only in process memory. Strings are never logged or written to disk.

Reason: the alternative (per-field credential entry) is more machinery for no security gain in a single-user local desktop tool. If a secure vault integration is needed later, it can be added as an optional layer.

## 2026-05-01: SSH Config Auto-Population

`SshConfigReader` parses `%USERPROFILE%\.ssh\config` at startup. The SSH Tunnel tab shows a dropdown of named hosts from that file (wildcard patterns are skipped). Selecting a host pre-fills SSH host, port, username, and key path.

Reason: reduces friction for users who already have SSH hosts configured. The user requested this explicitly.

## 2026-05-02: Web Prototype Removed

`src/PostgresCopy.Web/`, the `Start-PostgresCopy-Web*.cmd` launchers, and the `run-web` / `publish-web` / `run-published-web` scripts were deleted. The Web project was removed from the solution. `scripts/check.ps1` no longer builds or smoke-tests a web app.

Reason: the native Desktop GUI now covers every workflow the web prototype provided plus more (SSH tunnel, `~/.ssh/config` auto-population, schema copy). Maintaining two UI surfaces violates the "one tool, one job" rule. The decision to use a native desktop GUI over a local web server is preserved in the 2026-04-30 entry above; do not reintroduce a web UI without revisiting that decision first.

## 2026-05-02: Desktop Executable Is the Default User Path

Documentation, agent guidance, and release-facing work should lead with the native Windows desktop `.exe`. The CLI remains supported for automation, smoke checks, and shared-core parity, but it should not be presented as the default manual workflow.

Reason: the app now has a complete one-window GUI with connection fields, SSH tunnel configuration, schema copy, dry-run, destructive confirmation, cancel, and live operations log. That is the friendliest path for the intended no-terminal use case.

## 2026-05-07: Desktop Truncate Confirmation Uses a Warning Dialog

The desktop app still requires an explicit **Truncate destination** checkbox, but it no longer requires typing `TRUNCATE` into a separate field. A real copy with truncation selected shows a warning dialog explaining what will be deleted, why it matters, and that origin is not changed. The default button is non-destructive.

Reason: the typed confirmation was clumsy in the one-window flow. The safety boundary remains explicit at the moment rows would be deleted, while the Connection tab stays cleaner and easier to understand.

## 2026-05-22: Desktop Run History Is Local and Redacted

The desktop app keeps a durable History tab for successful dry runs, successful copies, failures, and cancellations. Entries are stored as JSON under `%LOCALAPPDATA%\PostgresCopy\history.json` for the current Windows profile only. Raw connection strings are not stored; history uses the same redacted connection string representation as the operations log.

Reason: users need memory of what they tried and what completed without turning PostgresCopy into a hosted dashboard or credential store. Local-only, redacted history preserves the privacy promise while making repeat manual work more visible.
