using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace SpireTalk.Ui;

/// <summary>
/// Co-op: a teammate's conversation, live, down the left edge of the screen. "Bro → Neow: …" and
/// Neow's answer under it, each line fading after a while, at most a handful on screen.
/// </summary>
public static class PartyFeed
{
    private const int MaxEntries = 6;
    private const double HoldSeconds = 16;

    private static CanvasLayer? _layer;
    private static VBoxContainer? _list;

    private static readonly Color NameColor = new(1f, 0.82f, 0.4f);
    private static readonly Color NpcColor = new(0.62f, 0.86f, 1f);
    private static readonly Color TextColor = new(0.95f, 0.93f, 0.88f);

    public static void Show(string playerName, string npcName, bool fromNpc, string text)
    {
        Callable.From(() => ShowNow(playerName, npcName, fromNpc, text)).CallDeferred();
    }

    private static void ShowNow(string playerName, string npcName, bool fromNpc, string text)
    {
        try
        {
            var list = Ensure();
            if (list == null) return;

            var label = new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(500, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.04f, 0.07f, 0.62f),
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8,
            });
            card.AddChild(label);
            label.AddThemeFontSizeOverride("normal_font_size", 20);
            label.AddThemeConstantOverride("outline_size", 6);
            label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
            label.AddThemeColorOverride("default_color", TextColor);
            var who = fromNpc
                ? $"[color=#{NpcColor.ToHtml(false)}]{Esc(npcName)}[/color] → [color=#{NameColor.ToHtml(false)}]{Esc(playerName)}[/color]"
                : $"[color=#{NameColor.ToHtml(false)}]{Esc(playerName)}[/color] → [color=#{NpcColor.ToHtml(false)}]{Esc(npcName)}[/color]";
            label.Text = $"{who}: {Esc(Trim(text))}";
            list.AddChild(card);
            while (list.GetChildCount() > MaxEntries) list.GetChild(0).QueueFree();

            card.Modulate = new Color(1, 1, 1, 0);
            var tween = card.CreateTween();
            tween.TweenProperty(card, "modulate:a", 1.0, 0.25);
            tween.TweenInterval(HoldSeconds);
            tween.TweenProperty(card, "modulate:a", 0.0, 1.2);
            tween.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(card)) card.QueueFree(); }));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Coop] feed failed: {ex.Message}", 1);
        }
    }

    private static VBoxContainer? Ensure()
    {
        if (_list != null && GodotObject.IsInstanceValid(_list)) return _list;
        if (NGame.Instance == null) return null;
        _layer = new CanvasLayer { Name = "SpireTalkPartyFeed", Layer = 90 };
        var root = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        // Below the party list on the left (it grows with up to four players), above the deck.
        var h = NGame.Instance.GetViewport()?.GetVisibleRect().Size.Y ?? 1080f;
        root.Position = new Vector2(24, h * 0.36f);
        _list = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _list.AddThemeConstantOverride("separation", 6);
        root.AddChild(_list);
        _layer.AddChild(root);
        NGame.Instance.AddChild(_layer);
        return _list;
    }

    private static string Trim(string s)
    {
        s = s.Replace("\n", " ").Trim();
        return s.Length <= 160 ? s : s[..160] + "…";
    }

    private static string Esc(string s) => s.Replace("[", "[lb]");
}
