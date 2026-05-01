# AGENT.md

Quick self-check for the next agent.

## Mission

PostgresCopy should stay dead simple: paste or pass two PostgreSQL connection strings, see the plan, run the copy, get clear progress, and know if anything failed.

## Before Editing

1. Read `AGENTS.md`.
2. Read `TODO.md`.
3. Check `git status --short --untracked-files=all`.
4. Check whether the interim web prototype is running before building:

```powershell
Get-NetTCPConnection -LocalPort 5087 -ErrorAction SilentlyContinue
```

Stop only the known local dev server if it is locking build outputs.

## While Editing

- Keep CLI, native GUI, and interim web behavior backed by the same migration core.
- Prefer the small native C# desktop GUI for no-terminal work; do not expand the web prototype into the long-term product UI.
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

For native GUI changes, launch the desktop app and verify the one-window flow renders. For interim web changes, launch the web app only long enough to verify the page renders, then close or mention the running server before finishing.

## Current Caution

The project has an integration script, but Docker may not be installed. If Docker is unavailable, say so directly and keep unit/build verification honest.
