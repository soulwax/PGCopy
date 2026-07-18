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

### Desktop app from source

```powershell
.\Start-PostgresCopy-Desktop.cmd
```

Equivalently:

```powershell
dotnet run --project src\PostgresCopy.Desktop
```

### Desktop app — published single-file `.exe` (no .NET needed)

```powershell
.\scripts\publish-desktop.ps1
.\artifacts\PostgresCopy-desktop-win-x64\PostgresCopy.Desktop.exe
```

### CLI — dry run

```powershell
dotnet run --project src/PostgresCopy -- `
    --origin      "postgres://postgres:secret@localhost:5432/source" `
    --destination "postgres://postgres:secret@localhost:5433/target" `
    --dry-run
```

### CLI — full copy

```powershell
dotnet run --project src/PostgresCopy -- `
    --origin      "postgres://postgres:secret@localhost:5432/source" `
    --destination "postgres://postgres:secret@localhost:5433/target" `
    --create-schema --truncate-destination --yes --verify
```

---

## What It Is

PostgresCopy is a small, focused Windows desktop utility that copies the contents of one PostgreSQL database into another. You paste two connection strings into one window; it shows you a plan, optionally rebuilds the destination schema from the origin, copies the data using PostgreSQL's binary `COPY` protocol, and verifies and repairs row counts when it's done.

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
- **Row-count verification** with `--verify`, including bounded repair retries for mismatched tables.
- **Sequence sync** — identity/serial sequences are realigned after copy so new inserts don't collide.
- **Truncate gate** — destination truncation requires an explicit checkbox and warning confirmation before rows are deleted.
- **Clear diagnostics** — invalid URLs call out missing hosts, missing database names, bad ports, fragments, unsupported schemes, malformed schemes, and common password-encoding problems.
- **Private local history** — successful dry runs, successful copies, failures, and cancellations are saved under the current Windows profile with redacted connection strings.
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

```powershell
dotnet run --project src/PostgresCopy -- `
    --origin      "postgres://postgres:secret@localhost:5432/source" `
    --destination "postgres://postgres:secret@localhost:5433/target" `
    --dry-run
```

Always start with `--dry-run` against a new pair of databases.

---

## Workflow — Desktop App

This is the default manual workflow. The desktop window has five tabs (**Connection**, **Preflight**, **Peek into Database**, **History**, and **SSH Tunnel**) and a live operations log at the bottom.

### 1. Connection tab

| Field | Purpose |
|---|---|
| **Origin URL** | `postgres://user:pwd@host:5432/source` or any Npgsql connection string. |
| **Destination URL** | Same shape, must point to a *different* database. |
| **Schema** | Defaults to `public`. |
| **Tables** | Optional, comma-separated. Empty = all base tables in the schema. |
| **Verify and repair counts** | Compares origin and destination row counts after the copy. If a table mismatches, PostgresCopy clears and recopies that destination table plus planned dependent tables, then verifies again. |
| **Truncate destination** | Empties planned destination tables before copying (shows a warning confirmation before deleting rows). |
| **Create destination schema** | Copies DDL from origin to destination via `pg_dump \| psql` *before* opening data connections. Recommended for empty destination databases. |
| **Drop destination schema first (DESTRUCTIVE)** | When schema creation is enabled, drops and recreates the destination schema before applying origin DDL. Shows a separate warning confirmation. |

The footer has separate **Dry run** and **Copy** buttons. Use **Dry run** to preview the plan and counts; use **Copy** when the destination is ready. Leave **Create destination schema** off for a pure no-write preview.

### 2. Preflight tab

Use **Check environment** before a first copy or release smoke check. It writes local readiness checks to the operations log:

- `pg_dump` and `psql` availability for schema-copy workflows.
- Docker CLI and daemon availability for the integration script.
- `%USERPROFILE%\.ssh\config` host detection for SSH auto-population.

