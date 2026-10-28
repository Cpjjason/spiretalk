using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace SpireTalk.Ancients;

/// <summary>
/// Saves the run in the middle of an Ancient event once the conversation has changed it (a relic,
/// gold, a potion, a heal, the keepsake, a deal token). Without it Save &amp; Quit rolls the run back
/// to the save made on entering the room while the store keeps the reward as given.
///
/// The room stays unfinished on purpose: the blessing is still to be chosen. A reload into an
/// unfinished room re-enters the map point and appends this floor's history row again, so the save
/// is shaped like the game's own room-entry save, which is written before that row exists.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class AncientRunSave
{
    private static Task _chain = Task.CompletedTask;
    private static IRunState? _shaping;

    public static void Request(Player? owner)
    {
        if (owner == null) return;
        _chain = SaveAfter(_chain, owner.RunState);
    }

    private static bool InAncientRoom(IRunState run) =>
        run.Players.Count == 1
        && run.CurrentMapPoint?.PointType == MapPointType.Ancient
        && run.CurrentRoom is EventRoom { CanonicalEvent: AncientEventModel };

    private static async Task SaveAfter(Task previous, IRunState run)
    {
        try { await previous; } catch { /* the previous save logged its own failure */ }
        try
        {
            if (!RunManager.Instance.ShouldSave || !InAncientRoom(run)) return;
            _shaping = run;
            await SaveManager.Instance.SaveRun(null, saveProgress: false);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Talk] mid-event save failed: {ex.GetType().Name}: {ex.Message}", 1);
        }
        finally
        {
            _shaping = null;
        }
    }

    [HarmonyPostfix]
    private static void Shape(AbstractRoom? preFinishedRoom, SerializableRun __result)
    {
        var run = _shaping;
        if (run == null || preFinishedRoom != null) return;
        _shaping = null;
        try
        {
            if (!InAncientRoom(run)) return;
            var room = (EventRoom)run.CurrentRoom!;
            if (room.IsPreFinished)
            {
                // The blessing was picked while this save waited: write what the game would have.
                __result.PreFinishedRoom = room.ToSerializable();
                return;
            }
            var acts = __result.MapPointHistory;
            var i = __result.CurrentActIndex;
            if (i >= acts.Count || acts[i].Count == 0 || acts[i][^1].MapPointType != MapPointType.Ancient) return;
            acts[i].RemoveAt(acts[i].Count - 1);
            if (acts[i].Count == 0 && i == acts.Count - 1) acts.RemoveAt(i);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Talk] shaping the mid-event save failed: {ex.Message}", 1);
        }
    }
}
