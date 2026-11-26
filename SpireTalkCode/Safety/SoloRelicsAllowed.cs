using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Keepsakes;
using SpireTalk.Tokens;

namespace SpireTalk.Safety;

/// <summary>
/// Keepsakes, deal relics and the Merchant's tokens only exist in single player. Up to 0.113 they say so
/// by overriding RelicModel.IsAllowed; 0.114 made IsAllowed non-virtual (relics override
/// IsAllowedInternal instead), so there the overrides are never called. Same rule, applied from outside.
/// On older branches the overrides run instead of this method, so this changes nothing there.
/// </summary>
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.IsAllowed))]
internal static class SoloRelicsAllowed
{
    [HarmonyPostfix]
    private static void Postfix(RelicModel __instance, IRunState runState, ref bool __result)
    {
        if (__result && __instance is KeepsakeRelic or DealRelic or MerchantToken && runState.Players.Count != 1) __result = false;
    }
}
