# Problem Solving Loop

Use this loop for PostgresCopy work.

## 1. Name the Slice

Choose one user-visible improvement:

- better desktop `.exe` workflow
- safer migration
- clearer progress
- better verification
- smaller failure surface

Avoid broad rewrites. This app should feel boring in the best way.

## 2. Find the Shared Core

Before changing desktop or CLI code, ask whether the behavior belongs in:

- `PostgresCopy.Desktop` for desktop interaction, layout, SSH form wiring, and no-terminal affordances
- `Cli` for argument parsing and terminal prompts
- `Config` for validation and connection normalization
- `Database` for PostgreSQL inspection and SQL helpers
- `Migration` for planning, copying, cleanup, and verification
- `Logging` for progress events

If both the desktop app and CLI need it, put it in the core project. If only the desktop `.exe` needs it, keep it in `PostgresCopy.Desktop`.

## 3. Protect Data First

For each change, answer:

- Can this drop, truncate, overwrite, or duplicate destination data?
- Can this accidentally connect origin to destination?
- Can this expose a password?
- Can this fail halfway through and hide that fact?
- Can this run in dry-run mode without mutating data?

If the answer is risky, add a preflight, confirmation, or explicit error.

## 4. Keep the Plan Visible

Every productive run should make these obvious:

- origin and destination, redacted
- schema
- selected tables
- whether destination will be truncated
- whether this is dry-run
- what table is currently being copied
- what failed, if anything

## 5. Verify at the Right Level

Use unit tests for:

- parsing
- validation
- redaction
- identifier quoting
- plan ordering
- dry-run and destructive-action gates

Use integration tests for:

- actual PostgreSQL copy behavior
- identity/sequence behavior
- FK ordering
- row-count verification

## 6. Update the Human Surface

If the user can notice it, update at least one of:

- `README.md`
- `TODO.md`
- native GUI labels
- desktop publish/run scripts
- CLI help text
- integration script notes

## 7. Final Check

Summarize only what changed, what passed, and what could not be verified.
