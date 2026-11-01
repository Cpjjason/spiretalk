using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SpireTalk.Affection;
using SpireTalk.Npc;
using SpireTalk.Store;

namespace SpireTalk.Coop;

/// <summary>
/// Co-op: SpireTalk's own event options (Talk, the reply choices, End, deal answers) are this
/// machine's business only. The game would broadcast the clicked option's index and every peer
/// would run that index on their copy of our event, where it is a vanilla relic or nothing at all.
/// Run them locally instead, exactly as the solo game does.
/// </summary>
[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.OptionButtonClicked))]
internal static class LocalOptionClickPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NEventRoom __instance, EventOption option)
    {
        try
        {
            if (option.IsLocked || option.IsProceed) return true;
            // Only SpireTalk's chat options; a road encounter's real choices (TextKey "SPIRETALK-…")
            // change the run and must go through the game's own sync.
            if (!LocalOnly.Is(option)) return true;
            if (!Party.IsCoop(CoopMessages.LocalPlayer())) return true;
            __instance.Layout?.ClearOptions();
            TaskHelper.RunSafely(option.Chosen());
            return false;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[Coop] local option click failed: {ex}", 1);
            return true;
        }
    }
}

/// <summary>A new event room: last room's party talk is over; tell the party where I stand with this Ancient.</summary>
[HarmonyPatch(typeof(EventSynchronizer), nameof(EventSynchronizer.BeginEvent))]
internal static class EventBeginPatch
{
    [HarmonyPostfix]
    private static void Postfix(EventModel canonicalEvent)
    {
        try
        {
            Party.ClearRoom();
            if (canonicalEvent is not AncientEventModel) return;
            var me = CoopMessages.LocalPlayer();
            if (!Party.IsCoop(me)) return;
            var key = canonicalEvent.Id.Entry;
            if (NpcCatalog.Get(key) == null) return;
            var (affection, married) = AffectionService.Read(key);
            CoopMessages.SendStanding(me!, key, affection, ConversationRules.TierOf(affection, married), married, TalkStore.Instance.Get(key).Talks);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Coop] begin-event hook failed: {ex.Message}", 1);
        }
    }
}
