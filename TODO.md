# TODO

Keep PostgresCopy small: one tool, one job, clear output.

## Next

- [x] Add destination preflight checks before copying:
  - [x] destination table exists
  - [x] destination columns match origin columns
  - [x] fail before any data copy if the plan is unsafe
- [x] Fail clearly when requested origin tables do not exist.
- [x] Add a Docker Compose integration setup with two PostgreSQL databases.
- [x] Add seed SQL for a tiny realistic database with a foreign key.
- [x] Add one integration test or script that copies data and compares row counts.

## CLI

- [x] Keep `--origin` and `--destination` as the primary workflow.
- [x] Add a light prompt mode only when required values are missing.
- [x] Improve `--dry-run` output with row counts and destination readiness.
- [x] Keep future flags opt-in and obvious.

## Native GUI

- [x] Add a small native C# desktop app for no-terminal use.
- [x] Keep the GUI to one window: origin URL, destination URL, sensible options, progress log.
- [x] Use the existing migration core instead of duplicating copy logic.
- [x] Start GUI runs in dry-run mode.
- [x] Add a cancel button for active GUI runs.
- [x] Require typing `TRUNCATE` before destructive GUI truncation.
- [x] Do not store credentials or run a background service.
- [x] Add source and published desktop launchers.
- [x] Add a desktop publish script.

## Interim Web Prototype

- [x] Add a small local web app to prove the no-terminal workflow.
- [x] Add a one-command web launcher.
- [x] Add a one-command published web launcher.
- [x] Add a web cancel button for active runs.
- [x] Make web runs start safely in dry-run mode with a readiness summary.
- [ ] Replace or de-emphasize the web prototype once the native GUI exists.

## Safety

- [x] Add `--truncate-destination` with a clear confirmation prompt.
- [x] Require `--yes` for destructive actions in non-interactive use.
- [x] Print a final summary when a migration fails partway through.
- [x] Refuse to append into non-empty destination tables without truncation.
- [x] Keep raw connection strings and passwords out of logs.

## Copy Behavior

- [x] Copy parent tables before child tables when foreign keys are discoverable.
- [x] Add simple row-count verification with `--verify`.
- [x] Report elapsed time and rows copied per table.
- [x] Keep schema copying separate from data copying.
- [x] Add `--create-schema` to copy schema from origin via pg_dump before data transfer.

## SSH Tunnel

- [x] Add SSH tunnel support for databases reachable only through a jump host.
- [x] Read `~/.ssh/config` to auto-populate SSH connection fields in the GUI.

## Later

- Consider CLI progress polish only after the native GUI direction is settled.
- Add `--schema-only` and `--data-only` once the basic copy path is solid.
- [x] Add publish scripts for single-file builds.

## Not Planned

- No heavyweight GUI, hosted dashboard, local web server as the final UI, or background service.
- No ORM.
- No non-PostgreSQL engines.
- No hidden services.
- No automatic destructive behavior.
- No general ETL features.