Use **Get pg tools** if `pg_dump` and `psql` are missing and you want the app to download PostgreSQL client tools into the local `tools\` directory beside the executable. This requires `winget` and internet access, does not install system-wide tools, and deletes the downloaded zip after extraction.

Preflight does not connect to your databases and does not store credentials.

### 3. Peek into Database tab

Use this for a quick read-only look before copying:

- Paste a PostgreSQL URL without a database name, such as `postgres://user:pwd@host:5432`, to list databases visible to that user.
- Paste a PostgreSQL URL with a database name, such as `postgres://user:pwd@host:5432/app`, to list user tables and row counts.

Results are written to the operations log in the same console-style format as dry runs and copies. Passwords are redacted.

### 4. History tab

The History tab keeps a local-only record of dry runs and copies, separated into successful runs and failures/cancellations. It records the run time, mode, redacted origin and destination, schema, table filter, elapsed time, row totals when available, and a short result message.

History is stored as JSON under the current Windows user's local application data folder (`%LOCALAPPDATA%\PostgresCopy\history.json`). Passwords are redacted before saving, and raw connection strings are never written to disk. Use **Clear history** to delete the local history file. History is currently a record of past runs, not a saved-credential or one-click rerun system.

### 5. SSH Tunnel tab *(optional)*

If your database is only reachable via an SSH jump host:

1. Pick a host from the **~/.ssh/config** dropdown (auto-populated from `%USERPROFILE%\.ssh\config`) — or fill the fields manually.
2. Check **Origin**, **Destination**, or both under **Tunnel for**.
3. Paste or confirm the origin and destination database URLs shown on the tab.
4. Choose authentication: password or private key file.
5. Set **Remote host** to where PostgreSQL is visible *from the SSH server* (typically `localhost:5432`).
6. Click **Test tunnel** to open the SSH tunnel and run a tiny database read check through the selected origin/destination URL(s).

The tunnel is established before the migration starts and torn down in `finally` when the run ends.

### 6. Copy checklist

1. *(Empty destination?)* Check **Create destination schema**.
2. *(Need to rebuild a wrong destination schema?)* Check **Drop destination schema first (DESTRUCTIVE)** only when the destination schema can be deleted.
3. *(Behind a jump host?)* Configure the **SSH Tunnel** tab.
4. Paste both URLs.
5. Click **Dry run**. Read the operations log carefully.
6. *(Replacing existing data?)* Check **Truncate destination** and confirm the warning when you start the copy.
7. Keep **Verify and repair counts** checked and click **Copy**.
8. Watch the log. The final line reports tables copied, rows transferred, and elapsed time.

The **Cancel** button stops an in-flight migration cleanly via `CancellationToken`. The **Save log** button exports the visible, redacted operations log as a text or Markdown file.

---

## Workflow — CLI Automation

Use the CLI for repeatable scripts, CI smoke checks, or terminal-first workflows. For manual one-off copies, prefer the desktop app.

```powershell
dotnet run --project src/PostgresCopy -- `
    --origin      "postgres://postgres:secret@localhost:5432/source" `
    --destination "postgres://postgres:secret@localhost:5433/target" `
    --create-schema `
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
| `--drop-schema` | Before `--create-schema`, drop and recreate the destination schema. |
| `--schema-only` | Run the schema copy and stop before copying table data. |
| `--data-only` | Copy table data only. Destination schema must already match. |
| `--dry-run` | Print the plan, validate, report counts — but copy nothing. |
| `--truncate-destination` | Empty destination tables before copying. |
| `--yes` | Skip the interactive `TRUNCATE` confirmation (for scripts). |
| `--verify` | Compare origin and destination row counts after the copy. Mismatched tables are cleared, recopied, and verified again up to a bounded retry limit. |
| `--all-databases` | Copy all non-system databases from origin to destination (DESTRUCTIVE). Drops and recreates each destination database. Cannot be combined with `--schema`, `--table`/`--tables`, `--schema-only`, `--data-only`, `--create-schema`, `--drop-schema`, or `--truncate-destination`. Requires typing `OVERWRITE` at the confirmation prompt (or `--yes` non-interactively). |
| `--exclude-database <name>` | Skip this database when using `--all-databases`. May be passed multiple times. |
| `--batch-size <n>` | Reserved for future use. Defaults to 10000. |
| `--verbose` | Print stack traces for unexpected failures. |
| `--help` | Show the built-in help. |

### Copy all databases (destructive mode)

`--all-databases` enumerates every non-system database on the origin server and drops, recreates, and copies each same-named database on the destination. Schema creation is mandatory in this mode and cannot be disabled. The confirmation prompt requires typing `OVERWRITE` (or use `--yes` for non-interactive scripts) because every matching destination database will be completely recreated. Use `--exclude-database <name>` to skip specific databases from the operation.

### Scripted use

```powershell
dotnet run --project src/PostgresCopy -- `
    --origin      "$env:ORIGIN_URL" `
    --destination "$env:DEST_URL" `
    --truncate-destination --yes `
    --verify
