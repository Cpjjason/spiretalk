using System.Text.Json.Nodes;
using SpireTalk.Engine;
using SpireTalk.Npc;
using SpireTalk.Store;

namespace SpireTalk.Backend;

/// <summary>
/// Plays an NPC with the Yumina engine running inside the game and the player's own model key.
/// The card (persona, rules, variables) is the script; the engine assembles the prompt, parses the
/// reply and keeps the variables; <see cref="OpenAiClient"/> makes the one network call. Nothing
/// here needs an account anywhere.
///
/// Per NPC the engine state (variables + turn counter) lives in the profile store next to the
/// conversation log, so a run — or, with "remember past runs" on, every run — continues the same
/// session. The relationship numbers stay the mod's: they are pushed in before every turn and the
/// AI's proposals are read back out afterwards and validated by <see cref="ConversationRules"/>.
/// </summary>
public sealed class EngineBackend : INpcBackend
{
    private readonly OpenAiClient _llm = new();

    public string DisplayName => "Yumina engine · " + OpenAiClient.Model;

    public bool IsConfigured => _llm.IsConfigured;

    public OpenAiClient Client => _llm;

    public async Task<NpcTurn> ExchangeAsync(VisitContext ctx, string playerLine, bool isOpener, CancellationToken ct)
    {
        var card = CardLibrary.For(ctx.NpcKey)
            ?? throw new BackendException(string.Format(Loc.Ui("ErrNoCard"), ctx.NpcKey), null, "NO_CARD");
        var runtime = YuminaRuntime.Instance;
        var record = TalkStore.Instance.Get(ctx.NpcKey);

        // Same session rule as before: one per NPC; a new run starts fresh unless memory spans runs.
        var stateJson = record.EngineState;
        var reusable = !string.IsNullOrEmpty(stateJson)
                       && record.EngineCard == card.Source
                       && (SpireTalkConfig.RememberAcrossRuns || record.SessionRunKey == ctx.RunKey);
        if (!reusable)
        {
            stateJson = runtime.StartSession(card.Json, new JsonObject()).StateJson;
            record.SessionRunKey = ctx.RunKey;
            record.EngineCard = card.Source;
        }

        // The whole visit context every turn (gold and the deal lines move mid-visit), plus
        // the relationship numbers; last turn's proposals are cleared so a silent model can't replay them.
        var vars = (JsonObject)ctx.Variables.DeepClone();
        vars["affection"] = ctx.Affection;
        vars["affection_tier"] = ConversationRules.TierName(ctx.Tier);
        vars["married"] = ctx.Married;
        vars["lines_left"] = ctx.LinesLeft;
        vars["allowed_actions"] = ctx.AllowedActions.Count == 0 ? "none" : string.Join(", ", ctx.AllowedActions);
        vars["language"] = ctx.Language;
        vars["affection_delta"] = 0;
        vars["action"] = new JsonObject { ["type"] = "none", ["amount"] = 0 };
        vars["choices"] = new JsonArray();
        stateJson = runtime.ApplyVariables(card.Json, stateJson!, vars);

        // Read fresh every line: memory notes for earlier visits, this visit word for word.
        var history = MemoryText.BuildHistory(ctx.NpcKey, ctx.RunKey);

        var prompt = runtime.BuildTurnPrompt(card.Json, stateJson, history, playerLine, OpenAiClient.Model);
        if (SpireTalkConfig.DebugLogging)
        {
            var chars = prompt.Messages.Sum(m => m?["content"]?.GetValue<string>()?.Length ?? 0);
            MainFile.Logger.Info($"[Engine] {ctx.NpcKey}: {prompt.Messages.Count} messages, {chars} chars, card={card.Source}", 1);
        }

        var raw = await _llm.CompleteAsync(prompt.Messages, prompt.MaxTokens, prompt.Temperature, ct);

        if (SpireTalkConfig.DebugLogging)
        {
            MainFile.Logger.Info($"[Engine] {ctx.NpcKey} raw reply ({raw.Length} chars): {raw.ReplaceLineEndings(" ⏎ ")}", 1);
        }
        var result = runtime.FinishTurn(card.Json, prompt.StateJson, playerLine, raw);
        record.EngineState = result.StateJson;
        if (SpireTalkConfig.DebugLogging && result.DroppedEffects.Count > 0)
        {
            MainFile.Logger.Info($"[Engine] dropped AI writes: {result.DroppedEffects.ToJsonString()}", 1);
        }
        return ToTurn(result, raw, TalkStore.Instance.RecentNpcTexts(ctx.NpcKey), ctx.NpcKey);
    }

