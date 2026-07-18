# Release Notes

## 0.3.0

### Added

- **SSL required by default**: origin and destination connections now force `sslmode=require` unless opted out (`--no-origin-require-ssl` / `--no-destination-require-ssl` on the CLI, "Require SSL" checkboxes on the desktop Connection tab). Never downgrades a stricter mode already in the connection string.
- **`--all-databases` accepts a database-less URL**: the database name is no longer required in Origin/Destination for whole-server copies, since it's discarded and replaced per database anyway.
- **Per-user NSIS installer** for the Windows desktop app (`PostgresCopy-Setup-<version>.exe`), no admin rights required.

See [CHANGELOG.md](CHANGELOG.md) for the detailed, chronological list of changes going forward.

## 0.2.0

### Added

- **Copy all databases** (`--all-databases` / `--exclude-database` CLI flags, matching desktop UI): enumerates every non-system database on the origin server and drops, recreates, and copies each same-named database on the destination. Public schema only in this release; requires typed `OVERWRITE` confirmation (or `--yes` for scripts) since it is the most destructive operation in the tool.
- CLI-only publish script for Linux and macOS (`scripts/publish-cli.sh`) — the desktop app remains Windows-only.

See [CHANGELOG.md](CHANGELOG.md) for the detailed, chronological list of changes going forward.

## 0.1.0

First usable PostgresCopy build, centered on the native desktop `.exe` with a scriptable CLI companion.

### Included

- Native one-window C# desktop app for no-terminal use.
- CLI workflow using origin and destination PostgreSQL connection strings.
- Optional schema copy via `pg_dump --schema-only` (`--create-schema`).
- SSH tunneling with `~/.ssh/config` auto-population in the desktop app.
- Dry-run mode with origin/destination row counts.
- Destination schema preflight:
  - table exists
  - columns match in order
  - selected origin tables exist
- Safe destination data handling:
  - refuses to append into non-empty destination tables
  - explicit destination truncation with confirmation
- PostgreSQL binary COPY data transfer.
- Foreign-key-aware table ordering for common parent/child copies.
- Row-count verification with `--verify`.
- Identity/serial sequence synchronization after copy.
- Human-readable progress and partial failure summary.
- Docker-backed integration script.
- Single-file publish scripts for the native desktop app and CLI.
- Documented preference for native desktop UI over a localhost web UI.

### Not Included

- Non-PostgreSQL engines.
- Conflict resolution/upserts.
- Background services.
- Credential storage.
