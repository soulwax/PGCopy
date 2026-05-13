
# PostgresCopy
<p align="center">
  <img src="src/PostgresCopy.Desktop/Assets/kitsunedb.png" alt="PostgresCopy logo — a kitsune leaping from database A to database B" width="512" height="512" />
</p>

KISSS (Keep it simple, stupid, and safe) database copier. Copy database A to B. No surprises, no stored credentials, no telemetry, no background service. Just a single-window desktop app and a CLI automation companion that do one thing — copy Postgres databases — and do it well.

---

<p align="center">
  <img src="docs/assets/pgcopy.png" alt="PostgresCopy desktop app main window" width="900" />
</p>

<p align="center">
  <strong>This software is proudly Free and Open Source; as in Freedom, not free beer.</strong><br />
  <br />
  <a href="https://github.com/soulwax/PGCopy">GitHub repository</a>
</p>

---

## Quick Use (TL;DR)

### Recommended: desktop `.exe`

Build and run from source:

```powershell
.\Start-PostgresCopy-Desktop.cmd
```

Or build a local debug `.exe`:

```powershell
dotnet build src\PostgresCopy.Desktop -c Release
```

Output: `src\PostgresCopy.Desktop\bin\Release\net10.0-windows\PostgresCopy.Desktop.exe` — needs the .NET 10 runtime installed to run.

### Distributable (single-file, self-contained)

```powershell
.\scripts\publish-desktop.ps1
```

Output: `artifacts\PostgresCopy-desktop-win-x64\PostgresCopy.Desktop.exe` — bundles the .NET runtime, runs on any Windows x64 machine without prerequisites.

CLI automation build:
`.\scripts\publish-cli.ps1` → `artifacts\PostgresCopy-cli-win-x64\PostgresCopy.exe`

Make sure to get .NET 10 from https://dotnet.microsoft.com/download if you want to run the debug builds from source.

## What It Is

PostgresCopy is a small, focused Windows desktop utility that copies the contents of one PostgreSQL database into another. You paste two connection strings into one window; it shows you a plan, optionally rebuilds the destination schema from the origin, copies the data using PostgreSQL's binary `COPY` protocol, and verifies row counts when it's done.

It is deliberately *not* a generic ETL framework. It does one thing — clone a Postgres database — and tries to do that without surprises.

**Two ways to run it:**
- **Native desktop app** — the main workflow. Paste two URLs, configure SSH if needed, dry-run, copy, watch progress.
- **CLI** — the automation companion. Scriptable, pipeable, and backed by the same migration core.

Both share the same migration core, so copy behavior is kept consistent.

## Highlights

- **Binary COPY** for fast streaming data transfer (no row-at-a-time inserts).
- **Optional schema copy** via `pg_dump --schema-only` if the destination is empty.
- **SSH tunneling** for databases reachable only through a jump host, with auto-population from `~/.ssh/config`.
- **Dry-run by default** — every workflow starts with a no-op preview.
- **Foreign-key-aware ordering** — parent tables copy before children.
- **Row-count verification** with `--verify`.
- **Sequence sync** — identity/serial sequences are realigned after copy so new inserts don't collide.
- **Truncate gate** — destination truncation requires an explicit checkbox and warning confirmation before rows are deleted.
- **No stored credentials, no background service, no telemetry.**

## Quick Start

### Prerequisites

- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download)). The project targets `net10.0` exclusively.
- **PostgreSQL 13+** as origin and destination.
- *(Optional)* `pg_dump` and `psql` on PATH if you want `--create-schema`.
- *(Optional)* Docker if you want to run the integration test script.

### Run the desktop app from source

```powershell
.\Start-PostgresCopy-Desktop.cmd
```

Or, equivalently:

```powershell
dotnet run --project src\PostgresCopy.Desktop
```

### Run the CLI automation companion from source

```bash
dotnet run --project src/PostgresCopy -- \
  --origin      "postgres://postgres:secret@localhost:5432/source" \
  --destination "postgres://postgres:secret@localhost:5433/target" \
  --dry-run
```

Always start with `--dry-run` against a new pair of databases.

## Workflow — Desktop App

This is the default manual workflow. The desktop window has four tabs (**Connection**, **Preflight**, **Peek into Database**, and **SSH Tunnel**) and a live operations log at the bottom.

### 1. Connection tab

| Field | Purpose |
|---|---|
| **Origin URL** | `postgres://user:pwd@host:5432/source` or any Npgsql connection string. |
| **Destination URL** | Same shape, must point to a *different* database. |
| **Schema** | Defaults to `public`. |
| **Tables** | Optional, comma-separated. Empty = all base tables in the schema. |
| **Dry run** | On by default. Performs every check and reports counts without copying. |
| **Verify counts** | Compares origin and destination row counts after the copy. |
| **Truncate destination** | Empties planned destination tables before copying (shows a warning confirmation before deleting rows). |
| **Create schema (requires pg_dump)** | Copies DDL from origin to destination via `pg_dump \| psql` *before* opening data connections. |

### 2. Preflight tab

