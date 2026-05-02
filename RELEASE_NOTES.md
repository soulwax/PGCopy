# Release Notes

## 0.1.0

First usable PostgresCopy build.

### Included

- CLI workflow using origin and destination PostgreSQL connection strings.
- Native one-window C# desktop app for no-terminal use.
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
- Single-file publish scripts for CLI and the native desktop app.
- Documented preference for native desktop UI over a localhost web UI.

### Not Included

- Non-PostgreSQL engines.
- Conflict resolution/upserts.
- Background services.
- Credential storage.