```

Exit code is non-zero on any failure, validation error, or row-count mismatch that remains after repair retries.

---

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
│ 11. (Verify?) Compare row counts; repair mismatches, retry    │
└──────────────────────────────────────────────────────────────┘
```

---

## Safety Model

PostgresCopy refuses to act when something looks wrong:

- **Origin = destination.** The two connection strings must normalize to different databases.
- **Schema mismatch.** Every planned destination table must exist with matching columns in matching order. The migration aborts before any data is copied.
- **Non-empty destination.** Append-into-existing is refused; you must explicitly opt into `--truncate-destination`.
- **Truncate confirmation.** CLI requires `--yes` or an interactive confirmation. The GUI requires the truncate checkbox and a warning confirmation before the copy starts.
- **Credentials.** Passwords are redacted in every log line. Connection strings are never written to disk.
- **History.** Desktop history stores redacted run metadata only; it does not store reusable passwords or passphrases.

Stack traces are hidden behind `--verbose` so accidental log capture doesn't leak internals.

---

## Build, Test, Run — Complete Reference

### Solution layout

```
PostgresCopy.sln
├── src/
│   ├── PostgresCopy/              Console app (CLI), net10.0
│   │   ├── Cli/                   Argument parsing, help text
│   │   ├── Config/                Settings, validation, connection string handling
│   │   ├── Database/              Postgres inspection, identifier quoting
│   │   ├── Migration/             Planning, copying, schema creation, verification
│   │   └── Logging/               Progress events
│   └── PostgresCopy.Desktop/      Windows Forms GUI, net10.0-windows
│       ├── Assets/                Embedded logo
│       ├── MainForm.cs            One-window UI
│       ├── SshTunnelConnection.cs
│       └── SshConfigReader.cs
├── tests/
│   ├── PostgresCopy.Tests/        xUnit unit tests (no DB required)
│   └── integration/               Docker Compose + SQL seeds
└── scripts/                       PowerShell launchers, publish + check scripts
```

### Build

```powershell
# ── Build everything (CLI + Desktop + unit tests) ──
dotnet build PostgresCopy.sln

# ── Build individual projects ──
dotnet build src/PostgresCopy                     # CLI only
dotnet build src/PostgresCopy.Desktop             # Desktop only
dotnet build tests\PostgresCopy.Tests             # Unit tests only

# ── Release build ──
dotnet build src/PostgresCopy -c Release
dotnet build src/PostgresCopy.Desktop -c Release

# ── Clean + rebuild ──
dotnet clean PostgresCopy.sln
dotnet build PostgresCopy.sln
```

Output locations:

| Project | Debug | Release |
|---|---|---|
| CLI | `src\PostgresCopy\bin\Debug\net10.0\` | `src\PostgresCopy\bin\Release\net10.0\` |
| Desktop | `src\PostgresCopy.Desktop\bin\Debug\net10.0-windows\` | `src\PostgresCopy.Desktop\bin\Release\net10.0-windows\` |

### Run in Debug

```powershell
# ── Desktop GUI ──
dotnet run --project src\PostgresCopy.Desktop

# ── CLI: help ──
dotnet run --project src\PostgresCopy -- --help

