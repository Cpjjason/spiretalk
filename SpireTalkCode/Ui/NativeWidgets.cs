using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;

namespace SpireTalk.Ui;

/// <summary>
/// The Ancient event option button, built from the game's own scene and animated the way
/// NEventOptionButton animates it (1% grow and a blue outline on hover, 1% shrink on press, the
/// same click/hover sounds). Used where the mod needs a choice that is not an EventOption.
/// </summary>
public sealed partial class NativeOption : Control
{
    public const string HoverSfx = "event:/sfx/ui/clicks/ui_hover";
    public const string ClickSfx = "event:/sfx/ui/clicks/ui_click";

    private Control? _art;
    private NinePatchRect? _outline;
    private ShaderMaterial? _hsv;
    private Tween? _tween;
    private Action? _onPressed;
    private bool _pressedInside;

    /// <summary>Resting scale; hover and press animate relative to it.</summary>
    public Vector2 BaseScale { get; set; } = Vector2.One;

    public static NativeOption Create(string title, string? description, Color buttonColor, Action onPressed)
    {
        var option = new NativeOption { _onPressed = onPressed };
        option.Build(NativeScene.AncientOptionButton, new Vector2(1000, 100), title, description, buttonColor);
        return option;
    }

    private void Build(string scene, Vector2 size, string title, string? description, Color? buttonColor)
    {
        Name = "SpireTalkOption";
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        CustomMinimumSize = size;

        _art = NativeScene.Build(scene);
        if (_art != null)
        {
            _art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _art.CustomMinimumSize = Vector2.Zero;
            IgnoreMouse(_art);
            if (_art.GetNodeOrNull<NinePatchRect>("Image") is { } image)
            {
                if (buttonColor.HasValue) image.Modulate = buttonColor.Value;
                // Plain event buttons carry an HSV shader; hover brightens it (v 0.9 → 1.2).
                if (image.Material is ShaderMaterial hsv)
                {
                    _hsv = (ShaderMaterial)hsv.Duplicate();
                    image.Material = _hsv;
                    _hsv.SetShaderParameter("v", 0.9f);
                }
            }
            _outline = _art.GetNodeOrNull<NinePatchRect>("Outline");
            if (_art.GetNodeOrNull("PlayerVoteContainer") is Node votes) votes.QueueFree();
            if (FindLabel(_art) is { } label) label.Text = OptionText(title, description);
            AddChild(_art);
        }

        MouseEntered += () => { if (!HasFocus()) GrabFocus(); };
        MouseExited += () => { if (HasFocus()) ReleaseFocus(); };
        FocusEntered += OnFocus;
        FocusExited += OnUnfocus;
        Resized += () => PivotOffset = Size / 2;
    }

    /// <summary>The label text the vanilla button writes: gold bold title, plain description under it.</summary>
    public static string OptionText(string title, string? description)
    {
        if (string.IsNullOrEmpty(title)) return description ?? "";
        var head = "[gold][b]" + title + "[/b][/gold]";
        return string.IsNullOrEmpty(description) ? head : head + "\n" + description;
    }

    public static MegaRichTextLabel? FindLabel(Node root) => root.FindChild("Text", true, false) as MegaRichTextLabel;

    public static void IgnoreMouse(Node node)
    {
        if (node is Control c) c.MouseFilter = MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren()) IgnoreMouse(child);
    }

    private void OnFocus()
    {
        Play(HoverSfx);
        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(this, "scale", BaseScale * 1.01f, 0.05);
        if (_outline != null) _tween.TweenProperty(_outline, "modulate", StsColors.blueGlow, 0.05);
        _hsv?.SetShaderParameter("v", 1.2f);
    }

    private void OnUnfocus()
    {
        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(this, "scale", BaseScale, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        if (_outline != null) _tween.TweenProperty(_outline, "modulate:a", 0f, 0.3);
        if (_hsv != null)
        {
            _tween.TweenMethod(Callable.From<float>(v => _hsv.SetShaderParameter("v", v)), 1.2f, 0.9f, 0.5)
                .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                _pressedInside = true;
                _tween?.Kill();
                _tween = CreateTween();
                _tween.TweenProperty(this, "scale", BaseScale * 0.99f, 0.5).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
            }
            else if (_pressedInside)
            {
                _pressedInside = false;
                Fire();
            }
            AcceptEvent();
        }
        else if (e.IsActionPressed("ui_accept") && HasFocus())
        {
            Fire();
            AcceptEvent();
        }
    }

    private void Fire()
    {
        var cb = _onPressed;
        if (cb == null) return;
        Play(ClickSfx);
        cb();
    }

    public static void Play(string sfx)
    {
        try { SfxCmd.Play(sfx); } catch { /* sound is optional */ }
    }
}

/// <summary>
/// The small teal button the game uses for Continue / Skip on reward and game-over screens,
/// built from its scene, with the same hover (brighter, slightly bigger) and click sounds.
/// </summary>
public sealed partial class NativeSmallButton : Control
{
    private ShaderMaterial? _hsv;
    private Tween? _tween;
    private Action? _onPressed;
    private bool _pressedInside;

    public static NativeSmallButton Create(string text, Action onPressed)
    {
        var button = new NativeSmallButton { _onPressed = onPressed };
        button.Build(text);
        return button;
    }