    private static NpcTurn ToTurn(YuminaRuntime.TurnResult result, string raw, List<string> saidBefore, string npcKey)
    {
        var vars = result.Variables;
        var delta = 0;
        if (vars["affection_delta"] is JsonValue dv)
        {
            if (dv.TryGetValue<int>(out var i)) delta = i;
            else if (dv.TryGetValue<double>(out var d)) delta = (int)Math.Round(d);
        }

        var action = NpcAction.None;
        if (vars["action"] is JsonObject ao)
        {
            var type = ao["type"]?.GetValue<string>() ?? "none";
            var amount = 0;
            if (ao["amount"] is JsonValue av)
            {
                if (av.TryGetValue<int>(out var i)) amount = i;
                else if (av.TryGetValue<double>(out var d)) amount = (int)Math.Round(d);
            }
            action = new NpcAction(type.Trim().ToLowerInvariant(), amount);
        }

        var choices = new List<string>();
        if (vars["choices"] is JsonArray ca)
        {
            foreach (var n in ca)
            {
                var s = n?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(s)) choices.Add(s.Trim());
            }
        }

        // Models that ignore the directive syntax often still write the fields as loose
        // "key: value" lines. Recover them from the raw output and keep them off screen.
        var text = LooseFields.Extract(raw, result.CleanText, ref choices, ref delta, ref action);
        var lines = RepeatFilter.Apply(Ancients.AncientConversation.SplitIntoLines(text), saidBefore).Select(l => TidyDots(l, npcKey)).Where(l => l.Length > 0).ToList();
        choices = choices.Select(PlainChoice).Where(c => c.Length > 0).ToList();
        return new NpcTurn(lines, choices, delta, null, action, OpenAiClient.Model);
    }

    private static readonly System.Text.RegularExpressions.Regex DotRun = new(@"\.{4,}|…{2,}|(?:\.\s*){4,}");
    private static readonly System.Text.RegularExpressions.Regex AnyDots = new(@"\.{2,}|…+");

    /// <summary>
    /// Models pile up dots ("....... ..") far past how anyone writes. Runs longer than three become
    /// "..." for everyone. Neow really does talk in fragments, so she keeps her pauses; everyone else
    /// keeps at most two per bubble and the rest become plain commas or full stops.
    /// </summary>
    internal static string TidyDots(string line, string npcKey)
    {
        var s = DotRun.Replace(line, "...");
        if (!string.Equals(npcKey, "NEOW", StringComparison.OrdinalIgnoreCase))
        {
            var cjk = s.Any(c => c >= 0x3000 && c <= 0x9fff);
            var kept = 0;
            s = AnyDots.Replace(s, m =>
            {
                kept++;
                if (kept <= 2) return m.Value;
                var atEnd = m.Index + m.Length >= s.TrimEnd().Length;
                return atEnd ? (cjk ? "。" : ".") : (cjk ? "，" : ",");
            });
        }
        return System.Text.RegularExpressions.Regex.Replace(s, @"\s{2,}", " ").Trim();
    }

    private static readonly System.Text.RegularExpressions.Regex Ellipsis = new(@"\s*(?:\.{2,}|…+)\s*");
    private static readonly System.Text.RegularExpressions.Regex CjkGap = new(@"(?<=[　-鿿＀-￯])\s+(?=[　-鿿＀-￯])");

    /// <summary>
    /// The climber's suggested replies are theirs, not the NPC's: small models copy the NPC's
    /// voice into them (Neow's "我... 会... 等你..."). Ellipsis runs become plain spaces, and the
    /// spaces left between Chinese characters go ("我会等你"); a trailing one is kept as "…".
    /// </summary>
    internal static string PlainChoice(string choice)
    {
        var s = choice.Trim();
        var trailing = s.EndsWith("...") || s.EndsWith("…");
        s = Ellipsis.Replace(s, " ").Trim();
        s = CjkGap.Replace(s, "");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s{2,}", " ");
        return trailing && s.Length > 0 && !char.IsPunctuation(s[^1]) ? s + "…" : s;
    }
}
