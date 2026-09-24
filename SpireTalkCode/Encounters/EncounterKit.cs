using System.Text.Json.Nodes;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using SpireTalk.Affection;
using SpireTalk.Ancients;
using SpireTalk.Backend;
using SpireTalk.Engine;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;

namespace SpireTalk.Encounters;

/// <summary>
/// Which picture an encounter shows. Every scene has two: one in the game's own style and one
/// matching the waifu mods (AncientWaifus for the Ancients, Merchant2CuteII for the Merchant),
/// chosen by whether the player has that mod loaded. Missing pictures fall back to the other
/// style, then to a vanilla event painting, so the event never shows an empty frame.
/// </summary>
public static class EncounterArt
{
    private const string Folder = "res://SpireTalk/images/encounters/";

    public static bool AncientWaifus => Loaded("AncientWaifus");
    public static bool MerchantWaifu => Loaded("Merchant2CuteII");

    private static readonly Dictionary<string, bool> LoadedCache = new();

    private static bool Loaded(string assembly)
    {
        if (LoadedCache.TryGetValue(assembly, out var hit)) return hit;
        var found = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == assembly);
        LoadedCache[assembly] = found;
        return found;
    }

    /// <summary>res:// path of <paramref name="scene"/> in the style the player's mods call for.</summary>
    public static string For(string scene, bool waifu, string vanillaFallback)
    {
        var preferred = Folder + scene + (waifu ? "_waifu.png" : "_vanilla.png");
        if (ResourceLoader.Exists(preferred)) return preferred;
        var other = Folder + scene + (waifu ? "_vanilla.png" : "_waifu.png");
        if (!waifu && ResourceLoader.Exists(other)) return vanillaFallback; // never show waifu art to a vanilla player
        return ResourceLoader.Exists(other) ? other : vanillaFallback;
    }
}

/// <summary>How many road encounters a run gets, and which ones it already had.</summary>
public static class EncounterBudget
{
    public const int PerRun = 2;

    public static bool Allows(Player player, string eventId)
    {
        var run = TalkStore.Instance.GetRun(RewardExecutor.RunKey(player));
        if (run.Encounters.Contains(eventId)) return false;
        return run.Encounters.Count < PerRun;
    }

    public static void Spend(Player player, string eventId)
    {
        var run = TalkStore.Instance.GetRun(RewardExecutor.RunKey(player));
        if (run.Encounters.Contains(eventId)) return;
        run.Encounters.Add(eventId);
        TalkStore.Instance.Save();
    }
}

/// <summary>
/// An NPC speaking inside a road encounter: their own card, their memory of the climber, and a
/// note on the situation, run once through the Yumina runtime. The event keeps the outcome;
/// the NPC only reacts to it. Whatever happened is written into their history, so the next
/// real visit remembers the road.
/// </summary>
public static class EncounterVoice
{
    public static async Task<string?> SayAsync(NpcDefinition npc, Player player, string situation, CancellationToken ct)
    {
        try
        {
            if (!MainFile.Backend.IsConfigured) return null;
            var card = CardLibrary.For(npc.Key);
            if (card == null) return null;
            var runtime = YuminaRuntime.Instance;
            var runKey = RewardExecutor.RunKey(player);

            var vars = GameContext.CollectForMerchant(player);
            vars["npc"] = npc.Key;
            var (affection, married) = AffectionService.Read(npc.Key);
            vars["affection"] = affection;
            vars["affection_tier"] = ConversationRules.TierName(ConversationRules.TierOf(affection, married));
            vars["married"] = married;
            vars["talks"] = TalkStore.Instance.Get(npc.Key).Talks;
            vars["lines_left"] = 0;
            vars["allowed_actions"] = "none";
            vars["deal_offer"] = "none";

            var state = runtime.StartSession(card.Json, vars).StateJson;
            var history = MemoryText.BuildHistory(npc.Key, runKey);
            var request =
                "[ROAD ENCOUNTER - not your usual place. " + situation +
                " Speak as yourself: 1 to 3 short sentences in your usual voice, one paragraph, in " + Loc.ReplyLanguageName() +
                ". Say something new: do not reuse the wording of anything you said earlier." +
                " Plain prose only: no choices, no offers, no directive lines.]";
            var prompt = runtime.BuildTurnPrompt(card.Json, state, history, request, OpenAiClient.Model);
            var raw = await MainFile.Backend.Client.CompleteAsync(prompt.Messages, Math.Max(300, prompt.MaxTokens), prompt.Temperature, ct);
            var result = runtime.FinishTurn(card.Json, prompt.StateJson, request, raw);

            var choices = new List<string>();
            var delta = 0;
            var action = NpcAction.None;
            var text = LooseFields.Extract(raw, result.CleanText, ref choices, ref delta, ref action).Trim();
            text = string.Join("\n", RepeatFilter.Apply(Ancients.AncientConversation.SplitIntoLines(text), TalkStore.Instance.RecentNpcTexts(npc.Key)));
            if (string.IsNullOrWhiteSpace(text)) return null;
            TalkStore.Instance.AppendHistory(npc.Key, "npc", text, runKey);
            TalkStore.Instance.Save();
            return Loc.Sanitize(OneParagraph(text));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Encounter] {npc.Key} silent: {ex.GetType().Name}: {ex.Message}", 1);
            return null;
        }
    }

    /// <summary>Speech-bubble lines run together: an event page reads as one paragraph.</summary>
    private static string OneParagraph(string text)
    {
        var parts = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            if (sb.Length > 0 && !IsCjk(sb[^1]) && !IsCjk(part[0])) sb.Append(' ');
            sb.Append(part);
        }
        return sb.ToString();
    }

    private static bool IsCjk(char c) => c >= 0x2E80;

    /// <summary>What happened on the road becomes one of the NPC's memory notes (text from the mod's events table).</summary>
    public static void Remember(NpcDefinition npc, Player player, string memoryKey, params object[] args)
    {
        var text = string.Format(Loc.EventText(memoryKey), new object[] { Loc.NpcName(npc) }.Concat(args).ToArray());
        if (text.Length > 0) text = char.ToUpperInvariant(text[0]) + text[1..];
        TalkStore.Instance.AddMemory(npc.Key, text, "road", RewardExecutor.RunKey(player));
        TalkStore.Instance.Save();
    }
}
