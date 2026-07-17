# Copy All Databases (Whole-Server Overwrite) — Design

Status: proposed, pending user review.

## Problem

PostgresCopy today copies exactly one origin database to one destination database, scoped to one schema and an optional table filter. There is no way to replicate an entire PostgreSQL server (every user database) to another server in one run, and no destructive primitive stronger than "drop destination schema, then recreate it" (`DestinationTableCleaner` / `SchemaCreator` with `--drop-schema`). This feature request asks for both: enumerate every database on the origin server, and for each one, completely overwrite the same-named destination database (drop the whole database, not just its schema, then recreate and copy).

This is a materially larger blast radius than anything the app does today — dropping a database requires terminating any other sessions connected to it, and operates against a *different* connection (a maintenance database, not the target database itself) than the rest of the app has ever needed. It sits close to the "general ETL framework" boundary called out in `AGENTS.md`, so this design deliberately keeps the feature narrow: whole-database schema + data copy, nothing about roles, tablespaces, extensions, or cluster-level objects (i.e. not `pg_dumpall --globals`).

## Decisions locked in during brainstorming

- Scope: enumerate every database on the **origin server** (not "every schema in one database").
- Overwrite level: **drop the entire destination database** and recreate it (stronger than existing truncate/drop-schema paths).
- Maintenance database reachability is **not assumed** — preflight must explicitly verify it and fail with a clear message if unreachable.
- Other active sessions on a destination database being dropped are **force-terminated** (`pg_terminate_backend`), logged with a count, gated behind the same explicit confirmation as the drop itself.
- Confirmation UX: **one global confirmation** that lists every affected database by name, not a per-database pause.
- System databases (`template0`, `template1`, `postgres`) are **always excluded** from "copy all", non-configurable.
- Database selection is a **checklist** (defaults to all non-system databases selected), not strictly all-or-nothing — covers both "copy everything" and "copy these 3 of 12".
- Within each selected database, the existing Schema/Tables filters are **ignored** — full database copy (every schema, every table). These fields are disabled in the UI while this mode is active.
- Schema creation (pg_dump/psql) is **mandatory**, not optional, in this mode — the destination database was just dropped and recreated empty, so there is nothing to copy data into otherwise.
- Ships in **both** desktop GUI and CLI in the same pass.
- Partial failure handling: if one database fails mid-batch, the run **continues to the remaining databases** and reports all failures in a final summary — one bad database does not block the rest, and no failure is ever silently swallowed.
- Final confirmation requires **typing a confirmation word** (e.g. `OVERWRITE`), not just a Yes/No click — proportional to the larger blast radius (whole databases, other sessions forcibly disconnected) versus the existing Yes/No dialogs used for schema-level drops.

## Architecture

### New primitive: database lifecycle operations

A new class in `src/PostgresCopy/Database/` (name TBD at planning time, e.g. `DestinationDatabaseLifecycle`), responsible for server-level operations that operate on a **maintenance connection** — a connection to a database other than the one being dropped/created, since PostgreSQL cannot drop the database a connection is currently using.

