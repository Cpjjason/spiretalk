using MegaCrit.Sts2.Core.Entities.Players;
using SpireTalk.Deals;
using SpireTalk.Keepsakes;

namespace SpireTalk.Npc;

/// <summary>
/// The parts of a visit that are not the three lines: the deals. Both conversations
/// (Ancients, Merchant) call these at the same points so the two stay in step.
/// </summary>
public static class VisitExtras
{
    /// <summary>
    /// Before the opener: hand over or level the keepsake, put deal status into the prompt and decide
    /// whether the NPC proposes a deal this visit.
    /// </summary>
    public static async Task<DealService.Offer?> PrepareAsync(VisitContext ctx, NpcDefinition npc, Player player, string runKey)
    {
        ctx.Variables["keepsake"] = await KeepsakeService.OnVisitAsync(npc, player, ctx.Tier);
        ctx.Variables["deals"] = DealService.PromptStatus(npc.Key, runKey);

        var offer = DealService.PickOffer(npc, player, ctx.Tier, runKey);
        ctx.Variables["deal_offer"] = offer != null
            ? $"Propose this deal to the climber in your first reply, in your own words and voice: they should {offer.PromptText()}. If they keep it, you will be pleased and give them something. Do not invent other terms."
            : "none";
        return offer;
    }

    /// <summary>The opener aired any kept/broken result; it is old news from here on.</summary>
    public static void AfterOpener(VisitContext ctx, NpcDefinition npc, string runKey)
    {
        DealService.MarkResultTold(npc.Key);
        ctx.Variables["deals"] = DealService.PromptStatus(npc.Key, runKey);
    }

    /// <summary>The climber answered the proposal; the NPC hears the answer on the next line.</summary>
    public static void OnOfferAnswered(VisitContext ctx, NpcDefinition npc, DealService.Offer offer, bool accepted, string runKey)
    {
        if (accepted) DealService.Accept(offer);
        ctx.Variables["deal_offer"] = accepted
            ? "The climber ACCEPTED your proposal. Acknowledge it briefly when it fits; do not propose another."
            : "The climber DECLINED your proposal. Take it in stride; do not propose another this visit.";
        ctx.Variables["deals"] = DealService.PromptStatus(npc.Key, runKey);
    }

    /// <summary>After affection moved mid-visit: the keepsake may have grown.</summary>
    public static async Task AfterAffectionChangedAsync(VisitContext ctx, NpcDefinition npc, Player player)
    {
        var raised = await KeepsakeService.RefreshAsync(npc, player, quiet: false);
        if (raised)
        {
            var owned = KeepsakeService.Owned(player, npc.Key);
            ctx.Variables["keepsake"] = $"Your keepsake \"{KeepsakeService.KeepsakeName(npc.Key)}\" just {KeepsakeService.Grew(owned?.Level ?? 1)} because of what they said. Mention it in this reply.";
        }
    }
}
