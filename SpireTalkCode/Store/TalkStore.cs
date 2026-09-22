using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using SpireTalk.Npc;

namespace SpireTalk.Store;

/// <summary>
/// Profile-scoped persistence for everything SpireTalk remembers between runs: each NPC's engine
/// session state, our own affection mirror, conversation history ("remember past visits"), and
/// which one-per-run rewards were given.
/// Stored as JSON next to the game's own profile data so profiles stay isolated.
/// </summary>
public sealed class TalkStore
{
    public sealed class NpcRecord
    {
        /// <summary>Engine state snapshot (variables + turn counter) of this NPC's session, as JSON.</summary>
        [JsonPropertyName("engineState")] public string? EngineState { get; set; }
        /// <summary>Which card file the session was opened on; a different card starts fresh.</summary>
        [JsonPropertyName("engineCard")] public string? EngineCard { get; set; }
        /// <summary>Run the session was opened in; only consulted when memory is per-run.</summary>
        [JsonPropertyName("sessionRun")] public string? SessionRunKey { get; set; }
        [JsonPropertyName("affection")] public int Affection { get; set; }
        [JsonPropertyName("talks")] public int Talks { get; set; }
        [JsonPropertyName("lastTalkUtc")] public string? LastTalkUtc { get; set; }
        [JsonPropertyName("lastLine")] public string? LastLine { get; set; }
        /// <summary>Type of the last deal this NPC proposed, so the next one differs.</summary>
        [JsonPropertyName("lastDeal")] public string? LastDealType { get; set; }
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
        /// <summary>Ancient whose relic the merchant put on the shelf this run, and which relic (id entry).</summary>
        [JsonPropertyName("shelfAncient")] public string? ShelfAncient { get; set; }
        [JsonPropertyName("shelfRelic")] public string? ShelfRelic { get; set; }
        /// <summary>Whether the epilogue for this run was written (one per run).</summary>
        [JsonPropertyName("epilogueDone")] public bool EpilogueDone { get; set; }
    }

    /// <summary>A deal an NPC proposed and the climber accepted. Settled by the game, not by the AI.</summary>
    public sealed class DealRecord
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("npc")] public string Npc { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        /// <summary>Threshold the deal is judged against (hp, gold, gold to spend); 0 when the type has none.</summary>
        [JsonPropertyName("param")] public int Param { get; set; }
        /// <summary>Act index (0-based) the deal settles in: entering it, or leaving the one before.</summary>
        [JsonPropertyName("targetAct")] public int TargetAct { get; set; }
        [JsonPropertyName("run")] public string Run { get; set; } = "";
        [JsonPropertyName("madeAct")] public int MadeAct { get; set; }
        /// <summary>pending / kept / broken / void</summary>
        [JsonPropertyName("status")] public string Status { get; set; } = "pending";
        /// <summary>Running total for the counting deals (gold spent at the shop).</summary>
        [JsonPropertyName("progress")] public int Progress { get; set; }
        /// <summary>The NPC has mentioned the outcome once; after that it is old news.</summary>
        [JsonPropertyName("told")] public bool Told { get; set; }
        [JsonPropertyName("utc")] public string? Utc { get; set; }
    }

    /// <summary>Something the climber said about another NPC, as the NPC who heard it would repeat it.</summary>
    public sealed class RumorRecord
    {
        [JsonPropertyName("from")] public string From { get; set; } = "";
        /// <summary>NPC key the rumor is about, or "*" for everyone (broken deals).</summary>
        [JsonPropertyName("about")] public string About { get; set; } = "";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        /// <summary>"+", "-" or "0"</summary>
        [JsonPropertyName("sentiment")] public string Sentiment { get; set; } = "0";
        [JsonPropertyName("run")] public string? Run { get; set; }
        /// <summary>The NPC it is about has reacted (affection moved once).</summary>
        [JsonPropertyName("resolved")] public bool Resolved { get; set; }
        [JsonPropertyName("utc")] public string? Utc { get; set; }
    }

    /// <summary>What an NPC said over a finished run.</summary>
    public sealed class EpilogueRecord
    {
        [JsonPropertyName("npc")] public string Npc { get; set; } = "";
        [JsonPropertyName("run")] public string Run { get; set; } = "";
        [JsonPropertyName("win")] public bool Win { get; set; }
        [JsonPropertyName("floor")] public int Floor { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("utc")] public string? Utc { get; set; }
    }

    public sealed class Data
    {
        [JsonPropertyName("version")] public int Version { get; set; } = 3;
        [JsonPropertyName("npcs")] public Dictionary<string, NpcRecord> Npcs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        [JsonPropertyName("runs")] public Dictionary<string, RunRecord> Runs { get; set; } = new();
        [JsonPropertyName("deals")] public List<DealRecord> Deals { get; set; } = new();
        [JsonPropertyName("rumors")] public List<RumorRecord> Rumors { get; set; } = new();
        [JsonPropertyName("epilogues")] public List<EpilogueRecord> Epilogues { get; set; } = new();
    }

    private const int MaxDeals = 60;
    private const int MaxRumors = 30;
    private const int MaxEpilogues = 20;

    public List<DealRecord> Deals
    {
        get
        {
            EnsureLoaded();
            return _data.Deals;
        }
    }

    public List<RumorRecord> Rumors
    {
        get
        {
            EnsureLoaded();
            return _data.Rumors;
        }
    }

    public List<EpilogueRecord> Epilogues
    {
        get
        {
            EnsureLoaded();
            return _data.Epilogues;
        }
    }

    public void AddDeal(DealRecord deal)
    {
        Deals.Add(deal);
        if (_data.Deals.Count > MaxDeals) _data.Deals.RemoveRange(0, _data.Deals.Count - MaxDeals);
    }

    public void AddRumor(RumorRecord rumor)
    {
        Rumors.Add(rumor);
        if (_data.Rumors.Count > MaxRumors) _data.Rumors.RemoveRange(0, _data.Rumors.Count - MaxRumors);
    }

    public void AddEpilogue(EpilogueRecord epilogue)
    {
        Epilogues.Add(epilogue);
        if (_data.Epilogues.Count > MaxEpilogues) _data.Epilogues.RemoveRange(0, _data.Epilogues.Count - MaxEpilogues);
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
        _data.Deals.RemoveAll(d => string.Equals(d.Npc, npcKey, StringComparison.OrdinalIgnoreCase));
        _data.Rumors.RemoveAll(r => string.Equals(r.From, npcKey, StringComparison.OrdinalIgnoreCase) || string.Equals(r.About, npcKey, StringComparison.OrdinalIgnoreCase));
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
