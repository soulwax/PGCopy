using System.Diagnostics;
using System.Text;
using Npgsql;

namespace PostgresCopy.Migration;

public static class SchemaCreator
{
    public static async Task<string?> CreateAsync(
        string originConnectionString,
        string destConnectionString,
        string schema,
        CancellationToken cancellationToken)
    {
        var pgDumpError = CheckToolAvailable("pg_dump");
        if (pgDumpError != null) return pgDumpError;

        var psqlError = CheckToolAvailable("psql");
        if (psqlError != null) return psqlError;

        var originUrl = BuildPgUrl(originConnectionString);
        var destUrl = BuildPgUrl(destConnectionString);

        var (schemaSql, dumpError) = await RunDumpAsync(originUrl, schema, cancellationToken);
        if (dumpError != null) return dumpError;

        return await RunPsqlAsync(destUrl, schemaSql, cancellationToken);
    }

    private static string? CheckToolAvailable(string tool)
    {
        try
        {
            var psi = new ProcessStartInfo(tool)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("--version");

            using var p = Process.Start(psi)!;
            if (!p.WaitForExit(3000))
            {
                p.Kill();
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
        string originUrl, string schema, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("pg_dump")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
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

    private static async Task<string?> RunPsqlAsync(string destUrl, string sql, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("psql")
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(destUrl);

        using var process = Process.Start(psi)!;

        var writeTask = Task.Run(async () =>
        {
            await process.StandardInput.WriteAsync(sql.AsMemory(), ct);
            await process.StandardInput.FlushAsync(ct);
            process.StandardInput.Close();
        }, ct);

        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        // Collect both results regardless of which side faults so stderr is never lost.
        string stderr;
        try
        {
            await Task.WhenAll(writeTask, stderrTask);
            stderr = stderrTask.Result;
        }
        catch
        {
            stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty;
            if (!writeTask.IsCompletedSuccessfully)
                writeTask.Exception?.Handle(_ => true);
            throw;
        }

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            return $"psql failed (exit {process.ExitCode}): {stderr.Trim()}";
        }

        return null;
    }

    private static string BuildPgUrl(string npgsqlConnectionString)
    {
        var b = new NpgsqlConnectionStringBuilder(npgsqlConnectionString);
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
        sb.Append('/').Append(Uri.EscapeDataString(b.Database ?? string.Empty));

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