Responsibilities:
- `ListDatabasesAsync(connection)` — enumerate databases on a server, excluding `template0`, `template1`, `postgres`. Reuses the same catalog query `DatabasePeekInspector` already uses for the Peek tab's "list databases" mode.
- `TryOpenMaintenanceConnectionAsync(connectionInfo)` — attempts to open a connection to the same server/credentials but against the `postgres` database, falling back to `template1` if `postgres` is not reachable. Returns a clear failure reason if neither works. This is the explicit preflight check requested — never assumed to succeed.
- `TerminateOtherBackendsAsync(maintenanceConnection, targetDbName)` — runs a `pg_terminate_backend` sweep over `pg_stat_activity` for the named database (excluding the maintenance connection's own backend), returns the count terminated for logging.
- `DropDatabaseAsync(maintenanceConnection, targetDbName)` — `DROP DATABASE IF EXISTS "targetDbName"` after backends are terminated.
- `CreateDatabaseAsync(maintenanceConnection, targetDbName)` — `CREATE DATABASE "targetDbName"`.

All identifiers go through `SqlIdentifier.Quote`, consistent with the rest of the codebase's identifier-quoting rule.

### New orchestration: batch runner

A new class (e.g. `AllDatabasesMigrationRunner`) that wraps the existing `MigrationRunner` rather than duplicating its pipeline:

1. Open a connection to the origin server's maintenance database; enumerate databases; exclude system databases; intersect with the user's checklist selection (desktop) or `--exclude-database` list (CLI).
2. Open (or verify openable) a maintenance connection to the destination server. Fail the entire run here, before anything destructive, if this does not succeed.
3. **Dry run**: for each selected database, connect read-only to the origin copy of that database, report table/row counts, and state "destination database `X` will be dropped and recreated" — no destructive action taken.
4. **Real run**, sequentially per database (not parallel, to keep log output legible and avoid overloading the server):
   a. Terminate other backends on the destination database (log count).
   b. Drop the destination database.
   c. Create the destination database.
   d. Run `SchemaCreator` (pg_dump/psql) unconditionally against the fresh database.
   e. Run the existing `MigrationRunner` pipeline scoped to that database, with schema/table filters disabled (full copy).
   f. Row-count verification, same as today's opt-in verify step (respects the existing verify checkbox/flag).
   g. Record the outcome (succeeded/failed + message) without stopping the batch.
5. After all databases are attempted, emit one summary: total requested, succeeded, failed, with per-database elapsed time and row counts — and continue even if some failed, per the locked-in decision.

### History

One `DesktopRunHistoryEntry` per database in the batch, sharing a batch identifier (new field, or encoded into the existing `Message` field at first pass — exact shape decided during implementation planning) so related entries can be visually grouped later. No change to the existing History ListView, filtering, or (previously designed) prefill behavior — they keep working unchanged since each row is still a normal entry.

## Desktop UI changes

- **Connection tab**: new checkbox, "Copy all databases (overwrite destination entirely)". When checked:
  - Schema and Tables text fields are disabled with a tooltip explaining they don't apply in this mode.
  - A "Load databases" button appears; clicking it connects to the origin server and populates a checklist (`CheckedListBox`) of every non-system database found, all pre-checked.
  - The existing Truncate/Create-schema/Drop-schema checkboxes are disabled in this mode — this mode's drop+recreate+schema-create sequence supersedes them, avoiding overlapping/contradictory destructive toggles.
- **Confirmation dialog**: new, more severe than the existing Yes/No warning dialogs. Lists every database that will be dropped and recreated by name, states that other active connections to those databases will be forcibly terminated, states there is no undo, and requires typing a confirmation word (e.g. `OVERWRITE`) into a text box before the Copy button in the dialog becomes enabled.
- **Dry run**: reports per-database plan (see orchestration step 3) in the operations log, same visual style as today's dry-run reporting.
- **History tab**: unaffected structurally; gains one row per database per batch run.

## CLI changes

- `--all-databases` flag. Mutually exclusive with `--schema`, `--tables`, `--data-only`, `--schema-only` (parser rejects the combination with a clear error, consistent with existing mutually-exclusive flag validation).
- `--exclude-database <name>` (repeatable), for opting specific databases out without an interactive checklist.
- Destructive confirmation reuses the existing `--yes` / `InteractiveCliPrompt` gate, but this flag specifically requires typing `OVERWRITE` at the interactive prompt (extending `InteractiveCliPrompt`) — mirrors the desktop's typed-confirmation dialog. `--yes` alone still works non-interactively (for scripting), consistent with how other destructive flags behave today.
- Dry-run output lists every database and its drop/recreate/copy plan, matching desktop.

## Explicitly out of scope

- Parallel per-database copying — sequential only.
- Per-database schema/table filtering — all-or-nothing full copy per database in this mode.
- Roles, tablespaces, extensions, or other cluster-level objects (no `pg_dumpall --globals` equivalent).
- A generalized "recipe" or scheduling system around this feature — it is a single run, same lifecycle as every other copy in the app today.

## Testing implications

- Unit tests for the new lifecycle primitive: system-database exclusion list, maintenance-connection fallback logic (postgres → template1), backend-termination query shape, identifier quoting.
- Unit tests for the batch runner: partial-failure continuation behavior (one failing database does not abort the batch), summary correctness.
- Integration test extension (`scripts/integration-test.ps1`): seed multiple databases on the origin container, verify all are dropped/recreated/copied correctly on the destination container, and that excluded system databases are untouched.
- CLI smoke test: `--all-databases` combined with a forbidden flag (e.g. `--schema`) fails fast with a clear error, consistent with existing CLI validation smoke checks.
