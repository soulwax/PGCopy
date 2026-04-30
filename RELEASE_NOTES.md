# Release Notes

## 0.1.0

First usable PostgresCopy build.

### Included

- CLI workflow using origin and destination PostgreSQL connection strings.
- Local one-window web app for no-terminal use.
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
- Human-readable progress, partial failure summary, and web cancellation.
- Docker-backed integration script.
- Single-file publish scripts for CLI and web app.

### Not Included

- Schema generation or schema copy.
- Non-PostgreSQL engines.
- Conflict resolution/upserts.
- Background services.
- Credential storage.
