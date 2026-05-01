# Release Notes

## 0.1.0

First usable PostgresCopy build.

### Included

- CLI workflow using origin and destination PostgreSQL connection strings.
- Native one-window C# desktop app for no-terminal use.
- Interim local one-window web prototype for no-terminal use.
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
- Human-readable progress, partial failure summary, and interim web cancellation.
- Docker-backed integration script.
- Single-file publish scripts for CLI and interim web prototype.
- Single-file publish script for the native desktop app.
- Documented preference for native desktop UI over a localhost web UI.

### Not Included

- Schema generation or schema copy.
- Non-PostgreSQL engines.
- Conflict resolution/upserts.
- Background services.
- Credential storage.