# ── CLI: dry run (no data written) ──
dotnet run --project src/PostgresCopy -- `
    --origin      "postgres://postgres:secret@localhost:5432/source" `
    --destination "postgres://postgres:secret@localhost:5433/target" `
    --dry-run

# ── CLI: schema + data copy ──
dotnet run --project src/PostgresCopy -- `
    --origin              "postgres://postgres:secret@localhost:5432/source" `
    --destination         "postgres://postgres:secret@localhost:5433/target" `
    --create-schema       `
    --truncate-destination `
    --yes                 `
    --verify

# ── CLI: schema-only (DDL only, no data) ──
dotnet run --project src/PostgresCopy -- `
    --origin      "postgres://postgres:secret@localhost:5432/source" `
    --destination "postgres://postgres:secret@localhost:5433/target" `
    --schema-only

# ── CLI: data-only (destination schema must match) ──
dotnet run --project src/PostgresCopy -- `
    --origin      "postgres://postgres:secret@localhost:5432/source" `
    --destination "postgres://postgres:secret@localhost:5433/target" `
    --data-only

# ── CLI: specific tables ──
dotnet run --project src/PostgresCopy -- `
    --origin      "postgres://postgres:secret@localhost:5432/source" `
    --destination "postgres://postgres:secret@localhost:5433/target" `
    --tables accounts,orders,invoices `
    --verify

# ── CLI: custom schema ──
dotnet run --project src/PostgresCopy -- `
    --origin      "postgres://postgres:secret@localhost:5432/source" `
    --destination "postgres://postgres:secret@localhost:5433/target" `
    --schema myschema
```

**Important:** The `--` separator tells `dotnet run` to pass everything after it to the application. Omitting it causes `dotnet run` to consume the flags instead.

### Run in Release (framework-dependent)

```powershell
dotnet build src/PostgresCopy -c Release
dotnet run --project src/PostgresCopy -c Release -- --help

# Or run the compiled DLL directly:
dotnet src\PostgresCopy\bin\Release\net10.0\PostgresCopy.dll --help
```

Requires the .NET 10 runtime on the target machine. The DLL is small (~70 KB) but the runtime overhead (≈100 MB install) is separate.

### Publish self-contained single-file (no prerequisites)

```powershell
# ── Desktop ──
dotnet publish src\PostgresCopy.Desktop `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    --output artifacts\PostgresCopy-desktop-win-x64

# ── CLI ──
dotnet publish src\PostgresCopy `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    --output artifacts\PostgresCopy-cli-win-x64

# ── Run the published binaries ──
.\artifacts\PostgresCopy-desktop-win-x64\PostgresCopy.Desktop.exe
.\artifacts\PostgresCopy-cli-win-x64\PostgresCopy.exe --help
```

**Flag reference for `dotnet publish`:**

| Flag | Purpose |
|---|---|
| `--configuration Release` | Optimised build: enables JIT tiering, inlining, no debug symbols |
| `--runtime win-x64` | Target runtime identifier for x64 Windows. Change to `win-arm64` for ARM64 |
| `--self-contained true` | Embed the .NET runtime — no system-wide install needed on the target machine |
| `-p:PublishSingleFile=true` | Merge all assemblies into a single `.exe` file (no side-by-side DLLs) |
| `-p:EnableCompressionInSingleFile=true` | Compress embedded assemblies inside the single file (smaller output, slightly slower startup) |
| `--output <path>` | Destination directory for the published files |

Result: a single `PostgresCopy.Desktop.exe` (~80 MB) or `PostgresCopy.exe` (~60 MB) that runs on any Windows x64 machine with **zero prerequisites**.

For ARM64 targets:

```powershell
dotnet publish src\PostgresCopy --configuration Release --runtime win-arm64 --self-contained true -p:PublishSingleFile=true --output artifacts\PostgresCopy-cli-win-arm64
```

### Run unit tests

```powershell
# ── Build + test in one step ──
dotnet test tests\PostgresCopy.Tests

# ── Build once, test repeatedly ──
dotnet build tests\PostgresCopy.Tests
dotnet test tests\PostgresCopy.Tests --no-build

# ── Filter by test class name ──
dotnet test tests\PostgresCopy.Tests --filter "FullyQualifiedName~CliOptionsParserTests"

# ── Filter by test trait / category ──
dotnet test tests\PostgresCopy.Tests --filter "Category=Validation"

# ── Verbose output ──
dotnet test tests\PostgresCopy.Tests -v d

