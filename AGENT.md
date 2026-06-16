# AGENT.md

Quick self-check for the next agent.

## Mission

PostgresCopy should stay dead simple: open the desktop `.exe`, paste two PostgreSQL connection strings, see the plan, run the copy, get clear progress, and know if anything failed. The CLI remains the automation companion, not the main human-facing path.

## Before Editing

1. Read `AGENTS.md`.
2. Read `TODO.md`.
3. Check `git status --short --untracked-files=all`.

## While Editing

- Treat `src/PostgresCopy.Desktop` as the primary user surface.
- Keep desktop and CLI behavior backed by the same migration core.
- Do not reintroduce a web UI — the prototype was removed; the desktop app is the no-terminal path.
- Keep destructive actions opt-in and confirmed.
- Keep database SQL simple and quoted.
- Keep messages readable enough for a non-expert.
- Prefer small files with explicit names.

## Before Final Response

Run:

```powershell
dotnet build PostgresCopy.sln
dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build
```

Also run `dotnet run --project src\PostgresCopy -- --help` after CLI changes.

For native GUI, documentation, or release-facing changes, launch the desktop app and verify the one-window flow renders. If publishing behavior changes, run or document `.\scripts\dist.ps1`.

## Current Caution

The project has an integration script, but Docker may not be installed. If Docker is unavailable, say so directly and keep unit/build verification honest.

This environment has **.NET 10 only** — `net9.0` and below are unavailable. Scaffolding commands must use `-f net10.0`. If dotnet reports a version error, that is why.

## Useful Next Work

The app is close to v1-complete. Prefer finishing touches over new product surface:

- desktop `.exe` polish, app icon, and published smoke-check notes
- first-run/preflight clarity for `pg_dump`, `psql`, Docker, and SSH config
- `--schema-only` / `--data-only` CLI flags
- redacted copy report export from the desktop operations log
- easier Docker integration diagnostics

Do not add stored credentials, a local web UI, non-PostgreSQL engines, ORM layers, background services, or cloud-specific workflow branches.
