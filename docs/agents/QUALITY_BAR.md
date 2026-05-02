# Quality Bar

PostgresCopy is productive when a user can trust the desktop `.exe` for a straightforward database copy without learning a large tool.

## Minimum Productive Flow

The desktop user can:

1. Open the native Windows `.exe`.
2. Provide origin and destination PostgreSQL URLs.
3. See a clear, redacted plan.
4. Run dry-run without data mutation.
5. See origin and destination row counts during dry-run.
6. Copy selected or all public tables.
7. Watch per-table progress.
8. Know exactly what failed and how far the copy got if a table fails.
9. Repeat a migration by explicitly truncating destination tables.
10. Use the same core behavior as the CLI automation path.

## Safety Quality

The app should:

- reject identical origin/destination databases
- preflight destination table and column shape
- avoid raw password output
- quote identifiers
- require confirmation for destructive actions
- refuse non-empty destination tables unless truncation is explicit
- avoid silent table skips
- keep schema-copy behavior separate from data transfer

## UX Quality

The native GUI should:

- fit in one window
- keep connection strings visible only in input fields
- show live operations in chronological order
- expose only sensible options
- make dangerous options visually and procedurally explicit
- make the published `.exe` feel like the expected way to run the app
- avoid requiring a separate localhost web server

The CLI should:

- have useful `--help`
- use clear exit codes
- avoid noisy stack traces unless `--verbose`
- print a concise final summary
- preserve parity with the migration behavior exposed in the desktop app

## Test Quality

Unit tests should cover the logic that does not require PostgreSQL.

Integration tests should cover the facts only PostgreSQL can prove:

- COPY actually succeeds
- FK ordering works against real constraints
- truncation permits repeat runs
- sequence synchronization prevents immediate identity collisions
- row counts match after copy
- `--verify` fails loudly when counts differ

## Signs of Drift

Pause and simplify if a change introduces:

- reusable workflow engines
- provider abstractions for non-PostgreSQL databases
- persistent credential storage
- background daemons
- a return of a localhost web UI in any form
- complex config files before the desktop workflow needs them
- UI pages that do not directly help run or inspect a migration
