# Copy All Databases (Whole-Server Overwrite) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let PostgresCopy enumerate every non-system database on an origin PostgreSQL server, and for each one, completely drop and recreate the same-named destination database, then copy schema and data into it — exposed via both the desktop GUI and CLI.

**Architecture:** A new `PostgresConnectionString.WithDatabase` helper builds per-database connection strings. A new `DestinationDatabaseLifecycle` class opens a maintenance connection (not the target database itself) to enumerate, terminate backends on, drop, and create databases. A new `AllDatabasesMigrationRunner` orchestrates: enumerate origin databases → verify destination maintenance connection → for each selected database, drop+recreate+create-schema+run the *existing* `MigrationRunner` unmodified, scoped to that one database → continue past per-database failures → emit a summary. CLI gets `--all-databases`/`--exclude-database` flags and a typed `OVERWRITE` confirmation. Desktop gets a checkbox, a database checklist, and a typed-confirmation dialog.

**Tech Stack:** C# / .NET 10, Npgsql, WinForms (desktop), xUnit (existing test project), Docker + PowerShell (integration tests).

## Global Constraints

- Targets `net10.0` exclusively (`dotnet new`/build must use `-f net10.0` where relevant).
- No stored credentials; never write raw connection strings to disk or logs — always `RedactedConnectionString`.
- All user-provided identifiers (schema/table/database names) go through `SqlIdentifier.Quote`/`SqlIdentifier.Qualify`, never string-concatenated raw.
- Destructive actions require explicit confirmation: CLI via `--yes` or a typed word at an interactive prompt (`Console.IsInputRedirected` check); desktop via a checkbox/dialog. This feature's confirmation word is `OVERWRITE`, chosen to be distinct from the existing `TRUNCATE`/`DROP` words used by other destructive flags.
- System databases `template0`, `template1`, `postgres` are always excluded from enumeration/selection, non-configurable.
- Schema copy and data copy stay conceptually separate — this feature always runs both (create-schema is mandatory here) but reuses the existing `SchemaCreator`/`MigrationRunner` split rather than merging them.
- Keep `src/PostgresCopy.Desktop` thin — new orchestration logic lives in `src/PostgresCopy`, the desktop only translates UI state and displays results.
- Prefer async APIs; dispose all Npgsql connections/transactions via `await using`.
- Every new destructive primitive follows the existing pattern exactly: a `bool destructiveActionsConfirmed` parameter checked before acting, never inferred from other state.

---

## File Structure

New files:
- `src/PostgresCopy/Database/DestinationDatabaseLifecycle.cs` — maintenance-connection primitive (enumerate, terminate backends, drop, create).
- `src/PostgresCopy/Migration/AllDatabasesMigrationRunner.cs` — orchestrator looping `MigrationRunner` per database.
- `src/PostgresCopy/Migration/AllDatabasesRunResult.cs` — per-database + summary result record.
- `src/PostgresCopy/Cli/DestructiveActionPrompt.cs` — modified, add `ConfirmOverwriteAllDatabases`.
- `src/PostgresCopy/Cli/CliOptionsParser.cs` — modified, add `--all-databases`/`--exclude-database` flags + validation.
- `src/PostgresCopy/Cli/CliOptions.cs` — modified, add `AllDatabases`/`ExcludeDatabases` properties.
- `src/PostgresCopy/Config/PostgresConnectionString.cs` — modified, add `WithDatabase`.
- `src/PostgresCopy/Logging/IMigrationLogger.cs` — modified, add `DatabaseStart`/`DatabaseDone`/`DatabaseFailed`.
- `src/PostgresCopy/Logging/ConsoleMigrationLogger.cs` — modified, implement the 3 new methods.
- `src/PostgresCopy.Desktop/UiMigrationLogger.cs` — modified, implement the 3 new methods.
- `src/PostgresCopy/Program.cs` — modified, wire `--all-databases` path.
- `src/PostgresCopy.Desktop/MainForm.cs` — modified, add checkbox, database checklist, confirmation dialog, wiring.
- `src/PostgresCopy.Desktop/DesktopRunHistoryEntry.cs` — modified, add optional `BatchId` field.
- Tests: `tests/PostgresCopy.Tests/PostgresConnectionStringWithDatabaseTests.cs`, `tests/PostgresCopy.Tests/DestinationDatabaseLifecycleTests.cs`, `tests/PostgresCopy.Tests/AllDatabasesMigrationRunnerTests.cs`, `tests/PostgresCopy.Tests/CliOptionsParserAllDatabasesTests.cs`, `tests/PostgresCopy.Tests/DestructiveActionPromptTests.cs` (new file — no existing tests cover this class; confirm during Task 1 whether one already exists under a different name before creating).
- Integration: `tests/integration/origin-multi.sql`, `tests/integration/docker-compose-multi.yml`, `scripts/integration-test.ps1` (modified, new `-AllDatabases` scenario section).

---

### Task 1: `PostgresConnectionString.WithDatabase`

**Files:**
- Modify: `src/PostgresCopy/Config/PostgresConnectionString.cs`
- Test: `tests/PostgresCopy.Tests/PostgresConnectionStringWithDatabaseTests.cs` (new)

**Interfaces:**
- Consumes: existing private `ParseBuilder(string value, bool allowMissingDatabase)` (already defined in this file at line 103).
- Produces: `public static string WithDatabase(string connectionStringOrUrl, string databaseName)` — later tasks (`DestinationDatabaseLifecycle`, `AllDatabasesMigrationRunner`) call this to build per-database connection strings and the maintenance connection string.

This method takes any origin/destination connection string (URL or Npgsql keyword form) and a target database name, and returns a new Npgsql keyword-form connection string with only the database swapped — everything else (host, port, user, password, sslmode, etc.) preserved.

- [ ] **Step 1: Write the failing test**

```csharp
// File: tests/PostgresCopy.Tests/PostgresConnectionStringWithDatabaseTests.cs

using Npgsql;
using PostgresCopy.Config;
using Xunit;

namespace PostgresCopy.Tests;

public class PostgresConnectionStringWithDatabaseTests
{
    [Fact]
    public void WithDatabase_ReplacesDatabaseName_KeepsHostAndCredentials()
    {
        var result = PostgresConnectionString.WithDatabase(
            "postgres://user:secret@localhost:5432/original",
            "replacement");

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("replacement", builder.Database);
        Assert.Equal("localhost", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("user", builder.Username);
        Assert.Equal("secret", builder.Password);
    }

    [Fact]
    public void WithDatabase_AcceptsNpgsqlKeywordFormat()
    {
        var result = PostgresConnectionString.WithDatabase(
            "Host=localhost;Port=5432;Username=user;Password=secret;Database=original",
            "replacement");

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("replacement", builder.Database);
    }

    [Fact]
    public void WithDatabase_WorksWhenOriginalHasNoDatabase()
    {
        var result = PostgresConnectionString.WithDatabase(
            "postgres://user:secret@localhost:5432",
            "postgres");

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("postgres", builder.Database);
    }

    [Fact]
    public void WithDatabase_ThrowsValidationException_ForEmptyDatabaseName()
    {
        Assert.Throws<ValidationException>(() =>
            PostgresConnectionString.WithDatabase(
                "postgres://user:secret@localhost:5432/original",
                ""));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~PostgresConnectionStringWithDatabaseTests"`
Expected: FAIL (compile error — `WithDatabase` does not exist).

- [ ] **Step 3: Write minimal implementation**

Add to `src/PostgresCopy/Config/PostgresConnectionString.cs`, directly below the existing `Redact` method (after line 101):

```csharp
    public static string WithDatabase(string connectionStringOrUrl, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ValidationException(
                "Database name cannot be empty." + Environment.NewLine +
                "What happened: PostgresCopy tried to build a connection string for a specific database, but no database name was provided." + Environment.NewLine +
                "How to resolve: this is an internal error; please report it with the steps that led here.");
        }

        var builder = ParseBuilder(connectionStringOrUrl, allowMissingDatabase: true);
        builder.Database = databaseName;
        return builder.ConnectionString;
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~PostgresConnectionStringWithDatabaseTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/PostgresCopy/Config/PostgresConnectionString.cs tests/PostgresCopy.Tests/PostgresConnectionStringWithDatabaseTests.cs
git commit -m "feat: add PostgresConnectionString.WithDatabase for per-database connection strings"
```

---

### Task 2: `IMigrationLogger` database-level vocabulary

**Files:**
- Modify: `src/PostgresCopy/Logging/IMigrationLogger.cs`
- Modify: `src/PostgresCopy/Logging/ConsoleMigrationLogger.cs`
- Modify: `src/PostgresCopy.Desktop/UiMigrationLogger.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: three new interface methods — `DatabaseStart(string databaseName)`, `DatabaseDone(string databaseName, TimeSpan elapsed)`, `DatabaseFailed(string databaseName, string message)` — that `AllDatabasesMigrationRunner` (Task 4) calls once per database in the batch.

No existing test file covers `ConsoleMigrationLogger` or `UiMigrationLogger` directly (confirmed: neither appears in the test file listing). This task has no dedicated test — it is pure interface/implementation plumbing exercised indirectly by Task 4's `AllDatabasesMigrationRunnerTests` via a fake logger. Per the "Task Right-Sizing" rule, this is folded into a single task since a reviewer would not meaningfully approve one method without the others (they're added as a set).

- [ ] **Step 1: Add the three methods to the interface**

In `src/PostgresCopy/Logging/IMigrationLogger.cs`, add after the existing `TableFailed` method:

```csharp
    void DatabaseStart(string databaseName);
    void DatabaseDone(string databaseName, TimeSpan elapsed);
    void DatabaseFailed(string databaseName, string message);
