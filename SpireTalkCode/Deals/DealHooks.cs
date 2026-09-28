using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace SpireTalk.Deals;

/// <summary>
/// The game's own hook points, tapped after the fact so a deal settles the moment the run decides
/// it. Every handler swallows its errors: a deal must never be able to break a fight.
/// </summary>
[HarmonyPatch]
internal static class DealHooks
{
    private static void Guard(string where, Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Deal] {where} failed: {ex.GetType().Name}: {ex.Message}", 1);
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Hook), nameof(Hook.AfterActEntered))]
    private static void AfterActEntered(IRunState runState) => Guard("act", () => DealService.OnActEntered(runState));

    [HarmonyPostfix, HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory))]
    private static void AfterCombatVictory(IRunState runState, CombatRoom room) => Guard("victory", () => DealService.OnCombatVictory(runState, room));

    [HarmonyPostfix, HarmonyPatch(typeof(Hook), nameof(Hook.AfterPotionUsed))]
    private static void AfterPotionUsed(IRunState runState) => Guard("potion", () => DealService.OnPotionUsed(runState));

    [HarmonyPostfix, HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardRemoved))]
    private static void BeforeCardRemoved(IRunState runState, CardModel card) => Guard("remove", () => DealService.OnCardRemoved(runState, card));

    // The Thieving Hopper takes the card out of the deck through the same removal path; mark it first
    // so a theft doesn't count against a no-removal deal.
    [HarmonyPrefix, HarmonyPatch(typeof(SwipePower), nameof(SwipePower.Steal))]
    private static void BeforeSteal(CardModel card) => Guard("steal", () => DealService.OnCardStolen(card));

    [HarmonyPostfix, HarmonyPatch(typeof(Hook), nameof(Hook.AfterItemPurchased))]
    private static void AfterItemPurchased(IRunState runState, Player player, int goldSpent) => Guard("purchase", () => DealService.OnItemPurchased(runState, player, goldSpent));
}
