# TODO

Keep PostgresCopy small: one tool, one job, clear output.

## Next

- Add destination preflight checks before copying:
  - destination table exists
  - destination columns match origin columns
  - fail before any data copy if the plan is unsafe
- Add a Docker Compose integration setup with two PostgreSQL databases.
- Add seed SQL for a tiny realistic database with a foreign key.
- Add one integration test or script that copies data and compares row counts.

## CLI

- Keep `--origin` and `--destination` as the primary workflow.
- Add a light prompt mode only when required values are missing.
- Improve `--dry-run` output with row counts and destination readiness.
- Keep future flags opt-in and obvious.

## Safety

- Add `--truncate-destination` with a clear confirmation prompt.
- Require `--yes` for destructive actions in non-interactive use.
- Print a final summary when a migration fails partway through.
- Keep raw connection strings and passwords out of logs.

## Copy Behavior

- Copy parent tables before child tables when foreign keys are discoverable.
- Add simple row-count verification with `--verify`.
- Report elapsed time and rows copied per table.
- Keep schema copying separate from data copying.

## Later

- Consider a small Spectre.Console progress view.
- Add `--schema-only` and `--data-only` once the basic copy path is solid.
- Add schema creation only as an explicit mode.
- Package a single-file executable when the CLI stabilizes.

## Not Planned

- No GUI.
- No ORM.
- No non-PostgreSQL engines.
- No hidden services.
- No automatic destructive behavior.
- No general ETL features.
