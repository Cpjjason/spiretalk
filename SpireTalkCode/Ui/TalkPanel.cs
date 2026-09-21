using Godot;
using SpireTalk.Ancients;

namespace SpireTalk.Ui;

/// <summary>
/// A self-contained conversation overlay for NPCs that have no dialogue layout of their own
/// (the Merchant). Dim backdrop, a dark card with the NPC's lines, up to three choice buttons,
/// an optional free-text line and a close button. Purely presentational: the owning
/// conversation feeds it turns and listens for choices.
/// </summary>
public sealed partial class TalkPanel : Control
{
    private RichTextLabel _body = null!;
    private Label _title = null!;
    private Label _footer = null!;
    private VBoxContainer _choices = null!;
    private TalkInputBox? _input;
    private VBoxContainer _card = null!;

    private Action<string>? _onChoice;
    private Action? _onClose;

    public static TalkPanel Create(string title, Action<string> onChoice, Action onClose)
    {
        var panel = new TalkPanel { _onChoice = onChoice, _onClose = onClose };
        panel.Build(title);
        return panel;
    }

    private void Build(string title)
    {
        Name = "SpireTalkPanel";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 200;

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.GuiInput += OnDimInput;
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var frame = new PanelContainer { CustomMinimumSize = new Vector2(980, 0) };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.08f, 0.96f),
            BorderColor = new Color(1f, 0.85f, 0.45f, 0.55f),
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ContentMarginLeft = 34,
            ContentMarginRight = 34,
            ContentMarginTop = 26,
            ContentMarginBottom = 26,
            ShadowColor = new Color(0f, 0f, 0f, 0.6f),
            ShadowSize = 24,
        };
        style.SetBorderWidthAll(2);
        frame.AddThemeStyleboxOverride("panel", style);
        center.AddChild(frame);

        _card = new VBoxContainer();
        _card.AddThemeConstantOverride("separation", 16);
        frame.AddChild(_card);

        var header = new HBoxContainer();
        _title = new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _title.AddThemeFontSizeOverride("font_size", 34);
        _title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.45f));
        header.AddChild(_title);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(56, 56), Flat = true };
        close.AddThemeFontSizeOverride("font_size", 30);
        close.Pressed += () => _onClose?.Invoke();
        header.AddChild(close);
        _card.AddChild(header);

        _body = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _body.AddThemeFontSizeOverride("normal_font_size", 28);
        _body.AddThemeColorOverride("default_color", new Color(0.96f, 0.94f, 0.9f));
        _card.AddChild(_body);

        _choices = new VBoxContainer();
        _choices.AddThemeConstantOverride("separation", 10);
        _card.AddChild(_choices);

        _footer = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _footer.AddThemeFontSizeOverride("font_size", 22);
        _footer.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.55f));
        _card.AddChild(_footer);
    }

    public void SetTitle(string title)
    {
        _title.Text = title;
    }

    public void SetThinking()
    {
        ClearChoices();
        _body.Text = Loc.EventText("thinking");
        _footer.Text = "";
    }

    public void SetTurn(IReadOnlyList<string> lines, IReadOnlyList<string> choices, int linesLeft, bool showInput, string footer)
    {
        _body.Text = string.Join("\n\n", lines.Select(Loc.Sanitize));
        ClearChoices();
        if (linesLeft > 0)
        {
            foreach (var choice in choices.Take(3))
            {
                var text = choice;
                var b = MakeChoiceButton(text);
                b.Pressed += () => _onChoice?.Invoke(text);
                _choices.AddChild(b);
            }
            if (showInput)
            {
                _input = TalkInputBox.Create(string.Format(Loc.EventText("typePlaceholder"), linesLeft), t => _onChoice?.Invoke(t));
                _choices.AddChild(_input);
            }
        }
        var leave = MakeChoiceButton(Loc.EventText(linesLeft > 0 ? "leaveShop" : "leaveShopDone"));
        leave.Pressed += () => _onClose?.Invoke();
        _choices.AddChild(leave);
        _footer.Text = footer;
    }

    public void SetError(string message)
    {
        ClearChoices();
        _body.Text = Loc.Sanitize(message);
        var leave = MakeChoiceButton(Loc.EventText("leaveShopDone"));
        leave.Pressed += () => _onClose?.Invoke();
        _choices.AddChild(leave);
    }

    private static Button MakeChoiceButton(string text)
    {
        var b = new Button
        {
            Text = Loc.Sanitize(text),
            CustomMinimumSize = new Vector2(0, 62),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = HorizontalAlignment.Left,
            ClipText = true,
        };
        b.AddThemeFontSizeOverride("font_size", 26);
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.06f),
            BorderColor = new Color(1f, 0.85f, 0.45f, 0.35f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
        };
        normal.SetBorderWidthAll(2);
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(1f, 0.85f, 0.45f, 0.18f);
        hover.BorderColor = new Color(1f, 0.85f, 0.45f, 0.9f);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", hover);
        b.AddThemeStyleboxOverride("focus", hover);
        b.AddThemeColorOverride("font_color", new Color(0.96f, 0.94f, 0.9f));
        return b;
    }

    private void ClearChoices()
    {
        _input = null;
        foreach (var child in _choices.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void OnDimInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            _onClose?.Invoke();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            _onClose?.Invoke();
        }
    }
}
