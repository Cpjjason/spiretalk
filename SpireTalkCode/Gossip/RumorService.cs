using SpireTalk.Affection;
using SpireTalk.Npc;
using SpireTalk.Store;

namespace SpireTalk.Gossip;

/// <summary>
/// Gossip: what the climber says about one NPC reaches the others. After every reply the AI may
/// write a one-line rumor ("The climber called Orobas' eyes disgusting") with a +/−/0 mood; the mod
/// keeps it only when it names another NPC (alias table in npcs.json), so small talk never turns
/// into a rumor. The NPC it is about hears it on the climber's next visit and reacts once
/// (+2 / −3); the Merchant hears everything.
/// </summary>
public static class RumorService
{
    private const int MaxRumorChars = 200;
    private const int PromptRumors = 5;
    public const int PraiseDelta = 2;
    public const int SlightDelta = -3;

    /// <summary>Keep the rumor the AI proposed, if it is really about someone else.</summary>
    public static void Record(NpcDefinition from, string? text, string? sentiment, string runKey)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var clean = text.Trim();
        if (clean.Length > MaxRumorChars) clean = clean[..MaxRumorChars];
        var about = FindSubject(clean, from.Key);
        if (about == null)
        {
            if (SpireTalkConfig.DebugLogging) MainFile.Logger.Info($"[Rumor] dropped (names nobody): {clean}", 1);
            return;
        }
        var mood = (sentiment ?? "0").Trim();
        mood = mood.StartsWith('+') ? "+" : mood.StartsWith('-') ? "-" : "0";
        // One rumor per teller and subject per run; small models like to repeat themselves.
        if (TalkStore.Instance.Rumors.Any(r => r.Run == runKey && r.From == from.Key && string.Equals(r.About, about, StringComparison.OrdinalIgnoreCase)))
        {
            if (SpireTalkConfig.DebugLogging) MainFile.Logger.Info($"[Rumor] dropped (already told this run): {clean}", 1);
            return;
        }
        TalkStore.Instance.AddRumor(new TalkStore.RumorRecord
        {
            From = from.Key,
            About = about,
            Text = clean,
            Sentiment = mood,
            Run = runKey,
            Utc = DateTime.UtcNow.ToString("o"),
        });
        TalkStore.Instance.Save();
        if (SpireTalkConfig.DebugLogging) MainFile.Logger.Info($"[Rumor] {from.Key} → {about} ({mood}): {clean}", 1);
    }

    /// <summary>The other NPC named in the text, by any alias, or null. The speaker never counts.</summary>
    private static string? FindSubject(string text, string speakerKey)
    {
        foreach (var npc in NpcCatalog.All)
        {
            if (string.Equals(npc.Key, speakerKey, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var alias in npc.Aliases.Append(npc.Name).Append(npc.ZhName).Append(npc.DisplayName))
            {
                if (alias.Length >= 2 && text.Contains(alias, StringComparison.OrdinalIgnoreCase)) return npc.Key;
            }
        }
        return null;
    }

    /// <summary>What this NPC has heard, for the `rumors` prompt variable.</summary>
    public static string PromptFor(NpcDefinition npc)
    {
        var all = TalkStore.Instance.Rumors;
        IEnumerable<TalkStore.RumorRecord> mine = npc.Kind == NpcKind.Merchant
            ? all
            : all.Where(r => string.Equals(r.About, npc.Key, StringComparison.OrdinalIgnoreCase) || r.About == "*");
        var picked = mine.Reverse().Take(PromptRumors).Reverse().ToList();
        if (picked.Count == 0) return "none";
        return string.Join("\n", picked.Select(r =>
        {
            var teller = NpcCatalog.Get(r.From)?.Name ?? r.From;
            var mood = r.Sentiment == "+" ? "kind" : r.Sentiment == "-" ? "unkind" : "neutral";
            var fresh = r.Resolved ? "" : " [new — you have not reacted to this yet]";
            return $"- {teller} heard the climber say: \"{r.Text}\" ({mood}){fresh}";
        }));
    }

    /// <summary>
    /// The climber arrives; anything said about this NPC since last time lands now. One reaction per
    /// visit (the first unresolved rumor), all of them marked heard. Returns the affection change.
    /// </summary>
    public static int ReactOnArrival(NpcDefinition npc)
    {
        var fresh = TalkStore.Instance.Rumors
            .Where(r => !r.Resolved && string.Equals(r.About, npc.Key, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (fresh.Count == 0) return 0;
        var delta = fresh[0].Sentiment switch { "+" => PraiseDelta, "-" => SlightDelta, _ => 0 };
        foreach (var r in fresh) r.Resolved = true;
        if (delta != 0) AffectionService.Apply(npc.Key, npc.Kind, delta);
        TalkStore.Instance.Save();
        if (SpireTalkConfig.DebugLogging) MainFile.Logger.Info($"[Rumor] {npc.Key} heard {fresh.Count} rumor(s), reacted {delta:+#;-#;0}", 1);
        return delta;
    }
}
