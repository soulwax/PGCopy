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
          --dry-run               Print the plan without copying data.
          --truncate-destination  Empty planned destination tables before copying.
          --verify                Compare origin and destination row counts after copying.
          --yes                   Skip confirmation for destructive actions.
          --verbose               Show stack traces for unexpected failures.
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
                case "--data-only":
                case "--schema-only":
                    return CliParseResult.Failed($"{arg} is not implemented in the MVP yet.");
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
            batchSize));
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