    private void Build(string text)
    {
        Name = "SpireTalkSmallButton";
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        CustomMinimumSize = new Vector2(260, 58);
        Size = CustomMinimumSize;
        PivotOffset = CustomMinimumSize / 2;

        var art = NativeScene.Build(NativeScene.ContinueButton);
        if (art != null)
        {
            art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            NativeOption.IgnoreMouse(art);
            art.Material = null;
            if (art.GetNodeOrNull("HotkeyIcon") is Node hotkey) hotkey.QueueFree();
            if (art.GetNodeOrNull<Label>("Label") is { } label) label.Text = text;
            if (art.GetNodeOrNull<CanvasItem>("Image") is { Material: ShaderMaterial hsv } image)
            {
                _hsv = (ShaderMaterial)hsv.Duplicate();
                image.Material = _hsv;
            }
            AddChild(art);
        }
        MouseEntered += () => { if (!HasFocus()) GrabFocus(); };
        MouseExited += () => { if (HasFocus()) ReleaseFocus(); };
        FocusEntered += () => Hover(true);
        FocusExited += () => Hover(false);
    }

    private void Hover(bool on)
    {
        if (on) NativeOption.Play(NativeOption.HoverSfx);
        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(this, "scale", on ? Vector2.One * 1.05f : Vector2.One, on ? 0.05 : 0.3);
        _hsv?.SetShaderParameter("v", on ? 1.2f : 1f);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed) _pressedInside = true;
            else if (_pressedInside)
            {
                _pressedInside = false;
                NativeOption.Play(NativeOption.ClickSfx);
                _onPressed?.Invoke();
            }
            AcceptEvent();
        }
        else if (e.IsActionPressed("ui_accept") && HasFocus())
        {
            NativeOption.Play(NativeOption.ClickSfx);
            _onPressed?.Invoke();
            AcceptEvent();
        }
    }
}

/// <summary>
/// The Ancients' dialogue line (the rounded bubble with a speaker icon and a tail), built from
/// the game's scene. Speaker colour and icon are the caller's; the text keeps the scene's font,
/// colour and soft shadow.
/// </summary>
public static class NativeDialogue
{
    public static readonly Color DefaultBubble = new(0.1581f, 0.272f, 0.31f, 1f);

    public static Control? Create(string text, Texture2D? icon, Texture2D? iconOutline, Color? bubbleColor)
    {
        var line = NativeScene.Build(NativeScene.AncientDialogueLine);
        if (line == null) return null;
        NativeOption.IgnoreMouse(line);
        var color = bubbleColor ?? DefaultBubble;

        if (line.GetNodeOrNull<Control>("AncientIcon") is { } iconNode)
        {
            if (icon != null)
            {
                if (iconNode.GetNodeOrNull<TextureRect>("Icon") is { } tex) tex.Texture = icon;
                if (iconNode.GetNodeOrNull<TextureRect>("Icon/Outline") is { } outline)
                {
                    outline.Texture = iconOutline;
                    outline.Visible = iconOutline != null;
                }
                iconNode.Visible = true;
            }
            else
            {
                // Keep the icon's slot so the bubble sits where a speaker's line would.
                iconNode.Visible = true;
                iconNode.Modulate = new Color(1, 1, 1, 0);
            }
        }
        if (line.FindChild("DialogueTailLeft", true, false) is TextureRect tail)
        {
            tail.Visible = true;
            tail.SelfModulate = color;
        }
        if (line.FindChild("Bubble", true, false) is NinePatchRect bubble) bubble.SelfModulate = color;
        if (line.FindChild("TextContainer", true, false) is MarginContainer margin)
        {
            // What NAncientDialogueLine.SetAncientAsSpeaker does: the tail eats into the left pad.
            margin.AddThemeConstantOverride("margin_left", 48);
        }
        if (NativeOption.FindLabel(line) is { } label) label.Text = text;
        return line;
    }

    /// <summary>
    /// A line spoken by one of the mod's NPCs: an Ancient gets its run-history icon and its own
    /// dialogue colour from the game's model; the Merchant gets the map's shop icon.
    /// </summary>
    public static Control? ForNpc(SpireTalk.Npc.NpcDefinition npc, string text)
    {
        var key = npc.Key.ToLowerInvariant();
        var icon = GameArt.Tex($"res://images/ui/run_history/{(npc.Kind == SpireTalk.Npc.NpcKind.Merchant ? "shop" : key)}.png");
        var outline = GameArt.Tex($"res://images/ui/run_history/{(npc.Kind == SpireTalk.Npc.NpcKind.Merchant ? "shop" : key)}_outline.png");
        Color? color = null;
        if (npc.Kind == SpireTalk.Npc.NpcKind.Ancient)
        {
            try
            {
                color = MegaCrit.Sts2.Core.Models.ModelDb.AllAncients
                    .FirstOrDefault(a => string.Equals(a.Id.Entry, key, StringComparison.OrdinalIgnoreCase))?.DialogueColor;
            }
            catch { /* default colour */ }
        }
        return Create(text, icon, outline, color);
    }

    public static void SetText(Control? line, string text)
    {
        if (line != null && GodotObject.IsInstanceValid(line) && NativeOption.FindLabel(line) is { } label) label.Text = text;
    }
}
