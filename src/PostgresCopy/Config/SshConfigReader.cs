// File: src/PostgresCopy/Config/SshConfigReader.cs

namespace PostgresCopy.Config;

public sealed record SshConfigEntry(
    string Alias,
    string HostName,
    string? User,
    int Port,
    string? IdentityFile);

public static class SshConfigReader
{
    public static IReadOnlyList<SshConfigEntry> Read()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh", "config");

        if (!File.Exists(path))
            return [];

        return Parse(File.ReadAllLines(path));
    }

    internal static List<SshConfigEntry> Parse(string[] lines)
    {
        var entries = new List<SshConfigEntry>();

        string? currentAlias = null;
        string? hostName = null;
        string? user = null;
        int port = 22;
        string? identityFile = null;

        void Flush()
        {
            if (currentAlias is null || currentAlias.Contains('*') || currentAlias.Contains('?'))
                return;
            entries.Add(new SshConfigEntry(
                currentAlias,
                hostName ?? currentAlias,
                user,
                port,
                ExpandPath(identityFile)));
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var sep = line.IndexOf(' ');
            if (sep < 0) continue;

            var key = line[..sep].Trim();
            var value = line[(sep + 1)..].Trim();

            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                currentAlias = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                hostName = null;
                user = null;
                port = 22;
                identityFile = null;
            }
            else if (key.Equals("HostName", StringComparison.OrdinalIgnoreCase))
            {
                hostName = value;
            }
            else if (key.Equals("User", StringComparison.OrdinalIgnoreCase))
            {
                user = value;
            }
            else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, out var p) && p > 0)
            {
                port = p;
            }
            else if (key.Equals("IdentityFile", StringComparison.OrdinalIgnoreCase))
            {
                identityFile = value;
            }
        }

        Flush();
        return entries;
    }

    internal static string? ExpandPath(string? path)
    {
        if (path is null) return null;
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }
        return path;
    }
}