# ── Run in Release mode ──
dotnet test tests\PostgresCopy.Tests -c Release
```

Unit tests cover: argument parsing, settings validation, planner FK ordering, identifier quoting, credential redaction, and schema creator logic. They do **not** require a running PostgreSQL.

### One-shot check before committing

```powershell
.\scripts\check.ps1                      # build + unit tests + CLI smoke
.\scripts\check.ps1 -IncludeIntegration  # adds the Docker integration run
```

For desktop-facing changes, also launch the GUI:

```powershell
dotnet run --project src\PostgresCopy.Desktop
```

### Run the integration test (Docker)

```powershell
# ── Quick prereq check (no containers started) ──
.\scripts\integration-test.ps1 -Check

# ── Full integration run ──
.\scripts\integration-test.ps1

# ── Keep containers running for manual inspection ──
.\scripts\integration-test.ps1 -KeepContainers

# ── Also test the --drop-schema scenario ──
.\scripts\integration-test.ps1 -DropSchema
```

The integration script starts two PostgreSQL containers in Docker, seeds the origin, runs PostgresCopy, and compares row counts. Requires **Docker Desktop** (or compatible Docker runtime) running on the machine.

The `-DropSchema` variant spins up a third container with a deliberately wrong schema, runs `--drop-schema --schema-only` to rebuild it from the origin DDL, then `--data-only` to copy the rows — verifying the full repair workflow.

### Publishing distribution artifacts

#### One-shot full distribution

```powershell
.\scripts\dist.ps1
```

This runs: standard checks → publish desktop (with smoke test) → publish CLI → create `.zip` archives → write `SHA256SUMS.txt`.

Output:

```
artifacts/
  PostgresCopy-desktop-win-x64/PostgresCopy.Desktop.exe
  PostgresCopy-cli-win-x64/PostgresCopy.exe
  dist/
    PostgresCopy-desktop-win-x64.zip
    PostgresCopy-cli-win-x64.zip
    SHA256SUMS.txt
