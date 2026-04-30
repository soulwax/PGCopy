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
- [x] Add a small local web app for no-terminal use.
- [x] Add a one-command web launcher.
- [x] Add a web cancel button for active runs.
- [x] Make web runs start safely in dry-run mode with a readiness summary.
- [x] Improve `--dry-run` output with row counts and destination readiness.
- [x] Keep future flags opt-in and obvious.

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

## Later

- Consider a small Spectre.Console progress view.
- Add `--schema-only` and `--data-only` once the basic copy path is solid.
- Add schema creation only as an explicit mode.
- [x] Add publish scripts for single-file builds.

## Not Planned

- No GUI.
- No ORM.
- No non-PostgreSQL engines.
- No hidden services.
- No automatic destructive behavior.
- No general ETL features.
