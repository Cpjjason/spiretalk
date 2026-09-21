using Godot;

namespace SpireTalk.Ancients;

/// <summary>
/// Free-text line for keyboard players, appended below the choice buttons. Controller users can
/// ignore it — the choices cover the conversation on their own. Enter submits; the box clears and
/// disables itself until the next turn is presented (the conversation replaces it).
/// </summary>
public sealed partial class TalkInputBox : MarginContainer
{
    private LineEdit _edit = null!;
    private Action<string>? _onSubmit;

    public enum Look
    {
        /// <summary>Translucent dark box for the Ancients' dark event layout.</summary>
        Dark,
        /// <summary>Navy tooltip slab, for sitting on the Merchant's parchment sheet.</summary>
        Parchment,
    }

    public static TalkInputBox Create(string placeholder, Action<string> onSubmit, Look look = Look.Dark)
    {
        var box = new TalkInputBox { _onSubmit = onSubmit };
        box.Build(placeholder, look);
        return box;
    }

    private void Build(string placeholder, Look look)
    {
        Name = "SpireTalkInput";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("margin_top", 10);
        AddThemeConstantOverride("margin_left", 6);
        AddThemeConstantOverride("margin_right", 6);

        _edit = new LineEdit
        {
            PlaceholderText = placeholder,
            MaxLength = 400,
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 64),
            ContextMenuEnabled = false,
            CaretBlink = true,
            FocusMode = FocusModeEnum.All,
        };
        _edit.AddThemeFontSizeOverride("font_size", 30);

        // Match the translucent event-button look instead of Godot's default light box.
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.45f),
            BorderColor = new Color(1f, 0.85f, 0.45f, 0.35f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        };
        normal.SetBorderWidthAll(2);
        var focus = (StyleBoxFlat)normal.Duplicate();
        focus.BorderColor = new Color(1f, 0.85f, 0.45f, 0.9f);
        focus.BgColor = new Color(0f, 0f, 0f, 0.6f);
        if (look == Look.Parchment)
        {
            // The game's navy tooltip slab reads as "a field" on tan paper; brighten it on focus.
            var navy = SpireTalk.Ui.GameArt.Navy(20, 10);
            var navyFocus = SpireTalk.Ui.GameArt.Slice(SpireTalk.Ui.GameArt.NavyPath, 22, 20, 10, null, new Color(1.3f, 1.3f, 1.25f));
            _edit.AddThemeStyleboxOverride("normal", navy);
            _edit.AddThemeStyleboxOverride("focus", navyFocus);
            _edit.AddThemeFontSizeOverride("font_size", 26);
        }
        else
        {
            _edit.AddThemeStyleboxOverride("normal", normal);
            _edit.AddThemeStyleboxOverride("focus", focus);
        }
        _edit.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.95f));
        _edit.AddThemeColorOverride("font_placeholder_color", new Color(1f, 1f, 1f, 0.45f));
        _edit.AddThemeColorOverride("caret_color", new Color(1f, 0.85f, 0.45f, 1f));

        _edit.TextSubmitted += OnSubmitted;
        AddChild(_edit);
    }

    private void OnSubmitted(string text)
    {
        var trimmed = (text ?? "").Trim();
        if (trimmed.Length == 0) return;
        _edit.Editable = false;
        _edit.ReleaseFocus();
        var cb = _onSubmit;
        _onSubmit = null;
        cb?.Invoke(trimmed);
    }

    public override void _ExitTree()
    {
        _onSubmit = null;
    }
}
