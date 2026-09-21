using SpireTalk.Npc;
using SpireTalk.Yumina;

namespace SpireTalk.Backend;

/// <summary>Offline canned replies so the whole UI can be exercised without any key or account.</summary>
public sealed class MockBackend : INpcBackend
{
    public string DisplayName => "Offline";
    public bool IsConfigured => true;

    public async Task<NpcTurn> ExchangeAsync(VisitContext ctx, string playerLine, bool isOpener, CancellationToken ct)
    {
        var r = await MockYumina.TurnAsync(playerLine, ct);
        var lines = Ancients.AncientConversation.SplitIntoLines(r.Text);
        // Exercise the reward path too: offer whatever is allowed on the second line.
        var action = NpcAction.None;
        if (!isOpener && ctx.AllowedActions.Count > 0 && ctx.LinesLeft <= ConversationRules.LinesPerVisit - 1)
        {
            action = new NpcAction(ctx.AllowedActions[0], 0);
        }
        var delta = isOpener ? 0 : (playerLine.Length % 3) - 1;
        return new NpcTurn(lines, r.Choices, delta, "mock", action, "mock-model", r.CreditsBalance);
    }
}
