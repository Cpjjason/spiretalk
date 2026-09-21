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

    public static TalkInputBox Create(string placeholder, Action<string> onSubmit)
    {
        var box = new TalkInputBox { _onSubmit = onSubmit };
        box.Build(placeholder);
        return box;
    }

    private void Build(string placeholder)
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
        _edit.AddThemeStyleboxOverride("normal", normal);
        _edit.AddThemeStyleboxOverride("focus", focus);
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
