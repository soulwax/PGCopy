// File: src/PostgresCopy/Migration/SchemaCreator.cs

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using PostgresCopy.Config;
using PostgresCopy.Database;

namespace PostgresCopy.Migration;

public static class SchemaCreator
{
    public static async Task<string?> CreateAsync(
        string originConnectionString,
        string destConnectionString,
        string schema,
        CancellationToken cancellationToken)
    {
        var pgDump = ResolveToolPath("pg_dump");
        var pgDumpError = CheckToolAvailable(pgDump);
        if (pgDumpError != null) return pgDumpError;

        var psql = ResolveToolPath("psql");
        var psqlError = CheckToolAvailable(psql);
        if (psqlError != null) return psqlError;

        var originUrl = BuildPgUrl(originConnectionString);
        var destUrl = BuildPgUrl(destConnectionString);

        var (schemaSql, dumpError) = await RunDumpAsync(pgDump, originUrl, schema, cancellationToken);
        if (dumpError != null) return dumpError;

        // We always pre-create the destination schema (in DropAndRecreateSchemaAsync, or
        // implicitly because it already exists), so strip pg_dump's CREATE SCHEMA line
        // to avoid "schema already exists" failures under ON_ERROR_STOP=1.
        schemaSql = StripCreateSchemaStatements(schemaSql, schema);

        return await RunPsqlAsync(psql, destUrl, schemaSql, cancellationToken);
    }

    public static async Task<string?> DropAndRecreateSchemaAsync(
        string destConnectionString,
        string schema,
        CancellationToken cancellationToken)
    {
        var psql = ResolveToolPath("psql");
        var psqlError = CheckToolAvailable(psql);
        if (psqlError != null) return psqlError;

        var destUrl = BuildPgUrl(destConnectionString);
        var quoted = SqlIdentifier.Quote(schema);
        var sql = $"DROP SCHEMA IF EXISTS {quoted} CASCADE; CREATE SCHEMA {quoted};";

        return await RunPsqlCommandAsync(psql, destUrl, sql, cancellationToken);
    }

    // Probes a tools/ directory beside the running executable before falling back to
    // bare name resolution via PATH. This allows pg_dump.exe / psql.exe to be placed
    // in artifacts\pg-tools\ (via scripts\update-pg-tools.ps1) without requiring a
    // system-wide PostgreSQL installation.
    public static string ResolveToolPath(string toolName)
    {
        var exe = OperatingSystem.IsWindows() ? toolName + ".exe" : toolName;
        var processPath = Environment.ProcessPath;
        if (processPath is not null)
        {
            var toolsDir = Path.Combine(Path.GetDirectoryName(processPath)!, "tools");
            var candidate = Path.Combine(toolsDir, exe);
            if (File.Exists(candidate))
                return candidate;
        }
        return toolName;
    }

    // Returns true when both pg_dump and psql are reachable (bundled tools\ or PATH).
    // Cheap to call on the UI thread — each check shells out with a 3 s timeout.
    public static bool PgToolsAvailable() =>
        CheckToolAvailable(ResolveToolPath("pg_dump")) is null &&
        CheckToolAvailable(ResolveToolPath("psql")) is null;