```

Options:

```powershell
.\scripts\dist.ps1 -NoArchive          # skip zip creation, just the raw folders
.\scripts\dist.ps1 -SkipChecks         # skip build + test + CLI smoke
.\scripts\dist.ps1 -SkipSmokeCheck     # publish but skip desktop exe smoke test
.\scripts\dist.ps1 -Runtime win-arm64  # target ARM64
```

#### CLI-only distribution on Linux / macOS

The desktop app is Windows-only (WinForms, `net10.0-windows`) — there is no Linux or macOS build of it. The CLI (`src/PostgresCopy`) is OS-agnostic and can be published on Linux/macOS with `scripts/publish-cli.sh`:

```bash
./scripts/publish-cli.sh                 # auto-detects linux-x64 / osx-x64 / osx-arm64
./scripts/publish-cli.sh linux-arm64      # explicit runtime identifier
./scripts/publish-cli.sh osx-arm64 Debug  # explicit runtime + configuration
```

This mirrors `publish-cli.ps1`: a self-contained, single-file, compressed executable written to `artifacts/PostgresCopy-cli-<runtime>/PostgresCopy`. There is no `dist.sh` equivalent to `dist.ps1` — no desktop artifact exists to bundle alongside it, and no Linux/macOS integration/smoke-check tooling exists yet for the CLI (the existing `check.ps1`/`integration-test.ps1`/smoke scripts are PowerShell-only). Run `dotnet test tests/PostgresCopy.Tests/PostgresCopy.Tests.csproj` manually first if you want unit-test coverage before publishing on these platforms.

### Cutting a release (maintainer checklist)

1. **Land all changes on `main`** and confirm a clean working tree (`git status`).
2. **Update version and changelog:**
   - Bump the version in both `src/PostgresCopy/PostgresCopy.csproj` and `src/PostgresCopy.Desktop/PostgresCopy.Desktop.csproj` (`<Version>`/`<AssemblyVersion>` properties).
   - Add an entry to [RELEASE_NOTES.md](RELEASE_NOTES.md) and update the "Current version" line under [Release](#release) below.
3. **Run the full pre-flight check** (build + unit tests + CLI smoke, no publishing yet):
   ```powershell
   .\scripts\check.ps1
   .\scripts\check.ps1 -IncludeIntegration   # optional, requires Docker
   ```
4. **Build the Windows distribution:**
   ```powershell
   .\scripts\dist.ps1
   ```
   This re-runs the same checks as step 3 (skip with `-SkipChecks` only if you just ran them), then publishes and zips both the desktop `.exe` and CLI `.exe`, and writes `artifacts\dist\SHA256SUMS.txt`.
5. **(Optional) Build the Linux/macOS CLI artifact** on a machine with the corresponding OS, or via a CI runner targeting it — this repository's own dev environment is Windows, so this step cannot be exercised from here:
   ```bash
   ./scripts/publish-cli.sh linux-x64
   ./scripts/publish-cli.sh osx-arm64
   ```
   Compute checksums for these manually (e.g. `shasum -a 256 artifacts/PostgresCopy-cli-linux-x64/PostgresCopy`) and append them to the same `SHA256SUMS.txt` convention if distributing alongside the Windows artifacts.
6. **Smoke-test the published desktop build** (already covered by `dist.ps1`'s default smoke check, or run standalone):
   ```powershell
   .\scripts\smoke-published-desktop.ps1 -Launch
   ```
   Manually verify: origin/destination fields, dry-run/copy button text, cancel path, SSH tab, and — after this branch — the Connection tab's "Copy all databases" checkbox, database checklist, and typed-`OVERWRITE` confirmation dialog.
7. **Tag the release** in git (`git tag vX.Y.Z && git push --tags`) once the artifacts are verified.
8. **Attach artifacts to the release**: `artifacts\dist\PostgresCopy-desktop-win-x64.zip`, `artifacts\dist\PostgresCopy-cli-win-x64.zip`, `artifacts\dist\SHA256SUMS.txt`, plus any Linux/macOS CLI archives built in step 5.

### Lightweight per-user install

Install the published desktop app without an MSI or admin rights:

```powershell
.\scripts\install-desktop.ps1
```

This copies the desktop `.exe` and its clean uninstaller to `%LOCALAPPDATA%\Programs\PostgresCopy`, creates a Start Menu shortcut, and registers a per-user uninstall entry under Windows **Installed apps**.

Uninstall from Windows **Installed apps**, or run:

```powershell
& "$env:LOCALAPPDATA\Programs\PostgresCopy\Uninstall-PostgresCopy.ps1"
```

Uninstall removes the installed app folder, Start Menu shortcut, uninstall registry entry, and local PostgresCopy data under `%LOCALAPPDATA%\PostgresCopy` including history.
#### Individual publish steps

```powershell
.\scripts\publish-desktop.ps1                          # desktop only
.\scripts\publish-desktop.ps1 -SmokeCheck              # desktop + smoke test
.\scripts\publish-desktop.ps1 -Runtime win-arm64       # ARM64 desktop
.\scripts\publish-cli.ps1                              # CLI only
.\scripts\publish-cli.ps1 -Runtime win-arm64           # ARM64 CLI
```

#### Bundle PostgreSQL tools (pg_dump, psql)

If the target machine does not have PostgreSQL installed, these scripts download and bundle `pg_dump.exe`, `psql.exe`, and `libpq.dll` into the published artifact's `tools\` subdirectory:

```powershell
# Download + bundle PG17 tools into desktop + CLI artifacts
.\scripts\bundle-pg-tools.ps1 -UpdateFirst

# Bundle PG18 tools instead
.\scripts\bundle-pg-tools.ps1 -UpdateFirst -PgVersion 18

# Bundle only into the desktop artifact (existing pg-tools staging)
.\scripts\bundle-pg-tools.ps1 -Target desktop

