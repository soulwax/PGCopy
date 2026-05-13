using System.Diagnostics;
using PostgresCopy.Migration;

namespace PostgresCopy.Desktop;

internal static class EnvironmentPreflightChecker
{
    public static async Task<IReadOnlyList<EnvironmentPreflightResult>> CheckAsync(CancellationToken cancellationToken)
    {
        var results = new List<EnvironmentPreflightResult>
        {
            CheckPostgresTool("pg_dump", "Required for Create schema and --schema-only."),
            CheckPostgresTool("psql", "Required for Create schema and --schema-only."),
            CheckSshConfig()
        };

        results.Add(await CheckProcessAsync(
            "Docker CLI",
            "docker",
            ["--version"],
            "Optional. Required only for scripts/integration-test.ps1.",
            cancellationToken));

        results.Add(await CheckProcessAsync(
            "Docker daemon",
            "docker",
            ["info", "--format", "{{.ServerVersion}}"],
            "Optional. Integration tests need a reachable Docker daemon.",
            cancellationToken));

        return results;
    }

    private static EnvironmentPreflightResult CheckPostgresTool(string tool, string detail)
    {
        var error = SchemaCreator.CheckToolAvailable(tool);
        return error is null
            ? EnvironmentPreflightResult.Pass(tool, $"{detail} Found on PATH.")
            : EnvironmentPreflightResult.Warn(tool, $"{detail} {error}");
    }

    private static EnvironmentPreflightResult CheckSshConfig()
    {
        var entries = SshConfigReader.Read();
        return entries.Count > 0
            ? EnvironmentPreflightResult.Pass("SSH config", $"Found {entries.Count} host entr{(entries.Count == 1 ? "y" : "ies")} in %USERPROFILE%\\.ssh\\config.")
            : EnvironmentPreflightResult.Warn("SSH config", "No named SSH hosts found. This is fine unless you want SSH auto-population.");
    }

    private static async Task<EnvironmentPreflightResult> CheckProcessAsync(
        string name,
        string fileName,
        IReadOnlyList<string> arguments,
        string detail,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var processStart = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (var argument in arguments)
            {
                processStart.ArgumentList.Add(argument);
            }

            using var process = Process.Start(processStart);
            if (process is null)
            {
                return EnvironmentPreflightResult.Warn(name, $"{detail} Could not start '{fileName}'.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return EnvironmentPreflightResult.Warn(name, $"{detail} '{fileName}' did not respond within 5 seconds.");
            }

            var output = (await stdoutTask).Trim();
            var error = (await stderrTask).Trim();

            if (process.ExitCode == 0)
            {
                var found = string.IsNullOrWhiteSpace(output) ? "Command completed." : output.Split('\n')[0].Trim();
                return EnvironmentPreflightResult.Pass(name, $"{detail} {found}");
            }

            var message = string.IsNullOrWhiteSpace(error) ? $"'{fileName}' exited with code {process.ExitCode}." : error;
            return EnvironmentPreflightResult.Warn(name, $"{detail} {message}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return EnvironmentPreflightResult.Warn(name, $"{detail} '{fileName}' did not respond within 5 seconds.");
        }
        catch (Exception ex)
        {
            return EnvironmentPreflightResult.Warn(name, $"{detail} {ex.Message}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort cleanup for an optional local preflight check.
        }
    }
}

internal sealed record EnvironmentPreflightResult(
    string Name,
    bool Passed,
    string Detail)
{
    public static EnvironmentPreflightResult Pass(string name, string detail) => new(name, true, detail);

    public static EnvironmentPreflightResult Warn(string name, string detail) => new(name, false, detail);
}