    internal static string StripCreateSchemaStatements(string sql, string schema)
    {
        if (string.IsNullOrEmpty(sql)) return sql;

        // pg_dump emits either CREATE SCHEMA public; or CREATE SCHEMA "name";
        // optionally followed by ALTER SCHEMA <name> OWNER TO ...;
        // Match a whole-line statement; trailing newline is consumed so we don't leave blank lines.
        var pattern = @"^\s*CREATE\s+SCHEMA\s+(""" + Regex.Escape(schema) + @"""|" + Regex.Escape(schema) + @")\s*;\s*\r?\n";
        return Regex.Replace(sql, pattern, string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    public static string? CheckToolAvailable(string tool)
    {
        try
        {
            var psi = new ProcessStartInfo(tool)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--version");

            using var p = Process.Start(psi)!;
            if (!p.WaitForExit(3000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return $"'{tool}' timed out during version check. Ensure it is on PATH and responsive.";
            }
            return null;
        }
        catch
        {
            return $"'{tool}' was not found on PATH. Install PostgreSQL client tools and ensure pg_dump and psql are accessible from this shell.";
        }
    }

    private static async Task<(string output, string? error)> RunDumpAsync(
        string pgDump, string originUrl, string schema, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(pgDump)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--schema-only");
        psi.ArgumentList.Add("--no-owner");
        psi.ArgumentList.Add("--no-acl");
        psi.ArgumentList.Add("--schema");
        psi.ArgumentList.Add(schema);
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(originUrl);

        using var process = Process.Start(psi)!;
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(outputTask, stderrTask);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask;
            return (string.Empty, $"pg_dump failed (exit {process.ExitCode}): {stderr.Trim()}");
        }

        return (await outputTask, null);
    }

    private static async Task<string?> RunPsqlAsync(string psql, string destUrl, string sql, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(psql)
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("ON_ERROR_STOP=1");
        psi.ArgumentList.Add("--single-transaction");
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(destUrl);

        using var process = Process.Start(psi)!;

        // Swallow broken-pipe IOExceptions on the write side. They mean psql died early
        // (bad credentials, unreachable host, SSL failure) and closed stdin before we
        // finished sending DDL. The real diagnostic is in psql's stderr, which we read below.
        var writeTask = Task.Run(async () =>
        {
            try
            {
                await process.StandardInput.WriteAsync(sql.AsMemory(), ct);
                await process.StandardInput.FlushAsync(ct);
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                try { process.StandardInput.Close(); } catch { }
            }
        }, ct);

        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(writeTask, stderrTask);
        await process.WaitForExitAsync(ct);

        var stderr = stderrTask.Result.Trim();

        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrEmpty(stderr)
                ? $"psql exited with code {process.ExitCode} but produced no error output. The destination may be unreachable, the credentials may be wrong, or SSL may have failed."
                : $"psql failed (exit {process.ExitCode}): {stderr}";
            return $"Schema apply rolled back. No DDL was committed to the destination. {detail}";
        }

        return null;
    }

    private static async Task<string?> RunPsqlCommandAsync(string psql, string destUrl, string sql, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(psql)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("ON_ERROR_STOP=1");
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(destUrl);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(sql);

        using var process = Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var stderr = stderrTask.Result.Trim();
            var detail = string.IsNullOrEmpty(stderr)
                ? $"psql exited with code {process.ExitCode} but produced no error output. The destination may be unreachable, the credentials may be wrong, or SSL may have failed."
                : $"psql failed (exit {process.ExitCode}): {stderr}";
            return $"Drop and recreate schema failed. {detail}";
        }

        return null;
    }

    internal static string BuildPgUrl(string npgsqlConnectionString)
    {
        var b = new NpgsqlConnectionStringBuilder(npgsqlConnectionString);

        if (string.IsNullOrWhiteSpace(b.Database))
        {
            throw new ValidationException(
                "Connection string is missing a database name." + Environment.NewLine +
                "What happened: schema copy needs a specific source or target database, but the connection string has no Database= value." + Environment.NewLine +
                "How to resolve: include the database in the URL (postgres://user:pwd@host:5432/my_database) or add Database=my_database to the connection string.");
        }

        var sb = new StringBuilder("postgresql://");

        if (!string.IsNullOrEmpty(b.Username))
        {
            sb.Append(Uri.EscapeDataString(b.Username));
            if (!string.IsNullOrEmpty(b.Password))
                sb.Append(':').Append(Uri.EscapeDataString(b.Password));
            sb.Append('@');
        }

        sb.Append(b.Host ?? "localhost");
        sb.Append(':').Append(b.Port > 0 ? b.Port : 5432);
        sb.Append('/').Append(Uri.EscapeDataString(b.Database));

        var sslMode = MapSslMode(b.SslMode);
        if (sslMode != null)
            sb.Append("?sslmode=").Append(sslMode);

        return sb.ToString();
    }

    private static string? MapSslMode(SslMode mode) => mode switch
    {
        SslMode.Disable => "disable",
        SslMode.Allow => "allow",
        SslMode.Require => "require",
        SslMode.VerifyCA => "verify-ca",
        SslMode.VerifyFull => "verify-full",
        _ => null,
    };
}