# Update staging only, don't bundle yet
.\scripts\update-pg-tools.ps1
```

The bundled tools are automatically discovered by the app at runtime if present in the `tools\` sibling directory.

#### Smoke test a published desktop build

```powershell
.\scripts\smoke-published-desktop.ps1                # non-interactive CLI smoke (launch + close)
.\scripts\smoke-published-desktop.ps1 -Launch        # opens the GUI for visual check
.\scripts\publish-desktop.ps1 -SmokeCheck            # publish + smoke in one step
```

#### Convenience launchers

```powershell
.\Start-PostgresCopy-Desktop.cmd               # build & run desktop from source (debug)
.\Start-PostgresCopy-Desktop-Published.cmd     # launch published desktop .exe
.\Dist-PostgresCopy.cmd                        # run the full dist pipeline
```

### Debugging in VS Code

The `.vscode/launch.json` provides these debug configurations (press **F5** to select and launch):

| Configuration | What it does |
|---|---|
| **PostgresCopy Desktop** | Builds and launches the Windows Forms GUI |
| **PostgresCopy CLI: Help** | Builds CLI and runs `--help` |
| **PostgresCopy CLI: Dry Run Sample** | Builds CLI and runs a dry-run with sample URLs |
| **PostgresCopy Dist** | Runs `dist.ps1` from within VS Code |

Pre-launch build tasks (`build desktop`, `build cli`) build the relevant project automatically before each debug session.

---

## Troubleshooting

### Build and run errors

| Error | Likely cause | Fix |
|---|---|---|
| `error NETSDK1082: No runtime pack available for net10.0` | .NET 10 SDK not installed | Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| `error MSB4216: Could not run the "Dollar" task` | Solution filter mismatch | Run `dotnet build PostgresCopy.sln` instead of opening a nested `.csproj` directly |
| `The term 'dotnet' is not recognized` | .NET SDK not on PATH | Restart terminal after install, or use `C:\Program Files\dotnet\dotnet.exe` |
| `error MSB3030: Could not copy the file "..."` | File locked by another process (e.g. running exe) | Close the running app and `dotnet clean` first |
| `dotnet publish fails with NU1301: Unable to load the service index for source` | No network or offline | Run `dotnet restore` with `--source` pointing to a local or cached feed, or use `--no-restore` if packages are already cached |
| `warning MSB3277: Found conflicts between different versions of` | NuGet version mismatch | Run `dotnet restore` to refresh package references |

### Runtime errors

| Error | Likely cause | Fix |
|---|---|---|
| `System.DllNotFoundException: Unable to load DLL 'libpq'` | Published CLI run without bundled tools | Bundle pg-tools: `.\scripts\bundle-pg-tools.ps1` or install PostgreSQL locally |
| `Unhandled exception: Npgsql.NpgsqlException: Exception while connecting` | PostgreSQL not reachable at the given host/port | Verify with `pg_isready -h localhost -p 5432` |
| `FATAL: password authentication failed` | Wrong credentials in URL | Check `postgres://user:**password**@host:5432/db` — URL-encode `@`, `#`, `%` in the password |
| `Npgsql.PostgresException: 3D000: database "..." does not exist` | Database name in URL is wrong or hasn't been created | Create it first: `createdb -h localhost -U postgres dbname` |
| `Npgsql.PostgresException: 42710: schema "public" already exists` | Destination already has a schema when `--create-schema` is used | Add `--drop-schema` to rebuild from origin, or use `--data-only` if schema already matches |
| `Npgsql.PostgresException: 42P01: relation "..." does not exist` | Schema mismatch between origin and destination | Ensure destination has all tables with matching columns before `--data-only` |
| `Verification failed: row count mismatch` | Counts still differed after PostgresCopy retried mismatched tables | Check for triggers, concurrent writes, failed repair logs, or a destination database modified during the copy |
| `dotnet run --` arguments are ignored | Missing `--` separator before app arguments | Must be: `dotnet run --project src/PostgresCopy -- --dry-run` (note the space before `--dry-run`) |
| `No tables selected` | Schema is empty or table filter matches nothing | Run with `--dry-run` to see the plan, or check `--schema` and `--tables` values |
| `Relative path not found: tools\pg_dump.exe` | Published exe can't find bundled tools | The executable looks for a `tools\` directory next to itself. Run from within the artifact folder or bundle via `bundle-pg-tools.ps1` |

### SSH tunnel issues

