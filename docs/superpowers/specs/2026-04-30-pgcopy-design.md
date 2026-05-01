# PGCopy Design Spec

**Date:** 2026-04-30  
**Status:** Superseded by current README/TODO direction

## Problem

Cloning a Postgres database currently requires knowing pg_dump flags, psql invocation, and managing the pipe yourself. PGCopy should make this guided and visible: you give it an origin URL and a destination URL, review a plan, and run the copy.

Current direction: keep the CLI first-class and use a small native C# desktop GUI for no-terminal use. A separate local web server is not preferred for a utility that only needs input fields, options, and an operations log.

## Decisions

| Question | Decision | Rationale |
|---|---|---|
| Invocation model | CLI first, native desktop GUI for no-terminal use | Scriptable by default, easy without a terminal |
| Copy strategy | Hybrid: pg_dump schema + Npgsql binary COPY data | Best of both: pg_dump handles DDL complexity; Npgsql gives per-table live progress |
| GUI direction | Native C# desktop, not a localhost web app | A small desktop shell fits the job better than a web server |
| Dest conflict | Wipe + overwrite with confirmation | True 1:1 clone semantics |
| Tests in v1 | None — manual Docker recipe | Thin logic; integration value > unit value |

## Historical Architecture Sketch

This section is retained as the original sketch, not as current implementation guidance. Current agents should follow `README.md`, `TODO.md`, and `docs/agents/DECISIONS.md`.

```
PGCopy/
├── PGCopy.csproj           net9.0, single-file self-contained exe
├── Program.cs              early TUI sketch
├── CopyProgress.cs         CopyPhase enum + CopyProgress record
├── ConnectionTester.cs     Npgsql connection validation helpers
├── SchemaStep.cs           Wipe dest + shell pg_dump --schema-only | psql
└── CopyEngine.cs           Discover tables, topological sort, binary COPY streaming
```

**Historical NuGet idea:** `Npgsql`, `Spectre.Console`

## Key Types

```csharp
public enum CopyPhase { Schema, Data, Done, Error }

public record CopyProgress(
    CopyPhase Phase,
    string? TableName,
    long RowsCopied,
    long TotalRows,
    string? ErrorMessage = null
);

public class CopyEngine(string originUrl, string destUrl)
{
    public IAsyncEnumerable<CopyProgress> RunAsync(CancellationToken ct);
}

public static class ConnectionTester
{
    public static Task<bool> TestAsync(string connectionString, CancellationToken ct);
    public static Task<bool> HasTablesAsync(string connStr, CancellationToken ct);
    public static Task<string> GetDatabaseNameAsync(string connStr, CancellationToken ct);
}

public static class SchemaStep
{
    // Returns null on success, error message on failure
    public static Task<string?> RunAsync(string originUrl, string destUrl, CancellationToken ct);
}
```

## Data Flow

```
1. STARTUP       Check pg_dump + psql on PATH → fail fast with install hint
2. PROMPTS       Origin URL → validate; Dest URL → validate
                 If dest has tables → require user to type DB name to confirm wipe
3. SCHEMA COPY   DROP SCHEMA public CASCADE + CREATE SCHEMA public (Npgsql)
                 pg_dump --schema-only --no-owner --no-acl | psql
4. DATA COPY     Discover public tables, topological sort by FK deps
                 Per table: SELECT COUNT(*), then COPY BINARY TO/FROM STDOUT/STDIN
                 Yield CopyProgress events throughout
5. LIVE OUTPUT   Table | Rows | Status (updates as the copy runs)
6. DONE          "Copied N tables, M rows in X.Xs" + exit 0
```

## Error Handling

| Scenario | Behavior |
|---|---|
| pg_dump/psql not on PATH | Startup error with install hint, exit 1 |
| Connection fails | Red error on prompt, re-prompt |
| User cancels confirmation | Clean exit, dest unchanged |
| pg_dump non-zero exit | Print captured stderr in red, abort |
| Row copy fails mid-table | Print table + row count at failure, exit 1 |
| Ctrl+C | CancellationToken propagated everywhere |

## Manual Test Recipe

```bash
# Origin
docker run -d --name pg-origin -p 5432:5432 -e POSTGRES_PASSWORD=test postgres

# Destination
docker run -d --name pg-dest -p 5433:5432 -e POSTGRES_PASSWORD=test postgres

# Seed origin with tables + FKs, then:
dotnet run

# Verify
psql "postgres://postgres:test@localhost:5433/postgres" -c "SELECT COUNT(*) FROM <table>"
```
