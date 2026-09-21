using Godot;
using MegaCrit.Sts2.addons.mega_text;
using SpireTalk.Ancients;

namespace SpireTalk.Ui;

/// <summary>
/// The Merchant's conversation sheet: a parchment page (the game's own sub-menu panel) with the
/// merchant's words in a cream speech bubble, up to three teal event-style choice buttons, an
/// optional line to type your own, and a small ledger line at the bottom. Purely presentational:
/// the owning conversation feeds it turns and listens for choices.
/// </summary>
public sealed partial class TalkPanel : Control
{
    private const float SheetWidth = 1060f;

    private MegaRichTextLabel _body = null!;
    private MegaRichTextLabel _title = null!;
    private MegaRichTextLabel _footer = null!;
    private VBoxContainer _choices = null!;
    private TextureRect _tail = null!;
    private TalkInputBox? _input;

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

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.5f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.GuiInput += OnDimInput;
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var sheet = new PanelContainer { CustomMinimumSize = new Vector2(SheetWidth, 0) };
        sheet.AddThemeStyleboxOverride("panel", GameArt.Parchment(60, 40, 60));
        center.AddChild(sheet);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 14);
        sheet.AddChild(page);

        // Header: name in ink, a quiet ✕ on the right.
        var header = new HBoxContainer();
        _title = GameArt.Label("[b]" + Loc.Sanitize(title) + "[/b]", 36, GameArt.Ink, wrap: false);
        _title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(_title);
        var close = GameArt.LinkButton("✕", GameArt.Ink, 30);
        close.Pressed += () => _onClose?.Invoke();
        header.AddChild(close);
        page.AddChild(header);

        // Speech bubble with its tail hanging off the bottom-left, as the shop's own bubbles do.
        var bubbleWrap = new VBoxContainer();
        bubbleWrap.AddThemeConstantOverride("separation", 0);
        var bubble = new PanelContainer();
        bubble.AddThemeStyleboxOverride("panel", GameArt.Bubble(GameArt.Paper));
        _body = GameArt.Label("", 27, GameArt.Ink);
        _body.CustomMinimumSize = new Vector2(0, 90);
        bubble.AddChild(_body);
        bubbleWrap.AddChild(bubble);
        _tail = new TextureRect
        {
            Texture = GameArt.Tex(GameArt.BubbleTailPath),
            Modulate = GameArt.Paper,
            CustomMinimumSize = new Vector2(36, 40),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var tailRow = new MarginContainer();
        tailRow.AddThemeConstantOverride("margin_left", 48);
        tailRow.AddThemeConstantOverride("margin_top", -6);
        tailRow.AddChild(_tail);
        bubbleWrap.AddChild(tailRow);
        page.AddChild(bubbleWrap);

        _choices = new VBoxContainer();
        _choices.AddThemeConstantOverride("separation", 8);
        page.AddChild(_choices);

        _footer = GameArt.Label("", 21, GameArt.InkSoft, wrap: false);
        _footer.HorizontalAlignment = HorizontalAlignment.Right;
        page.AddChild(_footer);
    }

    public void SetTitle(string title)
    {
        _title.Text = "[b]" + Loc.Sanitize(title) + "[/b]";
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
                var b = GameArt.HexButton(text, 26, 66);
                b.Alignment = HorizontalAlignment.Left;
                b.Pressed += () => _onChoice?.Invoke(text);
                _choices.AddChild(b);
            }
            if (showInput)
            {
                _input = TalkInputBox.Create(string.Format(Loc.EventText("typePlaceholder"), linesLeft), t => _onChoice?.Invoke(t), TalkInputBox.Look.Parchment);
                _choices.AddChild(_input);
            }
        }
        var leave = GameArt.LinkButton(Loc.EventText(linesLeft > 0 ? "leaveShop" : "leaveShopDone"), GameArt.InkSoft, 24);
        leave.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        leave.Pressed += () => _onClose?.Invoke();
        _choices.AddChild(leave);
        _footer.Text = Loc.Sanitize(footer);
    }

    public void SetError(string message)
    {
        ClearChoices();
        _body.Text = Loc.Sanitize(message);
        var leave = GameArt.LinkButton(Loc.EventText("leaveShopDone"), GameArt.InkSoft, 24);
        leave.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        leave.Pressed += () => _onClose?.Invoke();
        _choices.AddChild(leave);
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

    // Esc: the press is swallowed and the panel closes on the RELEASE, so neither half of the key
    // stroke reaches the shop's own hotkeys (which would also close the inventory underneath).
    private bool _escArmed;

    public override void _Input(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel") && !e.IsEcho())
        {
            _escArmed = true;
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionReleased("ui_cancel") && _escArmed)
        {
            _escArmed = false;
            GetViewport().SetInputAsHandled();
            CallDeferred(nameof(RequestClose));
        }
    }

    public override void _EnterTree()
    {
        try { MegaCrit.Sts2.Core.Nodes.CommonUi.NHotkeyManager.Instance?.AddBlockingScreen(this); } catch { /* optional */ }
    }

    public override void _ExitTree()
    {
        try { MegaCrit.Sts2.Core.Nodes.CommonUi.NHotkeyManager.Instance?.RemoveBlockingScreen(this); } catch { /* optional */ }
    }

    private void RequestClose() => _onClose?.Invoke();
}
