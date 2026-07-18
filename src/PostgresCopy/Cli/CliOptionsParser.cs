// File: src/PostgresCopy/Cli/CliOptionsParser.cs

namespace PostgresCopy.Cli;

public static class CliOptionsParser
{
    public const int DefaultBatchSize = 10000;

    public const string HelpText = """
        PostgresCopy copies table data from one PostgreSQL database to another.

        Usage:
          PostgresCopy --origin "<ORIGIN_DATABASE_URL>" --destination "<DESTINATION_DATABASE_URL>"

        Options:
          --origin <value>        Origin PostgreSQL URL or Npgsql connection string.
          --destination <value>   Destination PostgreSQL URL or Npgsql connection string.
          --schema <value>        Schema to copy. Defaults to public.
          --table <value>         Copy one table. May be passed more than once.
          --tables <csv>          Copy comma-separated tables.
          --batch-size <number>   Reserved for future row batching. Defaults to 10000.
          --create-schema         Copy schema DDL from origin to destination via pg_dump
                                  before copying data. Requires pg_dump and psql on PATH.
                                  Use a direct (non-pooled) connection string for this step.
          --drop-schema           DESTRUCTIVE. Drop the destination schema (CASCADE) and
                                  recreate it before applying DDL. Requires --create-schema
                                  and --yes (or interactive confirmation). Deletes ALL
                                  objects in the destination schema permanently.
          --schema-only           Copy schema DDL only, then stop before copying table data.
          --data-only             Copy table data only. Destination schema must already match.
          --dry-run               Print the plan without copying data.
          --truncate-destination  Empty planned destination tables before copying.
          --verify                Compare row counts after copying; repair mismatched
                                  tables with bounded retries.
          --yes                   Skip confirmation for destructive actions.
          --verbose               Show stack traces for unexpected failures.
          --all-databases         DESTRUCTIVE. Copy every database on the origin server,
                                  dropping and recreating each matching database on the
                                  destination server. Cannot be combined with --schema,
                                  --table/--tables, --schema-only, --data-only,
                                  --create-schema, --drop-schema, or --truncate-destination.
          --exclude-database <name>  Skip this database when using --all-databases. May be
                                  passed more than once.
          --help                  Show help.
        """;

