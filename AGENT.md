# AGENT.md

Quick self-check for the next agent.

## Mission

PostgresCopy should stay dead simple: paste or pass two PostgreSQL connection strings, see the plan, run the copy, get clear progress, and know if anything failed.

## Before Editing

1. Read `AGENTS.md`.
2. Read `TODO.md`.
3. Check `git status --short --untracked-files=all`.

## While Editing

- Keep CLI and native GUI behavior backed by the same migration core.
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

For native GUI changes, launch the desktop app and verify the one-window flow renders.

## Current Caution

The project has an integration script, but Docker may not be installed. If Docker is unavailable, say so directly and keep unit/build verification honest.

This environment has **.NET 10 only** — `net9.0` and below are unavailable. Scaffolding commands must use `-f net10.0`. If dotnet reports a version error, that is why.
