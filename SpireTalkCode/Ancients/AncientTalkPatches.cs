using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SpireTalk.Ancients;

/// <summary>
/// Appends the "Talk" option to every Ancient's initial option page (single player only — a
/// shared multiplayer event can't wait on one player's private conversation).
/// </summary>
[HarmonyPatch(typeof(AncientEventModel), "GenerateInitialOptionsWrapper")]
internal static class AncientInitialOptionsPatch
{
    [HarmonyPostfix]
    private static void AppendTalkOption(AncientEventModel __instance, ref IReadOnlyList<EventOption> __result)
    {
        try
        {
            if (!SpireTalkConfig.EnableAncients) return;
            var owner = __instance.Owner;
            if (owner == null || owner.RunState.Players.Count > 1) return;
            if (__result.Count == 0) return;
            // Only the PROCEED page (ancient blocked by Wax Choker etc.) has a single proceed option.
            if (__result.Count == 1 && __result[0].IsProceed) return;

            // New list on purpose: the game keeps a reference to its own list for run-history
            // bookkeeping, and our option must not appear there.
            var withTalk = new List<EventOption>(__result.Count + 1);
            withTalk.AddRange(__result);
            withTalk.Add(AncientConversation.CreateTalkOption(__instance, __result));
            __result = withTalk;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[Talk] could not append talk option: {ex}", 1);
        }
    }
}

/// <summary>Drop per-event conversation state when the room goes away.</summary>
[HarmonyPatch(typeof(NEventRoom), "_ExitTree")]
internal static class EventRoomExitPatch
{
    [HarmonyPrefix]
    private static void ForgetConversation(NEventRoom __instance)
    {
        try
        {
            var field = AccessTools.Field(typeof(NEventRoom), "_event");
            if (field?.GetValue(__instance) is AncientEventModel ancient)
            {
                AncientConversation.Forget(ancient);
            }
        }
        catch
        {
            // best effort
        }
    }
}