Use **Check environment** before a first copy or release smoke check. It writes local readiness checks to the operations log:

- `pg_dump` and `psql` availability for schema-copy workflows.
- Docker CLI and daemon availability for the integration script.
- `%USERPROFILE%\.ssh\config` host detection for SSH auto-population.

Preflight does not connect to your databases and does not store credentials.

### 3. Peek into Database tab

Use this for a quick read-only look before copying:

- Paste a PostgreSQL URL without a database name, such as `postgres://user:pwd@host:5432`, to list databases visible to that user.
- Paste a PostgreSQL URL with a database name, such as `postgres://user:pwd@host:5432/app`, to list user tables and row counts.

Results are written to the operations log in the same console-style format as dry runs and copies. Passwords are redacted.

### 4. SSH Tunnel tab *(optional)*

If your database is only reachable via an SSH jump host:

1. Pick a host from the **~/.ssh/config** dropdown (auto-populated from `%USERPROFILE%\.ssh\config`) — or fill the fields manually.
2. Check **Origin**, **Destination**, or both under **Tunnel for**.
3. Paste or confirm the origin and destination database URLs shown on the tab.
4. Choose authentication: password or private key file.
5. Set **Remote host** to where PostgreSQL is visible *from the SSH server* (typically `localhost:5432`).
6. Click **Test tunnel** to open the SSH tunnel and run a tiny database read check through the selected origin/destination URL(s).

The tunnel is established before the migration starts and torn down in `finally` when the run ends.

### 5. Copy checklist

1. *(Empty destination?)* Check **Create schema**.
2. *(Behind a jump host?)* Configure the **SSH Tunnel** tab.
3. Paste both URLs.
4. Keep **Dry run** checked. Click **Run dry run**. Read the operations log carefully.
5. *(Replacing existing data?)* Check **Truncate destination** and confirm the warning when you start the copy.
6. Uncheck **Dry run**, keep **Verify counts** checked, click **Run copy**.
7. Watch the log. The final line reports tables copied, rows transferred, and elapsed time.

The **Cancel** button stops an in-flight migration cleanly via `CancellationToken`. The **Save log** button exports the visible, redacted operations log as a text or Markdown file.

## Workflow — CLI Automation

Use the CLI for repeatable scripts, CI smoke checks, or terminal-first workflows. For manual one-off copies, prefer the desktop app.

```bash
dotnet run --project src/PostgresCopy -- \
  --origin      "postgres://postgres:secret@localhost:5432/source" \
  --destination "postgres://postgres:secret@localhost:5433/target" \
  --create-schema \
  --verify
```

### All options

| Flag | Effect |
|---|---|
| `--origin <url>` | Origin URL or Npgsql connection string. **Required.** |
| `--destination <url>` | Destination URL or Npgsql connection string. **Required.** |
| `--schema <name>` | Schema to copy. Defaults to `public`. |
| `--table <name>` | Copy a single table. May be passed multiple times. |
| `--tables <csv>` | Copy comma-separated tables. |
| `--create-schema` | Run `pg_dump --schema-only` from origin into destination first. |
| `--schema-only` | Run the schema copy and stop before copying table data. |
| `--data-only` | Copy table data only. Destination schema must already match. |
| `--dry-run` | Print the plan, validate, report counts — but copy nothing. |
| `--truncate-destination` | Empty destination tables before copying. |
| `--yes` | Skip the interactive `TRUNCATE` confirmation (for scripts). |
| `--verify` | Compare origin and destination row counts after the copy. |
| `--batch-size <n>` | Reserved for future use. Defaults to 10000. |
| `--verbose` | Print stack traces for unexpected failures. |
| `--help` | Show the built-in help. |

### Scripted use

```bash
dotnet run --project src/PostgresCopy -- \
  --origin      "$ORIGIN_URL" \
  --destination "$DEST_URL" \
  --truncate-destination --yes \
  --verify
```

Exit code is non-zero on any failure, validation error, or count mismatch.

## How a Copy Runs

```
┌──────────────────────────────────────────────────────────────┐
│ 1. Validate connection strings (origin ≠ destination)         │
│ 2. (Optional) pg_dump --schema-only origin | psql destination │
│ 3. Open Npgsql connections                                    │
│ 4. Discover origin tables + foreign-key dependencies          │
│ 5. Topologically sort tables by FK dependency                 │
│ 6. Preflight: every planned table exists on destination       │
│                with matching columns in matching order        │
│ 7. (Dry run?) Report counts and stop                          │
│ 8. (Truncate?) Empty destination tables                       │
│ 9. For each table:                                            │
│      a. COPY <table> TO STDOUT (BINARY)   on origin           │
│      b. COPY <table> FROM STDIN (BINARY)  on destination      │
│      c. Stream-pipe between the two                           │
│      d. Log progress per table                                │
│ 10. Realign sequences on destination                          │
│ 11. (Verify?) Compare row counts; fail on mismatch            │
└──────────────────────────────────────────────────────────────┘
```

## Safety Model

PostgresCopy refuses to act when something looks wrong:

