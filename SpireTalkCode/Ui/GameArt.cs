using Godot;
using MegaCrit.Sts2.addons.mega_text;
using BaseLib.Config;

namespace SpireTalk.Ui;

/// <summary>
/// The handful of vanilla textures and fonts the mod's own screens are built from, so they read
/// as part of the game rather than a web page laid over it. Everything is loaded lazily from the
/// game's pck through the normal resource loader; a missing asset degrades to a flat colour.
/// </summary>
public static class GameArt
{
    // Parchment sheet used by the main menu's sub-panels (torn edges, warm tan).
    public const string ParchmentPath = "res://images/packed/common_ui/submenu_panel_short.png";
    // Dark slate slab used behind the selected settings tab.
    public const string SlatePath = "res://images/packed/common_ui/settings_tab_selected.png";
    // Navy tooltip box with a teal border.
    public const string NavyPath = "res://images/ui/hover_tip.png";
    // Teal hexagonal event button.
    public const string EventButtonPath = "res://images/packed/common_ui/event_button.png";
    // White rounded speech bubble (tinted at use) and its tail.
    public const string BubblePath = "res://images/ui/dialogue_nine_patch.png";
    public const string BubbleTailPath = "res://images/ui/dialogue_tail.png";

    public const string MushieSitPath = "res://SpireTalk/images/mushie_sit.png";
    public const string MushieStandPath = "res://SpireTalk/images/mushie_stand.png";
    public const string MushieCoinPath = "res://SpireTalk/images/mushie_coin.png";

    // Ink on parchment / text on slate.
    public static readonly Color Ink = new("3a2a17");
    public static readonly Color InkSoft = new(0.23f, 0.16f, 0.09f, 0.72f);
    public static readonly Color Gold = new("f0d68c");
    public static readonly Color GoldDeep = new("c9a24e");
    public static readonly Color Paper = new("f4ead4");
    public static readonly Color Cream = new(0.96f, 0.94f, 0.9f);
    public static readonly Color Muted = new(1f, 1f, 1f, 0.6f);

    private static readonly Dictionary<string, Texture2D?> Cache = new();

    public static Texture2D? Tex(string path)
    {
        if (Cache.TryGetValue(path, out var cached)) return cached;
        Texture2D? tex = null;
        try
        {
            if (ResourceLoader.Exists(path)) tex = ResourceLoader.Load<Texture2D>(path);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Art] {path}: {ex.Message}", 1);
        }
        Cache[path] = tex;
        return tex;
    }

    /// <summary>Nine-slice stylebox from a vanilla texture; flat fallback keeps layouts alive without it.</summary>
    public static StyleBox Slice(string path, int margin, int padX, int padY, Color? fallback = null, Color? modulate = null)
        => Slice(path, margin, padX, padY, padY, fallback, modulate);

    public static StyleBox Slice(string path, int margin, int padX, int padTop, int padBottom, Color? fallback, Color? modulate)
    {
        var tex = Tex(path);
        if (tex == null)
        {
            var flat = new StyleBoxFlat { BgColor = fallback ?? new Color(0.1f, 0.1f, 0.12f, 0.95f) };
            flat.SetCornerRadiusAll(8);
            flat.ContentMarginLeft = padX; flat.ContentMarginRight = padX;
            flat.ContentMarginTop = padTop; flat.ContentMarginBottom = padBottom;
            return flat;
        }
        var box = new StyleBoxTexture
        {
            Texture = tex,
            TextureMarginLeft = margin,
            TextureMarginRight = margin,
            TextureMarginTop = margin,
            TextureMarginBottom = margin,
            ContentMarginLeft = padX,
            ContentMarginRight = padX,
            ContentMarginTop = padTop,
            ContentMarginBottom = padBottom,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Stretch,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Stretch,
        };
        if (modulate.HasValue) box.ModulateColor = modulate.Value;
        return box;
    }

    // The parchment's torn bottom edge eats ~30px more than its top, so the bottom pad is deeper.
    public static StyleBox Parchment(int padX = 64, int padTop = 40, int padBottom = 64) => Slice(ParchmentPath, 70, padX, padTop, padBottom, new Color("d8c095"), null);
    public static StyleBox Slate(int padX = 28, int padY = 22) => Slice(SlatePath, 26, padX, padY, new Color(0.12f, 0.17f, 0.2f));
    public static StyleBox Navy(int padX = 18, int padY = 10) => Slice(NavyPath, 22, padX, padY, new Color(0.08f, 0.13f, 0.16f));
    public static StyleBox EventButton(Color? modulate = null) => Slice(EventButtonPath, 48, 30, 8, new Color(0.13f, 0.36f, 0.4f), modulate);
    public static StyleBox Bubble(Color tint) => Slice(BubblePath, 26, 26, 18, tint, tint);

    /// <summary>A game-font rich label (Kreon + CJK fallback) in a given colour and size.</summary>
    public static MegaRichTextLabel Label(string text, int size, Color color, bool wrap = true)
    {
        var label = ModConfig.CreateRawLabelControl(text, size);
        label.FitContent = true;
        label.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.CustomMinimumSize = new Vector2(0, size + 14);
        label.AddThemeColorOverride("default_color", color);
        return label;
    }

    public static TextureRect Icon(string path, float size)
    {
        return new TextureRect
        {
            Texture = Tex(path),
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    /// <summary>Teal hex button with white text, the shape the game uses for event options.</summary>
    public static Button HexButton(string text, int fontSize = 26, float minHeight = 68)
    {
        var b = new Button
        {
            Text = Loc.Sanitize(text),
            CustomMinimumSize = new Vector2(0, minHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            FocusMode = Control.FocusModeEnum.All,
        };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeStyleboxOverride("normal", EventButton());
        b.AddThemeStyleboxOverride("hover", EventButton(new Color(1.25f, 1.25f, 1.2f)));
        b.AddThemeStyleboxOverride("pressed", EventButton(new Color(0.85f, 0.85f, 0.85f)));
        b.AddThemeStyleboxOverride("focus", EventButton(new Color(1.25f, 1.25f, 1.2f)));
        b.AddThemeColorOverride("font_color", Cream);
        b.AddThemeColorOverride("font_hover_color", Gold);
        b.AddThemeColorOverride("font_focus_color", Gold);
        b.AddThemeColorOverride("font_pressed_color", Gold);
        return b;
    }

    /// <summary>Underlined ink text that opens something; the quiet kind of link that belongs in a corner.</summary>
    public static Button LinkButton(string text, Color color, int fontSize = 22)
    {
        var b = new Button
        {
            Text = text,
            Flat = true,
            FocusMode = Control.FocusModeEnum.All,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
        };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeColorOverride("font_color", color);
        b.AddThemeColorOverride("font_hover_color", Gold);
        b.AddThemeColorOverride("font_focus_color", Gold);
        b.AddThemeColorOverride("font_pressed_color", Gold);
        var empty = new StyleBoxEmpty();
        b.AddThemeStyleboxOverride("normal", empty);
        b.AddThemeStyleboxOverride("hover", empty);
        b.AddThemeStyleboxOverride("pressed", empty);
        b.AddThemeStyleboxOverride("focus", empty);
        return b;
    }

    public static Control Spacer(float height) => new Control { CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };

    public static ColorRect Rule(Color color, float height = 2)
    {
        return new ColorRect { Color = color, CustomMinimumSize = new Vector2(0, height), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
    }
}