| Error | Likely cause | Fix |
|---|---|---|
| `SSH.NET: Unable to connect to the remote server` | SSH host/port unreachable or wrong | Verify `ssh user@host -p port` works from the terminal first |
| `SSH.NET: Private key file not found` | Wrong path to `.pem`/`.ppk`/OpenSSH key | Use the full absolute path, or use password authentication |
| `SSH.NET: Key exchange negotiation failed` | Server uses an unsupported algorithm | Use `~/.ssh/config` entry — the desktop app auto-populates from it |
| `Tunnel test: connection refused` | PostgreSQL is not reachable from the SSH server | The remote host in the SSH tunnel config should typically be `localhost` (as seen from the jump host) |

### Connection URL syntax

PostgresCopy accepts `postgres://` or `postgresql://` URLs and Npgsql connection strings.

**Correct:**

```
postgres://myuser:mypassword@localhost:5432/mydb
postgresql://myuser:mypassword@localhost:5432/mydb
Host=localhost;Port=5432;Database=mydb;Username=myuser;Password=mypassword
```

**Common mistakes:**

| Wrong URL | Problem | Fix |
|---|---|---|
| `postgres://user:pass@localhost/mydb` | Missing port | Add `:5432` after host |
| `postgres://pass@localhost:5432/db` | Missing username | Use `user:pass@` format |
| `postgres://user:p@ss@localhost:5432/db` | `@` in password is parsed as delimiter | URL-encode `@` as `%40`: `user:p%40ss@` |
| `postgres://user:pass#1@localhost:5432/db` | `#` in password truncates the rest | URL-encode `#` as `%23`: `user:pass%231@` |
| `localhost:5432/mydb` | Missing scheme | Prepend `postgres://` |

---

## Known Limits

- Destination schema is either copied via `--create-schema`, `--schema-only`, or `pg_dump` *or* must already exist for `--data-only`.
- Copies are bulk table-data transfers, not upserts or conflict resolution.
- FK ordering covers discoverable in-schema dependencies only.
- `pg_dump` cannot use Neon pooled connection strings (`*.pooler.neon.tech`) — use a direct connection for `--create-schema`.
- Windows-first: the desktop GUI is Windows Forms (`net10.0-windows`). The CLI itself is OS-agnostic.

---

## FAQ

**Why does my PostgreSQL URL fail to parse?** Use `postgres://` or `postgresql://`, include a host and database name for copy operations, use a numeric port such as `5432`, and percent-encode special characters in usernames or passwords. For example, `@` inside a password should be written as `%40`, and `#` should be `%23`.

**Why PostgreSQL only?** A clear scope. Adding other engines would invite an ORM and a configuration framework, which would dilute everything.

**Why a desktop app *and* a CLI instead of a web UI?** A local web server is more machinery than this tool needs. The desktop app is the primary manual workflow and feels like the small utility it is; the CLI handles automation. A localhost web prototype existed early on and was deliberately removed.

**Why C# and .NET 10?** Strong PostgreSQL story (Npgsql), excellent async I/O, simple single-file publishing for both CLI and Windows Forms.

**Why not just use `pg_dump | pg_restore`?** PostgresCopy uses `pg_dump` for the optional schema step, but for data it streams binary COPY directly between the two live databases — no intermediate file, with live progress, FK-aware ordering, sequence sync, and verification baked in. For one-off operator work, `pg_dump | pg_restore` is fine. For a tool you'll run repeatedly, this is friendlier.

**How do I use this with Neon / RDS / Cloud SQL?** Use the same `postgres://` URL you'd use with `psql`. For Neon, avoid `*.pooler.neon.tech` (pooled) for the schema step — use the direct connection. For RDS, SSH tunneling is supported if the database is in a private subnet.

**Does PostgresCopy support Azure Database for PostgreSQL?** Yes — use your Azure PostgreSQL connection string in `postgres://` format. If SSL is required, Azure PostgreSQL connections work with Npgsql's default SSL settings.

**Can I copy between different PostgreSQL versions?** Within PostgreSQL 13+, yes. The binary COPY protocol is stable across versions. Schema DDL generated by `pg_dump` may need adjustment for very old origins.

---

## License

GPLv3.0 — see [LICENSE.md](LICENSE.md).

## Release

Current version: **0.1.0** — see [RELEASE_NOTES.md](RELEASE_NOTES.md).
