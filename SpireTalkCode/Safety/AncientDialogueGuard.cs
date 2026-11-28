using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SpireTalk.Safety;

/// <summary>
/// An Ancient's page has a click-anywhere hitbox over the whole screen while its lines are being read;
/// the game only turns it off when the last line is reached. But whenever the event's state changes the
/// game clears the lines and puts new options up without touching the hitbox. In co-op that happens
/// mid-conversation (another player's keepsake or choice being applied here), so the lines vanish, the
/// hitbox stays over everything, every click on it throws, and nothing on screen can be clicked again.
/// These patches turn the hitbox off whenever there are no lines left to read.
/// </summary>
internal static class AncientDialogueGuard
{
    private static readonly FieldInfo? Lines = AccessTools.Field(typeof(NAncientEventLayout), "_dialogue");
    private static readonly FieldInfo? Container = AccessTools.Field(typeof(NAncientEventLayout), "_dialogueContainer");
    private static readonly FieldInfo? Hitbox = AccessTools.Field(typeof(NAncientEventLayout), "_dialogueHitbox");
    private static readonly FieldInfo? NextButton = AccessTools.Field(typeof(NAncientEventLayout), "_fakeNextButtonContainer");

    /// <summary>True when the layout has nothing left to show for its current dialogue.</summary>
    internal static bool NoLines(NAncientEventLayout layout, int lineIndex)
    {
        var count = (Lines?.GetValue(layout) as System.Collections.ICollection)?.Count ?? 1;
        var shown = (Container?.GetValue(layout) as Node)?.GetChildCount() ?? 1;
        return count == 0 || shown == 0 || lineIndex >= shown;
    }

    /// <summary>Hide the click-anywhere hitbox and let the options be clicked.</summary>
    internal static void Release(NAncientEventLayout layout)
    {
        if (Hitbox?.GetValue(layout) is NClickableControl hitbox && GodotObject.IsInstanceValid(hitbox))
        {
            hitbox.Visible = false;
            hitbox.Disable();
        }
        if (NextButton?.GetValue(layout) is Control next && GodotObject.IsInstanceValid(next)) next.Visible = false;
        foreach (var button in layout.OptionButtons)
        {
            button.EnableButton();
            button.FocusMode = Control.FocusModeEnum.All;
        }
    }
}

/// <summary>The event's state changed: if that cleared the lines, the hitbox must go too.</summary>
[HarmonyPatch(typeof(NEventRoom), "RefreshEventState")]
internal static class DialogueGuardOnRefresh
{
    [HarmonyPostfix]
    private static void Postfix(NEventRoom __instance)
    {
        if (EventSafetyNet.Off) return;
        try
        {
            if (__instance.Layout is NAncientEventLayout layout && AncientDialogueGuard.NoLines(layout, 0))
                AncientDialogueGuard.Release(layout);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Safety] dialogue guard (refresh): {ex.Message}", 1);
        }
    }
}

/// <summary>A click asked for a line that is no longer there: free the screen instead of throwing.</summary>
[HarmonyPatch(typeof(NAncientEventLayout), "SetDialogueLineAndAnimate")]
internal static class DialogueGuardOnAdvance
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(NAncientEventLayout __instance, int lineIndex)
    {
        if (EventSafetyNet.Off || lineIndex == 0) return true;
        try
        {
            if (!AncientDialogueGuard.NoLines(__instance, lineIndex)) return true;
            MainFile.Logger.Warn($"[Safety] dialogue line {lineIndex} is gone; releasing the screen", 1);
            AncientDialogueGuard.Release(__instance);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
