using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Saves;

namespace SpireTalk.Store;

/// <summary>
/// Profile-scoped persistence for everything SpireTalk remembers between runs:
/// which Yumina session belongs to which NPC, affection, and visit counts.
/// Stored as JSON next to the game's own profile data so profiles stay isolated.
/// </summary>
public sealed class TalkStore
{
    public sealed class NpcRecord
    {
        [JsonPropertyName("sessionId")] public string? SessionId { get; set; }
        [JsonPropertyName("affection")] public int Affection { get; set; }
        [JsonPropertyName("talks")] public int Talks { get; set; }
        [JsonPropertyName("lastTalkUtc")] public string? LastTalkUtc { get; set; }
        [JsonPropertyName("lastLine")] public string? LastLine { get; set; }
    }

    public sealed class Data
    {
        [JsonPropertyName("version")] public int Version { get; set; } = 1;
        [JsonPropertyName("npcs")] public Dictionary<string, NpcRecord> Npcs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private const string FileName = "spiretalk.json";
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private Data _data = new();
    private string? _loadedPath;

    public static TalkStore Instance { get; } = new();

    public NpcRecord Get(string npcKey)
    {
        EnsureLoaded();
        if (!_data.Npcs.TryGetValue(npcKey, out var rec))
        {
            rec = new NpcRecord();
            _data.Npcs[npcKey] = rec;
        }
        return rec;
    }

    public void Save()
    {
        var path = ResolvePath();
        if (path == null) return;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(_data, JsonOpts));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Store] save failed: {ex.Message}", 1);
        }
    }

    public void ResetNpc(string npcKey)
    {
        EnsureLoaded();
        _data.Npcs.Remove(npcKey);
        Save();
    }

    public void ResetAll()
    {
        _data = new Data();
        Save();
    }

    private void EnsureLoaded()
    {
        var path = ResolvePath();
        if (path == null || path == _loadedPath) return;
        _loadedPath = path;
        _data = new Data();
        try
        {
            if (File.Exists(path))
            {
                var parsed = JsonSerializer.Deserialize<Data>(File.ReadAllText(path), JsonOpts);
                if (parsed != null) _data = parsed;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Store] load failed, starting fresh: {ex.Message}", 1);
        }
    }

    private static string? ResolvePath()
    {
        try
        {
            var sm = SaveManager.Instance;
            if (sm != null && sm.IsProfileInitialized)
            {
                var scoped = sm.GetProfileScopedPath(FileName);
                return scoped.StartsWith("user://", StringComparison.Ordinal)
                    ? ProjectSettings.GlobalizePath(scoped)
                    : scoped;
            }
        }
        catch
        {
            // fall through to the unscoped location
        }
        return ProjectSettings.GlobalizePath("user://spiretalk/" + FileName);
    }
}
