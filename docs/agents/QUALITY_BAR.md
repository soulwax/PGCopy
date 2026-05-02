# Quality Bar

PostgresCopy is productive when a user can trust it for a straightforward database copy without learning a large tool.

## Minimum Productive Flow

The user can:

1. Provide origin and destination PostgreSQL URLs.
2. See a clear, redacted plan.
3. Run dry-run without data mutation.
4. See origin and destination row counts during dry-run.
5. Copy selected or all public tables.
6. Watch per-table progress.
7. Know exactly what failed and how far the copy got if a table fails.
8. Repeat a migration by explicitly truncating destination tables.
9. Use the same core behavior from CLI or native GUI.

## Safety Quality

The app should:

- reject identical origin/destination databases
- preflight destination table and column shape
- avoid raw password output
- quote identifiers
- require confirmation for destructive actions
- refuse non-empty destination tables unless truncation is explicit
- avoid silent table skips
- leave schema-copy behavior separate until intentionally implemented

## UX Quality

The CLI should:

- have useful `--help`
- use clear exit codes
- avoid noisy stack traces unless `--verbose`
- print a concise final summary

The native GUI should:

- fit in one window
- keep connection strings visible only in input fields
- show live operations in chronological order
- expose only sensible options
- make dangerous options visually and procedurally explicit
- avoid requiring a separate localhost web server

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
- complex config files before CLI flags are stable
- UI pages that do not directly help run or inspect a migration