- **Origin = destination.** The two connection strings must normalize to different databases.
- **Schema mismatch.** Every planned destination table must exist with matching columns in matching order. The migration aborts before any data is copied.
- **Non-empty destination.** Append-into-existing is refused; you must explicitly opt into `--truncate-destination`.
- **Truncate confirmation.** CLI requires `--yes` or an interactive confirmation. The GUI requires the truncate checkbox and a warning confirmation before the copy starts.
- **Credentials.** Passwords are redacted in every log line. Connection strings are never written to disk.

Stack traces are hidden behind `--verbose` so accidental log capture doesn't leak internals.

## Build, Test, Run

### One-shot check before committing

```powershell
dotnet build PostgresCopy.sln
dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build
```

Or use the bundled script:

```powershell
.\scripts\check.ps1                      # build + unit tests + CLI smoke
.\scripts\check.ps1 -IncludeIntegration  # adds the Docker integration run
```

For desktop-facing changes, also launch the GUI:

```powershell
dotnet run --project src\PostgresCopy.Desktop
```

### Project layout

```
src/
  PostgresCopy/          Shared migration core + CLI automation companion
    Cli/                 Argument parsing, help text
    Config/              Settings, validation, connection string handling
    Database/            Postgres inspection, identifier quoting
    Migration/           Planning, copying, schema creation, verification
    Logging/             Progress events
  PostgresCopy.Desktop/  Primary Windows Forms GUI and desktop .exe
    Assets/              Embedded logo
    MainForm.cs          One-window UI
    SshTunnelConnection.cs / SshConfigReader.cs

tests/
  PostgresCopy.Tests/    xUnit unit tests (no DB required)
  integration/           Docker Compose + SQL seeds for the manual integration run

scripts/                 PowerShell launchers, publish + check scripts
```

### Run the unit tests

Unit tests cover argument parsing, settings validation, planner FK ordering, identifier quoting, and credential redaction. They do not need a running PostgreSQL.

```powershell
dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj
```

### Run the integration test (Docker)

The integration script spins up two PostgreSQL containers, seeds the origin, runs PostgresCopy, and compares row counts.

```powershell
.\scripts\integration-test.ps1 -Check          # verify Docker/.NET prerequisites only
.\scripts\integration-test.ps1
.\scripts\integration-test.ps1 -KeepContainers  # leave the containers running for inspection
```

Requires Docker Desktop or compatible runtime.

## Publishing Self-Contained Builds

The desktop app and CLI can be published as single-file, self-contained Windows executables. The desktop `.exe` is the primary release artifact for normal use.

```powershell
.\scripts\publish-desktop.ps1
.\scripts\publish-cli.ps1
```

Run a non-interactive smoke check after publishing the desktop `.exe`:

```powershell
.\scripts\publish-desktop.ps1 -SmokeCheck
.\scripts\smoke-published-desktop.ps1
```

For a visual smoke check, launch the published app and verify the header/logo, app icon, Connection tab, Preflight tab, Peek tab, SSH tab, Save log button, and operations log:

```powershell
.\scripts\smoke-published-desktop.ps1 -Launch
```

Output lands under `artifacts/`:

```
artifacts/
  PostgresCopy-desktop-win-x64/PostgresCopy.Desktop.exe
  PostgresCopy-cli-win-x64/PostgresCopy.exe
```

Convenience launchers for the published builds:

```powershell
.\Start-PostgresCopy-Desktop-Published.cmd
```

## Known Limits

- Destination schema is either copied via `--create-schema`, `--schema-only`, or `pg_dump` *or* must already exist for `--data-only`.
- Copies are bulk table-data transfers, not upserts or conflict resolution.
- FK ordering covers discoverable in-schema dependencies only.
- `pg_dump` cannot use Neon pooled connection strings (`*.pooler.neon.tech`) — use a direct connection for `--create-schema`.
- Windows-first: the desktop GUI is Windows Forms (`net10.0-windows`). The CLI itself is OS-agnostic.

## FAQ

**Why PostgreSQL only?** A clear scope. Adding other engines would invite an ORM and a configuration framework, which would dilute everything.

**Why a desktop app *and* a CLI instead of a web UI?** A local web server is more machinery than this tool needs. The desktop app is the primary manual workflow and feels like the small utility it is; the CLI handles automation. A localhost web prototype existed early on and was deliberately removed.

**Why C# and .NET 10?** Strong PostgreSQL story (Npgsql), excellent async I/O, simple single-file publishing for both CLI and Windows Forms.

**Why not just use `pg_dump | pg_restore`?** PostgresCopy uses `pg_dump` for the optional schema step, but for data it streams binary COPY directly between the two live databases — no intermediate file, with live progress, FK-aware ordering, sequence sync, and verification baked in. For one-off operator work, `pg_dump | pg_restore` is fine. For a tool you'll run repeatedly, this is friendlier.

## License

GPLv3.0 — see [LICENSE.md](LICENSE.md).

## Release

Current version: **0.1.0** — see [RELEASE_NOTES.md](RELEASE_NOTES.md).
