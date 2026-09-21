using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace SpireTalk.Ui;

/// <summary>
/// The little mushie in the top-right corner of the main menu, next to the patch-notes scroll.
/// Hovering makes it perk up; clicking opens <see cref="SpireTalkScreen"/>. It follows the
/// patch-notes button's visibility so it disappears with the other corner buttons in submenus.
/// </summary>
public sealed partial class MainMenuButton : TextureButton
{
    private const float ButtonSize = 76f;
    private Tween? _tween;

    public static MainMenuButton Create()
    {
        var b = new MainMenuButton
        {
            Name = "SpireTalkMenuButton",
            TextureNormal = GameArt.Tex(GameArt.MushieSitPath),
            IgnoreTextureSize = true,
            StretchMode = StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
            FocusMode = FocusModeEnum.None,
            PivotOffset = new Vector2(ButtonSize / 2f, ButtonSize),
            TooltipText = Loc.Ui("ScreenTitle"),
        };
        b.SetAnchorsPreset(LayoutPreset.TopRight);
        // The top-right column of 64px icons: patch notes at y=16, RitsuLib's gear at y=88 (when
        // installed); we take the next slot, aligned to the same x=1832 on the 1920-wide canvas.
        b.OffsetLeft = -(1920f - 1832f) - (ButtonSize - 64f) / 2f;
        b.OffsetTop = 160f - (ButtonSize - 64f) / 2f;
        b.OffsetRight = b.OffsetLeft + ButtonSize;
        b.OffsetBottom = b.OffsetTop + ButtonSize;
        b.MouseEntered += b.OnHover;
        b.MouseExited += b.OnUnhover;
        b.Pressed += SpireTalkScreen.Open;
        return b;
    }

    private void OnHover()
    {
        _tween?.Kill();
        _tween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(this, "scale", new Vector2(1.14f, 1.14f), 0.18);
        _tween.Parallel().TweenProperty(this, "rotation_degrees", -6f, 0.18);
    }

    private void OnUnhover()
    {
        _tween?.Kill();
        _tween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(this, "scale", Vector2.One, 0.18);
        _tween.Parallel().TweenProperty(this, "rotation_degrees", 0f, 0.18);
    }
}

[HarmonyPatch(typeof(NMainMenu), "_Ready")]
internal static class MainMenuReadyPatch
{
    [HarmonyPostfix]
    private static void AddMushie(NMainMenu __instance)
    {
        try
        {
            if (__instance.GetNodeOrNull<Control>("SpireTalkMenuButton") != null) return;
            __instance.AddChild(MainMenuButton.Create());
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Menu] could not add the mushie: {ex.Message}", 1);
        }
    }
}

[HarmonyPatch(typeof(NMainMenu), "OnSubmenuStackChanged")]
internal static class MainMenuSubmenuPatch
{
    [HarmonyPostfix]
    private static void FollowCornerButtons(NMainMenu __instance)
    {
        try
        {
            var ours = __instance.GetNodeOrNull<Control>("SpireTalkMenuButton");
            var patchNotes = __instance.GetNodeOrNull<Control>("%PatchNotesButton");
            if (ours != null && patchNotes != null) ours.Visible = patchNotes.Visible;
        }
        catch
        {
            // cosmetic only
        }
    }
}