```

- [ ] **Step 2: Implement in `ConsoleMigrationLogger`**

In `src/PostgresCopy/Logging/ConsoleMigrationLogger.cs`, add implementations following the existing `TableStart`/`TableDone`/`TableFailed` style in that file (read the file first to match exact color/formatting conventions used there, then add):

```csharp
    public void DatabaseStart(string databaseName)
    {
        Console.WriteLine($"== Database: {databaseName} ==");
    }

    public void DatabaseDone(string databaseName, TimeSpan elapsed)
    {
        Console.WriteLine($"Database {databaseName} done in {elapsed}.");
    }

    public void DatabaseFailed(string databaseName, string message)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Database {databaseName} failed: {message}");
        Console.ForegroundColor = previousColor;
    }
```

- [ ] **Step 3: Implement in `UiMigrationLogger`**

In `src/PostgresCopy.Desktop/UiMigrationLogger.cs`, add following the existing prefix convention (`"== "` for step-like messages, `"ERROR "` for failures):

```csharp
    public void DatabaseStart(string databaseName) => write($"== Database: {databaseName} ==");

    public void DatabaseDone(string databaseName, TimeSpan elapsed) =>
        write($"Database {databaseName} done in {elapsed}.");

    public void DatabaseFailed(string databaseName, string message) =>
        write($"ERROR Database {databaseName} failed: {message}");
```

- [ ] **Step 4: Build to verify no other `IMigrationLogger` implementers are broken**

Run: `dotnet build PostgresCopy.sln`
Expected: Build succeeds. If any other class implements `IMigrationLogger` (e.g. a test fake), the build error will name it — add the same three methods there too before proceeding.

- [ ] **Step 5: Run full unit suite to confirm nothing regressed**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build`
Expected: All existing tests still PASS.

- [ ] **Step 6: Commit**

```bash
git add src/PostgresCopy/Logging/IMigrationLogger.cs src/PostgresCopy/Logging/ConsoleMigrationLogger.cs src/PostgresCopy.Desktop/UiMigrationLogger.cs
git commit -m "feat: add database-level logging vocabulary to IMigrationLogger"
```

---

### Task 3: `DestinationDatabaseLifecycle`

**Files:**
- Create: `src/PostgresCopy/Database/DestinationDatabaseLifecycle.cs`
- Test: `tests/PostgresCopy.Tests/DestinationDatabaseLifecycleTests.cs` (new)