    public static CliParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return CliParseResult.Failed("Missing required options: --origin and --destination.");
        }

        string? origin = null;
        string? destination = null;
        var schema = "public";
        var tables = new List<string>();
        var dryRun = false;
        var truncateDestination = false;
        var verify = false;
        var verbose = false;
        var yes = false;
        var batchSize = DefaultBatchSize;
        var createSchema = false;
        var schemaOnly = false;
        var dataOnly = false;
        var dropSchema = false;
        var allDatabases = false;
        var excludeDatabases = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--help":
                case "-h":
                    return CliParseResult.Help();
                case "--origin":
                    if (!TryReadValue(args, ref i, arg, out origin, out var originError))
                    {
                        return CliParseResult.Failed(originError);
                    }

                    break;
                case "--destination":
                    if (!TryReadValue(args, ref i, arg, out destination, out var destinationError))
                    {
                        return CliParseResult.Failed(destinationError);
                    }

                    break;
                case "--schema":
                    if (!TryReadValue(args, ref i, arg, out schema, out var schemaError))
                    {
                        return CliParseResult.Failed(schemaError);
                    }

                    break;
                case "--table":
                    if (!TryReadValue(args, ref i, arg, out var table, out var tableError))
                    {
                        return CliParseResult.Failed(tableError);
                    }

                    AddTableFilter(tables, table);
                    break;
                case "--tables":
                    if (!TryReadValue(args, ref i, arg, out var tableCsv, out var tablesError))
                    {
                        return CliParseResult.Failed(tablesError);
                    }

                    foreach (var tableName in tableCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        AddTableFilter(tables, tableName);
                    }

                    break;
                case "--batch-size":
                    if (!TryReadValue(args, ref i, arg, out var batchValue, out var batchError))
                    {
                        return CliParseResult.Failed(batchError);
                    }

                    if (!int.TryParse(batchValue, out batchSize) || batchSize <= 0)
                    {
                        return CliParseResult.Failed("--batch-size must be a positive integer.");
                    }

                    break;
                case "--create-schema":
                    createSchema = true;
                    break;
                case "--drop-schema":
                    dropSchema = true;
                    break;
                case "--schema-only":
                    schemaOnly = true;
                    break;
                case "--data-only":
                    dataOnly = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--truncate-destination":
                    truncateDestination = true;
                    break;
                case "--verify":
                    verify = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--yes":
                    yes = true;
                    break;
                case "--all-databases":
                    allDatabases = true;
                    break;
                case "--exclude-database":
                    if (!TryReadValue(args, ref i, arg, out var excludeDatabase, out var excludeDatabaseError))
                    {
                        return CliParseResult.Failed(excludeDatabaseError);
                    }

                    excludeDatabases.Add(excludeDatabase);
                    break;
                case "--drop-and-recreate":
                    return CliParseResult.Failed($"{arg} is destructive and is not implemented yet.");
                default:
                    return CliParseResult.Failed($"Unknown option: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(origin))
        {
            return CliParseResult.Failed("Missing required option: --origin.");
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            return CliParseResult.Failed("Missing required option: --destination.");
        }

        if (string.IsNullOrWhiteSpace(schema))
        {
            return CliParseResult.Failed("--schema cannot be empty.");
        }

        if (schemaOnly && dataOnly)
        {
            return CliParseResult.Failed("--schema-only and --data-only cannot be used together.");
        }

        if (schemaOnly && dryRun)
        {
            return CliParseResult.Failed("--schema-only cannot be combined with --dry-run because applying DDL changes the destination.");
        }

        if (schemaOnly && truncateDestination)
        {
            return CliParseResult.Failed("--schema-only cannot be combined with --truncate-destination because no table data is copied.");
        }

        if (schemaOnly && verify)
        {
            return CliParseResult.Failed("--schema-only cannot be combined with --verify because no table data is copied.");
        }

        if (dataOnly && createSchema)
        {
            return CliParseResult.Failed("--data-only cannot be combined with --create-schema.");
        }

        if (dropSchema && dataOnly)
        {
            return CliParseResult.Failed("--drop-schema cannot be combined with --data-only.");
        }

        if (dropSchema && !createSchema && !schemaOnly)
        {
            return CliParseResult.Failed("--drop-schema requires --create-schema (or --schema-only). Dropping without recreating would leave the destination empty.");
        }

        if (allDatabases && !string.Equals(schema, "public", StringComparison.Ordinal))
        {
            return CliParseResult.Failed("--all-databases cannot be combined with --schema because it always copies the public schema of every database.");
        }

        if (allDatabases && tables.Count > 0)
        {
            return CliParseResult.Failed("--all-databases cannot be combined with --table/--tables because it copies every table in the public schema of every database.");
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

        return CliParseResult.Parsed(new CliOptions(
            origin,
            destination,
            schema,
            tables,
            dryRun,
            truncateDestination,
            verify,
            verbose,
            yes,
            batchSize,
            createSchema || schemaOnly,
            schemaOnly,
            dataOnly,
            dropSchema,
            allDatabases,
            excludeDatabases));
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string option,
        out string value,
        out string error)
    {
        value = string.Empty;
        error = string.Empty;

        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error = $"{option} requires a value.";
            return false;
        }

        value = args[++index];
        return true;
    }

    private static void AddTableFilter(List<string> tables, string table)
    {
        if (!string.IsNullOrWhiteSpace(table) && !tables.Contains(table, StringComparer.Ordinal))
        {
            tables.Add(table);
        }
    }
}
