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
        // The top-right column of 64px icons (patch notes at y=16, other mods' icons below it on
        // the same x=1832 of the 1920-wide canvas): take the first free slot, decided a frame
        // later so icons other mods add in their own _Ready patches are already there.
        b.PlaceAt(88f);
        b.CallDeferred(nameof(SettleIntoColumn));
        b.MouseEntered += b.OnHover;
        b.MouseExited += b.OnUnhover;
        b.Pressed += SpireTalkScreen.Open;
        b.AddDot();
        return b;
    }

    private static MainMenuButton? _current;

    /// <summary>A red dot for players who had the mod before the conversation-mode switch: something new in the settings.</summary>
    private void AddDot()
    {
        _current = this;
        var style = new StyleBoxFlat { BgColor = new Color("d8433a"), BorderColor = new Color("2a0f0c"), };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(9);
        var dot = new Panel
        {
            Name = "Dot",
            MouseFilter = MouseFilterEnum.Ignore,
            Position = new Vector2(ButtonSize - 20, 4),
            Size = new Vector2(18, 18),
        };
        dot.AddThemeStyleboxOverride("panel", style);
        AddChild(dot);
        RefreshDot();
    }

    public static void RefreshDot()
    {
        if (_current == null || !IsInstanceValid(_current)) return;
        var dot = _current.GetNodeOrNull<Control>("Dot");
        if (dot != null)
        {
            dot.Visible = (SpireTalkConfig.GuideSeen && !SpireTalkConfig.ModeNoticeSeen)
                || (Backend.ModService.Newer && Backend.ModService.State != Backend.ModService.UpdateState.Staged);
        }
    }

    private void PlaceAt(float top)
    {
        OffsetLeft = -(1920f - 1832f) - (ButtonSize - 64f) / 2f;
        OffsetTop = top - (ButtonSize - 64f) / 2f;
        OffsetRight = OffsetLeft + ButtonSize;
        OffsetBottom = OffsetTop + ButtonSize;
    }

    private void SettleIntoColumn()
    {
        try
        {
            var parent = GetParent<Control>();
            var lowest = 16f + 64f; // below the patch-notes scroll
            foreach (var child in parent.GetChildren())
            {
                if (child is not Control c || ReferenceEquals(c, this) || !c.Visible) continue;
                var x = c.GlobalPosition.X;
                if (x < 1800f || x > 1870f || c.GlobalPosition.Y > 600f) continue;
                lowest = Math.Max(lowest, c.GlobalPosition.Y + c.Size.Y);
            }
            PlaceAt(lowest + 8f);
        }
        catch
        {
            // keep the default slot
        }
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
            GuideScreen.OpenOnceWhenIdle();
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
