// File: src/PostgresCopy.Desktop/DesktopRunHistoryStore.cs

using System.Text.Json;

namespace PostgresCopy.Desktop;

internal sealed class DesktopRunHistoryStore
{
    private const int MaxEntries = 200;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string path;

    public DesktopRunHistoryStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostgresCopy",
            "history.json"))
    {
    }

    public DesktopRunHistoryStore(string path)
    {
        this.path = path;
    }

    public IReadOnlyList<DesktopRunHistoryEntry> Load()
    {
        if (!File.Exists(path))
            return [];

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<DesktopRunHistoryEntry>>(stream, JsonOptions) ?? [];
    }

    public void Append(DesktopRunHistoryEntry entry)
    {
        var entries = Load()
            .Append(entry)
            .OrderByDescending(item => item.StartedAtLocal)
            .Take(MaxEntries)
            .OrderBy(item => item.StartedAtLocal)
            .ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, entries, JsonOptions);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    public void Clear()
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
