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
    private const int HistoryLinesToSend = 40; // 20 exchanges

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

        // Full run snapshot on the opener, relationship numbers every turn; last turn's proposals
        // are cleared so a silent model can't replay them.
        var vars = isOpener ? (JsonObject)ctx.Variables.DeepClone() : new JsonObject();
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

        var history = new JsonArray();
        foreach (var h in ctx.History.TakeLast(HistoryLinesToSend))
        {
            history.Add(new JsonObject { ["role"] = h.Role == "npc" ? "assistant" : "user", ["content"] = h.Text });
        }

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
        return ToTurn(result, raw);
    }

    private static NpcTurn ToTurn(YuminaRuntime.TurnResult result, string raw)
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
        var lines = Ancients.AncientConversation.SplitIntoLines(text);
        return new NpcTurn(lines, choices, delta, null, action, OpenAiClient.Model);
    }
}