**Interfaces:**
- Consumes: `PostgresConnectionString.WithDatabase` (Task 1), `SqlIdentifier.Quote` (existing, in `src/PostgresCopy/Database/` or `Migration/` — confirm exact namespace via `Grep` before writing `using`; it's referenced unqualified as `SqlIdentifier.Quote`/`SqlIdentifier.Qualify` elsewhere in this codebase).
- Produces (used by Task 4 `AllDatabasesMigrationRunner`):
  - `public const string DefaultMaintenanceDatabase = "postgres";`
  - `public static readonly IReadOnlyList<string> ExcludedDatabaseNames = ["template0", "template1", "postgres"];`
  - `public static async Task<IReadOnlyList<string>> ListDatabasesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)` — returns non-system database names.
  - `public static async Task<(bool Reachable, string? FailureReason)> TryOpenMaintenanceConnectionAsync(string connectionString, CancellationToken cancellationToken)` — attempts `postgres` then `template1` against the same server/credentials; does not return the open connection (caller re-opens on success, since this is a probe used before the destructive path starts) — returns whether *some* maintenance database was reachable and, if not, why.
  - `public static async Task<int> TerminateOtherBackendsAsync(NpgsqlConnection maintenanceConnection, string targetDatabaseName, CancellationToken cancellationToken)` — returns count of backends terminated.
  - `public static async Task DropDatabaseAsync(NpgsqlConnection maintenanceConnection, string targetDatabaseName, CancellationToken cancellationToken)`.
  - `public static async Task CreateDatabaseAsync(NpgsqlConnection maintenanceConnection, string targetDatabaseName, CancellationToken cancellationToken)`.

Note on `TryOpenMaintenanceConnectionAsync`'s shape: it returns a tuple rather than the open connection itself, because the *caller* (`AllDatabasesMigrationRunner`) needs to open its own long-lived maintenance connection for the actual drop/create work — this method is purely a preflight probe so the failure message can be surfaced *before* any destructive action, per the design's requirement that maintenance-DB reachability is never assumed.

- [ ] **Step 1: Write the failing tests for the pure/testable parts**

`TryOpenMaintenanceConnectionAsync`, `TerminateOtherBackendsAsync`, `DropDatabaseAsync`, and `CreateDatabaseAsync` all require a live PostgreSQL connection and are exercised by the integration test (Task 8), not unit tests. The unit-testable surface is `ExcludedDatabaseNames` and the SQL-building/filtering logic inside `ListDatabasesAsync`. Since `ListDatabasesAsync` takes a live `NpgsqlConnection`, extract the filtering as a separate pure function so it's unit-testable without a database:

```csharp
// File: tests/PostgresCopy.Tests/DestinationDatabaseLifecycleTests.cs

using PostgresCopy.Database;
using Xunit;

namespace PostgresCopy.Tests;

public class DestinationDatabaseLifecycleTests
{
    [Fact]
    public void ExcludedDatabaseNames_ContainsSystemDatabasesOnly()
    {
        Assert.Equal(
            new[] { "template0", "template1", "postgres" },
            DestinationDatabaseLifecycle.ExcludedDatabaseNames);
    }

    [Theory]
    [InlineData("template0", true)]
    [InlineData("template1", true)]
    [InlineData("postgres", true)]
    [InlineData("app_db", false)]
    [InlineData("Template0", false)] // case-sensitive: PostgreSQL database names are case-sensitive by default
    public void IsExcludedSystemDatabase_MatchesOnlyExactSystemNames(string name, bool expectedExcluded)
    {
        Assert.Equal(expectedExcluded, DestinationDatabaseLifecycle.IsExcludedSystemDatabase(name));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~DestinationDatabaseLifecycleTests"`
Expected: FAIL (compile error — class does not exist).

- [ ] **Step 3: Write the implementation**

```csharp
// File: src/PostgresCopy/Database/DestinationDatabaseLifecycle.cs

using Npgsql;

namespace PostgresCopy.Database;

public static class DestinationDatabaseLifecycle
{
    public const string DefaultMaintenanceDatabase = "postgres";
    private const string FallbackMaintenanceDatabase = "template1";

    public static readonly IReadOnlyList<string> ExcludedDatabaseNames =
        ["template0", "template1", "postgres"];

    public static bool IsExcludedSystemDatabase(string databaseName) =>
        ExcludedDatabaseNames.Contains(databaseName, StringComparer.Ordinal);

    public const string ListDatabasesSql = """
        select datname
        from pg_database
        where datallowconn
          and not datistemplate
        order by datname;
        """;

    public static async Task<IReadOnlyList<string>> ListDatabasesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var databases = new List<string>();

        await using var command = new NpgsqlCommand(ListDatabasesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            if (!IsExcludedSystemDatabase(name))
            {
                databases.Add(name);
            }
        }

        return databases;
    }

    public static async Task<(bool Reachable, string? FailureReason)> TryOpenMaintenanceConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in new[] { DefaultMaintenanceDatabase, FallbackMaintenanceDatabase })
        {
            var candidateConnectionString = Config.PostgresConnectionString.WithDatabase(connectionString, candidate);

            try
            {
                await using var connection = new NpgsqlConnection(candidateConnectionString);
                await connection.OpenAsync(cancellationToken);
                return (true, null);
            }
            catch (Exception)
            {
                // Try the next candidate maintenance database.
            }
        }

        return (false,
            $"Could not open a maintenance connection to either \"{DefaultMaintenanceDatabase}\" or \"{FallbackMaintenanceDatabase}\" on the destination server. " +
            "Copy all databases requires a reachable maintenance database to create and drop destination databases. " +
            "Check that the destination user has CONNECT privilege on one of these databases.");
    }

    public static async Task<int> TerminateOtherBackendsAsync(
        NpgsqlConnection maintenanceConnection,
        string targetDatabaseName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select pg_terminate_backend(pid)
            from pg_stat_activity
            where datname = @databaseName
              and pid <> pg_backend_pid();
            """;

        await using var command = new NpgsqlCommand(sql, maintenanceConnection);
        command.Parameters.AddWithValue("databaseName", targetDatabaseName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var terminatedCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.GetBoolean(0))
            {
                terminatedCount++;
            }
        }

        return terminatedCount;
    }

    public static async Task DropDatabaseAsync(
        NpgsqlConnection maintenanceConnection,
        string targetDatabaseName,
        CancellationToken cancellationToken)
    {
        var sql = $"drop database if exists {SqlIdentifier.Quote(targetDatabaseName)};";
        await using var command = new NpgsqlCommand(sql, maintenanceConnection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task CreateDatabaseAsync(
        NpgsqlConnection maintenanceConnection,
        string targetDatabaseName,
        CancellationToken cancellationToken)
    {
        var sql = $"create database {SqlIdentifier.Quote(targetDatabaseName)};";
        await using var command = new NpgsqlCommand(sql, maintenanceConnection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
```

Before finalizing this step, run `Grep` for `namespace` at the top of `src/PostgresCopy/Database/SqlIdentifier.cs` (or wherever it lives) to confirm whether an explicit `using` is needed, since other files in `Database/` appear to reference it unqualified — match whatever the sibling files in this directory already do.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~DestinationDatabaseLifecycleTests"`
Expected: PASS (6 tests: 1 + 5 theory cases).

- [ ] **Step 5: Commit**

```bash
git add src/PostgresCopy/Database/DestinationDatabaseLifecycle.cs tests/PostgresCopy.Tests/DestinationDatabaseLifecycleTests.cs
git commit -m "feat: add DestinationDatabaseLifecycle for enumerate/drop/create/terminate operations"
```

---

### Task 4: `AllDatabasesMigrationRunner` orchestrator

**Files:**
- Create: `src/PostgresCopy/Migration/AllDatabasesRunResult.cs`
- Create: `src/PostgresCopy/Migration/AllDatabasesMigrationRunner.cs`
- Test: `tests/PostgresCopy.Tests/AllDatabasesMigrationRunnerTests.cs` (new)

**Interfaces:**
- Consumes: `MigrationRunner` (existing, unmodified — constructed as `new MigrationRunner(logger)`, called as `RunAsync(MigrationSettings, bool destructiveActionsConfirmed, CancellationToken)` → `Task<MigrationRunResult>`), `DestinationDatabaseLifecycle.*` (Task 3), `PostgresConnectionString.WithDatabase` (Task 1), `IMigrationLogger.DatabaseStart/DatabaseDone/DatabaseFailed` (Task 2), `MigrationSettings` (existing record, all 14 positional fields as reported), `MigrationSettingsValidator.Validate(CliOptions)` is **not** reused directly here since this runner builds `MigrationSettings` per-database itself rather than going through `CliOptions` — construct `MigrationSettings` directly.
- Produces (used by Task 5 CLI wiring and Task 9 desktop wiring):
  - `public sealed record AllDatabasesRunResult(int TotalDatabases, int Succeeded, int Failed, IReadOnlyList<PerDatabaseResult> Results);`
  - `public sealed record PerDatabaseResult(string DatabaseName, bool Succeeded, MigrationRunResult? Result, string? FailureMessage, TimeSpan Elapsed);`
  - `public sealed class AllDatabasesMigrationRunner(IMigrationLogger logger)` with `public async Task<AllDatabasesRunResult> RunAsync(AllDatabasesMigrationSettings settings, bool destructiveActionsConfirmed, CancellationToken cancellationToken)`.
  - `public sealed record AllDatabasesMigrationSettings(string OriginConnectionString, string DestinationConnectionString, IReadOnlyList<string> ExcludeDatabases, bool DryRun, bool Verify, bool Yes, int BatchSize, bool Verbose);` — deliberately a separate, smaller settings record from `MigrationSettings` since this mode has no `Schema`/`TableFilter`/`TruncateDestination`/`CreateSchema`/`SchemaOnly`/`DataOnly`/`DropSchema` (all superseded by this mode's always-on behavior, per the design's locked-in decisions).

Note: this task's result type introduces `PerDatabaseResult` which Task 9 (desktop) uses to write one `DesktopRunHistoryEntry` per database — keep the property names exactly as declared above since Task 9 references them by name.

- [ ] **Step 1: Write the failing test using an in-memory fake, not a live database**

Because `AllDatabasesMigrationRunner` needs to call the real `MigrationRunner` (which opens real Npgsql connections), full behavior is only testable via the integration test (Task 8). The unit test here covers the parts that don't require a live database: exclusion-list filtering interacting with `--exclude-database`, and the "continue past failure" aggregation logic. Extract that aggregation into a small pure static method so it's testable in isolation:

```csharp
// File: tests/PostgresCopy.Tests/AllDatabasesMigrationRunnerTests.cs

using PostgresCopy.Migration;
using Xunit;

namespace PostgresCopy.Tests;

public class AllDatabasesMigrationRunnerTests
{
    [Fact]
    public void FilterSelectedDatabases_ExcludesRequestedNames()
    {
        var result = AllDatabasesMigrationRunner.FilterSelectedDatabases(
            allDatabases: ["app_db", "reporting_db", "staging_db"],
            excludeDatabases: ["staging_db"]);

        Assert.Equal(["app_db", "reporting_db"], result);
    }

    [Fact]
    public void FilterSelectedDatabases_ExcludeIsCaseSensitive()
    {
        var result = AllDatabasesMigrationRunner.FilterSelectedDatabases(
            allDatabases: ["App_Db"],
            excludeDatabases: ["app_db"]);

        Assert.Equal(["App_Db"], result);
    }

    [Fact]
    public void BuildSummary_CountsSucceededAndFailed_AndPreservesOrder()
    {
        var results = new List<PerDatabaseResult>
        {
            new("db1", true, new MigrationRunResult(false, 2, 100), null, TimeSpan.FromSeconds(1)),
            new("db2", false, null, "connection refused", TimeSpan.FromSeconds(2)),
            new("db3", true, new MigrationRunResult(false, 1, 5), null, TimeSpan.FromSeconds(1)),
        };

        var summary = AllDatabasesMigrationRunner.BuildSummary(results);

        Assert.Equal(3, summary.TotalDatabases);
        Assert.Equal(2, summary.Succeeded);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(["db1", "db2", "db3"], summary.Results.Select(r => r.DatabaseName));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~AllDatabasesMigrationRunnerTests"`
Expected: FAIL (compile error — types do not exist).

- [ ] **Step 3: Write `AllDatabasesRunResult.cs`**

```csharp
// File: src/PostgresCopy/Migration/AllDatabasesRunResult.cs

namespace PostgresCopy.Migration;

public sealed record PerDatabaseResult(
    string DatabaseName,
    bool Succeeded,
    MigrationRunResult? Result,
    string? FailureMessage,
    TimeSpan Elapsed);

public sealed record AllDatabasesRunResult(
    int TotalDatabases,
    int Succeeded,
    int Failed,
    IReadOnlyList<PerDatabaseResult> Results);

public sealed record AllDatabasesMigrationSettings(
    string OriginConnectionString,
    string DestinationConnectionString,
    IReadOnlyList<string> ExcludeDatabases,
    bool DryRun,
    bool Verify,
    bool Yes,
    int BatchSize,
    bool Verbose);
```

- [ ] **Step 4: Write `AllDatabasesMigrationRunner.cs`**

```csharp
// File: src/PostgresCopy/Migration/AllDatabasesMigrationRunner.cs

using Npgsql;
using PostgresCopy.Config;
using PostgresCopy.Database;
using PostgresCopy.Logging;

namespace PostgresCopy.Migration;

public sealed class AllDatabasesMigrationRunner(IMigrationLogger logger)
{
    public static IReadOnlyList<string> FilterSelectedDatabases(
        IReadOnlyList<string> allDatabases,
        IReadOnlyList<string> excludeDatabases)
    {
        var excludeSet = new HashSet<string>(excludeDatabases, StringComparer.Ordinal);
        return allDatabases.Where(name => !excludeSet.Contains(name)).ToList();
    }

    public static AllDatabasesRunResult BuildSummary(IReadOnlyList<PerDatabaseResult> results)
    {
        return new AllDatabasesRunResult(
            results.Count,
            results.Count(r => r.Succeeded),
            results.Count(r => !r.Succeeded),
            results);
    }

    public async Task<AllDatabasesRunResult> RunAsync(
        AllDatabasesMigrationSettings settings,
        bool destructiveActionsConfirmed,
        CancellationToken cancellationToken)
    {
        if (!settings.DryRun && !destructiveActionsConfirmed)
        {
            throw new ValidationException("Copy all databases was not confirmed. Migration cancelled.");
        }

        logger.Step("Enumerating origin databases");
        var allDatabases = await ListOriginDatabasesAsync(settings.OriginConnectionString, cancellationToken);
        var selected = FilterSelectedDatabases(allDatabases, settings.ExcludeDatabases);
        logger.Info($"Found {allDatabases.Count} database(s) on origin, {selected.Count} selected after exclusions.");

        if (!settings.DryRun)
        {
            logger.Step("Checking destination maintenance connection");
            var (reachable, failureReason) = await DestinationDatabaseLifecycle.TryOpenMaintenanceConnectionAsync(
                settings.DestinationConnectionString, cancellationToken);
            if (!reachable)
            {
                throw new ValidationException(failureReason ?? "Destination maintenance connection is not reachable.");
            }
            logger.Success("Destination maintenance connection reachable.");
        }

        var results = new List<PerDatabaseResult>();
        foreach (var databaseName in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = DateTimeOffset.UtcNow;
            logger.DatabaseStart(databaseName);

            try
            {
                var result = await RunSingleDatabaseAsync(settings, databaseName, cancellationToken);
                var elapsed = DateTimeOffset.UtcNow - startedAt;
                logger.DatabaseDone(databaseName, elapsed);
                results.Add(new PerDatabaseResult(databaseName, true, result, null, elapsed));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var elapsed = DateTimeOffset.UtcNow - startedAt;
                logger.DatabaseFailed(databaseName, ex.Message);
                results.Add(new PerDatabaseResult(databaseName, false, null, ex.Message, elapsed));
            }
        }

        return BuildSummary(results);
    }

    private async Task<MigrationRunResult> RunSingleDatabaseAsync(
        AllDatabasesMigrationSettings settings,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var originConnectionString = PostgresConnectionString.WithDatabase(settings.OriginConnectionString, databaseName);
        var destConnectionString = PostgresConnectionString.WithDatabase(settings.DestinationConnectionString, databaseName);

        if (!settings.DryRun)
        {
            await using var maintenanceConnection = await OpenMaintenanceConnectionAsync(
                settings.DestinationConnectionString, cancellationToken);

            var terminatedCount = await DestinationDatabaseLifecycle.TerminateOtherBackendsAsync(
                maintenanceConnection, databaseName, cancellationToken);
            if (terminatedCount > 0)
            {
                logger.Info($"Terminated {terminatedCount} other connection(s) to \"{databaseName}\" on destination.");
            }

            await DestinationDatabaseLifecycle.DropDatabaseAsync(maintenanceConnection, databaseName, cancellationToken);
            await DestinationDatabaseLifecycle.CreateDatabaseAsync(maintenanceConnection, databaseName, cancellationToken);

            var schemaError = await SchemaCreator.CreateAsync(
                originConnectionString, destConnectionString, "public", cancellationToken);
            if (schemaError is not null)
            {
                throw new ValidationException($"Schema creation failed for \"{databaseName}\": {schemaError}");
            }
        }

        var origin = PostgresConnectionString.Parse(originConnectionString);
        var destination = PostgresConnectionString.Parse(destConnectionString);

        var perDatabaseSettings = new MigrationSettings(
            origin,
            destination,
            "public",
            [],
            settings.DryRun,
            false,
            settings.Verify,
            settings.Verbose,
            settings.Yes,
            settings.BatchSize,
            false,
            false,
            false,
            false);

        return await new MigrationRunner(logger).RunAsync(perDatabaseSettings, destructiveActionsConfirmed: true, cancellationToken);
    }

    private static async Task<NpgsqlConnection> OpenMaintenanceConnectionAsync(
        string destinationConnectionString,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in new[] { DestinationDatabaseLifecycle.DefaultMaintenanceDatabase, "template1" })
        {
            var candidateConnectionString = PostgresConnectionString.WithDatabase(destinationConnectionString, candidate);
            try
            {
                var connection = new NpgsqlConnection(candidateConnectionString);
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (Exception)
            {
                // Try the next candidate.
            }
        }

        throw new ValidationException(
            "Could not open a maintenance connection to the destination server to drop/create databases.");
    }

    private static async Task<IReadOnlyList<string>> ListOriginDatabasesAsync(
        string originConnectionString,
        CancellationToken cancellationToken)
    {
        var maintenanceConnectionString = PostgresConnectionString.WithDatabase(
            originConnectionString, DestinationDatabaseLifecycle.DefaultMaintenanceDatabase);

        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(cancellationToken);
        return await DestinationDatabaseLifecycle.ListDatabasesAsync(connection, cancellationToken);
    }
}
```

Note: `perDatabaseSettings` passes `destructiveActionsConfirmed: true` to the inner `MigrationRunner.RunAsync` call unconditionally — this is intentional and safe: `TruncateDestination`/`DropSchema`/`CreateSchema` are all hardcoded `false` on this settings object (the database was already dropped and recreated by the lines above, and schema was already created by the explicit `SchemaCreator.CreateAsync` call above), so `MigrationRunner`'s own confirmation gates for those flags never trigger — the actual destructive gate for this whole feature is the outer `destructiveActionsConfirmed` parameter checked once at the top of `RunAsync`.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~AllDatabasesMigrationRunnerTests"`
Expected: PASS (3 tests).

- [ ] **Step 6: Run full unit suite**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build`
Expected: All PASS.

- [ ] **Step 7: Commit**

```bash
git add src/PostgresCopy/Migration/AllDatabasesRunResult.cs src/PostgresCopy/Migration/AllDatabasesMigrationRunner.cs tests/PostgresCopy.Tests/AllDatabasesMigrationRunnerTests.cs
git commit -m "feat: add AllDatabasesMigrationRunner orchestrator for whole-server copy"
```

---

### Task 5: `DestructiveActionPrompt.ConfirmOverwriteAllDatabases`

**Files:**
- Modify: `src/PostgresCopy/Cli/DestructiveActionPrompt.cs`
- Test: `tests/PostgresCopy.Tests/DestructiveActionPromptTests.cs` (new — confirm no existing file covers this class first via `Glob "tests/PostgresCopy.Tests/*Destructive*"`)

**Interfaces:**
- Consumes: nothing new (same shape as existing `ConfirmTruncateDestination`/`ConfirmDropSchema` in this file).
- Produces: `public static bool ConfirmOverwriteAllDatabases(IReadOnlyList<string> databaseNames, bool yes)` — used by Task 6 (`Program.cs` wiring).

- [ ] **Step 1: Write the failing test**

```csharp
// File: tests/PostgresCopy.Tests/DestructiveActionPromptTests.cs

using PostgresCopy.Cli;
using Xunit;

namespace PostgresCopy.Tests;

public class DestructiveActionPromptTests
{
    [Fact]
    public void ConfirmOverwriteAllDatabases_ReturnsTrue_WhenYesFlagSet()
    {
        var result = DestructiveActionPrompt.ConfirmOverwriteAllDatabases(["app_db", "reporting_db"], yes: true);
        Assert.True(result);
    }
}
```

(Only the `yes: true` path is unit-testable without mocking `Console` input/redirection — the interactive-prompt path mirrors `ConfirmTruncateDestination`/`ConfirmDropSchema`, neither of which has deeper test coverage either, per the existing test file listing.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~DestructiveActionPromptTests"`
Expected: FAIL (compile error — method does not exist).

- [ ] **Step 3: Write the implementation**

Add to `src/PostgresCopy/Cli/DestructiveActionPrompt.cs`, following the exact shape of `ConfirmDropSchema` in that file:

```csharp
    public static bool ConfirmOverwriteAllDatabases(IReadOnlyList<string> databaseNames, bool yes)
    {
        if (yes)
        {
            return true;
        }

        if (Console.IsInputRedirected)
        {
            return false;
        }

        Console.WriteLine("This will DROP and recreate the following destination databases:");
        foreach (var name in databaseNames)
        {
            Console.WriteLine($"  {name}");
        }
        Console.WriteLine("Any other active connections to these databases will be forcibly terminated.");
        Console.WriteLine("All tables, indexes, sequences, functions, views, triggers, and data in each database will be permanently deleted before being recreated from origin.");
        Console.Write("Type OVERWRITE to continue: ");
        var response = Console.ReadLine();

        return string.Equals(response, "OVERWRITE", StringComparison.Ordinal);
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~DestructiveActionPromptTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/PostgresCopy/Cli/DestructiveActionPrompt.cs tests/PostgresCopy.Tests/DestructiveActionPromptTests.cs
git commit -m "feat: add ConfirmOverwriteAllDatabases destructive confirmation prompt"
```

---

### Task 6: CLI flags — `--all-databases` / `--exclude-database`

**Files:**
- Modify: `src/PostgresCopy/Cli/CliOptions.cs`
- Modify: `src/PostgresCopy/Cli/CliOptionsParser.cs`
- Modify: `src/PostgresCopy/Program.cs`
- Test: `tests/PostgresCopy.Tests/CliOptionsParserAllDatabasesTests.cs` (new)

**Interfaces:**
- Consumes: `AllDatabasesMigrationRunner`/`AllDatabasesMigrationSettings` (Task 4), `DestructiveActionPrompt.ConfirmOverwriteAllDatabases` (Task 5).
- Produces: `CliOptions.AllDatabases` (bool), `CliOptions.ExcludeDatabases` (`IReadOnlyList<string>`) — consumed only by `Program.cs` in this task, no later task depends on these property names besides `Program.cs` itself.

- [ ] **Step 1: Write the failing parser tests**

```csharp
// File: tests/PostgresCopy.Tests/CliOptionsParserAllDatabasesTests.cs

using PostgresCopy.Cli;
using Xunit;

namespace PostgresCopy.Tests;

public class CliOptionsParserAllDatabasesTests
{
    [Fact]
    public void Parse_AllDatabasesFlag_SetsOption()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--yes",
        ]);

        Assert.True(result.Success);
        Assert.True(result.Options!.AllDatabases);
    }

    [Fact]
    public void Parse_ExcludeDatabaseRepeatable_CollectsAllValues()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--exclude-database", "staging_db",
            "--exclude-database", "scratch_db",
            "--yes",
        ]);

        Assert.True(result.Success);
        Assert.Equal(["staging_db", "scratch_db"], result.Options!.ExcludeDatabases);
    }

    [Fact]
    public void Parse_AllDatabasesWithSchema_Fails()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--schema", "custom",
            "--yes",
        ]);

        Assert.False(result.Success);
        Assert.Contains("--all-databases", result.ErrorMessage);
    }

    [Fact]
    public void Parse_AllDatabasesWithTables_Fails()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--tables", "accounts",
            "--yes",
        ]);

        Assert.False(result.Success);
    }

    [Fact]
    public void Parse_AllDatabasesWithSchemaOnly_Fails()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--schema-only",
            "--yes",
        ]);

        Assert.False(result.Success);
    }

    [Fact]
    public void Parse_AllDatabasesWithDataOnly_Fails()
    {
        var result = CliOptionsParser.Parse([
            "--origin", "postgres://user:pass@localhost:5432/db",
            "--destination", "postgres://user:pass@localhost:5433/db",
            "--all-databases",
            "--data-only",
            "--yes",
        ]);

        Assert.False(result.Success);
    }
}
```

**IMPORTANT — read before writing Step 3:** `CliOptions` is currently constructed positionally (`new CliOptions(origin, destination, schema, tables, dryRun, truncateDestination, verify, verbose, yes, batchSize, createSchema || schemaOnly, schemaOnly, dataOnly, dropSchema)` per the parser research). Adding two new trailing parameters (`AllDatabases`, `ExcludeDatabases`) to a positional record with 14 existing fields means every existing positional-construction call site must be updated. Before writing code, run:

`Grep -n "new CliOptions(" src/PostgresCopy tests/PostgresCopy.Tests` (via the Grep tool)

to find every call site, and update each one to pass the two new arguments (default `false` and `[]` respectively) so nothing else breaks.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~CliOptionsParserAllDatabasesTests"`
Expected: FAIL (compile error — `AllDatabases`/`ExcludeDatabases` do not exist on `CliOptions`).

- [ ] **Step 3: Add properties to `CliOptions`**

Modify `src/PostgresCopy/Cli/CliOptions.cs` — add two new trailing positional parameters:

```csharp
public sealed record CliOptions(
    string Origin,
    string Destination,
    string Schema,
    IReadOnlyList<string> Tables,
    bool DryRun,
    bool TruncateDestination,
    bool Verify,
    bool Verbose,
    bool Yes,
    int BatchSize,
    bool CreateSchema,
    bool SchemaOnly,
    bool DataOnly,
    bool DropSchema = false,
    bool AllDatabases = false,
    IReadOnlyList<string>? ExcludeDatabasesRaw = null)
{
    public IReadOnlyList<string> ExcludeDatabases => ExcludeDatabasesRaw ?? [];
}
```

Using default values (`= false`, `= null`) keeps every existing positional call site (found via the `Grep` in Step 1's note) compiling unchanged, since C# allows omitting trailing parameters that have defaults — confirm this by running a full build after this step before touching the parser.

- [ ] **Step 4: Run a build to confirm existing call sites still compile**

Run: `dotnet build PostgresCopy.sln`
Expected: Build succeeds with no changes needed at existing `new CliOptions(...)` call sites, since the two new parameters have defaults.

- [ ] **Step 5: Add flag parsing and validation to `CliOptionsParser`**

In `src/PostgresCopy/Cli/CliOptionsParser.cs`, add parsing for `--all-databases` (bool flag, same pattern as `--dry-run`) and `--exclude-database <value>` (repeatable, same pattern as `--table <value>`). Add these validation checks in the same block as the existing mutual-exclusion checks (`schemaOnly && dataOnly`, etc.):

```csharp
if (allDatabases && !string.Equals(schema, "public", StringComparison.Ordinal))
{
    return CliParseResult.Failed("--all-databases cannot be combined with --schema because it copies every schema in every database.");
}

if (allDatabases && tables.Count > 0)
{
    return CliParseResult.Failed("--all-databases cannot be combined with --table/--tables because it copies every table in every database.");
}

if (allDatabases && schemaOnly)
{
    return CliParseResult.Failed("--all-databases cannot be combined with --schema-only.");
}

if (allDatabases && dataOnly)
{
    return CliParseResult.Failed("--all-databases cannot be combined with --data-only.");
}

if (allDatabases && createSchema)
{
    return CliParseResult.Failed("--all-databases cannot be combined with --create-schema because schema creation always runs in this mode.");
}

if (allDatabases && dropSchema)
{
    return CliParseResult.Failed("--all-databases cannot be combined with --drop-schema because whole databases are dropped, not schemas.");
}

if (allDatabases && truncateDestination)
{
    return CliParseResult.Failed("--all-databases cannot be combined with --truncate-destination because destination databases are dropped and recreated, not truncated.");
}
```

Note on the `--schema` check: since `Schema` defaults to `"public"` when not explicitly passed, this check as written would false-positive if a user runs `--all-databases` without ever touching `--schema` (it's already `"public"`) — that's fine, since `"public"` is the default and the check only rejects when the value differs from default, meaning the user explicitly typed a different `--schema`. Confirm this reads correctly by re-reading the existing default-value logic in the parser before finalizing.

Update the final `CliParseResult.Parsed(new CliOptions(...))` call to pass `allDatabases` and `excludeDatabases` as the two new trailing arguments.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --filter "FullyQualifiedName~CliOptionsParserAllDatabasesTests"`
Expected: PASS (6 tests).

- [ ] **Step 7: Wire `Program.cs`**

Read `src/PostgresCopy/Program.cs` in full first to see the exact existing flow (the earlier research showed the tail end: `truncateOk`/`dropOk`/`destructiveActionsConfirmed`/`new MigrationRunner(console).RunAsync(...)`). Add a branch before the existing `MigrationSettingsValidator.Validate`/`MigrationRunner` call:

```csharp
if (options.AllDatabases)
{
    var origin = PostgresConnectionString.Parse(options.Origin);
    var destination = PostgresConnectionString.Parse(options.Destination);

    if (origin.ComparisonKey.Equals(destination.ComparisonKey, StringComparison.Ordinal))
    {
        throw new ValidationException("Origin and destination point to the same database. Refusing to continue.");
    }

    var allDatabasesSettings = new AllDatabasesMigrationSettings(
        origin.ConnectionString,
        destination.ConnectionString,
        options.ExcludeDatabases,
        options.DryRun,
        options.Verify,
        options.Yes,
        options.BatchSize,
        options.Verbose);

    var runner = new AllDatabasesMigrationRunner(console);

    bool confirmed;
    if (options.DryRun)
    {
        confirmed = true;
    }
    else
    {
        var maintenanceConnectionString = PostgresConnectionString.WithDatabase(origin.ConnectionString, "postgres");
        await using var probeConnection = new NpgsqlConnection(maintenanceConnectionString);
        await probeConnection.OpenAsync(cancellation.Token);
        var allDatabaseNames = await DestinationDatabaseLifecycle.ListDatabasesAsync(probeConnection, cancellation.Token);
        var selectedNames = AllDatabasesMigrationRunner.FilterSelectedDatabases(allDatabaseNames, options.ExcludeDatabases);
        confirmed = DestructiveActionPrompt.ConfirmOverwriteAllDatabases(selectedNames, options.Yes);
    }

    var summary = await runner.RunAsync(allDatabasesSettings, confirmed, cancellation.Token);
    console.Info($"Completed {summary.TotalDatabases} database(s): {summary.Succeeded} succeeded, {summary.Failed} failed.");
    return summary.Failed == 0 ? 0 : 1;
}
```

Place this branch after CLI options are parsed but before the existing single-database `MigrationSettingsValidator.Validate`/`MigrationRunner` path, returning early so the existing path is untouched when `--all-databases` is not passed. Match the exact existing variable names for `console`/`cancellation` by reading the surrounding code first — do not guess these names.

- [ ] **Step 8: Manual CLI smoke check**

Run: `dotnet run --project src\PostgresCopy -- --help`
Expected: help text still prints without error (confirms no exception in flag registration). Then run the existing integration smoke check to confirm the non-`--all-databases` path is unaffected:

Run: `dotnet run --project src\PostgresCopy -- --origin "postgres://user:secret@localhost:5432/app" --destination "postgres://user:secret@localhost:5432/app" --dry-run`
Expected: fails fast with the existing "same database" error (unchanged behavior).

- [ ] **Step 9: Run full unit suite**

Run: `dotnet test tests\PostgresCopy.Tests\PostgresCopy.Tests.csproj --no-build`
Expected: All PASS.

- [ ] **Step 10: Commit**

```bash
git add src/PostgresCopy/Cli/CliOptions.cs src/PostgresCopy/Cli/CliOptionsParser.cs src/PostgresCopy/Program.cs tests/PostgresCopy.Tests/CliOptionsParserAllDatabasesTests.cs
git commit -m "feat: add --all-databases and --exclude-database CLI flags"
```

---

### Task 7: `README.md` CLI flag table update

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: nothing.
- Produces: nothing consumed by later tasks — pure documentation.

- [ ] **Step 1: Find the existing CLI flag table**

Run: `Grep -n "truncate-destination" README.md` (via the Grep tool) to locate the flag table's format and location.

- [ ] **Step 2: Add rows for the two new flags**

Add `--all-databases` and `--exclude-database <name>` to the table, following the exact column format of the surrounding rows (flag, description, default). Also add a short paragraph (2-3 sentences, matching the style of nearby sections like the existing `--drop-schema` writeup) explaining: it enumerates every non-system database on the origin server, drops and recreates the same-named destination database for each, always creates schema, ignores `--schema`/`--tables`, and requires typing `OVERWRITE` at the confirmation prompt (or `--yes` non-interactively).

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: document --all-databases and --exclude-database CLI flags"
```

---

### Task 8: Integration test — multi-database seed and script scenario

**Files:**
- Create: `tests/integration/docker-compose-multi.yml`
- Create: `tests/integration/origin-multi-init.sql`
- Modify: `scripts/integration-test.ps1`

**Interfaces:**
- Consumes: the shipped CLI's `--all-databases`/`--exclude-database`/`--yes` flags (Task 6) end-to-end against real Docker containers.
- Produces: nothing consumed by later tasks — this is the end-to-end verification for the whole feature.

This is the only task that exercises `DestinationDatabaseLifecycle`'s live-database methods (`TryOpenMaintenanceConnectionAsync`, `TerminateOtherBackendsAsync`, `DropDatabaseAsync`, `CreateDatabaseAsync`) and the full `AllDatabasesMigrationRunner.RunAsync` path, none of which are reachable from the unit test project without a real PostgreSQL server.

- [ ] **Step 1: Read the existing compose/seed files in full**

Read `tests/integration/docker-compose.yml` and `tests/integration/origin.sql` in full (already summarized in the design research, but re-read to copy exact syntax/indentation conventions before writing new files).

- [ ] **Step 2: Write `tests/integration/origin-multi-init.sql`**

A `docker-entrypoint-initdb.d`-compatible script that creates two extra databases beyond the default one, each with a small distinct table so row counts can be told apart:

```sql
-- File: tests/integration/origin-multi-init.sql

CREATE DATABASE app_one;
CREATE DATABASE app_two;

\connect app_one
CREATE TABLE public.widgets (id serial PRIMARY KEY, name text NOT NULL);
INSERT INTO public.widgets (name) VALUES ('alpha'), ('beta'), ('gamma');

\connect app_two
CREATE TABLE public.gadgets (id serial PRIMARY KEY, label text NOT NULL);
INSERT INTO public.gadgets (label) VALUES ('one'), ('two');
```

- [ ] **Step 3: Write `tests/integration/docker-compose-multi.yml`**

Follow the exact structure of `docker-compose.yml`, but origin mounts both `origin.sql` (for the default `pgcopy` database, kept for parity) and `origin-multi-init.sql`; destination starts with only the default `postgres` superuser database (no pre-seeded `app_one`/`app_two` — they must not exist yet, so the test proves `--all-databases` creates them from scratch):

```yaml
services:
  origin-multi:
    image: postgres:17-alpine
    container_name: pgcopy-origin-multi
    environment:
      POSTGRES_PASSWORD: test
      POSTGRES_DB: pgcopy
    ports:
      - "55442:5432"
    volumes:
      - ./origin.sql:/docker-entrypoint-initdb.d/01-origin.sql
      - ./origin-multi-init.sql:/docker-entrypoint-initdb.d/02-origin-multi.sql

  destination-multi:
    image: postgres:17-alpine
    container_name: pgcopy-destination-multi
    environment:
      POSTGRES_PASSWORD: test
      POSTGRES_DB: pgcopy
    ports:
      - "55443:5432"
```

(Confirm the exact `environment`/`POSTGRES_DB` keys against the real `docker-compose.yml` in Step 1 — match its style precisely rather than the sketch above if it differs.)

- [ ] **Step 4: Add a new `-AllDatabases` scenario section to `scripts/integration-test.ps1`**

Read the file in full first to match its existing `Wait-ForPostgres`/`Read-Counts` helper function signatures exactly. Add a new `param()` switch `-AllDatabases`, and a new scenario block (following the same shape as the existing `-DropSchema` block) that:
1. Brings up `docker-compose-multi.yml`.
2. Waits for both containers ready.
3. Runs: `dotnet run --project src/PostgresCopy -- --origin "postgres://postgres:test@localhost:55442/pgcopy" --destination "postgres://postgres:test@localhost:55443/pgcopy" --all-databases --yes --verify`.
4. Verifies via `psql`/`Read-Counts`-style helper that `app_one.public.widgets` has 3 rows and `app_two.public.gadgets` has 2 rows on the destination container.
5. Verifies the destination's `pgcopy` database itself was also recreated (since it's not a system database and is not excluded) — confirm its origin tables (`accounts`/`orders` from `origin.sql`) exist and match origin row counts too.
6. Tears down `docker-compose-multi.yml` in a `finally` block unless `-KeepContainers`.

- [ ] **Step 5: Run the new scenario manually (requires Docker)**

Run: `.\scripts\integration-test.ps1 -AllDatabases`
Expected: script prints success for all row-count checks across all three databases (`pgcopy`, `app_one`, `app_two`) and exits 0.

- [ ] **Step 6: Commit**

```bash
git add tests/integration/docker-compose-multi.yml tests/integration/origin-multi-init.sql scripts/integration-test.ps1
git commit -m "test: add multi-database integration scenario for --all-databases"
```

---

### Task 9: Desktop UI — checkbox, database checklist, confirmation dialog

**Files:**
- Modify: `src/PostgresCopy.Desktop/MainForm.cs`
- Modify: `src/PostgresCopy.Desktop/DesktopRunHistoryEntry.cs`

**Interfaces:**
- Consumes: `AllDatabasesMigrationRunner`, `AllDatabasesMigrationSettings`, `AllDatabasesRunResult`, `PerDatabaseResult` (Task 4), `DestructiveActionPrompt.ConfirmOverwriteAllDatabases` (Task 5), `DestinationDatabaseLifecycle.ListDatabasesAsync`/`ExcludedDatabaseNames` (Task 3), `PostgresConnectionString.WithDatabase` (Task 1).
- Produces: nothing consumed by later tasks — this is the final UI slice.

This task has no unit test (WinForms UI has no existing unit test coverage in this codebase — confirmed by the test file listing, none reference `MainForm`). Verification is manual, per the project's own `AGENTS.md` guidance: "For desktop GUI or release-facing changes: `dotnet run --project src\PostgresCopy.Desktop`... verify the origin field, destination field, dry-run/copy button text, cancel path...".

- [ ] **Step 1: Add the `DesktopRunHistoryEntry.BatchId` field**

Read `src/PostgresCopy.Desktop/DesktopRunHistoryEntry.cs` in full (already shown: a positional record with 11 fields). Add one new optional trailing field so existing call sites keep compiling:

```csharp
internal sealed record DesktopRunHistoryEntry(
    DateTime StartedAtLocal,
    bool Succeeded,
    string Mode,
    string Origin,
    string Destination,
    string Schema,
    string Tables,
    int TablesCopied,
    long RowsCopied,
    TimeSpan Elapsed,
    string Message,
    string? BatchId = null);
```

- [ ] **Step 2: Build to confirm existing call sites still compile**

Run: `dotnet build PostgresCopy.sln`
Expected: succeeds unchanged (trailing optional field, existing `new DesktopRunHistoryEntry(...)` call in `CreateHistoryEntry` doesn't pass it, defaults to `null`).

- [ ] **Step 3: Add the "Copy all databases" checkbox and database checklist to the Connection tab**

In `src/PostgresCopy.Desktop/MainForm.cs`, add a new field near the existing Connection-tab fields (around line 38-41):

```csharp
    private readonly CheckBox allDatabasesCheckBox = new();
    private readonly Button loadDatabasesButton = new();
    private readonly CheckedListBox allDatabasesChecklist = new();
```

In `BuildOptionsPanel()` (around line 424-474), add the new checkbox following the exact style of `truncateCheckBox`:

```csharp
        allDatabasesCheckBox.Text = "Copy all databases (overwrite destination entirely)";
        allDatabasesCheckBox.AutoSize = true;
        StyleCheckBox(allDatabasesCheckBox);
        allDatabasesCheckBox.CheckedChanged += (_, _) => UpdateAllDatabasesModeState();
        SetHelp(allDatabasesCheckBox,
            "Enumerates every database on the origin server, then drops and recreates the same-named database on the destination for each one, copying schema and data. Ignores Schema and Tables. Requires typing OVERWRITE to confirm.");

        AddOptionCheckBox(panel, allDatabasesCheckBox);
```

Add a new method `UpdateAllDatabasesModeState()` near `UpdatePgToolsState()` (around line 990):

```csharp
    private void UpdateAllDatabasesModeState()
    {
        var enabled = allDatabasesCheckBox.Checked;
        schemaTextBox.Enabled = !enabled;
        tablesTextBox.Enabled = !enabled;
        truncateCheckBox.Enabled = !enabled;
        createSchemaCheckBox.Enabled = !enabled && SchemaCreator.PgToolsAvailable();
        dropSchemaCheckBox.Enabled = !enabled && createSchemaCheckBox.Checked && SchemaCreator.PgToolsAvailable();
        allDatabasesChecklist.Visible = enabled;
        loadDatabasesButton.Visible = enabled;

        if (enabled)
        {
            allDatabasesChecklist.Items.Clear();
        }
    }
```

Add the "Load databases" button and checklist as a new row in `BuildInputPanel()` (after the existing `AddRow(panel, "Options", ...)` call, around line 419):

```csharp
        loadDatabasesButton.Text = "Load databases";
        loadDatabasesButton.AutoSize = true;
        loadDatabasesButton.Visible = false;
        StyleButton(loadDatabasesButton, ButtonTone.Secondary);
        loadDatabasesButton.Click += LoadDatabasesButton_Click;
        SetHelp(loadDatabasesButton,
            "Connect to the origin server and list every database (excluding template0, template1, and postgres) to select which ones to copy.");

        allDatabasesChecklist.CheckOnClick = true;
        allDatabasesChecklist.Visible = false;
        allDatabasesChecklist.Height = 120;
        allDatabasesChecklist.Dock = DockStyle.Fill;

        var allDatabasesPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true, BackColor = SurfaceBackColor };
        allDatabasesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        allDatabasesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        allDatabasesPanel.Controls.Add(loadDatabasesButton, 0, 0);
        allDatabasesPanel.Controls.Add(allDatabasesChecklist, 0, 1);
        AddRow(panel, "All databases", allDatabasesPanel);
```

- [ ] **Step 4: Implement `LoadDatabasesButton_Click`**

Add near `PeekButton_Click` (around line 667), following its exact error-handling shape (the same try/catch chain over `ValidationException`/`PostgresException`/`NpgsqlException`/`OperationCanceledException`/`Exception` used throughout this file):

```csharp
    private async void LoadDatabasesButton_Click(object? sender, EventArgs eventArgs)
    {
        BeginLogOperation("Load databases");
        runningStatusOverride = "Listing origin databases...";
        activeRun = new CancellationTokenSource();
        SetRunning(true);
        string? finalStatus = null;

        try
        {
            var origin = PostgresConnectionString.Parse(originTextBox.Text.Trim());
            var maintenanceConnectionString = PostgresConnectionString.WithDatabase(
                origin.ConnectionString, DestinationDatabaseLifecycle.DefaultMaintenanceDatabase);

            await using var connection = new NpgsqlConnection(maintenanceConnectionString);
            await connection.OpenAsync(activeRun.Token);
            var databases = await DestinationDatabaseLifecycle.ListDatabasesAsync(connection, activeRun.Token);

            allDatabasesChecklist.Items.Clear();
            foreach (var name in databases)
            {
                allDatabasesChecklist.Items.Add(name, isChecked: true);
            }

            AppendLog($"Found {databases.Count} database(s) on origin (excluding template0, template1, postgres).");
            finalStatus = "Databases loaded.";
        }
        catch (ValidationException ex)
        {
            AppendLog(FormatValidationError(ex.Message));
            finalStatus = "Validation failed.";
        }
        catch (PostgresException ex)
        {
            AppendLog(FormatPostgresError(ex));
            finalStatus = "PostgreSQL error.";
        }
        catch (NpgsqlException ex)
        {
            AppendLog(FormatNpgsqlError(ex.Message));
            finalStatus = "Connection failed.";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Load databases cancelled.");
            finalStatus = "Cancelled.";
        }
        catch (Exception ex)
        {
            AppendLog(FormatUnexpectedError(ex.Message));
            finalStatus = "Load databases failed.";
        }
        finally
        {
            activeRun?.Dispose();
            activeRun = null;
            runningStatusOverride = null;
            SetRunning(false);
            if (finalStatus is not null)
                statusLabel.Text = finalStatus;
        }
    }
```

- [ ] **Step 5: Build to catch signature mismatches**

Run: `dotnet build PostgresCopy.sln`
Expected: succeeds. Fix any mismatch against the exact `FormatValidationError`/`FormatPostgresError`/`FormatNpgsqlError`/`FormatUnexpectedError`/`BeginLogOperation`/`AppendLog`/`SetRunning` signatures already in the file (read them before assuming the above call shapes are exact).

- [ ] **Step 6: Wire the Copy/Dry run buttons to branch into the all-databases path**

Find the existing dry-run/copy button click handlers (search for `dryRunButton.Click` and `runButton.Click` in `MainForm.cs`). Read the full existing handler (it's the method that calls `MigrationSettingsValidator.Validate`, `ConfirmTruncateIfNeeded`, `ConfirmDropSchemaIfNeeded`, `new MigrationRunner(...).RunAsync(...)`, and `SaveRunHistory(...)` — read lines around 1800-1960 in full before editing, since this plan's earlier research only captured `SaveRunHistory`/`CreateHistoryEntry`, not the full button handler). Add a branch at the very top of that handler:

```csharp
if (allDatabasesCheckBox.Checked)
{
    await RunAllDatabasesAsync(isDryRun);
    return;
}
```

(Match `isDryRun` to whatever the existing handler's actual local variable/parameter is named — confirm by reading the handler first.)

- [ ] **Step 7: Implement `RunAllDatabasesAsync`**

Add a new method near the existing run handlers, following the same `BeginLogOperation`/`SetRunning`/try-catch-finally shape used throughout the file:

```csharp
    private async Task RunAllDatabasesAsync(bool isDryRun)
    {
        BeginLogOperation(isDryRun ? "All databases dry run" : "All databases copy");
        runningStatusOverride = isDryRun ? "Checking all databases..." : "Copying all databases...";
        activeRun = new CancellationTokenSource();
        activeRunDryRun = isDryRun;
        SetRunning(true);
        var startedAt = DateTime.Now;
        string? finalStatus = null;

        try
        {
            var origin = PostgresConnectionString.Parse(originTextBox.Text.Trim());
            var destination = PostgresConnectionString.Parse(destinationTextBox.Text.Trim());

            if (origin.ComparisonKey.Equals(destination.ComparisonKey, StringComparison.Ordinal))
            {
                throw new ValidationException("Origin and destination point to the same database. Refusing to continue.");
            }

            var excludeDatabases = DestinationDatabaseLifecycle.ExcludedDatabaseNames
                .Concat(allDatabasesChecklist.Items.Cast<string>()
                    .Where((_, index) => !allDatabasesChecklist.GetItemChecked(index)))
                .ToList();

            var settings = new AllDatabasesMigrationSettings(
                origin.ConnectionString,
                destination.ConnectionString,
                excludeDatabases,
                isDryRun,
                verifyCheckBox.Checked,
                false,
                CliOptionsParser.DefaultBatchSize,
                false);

            var confirmed = isDryRun || ConfirmOverwriteAllDatabasesDialog(
                allDatabasesChecklist.Items.Cast<string>()
                    .Where((_, index) => allDatabasesChecklist.GetItemChecked(index))
                    .ToList());

            if (!isDryRun && !confirmed)
            {
                finalStatus = "Cancelled.";
                return;
            }

            var runner = new AllDatabasesMigrationRunner(new UiMigrationLogger(AppendLog));
            var summary = await runner.RunAsync(settings, confirmed, activeRun.Token);

            var batchId = Guid.NewGuid().ToString("N")[..8];
            foreach (var perDatabase in summary.Results)
            {
                SaveRunHistory(
                    startedAt,
                    perDatabase.Elapsed,
                    null,
                    perDatabase.Result,
                    perDatabase.Succeeded,
                    perDatabase.Succeeded
                        ? $"[{perDatabase.DatabaseName}] {(isDryRun ? "Dry run" : "Copy")} succeeded."
                        : $"[{perDatabase.DatabaseName}] {perDatabase.FailureMessage}",
                    batchId);
            }

            finalStatus = $"Completed {summary.TotalDatabases} database(s): {summary.Succeeded} succeeded, {summary.Failed} failed.";
        }
        catch (ValidationException ex)
        {
            AppendLog(FormatValidationError(ex.Message));
            finalStatus = "Validation failed.";
        }
        catch (OperationCanceledException)
        {
            AppendLog("All databases run cancelled.");
            finalStatus = "Cancelled.";
        }
        catch (Exception ex)
        {
            AppendLog(FormatUnexpectedError(ex.Message));
            finalStatus = "All databases run failed.";
        }
        finally
        {
            activeRun?.Dispose();
            activeRun = null;
            runningStatusOverride = null;
            SetRunning(false);
            if (finalStatus is not null)
                statusLabel.Text = finalStatus;
        }
    }

    private bool ConfirmOverwriteAllDatabasesDialog(IReadOnlyList<string> databaseNames)
    {
        using var dialog = new OverwriteAllDatabasesDialog(databaseNames);
        return dialog.ShowDialog(this) == DialogResult.OK;
    }
```

Note: `SaveRunHistory`'s existing signature (per the earlier-read code) is `SaveRunHistory(DateTime startedAt, TimeSpan elapsed, MigrationSettings? settings, MigrationRunResult? result, bool succeeded, string message)` — six parameters, no `batchId`. This call adds a seventh argument. **Before this compiles**, Step 8 below modifies `SaveRunHistory`'s signature to accept an optional trailing `string? batchId = null` parameter and thread it into `CreateHistoryEntry`'s `new DesktopRunHistoryEntry(...)` call as the new `BatchId` field from Step 1.

- [ ] **Step 8: Add the `batchId` parameter to `SaveRunHistory`/`CreateHistoryEntry`**

Modify the existing methods (originally at lines 1859-1910):

```csharp
    private void SaveRunHistory(
        DateTime startedAt,
        TimeSpan elapsed,
        MigrationSettings? settings,
        MigrationRunResult? result,
        bool succeeded,
        string message,
        string? batchId = null)
    {
        try
        {
            var entry = CreateHistoryEntry(startedAt, elapsed, settings, result, succeeded, message, batchId);
            historyStore.Append(entry);
            LoadRunHistory();
        }
        catch (Exception ex)
        {
            AppendLog($"[warn] Could not save local run history: {ex.Message}");
        }
    }

    private DesktopRunHistoryEntry CreateHistoryEntry(
        DateTime startedAt,
        TimeSpan elapsed,
        MigrationSettings? settings,
        MigrationRunResult? result,
        bool succeeded,
        string message,
        string? batchId = null)
    {
        var origin = TryRedactForHistory(
            originTextBox.Text,
            settings?.Origin.RedactedConnectionString ?? "Origin unavailable");
        var destination = TryRedactForHistory(
            destinationTextBox.Text,
            settings?.Destination.RedactedConnectionString ?? "Destination unavailable");
        var schema = settings?.Schema
            ?? (string.IsNullOrWhiteSpace(schemaTextBox.Text) ? "public" : schemaTextBox.Text.Trim());
        var tables = FormatTablesForHistory(settings?.TableFilter ?? ParseTables(tablesTextBox.Text));
        var mode = settings?.DryRun ?? activeRunDryRun ? "Dry run" : "Copy";

        return new DesktopRunHistoryEntry(
            startedAt,
            succeeded,
            mode,
            origin,
            destination,
            schema,
            tables,
            result?.TablesCopied ?? 0,
            result?.RowsCopied ?? 0,
            elapsed,
            CompactHistoryMessage(message),
            batchId);
    }
```

This is a backward-compatible change (new trailing optional parameter) — the existing single-database call site to `SaveRunHistory(...)` (search for its other call site in the copy/dry-run handler) keeps compiling unchanged.

- [ ] **Step 9: Create the `OverwriteAllDatabasesDialog` typed-confirmation dialog**

Create a new file `src/PostgresCopy.Desktop/OverwriteAllDatabasesDialog.cs`:

```csharp
// File: src/PostgresCopy.Desktop/OverwriteAllDatabasesDialog.cs

namespace PostgresCopy.Desktop;

internal sealed class OverwriteAllDatabasesDialog : Form
{
    private readonly TextBox confirmationTextBox = new();
    private readonly Button confirmButton = new();

    public OverwriteAllDatabasesDialog(IReadOnlyList<string> databaseNames)
    {
        Text = "Confirm overwrite of destination databases";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(520, 420);
        Padding = new Padding(16);

        var message = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 100,
            Text = "The following destination databases will be DROPPED and recreated from origin. " +
                   "Any other active connections to these databases will be forcibly terminated. " +
                   "All tables, indexes, sequences, functions, views, triggers, and data in each one will be permanently deleted. There is no undo.",
        };

        var list = new ListBox { Dock = DockStyle.Fill };
        foreach (var name in databaseNames)
        {
            list.Items.Add(name);
        }

        var confirmLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Text = "Type OVERWRITE to continue:",
            Margin = new Padding(0, 8, 0, 4),
        };

        confirmationTextBox.Dock = DockStyle.Top;
        confirmationTextBox.TextChanged += (_, _) =>
            confirmButton.Enabled = string.Equals(confirmationTextBox.Text, "OVERWRITE", StringComparison.Ordinal);

        confirmButton.Text = "Overwrite";
        confirmButton.DialogResult = DialogResult.OK;
        confirmButton.Enabled = false;
        confirmButton.Dock = DockStyle.Right;

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Dock = DockStyle.Right,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        buttonPanel.Controls.Add(confirmButton);
        buttonPanel.Controls.Add(cancelButton);

        Controls.Add(list);
        Controls.Add(confirmationTextBox);
        Controls.Add(confirmLabel);
        Controls.Add(message);
        Controls.Add(buttonPanel);

        AcceptButton = confirmButton;
        CancelButton = cancelButton;
    }
}
```

- [ ] **Step 10: Build**

Run: `dotnet build PostgresCopy.sln`
Expected: succeeds. Fix any remaining signature mismatches (e.g. `BeginLogOperation`, `AppendLog`, `FormatValidationError` exact signatures) by reading their actual definitions in `MainForm.cs` if the build reports errors.

- [ ] **Step 11: Manual GUI verification**

Run: `dotnet run --project src\PostgresCopy.Desktop`

Verify manually:
1. Connection tab shows the new "Copy all databases" checkbox.
2. Checking it disables Schema/Tables fields and the Truncate/Create-schema/Drop-schema checkboxes, and reveals "Load databases" + the checklist.
3. "Load databases" against a real local PostgreSQL instance populates the checklist with real database names, excluding `template0`/`template1`/`postgres`.
4. Unchecking a database in the list excludes it from the plan (verify via a dry run first — the log should list only checked databases).
5. Dry run reports the plan without prompting for confirmation.
6. A real run (against a disposable local/Docker PostgreSQL instance — **do not run this against any database containing real data**) prompts the `OverwriteAllDatabasesDialog`, requires typing `OVERWRITE` before the button enables, and only proceeds after confirmation.
7. History tab shows one row per database after a batch run.
8. Cancel button still works mid-run (existing `activeRun` cancellation token plumbing).

- [ ] **Step 12: Commit**

```bash
git add src/PostgresCopy.Desktop/MainForm.cs src/PostgresCopy.Desktop/DesktopRunHistoryEntry.cs src/PostgresCopy.Desktop/OverwriteAllDatabasesDialog.cs
git commit -m "feat: add copy-all-databases UI to desktop Connection tab"
```

---

### Task 10: Update `TODO.md` / `AGENTS.md` "Good Next Slices" bookkeeping

**Files:**
- Modify: `TODO.md`
- Modify: `AGENTS.md`

**Interfaces:** None — pure bookkeeping, per this repo's stated "Agile Working Style: Update `TODO.md` when a task becomes true."

- [ ] **Step 1: Check off or add the completed item in `TODO.md`**

Read `TODO.md`, find or add a line for "copy all databases / whole-server overwrite" and mark it done, following the file's existing checkbox convention.

- [ ] **Step 2: Update `AGENTS.md`'s "Do not re-add completed items" list**

In `AGENTS.md` (around line 139), add "whole-server all-databases overwrite" to the list of completed items so future agents don't accidentally propose re-doing it.

- [ ] **Step 3: Commit**

```bash
git add TODO.md AGENTS.md
git commit -m "docs: mark copy-all-databases feature complete in backlog"
```

---

## Self-Review Notes

**Spec coverage check** — every locked-in decision from the design spec maps to a task:
- Enumerate origin databases → Task 3 (`ListDatabasesAsync`) + Task 4 (orchestration).
- Drop entire destination database → Task 3 (`DropDatabaseAsync`).
- Maintenance DB reachability never assumed → Task 3 (`TryOpenMaintenanceConnectionAsync`) + Task 4 (preflight check before destructive path).
- Force-terminate other sessions, logged → Task 3 (`TerminateOtherBackendsAsync`) + Task 4 (logs count via `logger.Info`).
- One global confirmation listing every database by name → Task 5 (CLI) + Task 9 (`OverwriteAllDatabasesDialog`).
- System databases always excluded → Task 3 (`ExcludedDatabaseNames`).
- Checklist selection, not all-or-nothing → Task 9 (`CheckedListBox`) + Task 6 (`--exclude-database`).
- Ignore Schema/Tables filters → Task 6 (parser rejects `--schema`/`--tables` combination) + Task 9 (fields disabled in UI) + Task 4 (hardcoded `"public"`/`[]` — note: this hardcodes schema to `"public"` specifically, since "every schema" was never actually selected as a decision; re-check against the design doc's "every schema, every table" wording before implementing Task 4 — **flagging this as a gap**, see below).
- Mandatory schema creation → Task 4 (`SchemaCreator.CreateAsync` called unconditionally, not gated by a checkbox).
- Both desktop and CLI → Tasks 6 (CLI) and 9 (desktop).
- Continue past per-database failure → Task 4 (`RunAsync`'s `try/catch` inside the `foreach`, no early return).
- Typed `OVERWRITE` confirmation → Task 5 + Task 9.

**Resolved during self-review:** the design spec says "every schema, every table" per database, but `SchemaCreator.CreateAsync`/`MigrationRunner` are schema-scoped by construction (`PostgresSchemaInspector.GetUserTablesAsync(settings.Schema, ...)` takes exactly one schema name), so copying *every* schema per database would require a nested loop (schemas inside databases) beyond what this plan builds. Confirmed with the user: v1 ships **`public` schema only per database**, matching every existing integration seed/test in this codebase. True multi-schema-per-database is deliberately deferred — tracked as a follow-up in `TODO_POLISHING.md` rather than silently dropped.

**Placeholder scan:** no "TBD"/"add error handling"/"similar to Task N" patterns found in step content. Task 7 and Task 10 are documentation-only tasks with concrete instructions (find via Grep, then edit) rather than vague "update docs" — acceptable since they're bookkeeping, not implementation, and reference exact files.

**Type consistency check:** `PerDatabaseResult`, `AllDatabasesRunResult`, `AllDatabasesMigrationSettings` property names are declared once in Task 4 and referenced identically in Task 9 (`perDatabase.DatabaseName`, `perDatabase.Succeeded`, `perDatabase.Result`, `perDatabase.FailureMessage`, `perDatabase.Elapsed`, `summary.TotalDatabases`, `summary.Succeeded`, `summary.Failed`, `summary.Results`) — verified matching. `DestinationDatabaseLifecycle.DefaultMaintenanceDatabase`/`ExcludedDatabaseNames`/`ListDatabasesAsync` used identically across Task 4, Task 6 (`Program.cs`), and Task 9. `SaveRunHistory`/`CreateHistoryEntry`'s new `batchId` parameter is threaded consistently from Task 9 Step 7's call site through Task 9 Step 8's signature change.
