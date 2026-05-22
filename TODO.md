# TODO

Keep PostgresCopy small: one tool, one job, clear output.

## Next

- [ ] Polish the desktop `.exe` as the default user path:
  - [x] app/window icon
  - [x] first-run sizing and header spacing
  - [x] published `.exe` smoke check notes
  - [x] local preflight tab for pg_dump, psql, Docker, and SSH config readiness
- [x] Add destination preflight checks before copying:
  - [x] destination table exists
  - [x] destination columns match origin columns
  - [x] fail before any data copy if the plan is unsafe
- [x] Fail clearly when requested origin tables do not exist.
- [x] Add a Docker Compose integration setup with two PostgreSQL databases.
- [x] Add seed SQL for a tiny realistic database with a foreign key.
- [x] Add one integration test or script that copies data and compares row counts.

## CLI

- [x] Keep `--origin` and `--destination` as the automation workflow.
- [x] Add a light prompt mode only when required values are missing.
- [x] Improve `--dry-run` output with row counts and destination readiness.
- [x] Keep future flags opt-in and obvious.

## Native GUI

- [x] Add a small native C# desktop app for no-terminal use.
- [x] Keep the GUI to one window: origin URL, destination URL, sensible options, progress log.
- [x] Use the existing migration core instead of duplicating copy logic.
- [x] Start GUI runs in dry-run mode.
- [x] Add a cancel button for active GUI runs.
- [x] Require explicit confirmation before destructive GUI truncation.
- [x] Do not store credentials or run a background service.
- [x] Add source and published desktop launchers.
- [x] Add a desktop publish script.
- [x] Add a published desktop smoke-check script.
- [x] Keep the Connection tab log scrollable and retain the latest six operation sessions.
- [x] Replace typed GUI truncate confirmation with a clearer warning confirmation.
- [x] Add a read-only database peek tab for listing databases or table row counts.
- [x] Add a local preflight tab for optional tooling checks.
- [x] Add operations log export from the desktop app.
- [x] Add severity coloring to the desktop operations log.
- [x] Expand operations log colors for active work, table data, and guidance lines.
- [x] Add private local desktop history for successful and failed dry runs/copies.

## Safety

- [x] Add `--truncate-destination` with a clear confirmation prompt.
- [x] Require `--yes` for destructive actions in non-interactive use.
- [x] Print a final summary when a migration fails partway through.
- [x] Refuse to append into non-empty destination tables without truncation.
- [x] Keep raw connection strings and passwords out of logs.
- [x] Make invalid PostgreSQL URL errors more specific.
- [x] Cover malformed PostgreSQL URL cases: missing host, invalid port, fragments, and malformed schemes.

## Copy Behavior

- [x] Copy parent tables before child tables when foreign keys are discoverable.
- [x] Add simple row-count verification with `--verify`.
- [x] Report elapsed time and rows copied per table.
- [x] Keep schema copying separate from data copying.
- [x] Add `--create-schema` to copy schema from origin via pg_dump before data transfer.
- [x] Add `--schema-only` and `--data-only` CLI flags.

## SSH Tunnel

- [x] Add SSH tunnel support for databases reachable only through a jump host.
- [x] Read `~/.ssh/config` to auto-populate SSH connection fields in the GUI.

## Tests

- [x] Add unit tests for `SchemaCreator.DropAndRecreateSchemaAsync()` — the drop-schema path added in the most recent feature slice has no coverage; at minimum test the error-exit-code branch.
- [x] Add unit tests for `MigrationSettingsValidator` — the origin ≠ destination duplicate-database check is untested.
- [x] Add unit tests for `SshConfigReader` — Host/HostName/User/Port/IdentityFile parsing, `~/` path expansion, and wildcard-host filtering are all untested.
- [x] Add integration test coverage for `--drop-schema` — CLI parser tests exist but the actual `DropAndRecreateSchemaAsync` execution path is never exercised in the Docker integration script.

## Later

- Consider CLI progress polish only when it improves automation or matches desktop log clarity.
- [x] Add elapsed-time summaries to shared dry-run/copy/schema-only completion logs.
- [x] Add publish scripts for single-file builds.
- Consider implementing `--batch-size` or removing it — the flag is parsed and validated but the binary COPY path does not batch; either wire it up or drop the flag and the README note to avoid confusion.

## Not Planned

- No heavyweight GUI, hosted dashboard, local web server as the final UI, or background service.
- No ORM.
- No non-PostgreSQL engines.
- No hidden services.
- No automatic destructive behavior.
- No general ETL features.
