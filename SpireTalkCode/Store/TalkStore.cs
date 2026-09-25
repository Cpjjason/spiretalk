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
        /// <summary>Deals kept with this NPC, ever — marks on the keepsake relic.</summary>
        [JsonPropertyName("engravings")] public int Engravings { get; set; }
        /// <summary>Highest keepsake level ever reached on this profile (0 = never received), for the keepsake book.</summary>
        [JsonPropertyName("bestKeepsake")] public int BestKeepsakeLevel { get; set; }
        /// <summary>When the keepsake first became a vow (UTC, ISO), or null.</summary>
        [JsonPropertyName("vowUtc")] public string? VowUtc { get; set; }
        [JsonPropertyName("history")] public List<HistoryEntry> History { get; set; } = new();
        /// <summary>What the NPC remembers of earlier visits: short notes, never its own old lines.</summary>
        [JsonPropertyName("memories")] public List<MemoryNote> Memories { get; set; } = new();
        /// <summary>Id of the visit in progress (or the last one); its lines are sent in full.</summary>
        [JsonPropertyName("visit")] public string? CurrentVisit { get; set; }
    }

    public sealed class HistoryEntry
    {
        [JsonPropertyName("role")] public string Role { get; set; } = "player";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("utc")] public string? Utc { get; set; }
        /// <summary>Run (seed:character) the line was said in, so per-run memory can filter.</summary>
        [JsonPropertyName("run")] public string? Run { get; set; }
        [JsonPropertyName("visit")] public string? Visit { get; set; }
    }

    /// <summary>One thing an NPC remembers about the climber. Players can read, edit, add and delete these.</summary>
    public sealed class MemoryNote
    {
        [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        /// <summary>auto (summed up from a visit) / road (an encounter) / player (written by the player)</summary>
        [JsonPropertyName("source")] public string Source { get; set; } = "auto";
        [JsonPropertyName("run")] public string? Run { get; set; }
        [JsonPropertyName("visit")] public string? Visit { get; set; }
        [JsonPropertyName("utc")] public string? Utc { get; set; }
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
        /// <summary>Road encounters (event ids) met this run; a run gets at most a couple.</summary>
        [JsonPropertyName("encounters")] public List<string> Encounters { get; set; } = new();
        /// <summary>The Ancient met resting by the road this run, so a reload meets the same one.</summary>
        [JsonPropertyName("restAncient")] public string? RestAncient { get; set; }
        /// <summary>Helped the merchant on the road: his next shop this run is cheaper, once.</summary>
        [JsonPropertyName("merchantFavor")] public bool MerchantFavor { get; set; }
        /// <summary>The floor whose shop spent the favour, so leaving and re-entering (or reloading) that shop keeps the price.</summary>
        [JsonPropertyName("favorFloor")] public int? FavorFloor { get; set; }
        /// <summary>Robbed the merchant on the road: every shop for the rest of this run costs more.</summary>
        [JsonPropertyName("merchantGrudge")] public bool MerchantGrudge { get; set; }
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
        [JsonPropertyName("epilogues")] public List<EpilogueRecord> Epilogues { get; set; } = new();
    }

    private const int MaxDeals = 60;
    private const int MaxEpilogues = 20;

    public List<DealRecord> Deals
    {
        get
        {
            EnsureLoaded();
            return _data.Deals;
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
        rec.History.Add(new HistoryEntry { Role = role, Text = text, Utc = DateTime.UtcNow.ToString("o"), Run = runKey, Visit = rec.CurrentVisit });
        if (rec.History.Count > MaxHistoryPerNpc)
        {
            rec.History.RemoveRange(0, rec.History.Count - MaxHistoryPerNpc);
        }
    }

    private const int MaxAutoMemories = 30;

    /// <summary>
    /// A new visit starts. Whatever the last visit(s) left in the log is summed up into a memory
    /// note first, so from now on the model reads a note about those visits instead of its own
    /// old lines (which it would otherwise copy).
    /// </summary>
    public string BeginVisit(string npcKey, string? runKey)
    {
        var rec = Get(npcKey);
        SummarizeFinishedVisits(rec, except: null);
        rec.CurrentVisit = Guid.NewGuid().ToString("N")[..10];
        return rec.CurrentVisit;
    }

    private static void SummarizeFinishedVisits(NpcRecord rec, string? except)
    {
        foreach (var entry in rec.History.Where(h => h.Visit == null)) entry.Visit = "legacy";
        var noted = rec.Memories.Select(m => m.Visit).Where(v => v != null).ToHashSet();
        var visits = rec.History.Select(h => h.Visit!).Distinct().Where(v => v != except && !noted.Contains(v)).ToList();
        foreach (var visit in visits)
        {
            var lines = rec.History.Where(h => h.Visit == visit).ToList();
            var text = MemoryText.FromVisit(lines);
            // An empty note still marks the visit as handled.
            rec.Memories.Add(new MemoryNote { Text = text ?? "", Source = "auto", Run = lines.LastOrDefault()?.Run, Visit = visit, Utc = lines.LastOrDefault()?.Utc });
        }
        var auto = rec.Memories.Where(m => m.Source == "auto").ToList();
        if (auto.Count > MaxAutoMemories)
        {
            foreach (var old in auto.Take(auto.Count - MaxAutoMemories)) rec.Memories.Remove(old);
        }
    }

    /// <summary>Lines of the visit in progress, oldest first.</summary>
    public List<HistoryEntry> CurrentVisitLines(string npcKey)
    {
        var rec = Get(npcKey);
        return rec.CurrentVisit == null ? new() : rec.History.Where(h => h.Visit == rec.CurrentVisit).ToList();
    }

    /// <summary>Notes the model may read this visit; with per-run memory only this run's (and the player's own).</summary>
    public List<MemoryNote> MemoriesFor(string npcKey, string? runKey)
    {
        var rec = Get(npcKey);
        return rec.Memories
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .Where(m => SpireTalkConfig.RememberAcrossRuns || m.Source == "player" || runKey == null || m.Run == runKey)
            .ToList();
    }

    /// <summary>What the NPC said in this visit and the two before it, clean of directives — the repeat filter's reference.</summary>
    public List<string> RecentNpcTexts(string npcKey, int visits = 3)
    {
        var rec = Get(npcKey);
        var recent = rec.History.Select(h => h.Visit).Distinct().TakeLast(visits).ToHashSet();
        return rec.History.Where(h => h.Role == "npc" && recent.Contains(h.Visit)).Select(h => MemoryText.CleanNpcText(h.Text)).ToList();
    }

    public MemoryNote AddMemory(string npcKey, string text, string source, string? runKey)
    {
        var note = new MemoryNote { Text = text, Source = source, Run = runKey, Utc = DateTime.UtcNow.ToString("o") };
        Get(npcKey).Memories.Add(note);
        return note;
    }

    public void DeleteMemory(string npcKey, string id) => Get(npcKey).Memories.RemoveAll(m => m.Id == id);

    public void DeleteHistoryEntry(string npcKey, HistoryEntry entry) => Get(npcKey).History.Remove(entry);

    /// <summary>For the memory screen: finish any visit that is over so its note shows up.</summary>
    public void SummarizeForViewing(string npcKey)
    {
        var rec = Get(npcKey);
        SummarizeFinishedVisits(rec, except: null);
    }

    /// <summary>Forget what was said (log, notes, engine session) but keep the relationship and keepsake marks.</summary>
    public void ForgetConversation(string npcKey)
    {
        var rec = Get(npcKey);
        rec.History.Clear();
        rec.Memories.Clear();
        rec.EngineState = null;
        rec.CurrentVisit = null;
        rec.LastLine = null;
        Save();
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
