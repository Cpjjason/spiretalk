using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SpireTalk.Safety;

/// <summary>
/// An event page must never end up with no buttons. The game runs an event option's work and only
/// logs what it throws, so when a relic from another mod (or one a game update broke) fails while
/// it's being obtained, the Ancient never says "done" and the player is left on a page with every
/// button already spent. Same if building the page itself throws. These patches catch exactly
/// those cases and let the run go on: they do nothing at all when nothing went wrong.
/// </summary>
internal static class EventSafetyNet
{
#if SPIRETALK_DEV
    // test builds only: SPIRETALK_NO_SAFETY=1 turns the net off to reproduce what it guards against
    internal static readonly bool Off = System.Environment.GetEnvironmentVariable("SPIRETALK_NO_SAFETY") == "1";
#else
    internal const bool Off = false;
#endif

    // EventOption doesn't know its event; remember it whenever an event shows a page.
    private static readonly ConditionalWeakTable<EventOption, EventModel> Owners = new();

    private static readonly MethodInfo? AncientDone = AccessTools.Method(typeof(AncientEventModel), "Done");
    private static readonly MethodInfo? SetFinished = AccessTools.Method(typeof(EventModel), "SetEventFinished", new[] { typeof(LocString) });

    internal static void Remember(EventModel ev)
    {
        try
        {
            foreach (var o in ev.CurrentOptions) Owners.AddOrUpdate(o, ev);
        }
        catch
        {
            // bookkeeping only
        }
    }

    /// <summary>An option's work threw. If its page is still up, finish the event so Proceed appears.</summary>
    internal static bool Rescue(EventOption option, Exception ex)
    {
        if (Off) return false;
        try
        {
            if (!Owners.TryGetValue(option, out var ev)) return false;
            // the event moved on (new page, or finished) before failing: the player has buttons
            if (ev.IsFinished || !ev.CurrentOptions.Contains(option)) return false;

            MainFile.Logger.Error($"[Safety] {ev.Id.Entry}: option '{option.TextKey}' failed; ending the event so the run can go on. {ex}", 1);
            try
            {
                if (ev is AncientEventModel && AncientDone != null) AncientDone.Invoke(ev, null);
                else SetFinished?.Invoke(ev, new object?[] { ev.Description ?? ev.InitialDescription });
            }
            catch (Exception doneFailed)
            {
                // Done() also writes run history; if that is what breaks, finishing plainly still works
                if (ev.IsFinished) throw;
                MainFile.Logger.Warn($"[Safety] {ev.Id.Entry}: Done() failed ({doneFailed.GetBaseException().Message}); finishing plainly", 1);
                SetFinished?.Invoke(ev, new object?[] { ev.Description ?? ev.InitialDescription });
            }
            Rewards.RewardExecutor.Toast(Loc.Ui("EventRescued"));
            return ev.IsFinished;
        }
        catch (Exception rescueFailed)
        {
            MainFile.Logger.Error($"[Safety] could not rescue the event: {rescueFailed}", 1);
            return false;
        }
    }

    /// <summary>The page about to be built threw: the options the game made, else a plain Proceed.</summary>
    internal static IReadOnlyList<EventOption> FallbackPage(AncientEventModel ancient, List<EventOption>? vanilla)
    {
        if (vanilla is { Count: > 0 }) return vanilla;
        return new List<EventOption> { new(ancient, NEventRoom.Proceed, "PROCEED", false, true) };
    }
}

[HarmonyPatch(typeof(EventModel), "SetEventState")]
internal static class SafetyRememberOptions
{
    [HarmonyPostfix]
    private static void Postfix(EventModel __instance) => EventSafetyNet.Remember(__instance);
}

[HarmonyPatch(typeof(EventOption), nameof(EventOption.Chosen))]
internal static class SafetyGuardOption
{
    [HarmonyPostfix]
    private static void Postfix(EventOption __instance, ref Task __result) => __result = Guard(__result, __instance);

    private static async Task Guard(Task inner, EventOption option)
    {
        try
        {
            await inner;
        }
        catch (Exception ex)
        {
            // recovered: logged there. Otherwise let the game log it as before.
            if (!EventSafetyNet.Rescue(option, ex)) throw;
        }
    }
}

/// <summary>
/// Building an Ancient's first page goes through several mods' patches (ours, Ancient Affection,
/// RitsuLib). If any of them throws, show the options the game itself made, or at least Proceed.
/// </summary>
[HarmonyPatch(typeof(AncientEventModel), "GenerateInitialOptionsWrapper")]
internal static class SafetyAncientPage
{
    [ThreadStatic] private static List<EventOption>? _vanilla;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix() => _vanilla = null;

    // first postfix: what the game made, before anyone else rewrites it
    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore("AncientAffection.harmony", "com.ritsukage.sts2-RitsuLib.framework-core")]
    private static void Postfix(IReadOnlyList<EventOption> __result)
    {
        try { _vanilla = __result.ToList(); }
        catch { _vanilla = null; }
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, AncientEventModel __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (__exception == null || EventSafetyNet.Off) return __exception;
        try
        {
            MainFile.Logger.Error($"[Safety] {__instance.Id.Entry}: building the page failed; showing {(_vanilla is { Count: > 0 } ? "the game's own options" : "Proceed")}. {__exception}", 1);
            __result = EventSafetyNet.FallbackPage(__instance, _vanilla);
            return null;
        }
        catch
        {
            return __exception;
        }
        finally
        {
            _vanilla = null;
        }
    }
}

/// <summary>
/// Ancient Affection adds its own option while the page's buttons are being made. If adding the
/// buttons throws before any exist, make them plainly from the event's current options.
/// </summary>
[HarmonyPatch(typeof(NEventLayout), "AddOptions")]
internal static class SafetyAddButtons
{
    private static readonly FieldInfo? EventField = AccessTools.Field(typeof(NEventLayout), "_event");
    private static readonly FieldInfo? ContainerField = AccessTools.Field(typeof(NEventLayout), "_optionsContainer");
    private static readonly MethodInfo? AnimateIn = AccessTools.Method(typeof(NEventLayout), "AnimateButtonsIn");

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, NEventLayout __instance)
    {
        if (__exception == null || EventSafetyNet.Off) return __exception;
        try
        {
            if (EventField?.GetValue(__instance) is not EventModel ev || ContainerField?.GetValue(__instance) is not Container box) return __exception;
            if (box.GetChildCount() > 0) return __exception; // some buttons made it: leave them
            MainFile.Logger.Error($"[Safety] {ev.Id.Entry}: adding the option buttons failed; adding them plainly. {__exception}", 1);
            foreach (var option in ev.CurrentOptions)
            {
                var button = NEventOptionButton.Create(ev, option, box.GetChildCount());
                box.AddChildSafely(button);
            }
            if (box.GetChildCount() > 0) AnimateIn?.Invoke(__instance, null);
            return null;
        }
        catch (Exception again)
        {
            MainFile.Logger.Error($"[Safety] could not add the buttons plainly: {again.GetBaseException().Message}", 1);
            return __exception;
        }
    }
}
