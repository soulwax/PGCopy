# TODO_POLISHING

Ideas for making PostgresCopy feel more like a small Swiss Army knife while keeping its core promise: PostgreSQL-only, desktop-first, predictable, visible, and safe.

This file is exploratory. It is not permission to store secrets, add a background service, add cloud-provider branches, or turn the app into a general ETL tool.

## Guardrails

- Keep the default privacy model: no raw connection strings, database passwords, SSH passwords, or SSH key passphrases written to disk.
- Saved convenience should start as local redacted metadata: labels, hosts, ports, database names, schemas, table filters, options, row counts, timestamps, and result summaries.
- Treat "saved transaction" as a saved copy recipe plus immutable run report, not a long-lived PostgreSQL transaction.
- Keep every destructive rerun behind the same dry-run, checkbox, and warning-confirmation gates.
- Password and passphrase hashes are useful only for fingerprints or "same secret typed again" checks. They cannot reconnect to PostgreSQL or unlock SSH keys. If no-retyping auth is desired, design an explicit opt-in OS vault feature.

## Saved Copy Recipes

- [ ] Promote selected History rows into saved recipes with a friendly name, redacted origin/destination metadata, schema, table filter, and option defaults.
- [ ] Add "Run again" from History/Recipes that pre-fills the Connection and SSH tabs but still requires missing secrets unless an approved vault feature exists.
- [ ] Add "Update recipe from this run" after a successful dry run or copy, with a small diff of changed fields before saving.
- [ ] Show last dry run, last copy, last failure, row totals, and elapsed time on each recipe.
- [ ] Export a saved recipe as a CLI command that uses environment-variable placeholders for secrets.

## Credential Convenience, Explicit Opt-In

- [ ] Write a short design decision before implementing any credential persistence.
- [ ] Prefer Windows Credential Manager or DPAPI for optional no-retyping support; never store usable secrets in `history.json`, recipe JSON, logs, or exports.
- [ ] If hashes are added, use them only as salted local fingerprints to match a typed secret to a saved vault entry or warn "this appears to be the same passphrase."
- [ ] Add per-secret delete controls, a "forget all secrets" action, and clear UI copy that says where secrets live.
- [ ] For SSH keys, save non-secret key path, host, user, and key fingerprint first; make passphrase vaulting a separate checkbox.

## Better History

- [ ] Make History searchable and filterable by mode, success/failure, endpoint label, schema, table, and date.
- [ ] Add a details view with the full redacted plan, run options, row counts, elapsed time, and saved log link.
- [ ] Keep the current 200-entry cap by default, but consider a setting for retention count or age.
- [ ] Add "Copy report" and "Save report" actions for Markdown/JSON summaries.
- [ ] Detect repeated failures for the same recipe and surface the latest likely cause without hiding the raw log.

## Updateable Past Copies

- [ ] Add a "Refresh destination" workflow for a saved recipe: dry-run first, show destination row counts and schema drift, then allow Copy with existing confirmations.
- [ ] Show what changed since the last successful run: origin row-count deltas, destination row-count deltas, schema/table list differences, and verification state.
- [ ] Add "Schema changed since last run" warnings when the saved recipe's last known table/column fingerprint differs from the current origin or destination.
- [ ] Let users intentionally update a saved recipe after changing table filters, schema, SSH host, or verification options.
- [ ] Avoid incremental/upsert semantics unless a future decision explicitly expands scope; default refresh should remain safe bulk copy with preflight and optional truncate/drop.

## Stronger Verification

- [ ] Add opt-in sampled checksum verification for selected tables.
- [ ] Add opt-in full-table checksum verification with clear warnings about time, locking, and database load.
- [ ] Verify sequence positions after copy and report identities/sequences that could still collide.
- [ ] Show verification coverage in the final report: row counts only, sampled checksum, or full checksum.
- [ ] Let users mark critical tables for stronger verification while leaving large low-risk tables on row counts.

## Preflight And Planning

- [ ] Show an estimated copy size and rough duration from relation sizes and row counts.
- [ ] Expand schema drift preflight to summarize missing indexes, constraints, triggers, enums, extensions, and sequence defaults.
- [ ] Add a permissions preflight that checks whether the current roles can read origin tables and write/truncate/drop destination objects before the run starts.
- [ ] Warn when origin or destination appears to be receiving writes during a dry run or copy.
- [ ] Surface direct-vs-pooled connection hints for schema copy without becoming provider-specific.

## Local Tooling Polish

- [ ] Turn "Get pg tools" into a small tools manager: installed version, source, update action, and bundle status.
- [ ] Verify `pg_dump`/`psql` major versions and warn when they are likely too old for the origin server.
- [ ] Add a copyable diagnostics bundle: app version, .NET version, OS, pg tools status, Docker status, and redacted recent errors.
- [ ] Improve Docker integration feedback when Docker Desktop is installed but the daemon is stopped.

## SSH Polish

- [ ] Save SSH profiles as non-secret metadata: config host, actual host, port, username, key path, key fingerprint, remote database host/port.
- [ ] Add a "Test SSH only" action separate from "Test tunnel + database."
- [ ] Support separate remote host/port for origin and destination when both tunnel through the same jump host.
- [ ] Remember the last selected SSH config host without storing passwords or passphrases.

## CLI Parity

- [ ] Add `--recipe <name>` only after desktop recipes exist, and keep secrets supplied by env vars, prompts, or an explicit vault feature.
- [ ] Add a JSONL progress mode for automation while preserving the current human-readable default.
- [ ] Decide whether `--batch-size` should become real behavior or be removed from parser/help/docs.
- [ ] Add a CLI command to print the same redacted final report format the desktop can save.

## UI Shine

- [ ] Double-click a History/Recipe row to prefill the form.
- [ ] Add small status badges for pg tools, Docker, SSH config, and last successful copy.
- [ ] Add keyboard shortcuts for Dry run, Copy, Cancel, Save log, and search history.
- [ ] Add a compact "review before copy" summary dialog for saved recipes and destructive runs.
- [ ] Keep the one-window design; prefer tabs, split panels, and details drawers over new top-level windows.

## Not Planned Without A New Decision

- Background scheduled sync jobs.
- Non-PostgreSQL engines.
- Upsert/conflict-resolution modes.
- Cloud-provider-specific workflows.
- A localhost web UI.
- Raw credential storage.
- Password/passphrase hashes used as if they were passwords.
