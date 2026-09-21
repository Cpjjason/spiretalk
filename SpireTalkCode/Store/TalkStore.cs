using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using SpireTalk.Npc;

namespace SpireTalk.Store;

/// <summary>
/// Profile-scoped persistence for everything SpireTalk remembers between runs: which Yumina
/// session belongs to which NPC, our own affection mirror, conversation history (for the
/// direct backend and for "remember past visits"), and which one-per-run rewards were given.
/// Stored as JSON next to the game's own profile data so profiles stay isolated.
/// </summary>
public sealed class TalkStore
{
    public sealed class NpcRecord
    {
        [JsonPropertyName("sessionId")] public string? SessionId { get; set; }
        /// <summary>Run the Yumina session was opened in; only consulted when memory is per-run.</summary>
        [JsonPropertyName("sessionRun")] public string? SessionRunKey { get; set; }
        [JsonPropertyName("affection")] public int Affection { get; set; }
        [JsonPropertyName("talks")] public int Talks { get; set; }
        [JsonPropertyName("lastTalkUtc")] public string? LastTalkUtc { get; set; }
        [JsonPropertyName("lastLine")] public string? LastLine { get; set; }
        [JsonPropertyName("history")] public List<HistoryEntry> History { get; set; } = new();
    }

    public sealed class HistoryEntry
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "player";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("utc")] public string? Utc { get; set; }
        /// <summary>Run (seed:character) the line was said in, so per-run memory can filter.</summary>
        [JsonPropertyName("run")] public string? Run { get; set; }
    }

    /// <summary>Rewards already handed out in one run (keyed by run seed + character).</summary>
    public sealed class RunRecord
    {
        [JsonPropertyName("gold")] public List<string> GoldGiven { get; set; } = new();
        [JsonPropertyName("heal")] public List<string> HealGiven { get; set; } = new();
        [JsonPropertyName("potion")] public List<string> PotionGiven { get; set; } = new();
        [JsonPropertyName("gift")] public List<string> GiftGiven { get; set; } = new();
        [JsonPropertyName("affectionRelic")] public List<string> AffectionRelicGiven { get; set; } = new();
        [JsonPropertyName("touchedUtc")] public string? TouchedUtc { get; set; }
    }

    public sealed class Data
    {
        [JsonPropertyName("version")] public int Version { get; set; } = 2;
        [JsonPropertyName("npcs")] public Dictionary<string, NpcRecord> Npcs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        [JsonPropertyName("runs")] public Dictionary<string, RunRecord> Runs { get; set; } = new();
    }

    private const string FileName = "spiretalk.json";
    private const int MaxHistoryPerNpc = 80;
    private const int MaxRunsKept = 20;
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

    public RunRecord GetRun(string runKey)
    {
        EnsureLoaded();
        if (!_data.Runs.TryGetValue(runKey, out var run))
        {
            run = new RunRecord();
            _data.Runs[runKey] = run;
            if (_data.Runs.Count > MaxRunsKept)
            {
                var oldest = _data.Runs.OrderBy(kv => kv.Value.TouchedUtc ?? "").First().Key;
                if (oldest != runKey) _data.Runs.Remove(oldest);
            }
        }
        run.TouchedUtc = DateTime.UtcNow.ToString("o");
        return run;
    }

    public void AppendHistory(string npcKey, string role, string text, string? runKey = null)
    {
        var rec = Get(npcKey);
        rec.History.Add(new HistoryEntry { Role = role, Text = text, Utc = DateTime.UtcNow.ToString("o"), Run = runKey });
        if (rec.History.Count > MaxHistoryPerNpc)
        {
            rec.History.RemoveRange(0, rec.History.Count - MaxHistoryPerNpc);
        }
    }

    /// <summary>
    /// What this NPC remembers. Every NPC has its own log; with "remember past runs" off only
    /// the lines from <paramref name="runKey"/> come back, so each run starts as a stranger.
    /// </summary>
    public IEnumerable<HistoryLine> HistoryOf(string npcKey, string? runKey = null)
    {
        var history = Get(npcKey).History.AsEnumerable();
        if (!SpireTalkConfig.RememberAcrossRuns && runKey != null)
        {
            history = history.Where(h => h.Run == runKey);
        }
        return history.Select(h => new HistoryLine(h.Role, h.Text, h.Utc));
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
