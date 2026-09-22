using Godot;
using MegaCrit.Sts2.Core.Helpers;
using SpireTalk.Ui;

namespace SpireTalk.Ancients;

/// <summary>
/// Free-text line for keyboard players, shown as one more Ancient option button (the game's own
/// scene, see <see cref="NativeScene"/>) with the text field where the option text would be.
/// Controller users can ignore it — the choices cover the conversation on their own. Enter
/// submits; the box disables itself until the next turn is presented (the conversation replaces it).
/// </summary>
public sealed partial class TalkInputBox : Control
{
    private static readonly Color Cream = new(1f, 0.9647f, 0.8863f, 1f);

    private LineEdit _edit = null!;
    private NinePatchRect? _outline;
    private Tween? _tween;
    private Action<string>? _onSubmit;

    public static TalkInputBox Create(string placeholder, Action<string> onSubmit, Color buttonColor)
    {
        var box = new TalkInputBox { _onSubmit = onSubmit };
        box.Build(placeholder, buttonColor);
        return box;
    }

    private void Build(string placeholder, Color buttonColor)
    {
        Name = "SpireTalkInput";
        CustomMinimumSize = new Vector2(0, 84);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;

        var art = NativeScene.Build(NativeScene.AncientOptionButton);
        if (art != null)
        {
            art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            art.CustomMinimumSize = Vector2.Zero;
            NativeOption.IgnoreMouse(art);
            if (art.GetNodeOrNull<NinePatchRect>("Image") is { } image) image.Modulate = buttonColor;
            _outline = art.GetNodeOrNull<NinePatchRect>("Outline");
            if (art.GetNodeOrNull("HBoxContainer") is CanvasItem row) row.Visible = false;
            if (art.GetNodeOrNull("PlayerVoteContainer") is Node votes) votes.QueueFree();
            AddChild(art);
        }

        _edit = new LineEdit
        {
            PlaceholderText = placeholder,
            MaxLength = 400,
            ContextMenuEnabled = false,
            CaretBlink = true,
            FocusMode = FocusModeEnum.All,
            Flat = true,
        };
        _edit.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Same inset and type as the option buttons' text so the line reads as one of them.
        _edit.OffsetLeft = 38;
        _edit.OffsetRight = -38;
        var empty = new StyleBoxEmpty();
        _edit.AddThemeStyleboxOverride("normal", empty);
        _edit.AddThemeStyleboxOverride("focus", empty);
        _edit.AddThemeStyleboxOverride("read_only", empty);
        var font = GameArt.Font("res://themes/kreon_regular_shared.tres");
        if (font != null) _edit.AddThemeFontOverride("font", font);
        _edit.AddThemeFontSizeOverride("font_size", 24);
        _edit.AddThemeColorOverride("font_color", Cream);
        _edit.AddThemeColorOverride("font_uneditable_color", new Color(Cream, 0.5f));
        _edit.AddThemeColorOverride("font_placeholder_color", new Color(Cream, 0.5f));
        _edit.AddThemeColorOverride("caret_color", Cream);
        _edit.AddThemeColorOverride("selection_color", new Color(StsColors.blueGlow, 0.35f));

        _edit.MouseEntered += () => Glow(true);
        _edit.MouseExited += () => { if (!_edit.HasFocus()) Glow(false); };
        _edit.FocusEntered += () => Glow(true);
        _edit.FocusExited += () => Glow(false);
        _edit.TextSubmitted += OnSubmitted;
        AddChild(_edit);
    }

    // The option buttons' hover: a blue outline fades in.
    private void Glow(bool on)
    {
        if (_outline == null) return;
        _tween?.Kill();
        _tween = CreateTween();
        if (on)
        {
            NativeOption.Play(NativeOption.HoverSfx);
            _tween.TweenProperty(_outline, "modulate", StsColors.blueGlow, 0.05);
        }
        else
        {
            _tween.TweenProperty(_outline, "modulate:a", 0f, 0.3);
        }
    }

    private void OnSubmitted(string text)
    {
        var trimmed = (text ?? "").Trim();
        if (trimmed.Length == 0) return;
        _edit.Editable = false;
        _edit.ReleaseFocus();
        var cb = _onSubmit;
        _onSubmit = null;
        NativeOption.Play(NativeOption.ClickSfx);
        cb?.Invoke(trimmed);
    }

    public override void _ExitTree()
    {
        _onSubmit = null;
    }
}
