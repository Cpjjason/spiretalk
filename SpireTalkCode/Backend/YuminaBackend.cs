using System.Text.Json.Nodes;
using SpireTalk.Npc;
using SpireTalk.Store;
using SpireTalk.Yumina;

namespace SpireTalk.Backend;

/// <summary>
/// Plays an NPC through a Yumina world session. The world carries the same persona and rules
/// as npcs.json; the mod feeds it the run snapshot as session variables and reads the AI's
/// proposals (affection_delta, choices, action) back out of the returned state.
/// </summary>
public sealed class YuminaBackend : INpcBackend
{
    private readonly YuminaClient _client;

    public YuminaBackend(YuminaClient client)
    {
        _client = client;
    }

    public string DisplayName => "Yumina";

    public bool IsConfigured => _client.HasToken;

    public async Task<NpcTurn> ExchangeAsync(VisitContext ctx, string playerLine, bool isOpener, CancellationToken ct)
    {
        SpireTalkConfig.ApplyToClient();
        var record = TalkStore.Instance.Get(ctx.NpcKey);

        var worldId = await MainFile.ResolveWorldIdAsync(ctx.NpcKey, ct);
        if (worldId == null)
        {
            throw new YuminaException(string.Format(Loc.Ui("ErrNoWorld"), ctx.NpcKey), 404, "NO_WORLD");
        }

        var sessionId = await EnsureSessionAsync(ctx, record, worldId, ct);

        // Full snapshot on the opener, relationship deltas afterwards.
        var patch = isOpener ? (JsonObject)ctx.Variables.DeepClone() : new JsonObject();
        patch["affection"] = ctx.Affection;
        patch["affection_tier"] = ConversationRules.TierName(ctx.Tier);
        patch["married"] = ctx.Married;
        patch["lines_left"] = ctx.LinesLeft;
        patch["allowed_actions"] = ctx.AllowedActions.Count == 0 ? "none" : string.Join(", ", ctx.AllowedActions);
        patch["language"] = ctx.Language;
        // Clear last turn's proposals so a silent model can't replay them.
        patch["affection_delta"] = 0;
        patch["action"] = new JsonObject { ["type"] = "none", ["amount"] = 0 };
        patch["choices"] = new JsonArray();
        await _client.PatchStateAsync(sessionId, patch, ct);

        var model = SpireTalkConfig.YuminaModel?.Trim();
        if (string.IsNullOrEmpty(model)) model = MainFile.Manifest?.DefaultModel;

        var result = await _client.SendMessageAsync(sessionId, playerLine, model, ct);
        return ToTurn(result);
    }

    private async Task<string> EnsureSessionAsync(VisitContext ctx, TalkStore.NpcRecord record, string worldId, CancellationToken ct)
    {
        if (SpireTalkConfig.RememberAcrossRuns && !string.IsNullOrEmpty(record.SessionId))
        {
            try
            {
                if (await _client.SessionExistsAsync(record.SessionId, ct)) return record.SessionId;
            }
            catch (YuminaException ex) when (ex.StatusCode is 403 or 404)
            {
                // fall through
            }
        }
        var id = await _client.CreateSessionAsync(worldId, "STS2 · " + ctx.Npc.Name, ct);
        record.SessionId = id;
        TalkStore.Instance.Save();
        return id;
    }

    private static NpcTurn ToTurn(TurnResult result)
    {
        var vars = result.Variables;
        var delta = 0;
        var action = NpcAction.None;
        if (vars != null)
        {
            if (vars["affection_delta"] is JsonValue dv)
            {
                if (dv.TryGetValue<int>(out var i)) delta = i;
                else if (dv.TryGetValue<double>(out var d)) delta = (int)Math.Round(d);
                else if (dv.TryGetValue<string>(out var s) && int.TryParse(s, out var p)) delta = p;
            }
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
            else if (vars["action"] is JsonValue asv && asv.TryGetValue<string>(out var at) && !string.IsNullOrWhiteSpace(at))
            {
                action = new NpcAction(at.Trim().ToLowerInvariant(), 0);
            }
        }
        // Models that ignore the directive syntax often still write the fields as loose
        // "key: value" lines. Recover them from the raw output and keep them off screen.
        var choices = result.Choices.ToList();
        var text = LooseFields.Extract(result.RawContent ?? result.Text, result.Text, ref choices, ref delta, ref action);
        var lines = Ancients.AncientConversation.SplitIntoLines(text);
        return new NpcTurn(lines, choices, delta, null, action, result.ModelUsed, result.CreditsBalance);
    }
}
