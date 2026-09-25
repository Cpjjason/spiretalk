using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SpireTalk.Affection;
using SpireTalk.Npc;

namespace SpireTalk.Ui;

/// <summary>
/// The heart in the top right that shows a change in affection the moment it happens: the new
/// value on the heart, "Name +3" beside it, held a couple of seconds, then gone. With Ancient
/// Affection installed its own heart is used instead (it already sits there on an Ancient's
/// page), so the screen never shows two.
/// </summary>
public static class AffectionHud
{
    private const double HoldSeconds = 4.0;
    private static readonly Color Gain = new(1f, 0.88f, 0.42f);
    private static readonly Color Loss = new(1f, 0.36f, 0.32f);

    private static CanvasLayer? _layer;

    public static void Show(string npcKey, NpcKind kind, int value, int delta)
    {
        if (delta == 0) return;
        Callable.From(() => ShowNow(npcKey, kind, value, delta)).CallDeferred();
    }

    private static void ShowNow(string npcKey, NpcKind kind, int value, int delta)
    {
        try
        {
            var name = NpcCatalog.Get(npcKey)?.DisplayName ?? npcKey;
            if (AffectionService.UsingAncientAffection && OnAncientAffectionScreen(kind)
                && AncientAffectionBridge.ShowChange(npcKey, name, value, delta, HoldSeconds))
            {
                return;
            }
            Build(name, kind, value, delta);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Hud] affection change failed: {ex.Message}", 1);
        }
    }

    // Ancient Affection's Ancient heart hides itself anywhere but an Ancient's page. Its Merchant
    // toast is not used: its long label runs off the screen and sits on the shop's Talk button.
    private static bool OnAncientAffectionScreen(NpcKind kind) =>
        kind == NpcKind.Ancient && NEventRoom.Instance?.Layout is NAncientEventLayout;

    private static void Build(string name, NpcKind kind, int value, int delta)
    {
        var game = NGame.Instance;
        if (game == null || !GodotObject.IsInstanceValid(game)) return;
        if (_layer != null && GodotObject.IsInstanceValid(_layer)) _layer.QueueFree();

        _layer = new CanvasLayer { Layer = 140, Name = "SpireTalkAffectionHud" };
        var host = new Control
        {
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -300f,
            // Below the shop's Talk button when it is the Merchant.
            OffsetTop = kind == NpcKind.Merchant ? 196f : 96f,
            OffsetRight = -24f,
            OffsetBottom = kind == NpcKind.Merchant ? 284f : 184f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            PivotOffset = new Vector2(44, 44),
        };

        var heart = GameArt.Icon("res://SpireTalk/images/ui/heart_full.png", 84f);
        heart.Size = new Vector2(84, 84);
        var shadow = GameArt.Icon("res://SpireTalk/images/ui/heart_full.png", 84f);
        shadow.Size = new Vector2(84, 84);
        shadow.Position = new Vector2(5, 7);
        shadow.Modulate = new Color(0, 0, 0, 0.32f);

        var max = kind == NpcKind.Merchant ? ConversationRules.MaxMerchantAffection : ConversationRules.MaxAncientAffection;
        var valueLabel = new Label
        {
            Text = kind == NpcKind.Merchant ? value.ToString() : $"{value}/{max}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Size = new Vector2(84, 80),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Style(valueLabel, 22, new Color(1f, 0.96f, 0.82f), new Color(0.22f, 0.03f, 0.08f));

        var deltaLabel = new Label
        {
            Text = $"{name} {(delta >= 0 ? "+" : "")}{delta}",
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(92, 0),
            Size = new Vector2(200, 84),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Style(deltaLabel, 24, delta >= 0 ? Gain : Loss, new Color(0.12f, 0.035f, 0.06f));

        host.AddChild(shadow);
        host.AddChild(heart);
        host.AddChild(valueLabel);
        host.AddChild(deltaLabel);
        _layer.AddChild(host);
        game.AddChild(_layer);

        var layer = _layer;
        host.Modulate = new Color(1, 1, 1, 0);
        var tween = host.CreateTween();
        tween.TweenProperty(host, "modulate:a", 1f, 0.2);
        tween.TweenProperty(host, "scale", Vector2.One * 1.12f, 0.1);
        tween.TweenProperty(host, "scale", Vector2.One, 0.16);
        tween.TweenInterval(HoldSeconds);
        tween.TweenProperty(host, "modulate:a", 0f, 0.34);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
            if (_layer == layer) _layer = null;
        }));
    }

    private static void Style(Label label, int size, Color color, Color outline)
    {
        var font = GameArt.Font("res://themes/kreon_bold_shared.tres") ?? GameArt.Font("res://themes/kreon_regular_shared.tres");
        if (font != null) label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", outline);
        label.AddThemeConstantOverride("outline_size", 8);
    }
}
