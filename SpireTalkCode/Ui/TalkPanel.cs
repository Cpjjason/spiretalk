using Godot;
using SpireTalk.Ancients;
using SpireTalk.Npc;

namespace SpireTalk.Ui;

/// <summary>
/// The Merchant's conversation, laid out the way the game lays out an Ancient's: the screen dims,
/// his lines come up as the game's own dialogue bubbles (shop icon, tail), and the replies are
/// the game's own Ancient option buttons, with the typed line as one more of them. Everything is
/// built from vanilla scenes (<see cref="NativeScene"/>), so fonts, colours and shadows match.
/// Purely presentational: the owning conversation feeds it turns and listens for choices.
/// </summary>
public sealed partial class TalkPanel : Control
{
    private const float ColumnWidth = 1000f;
    private static readonly Color ButtonColor = new(0f, 0f, 0f, 0.45f);

    private NpcDefinition _npc = null!;
    private VBoxContainer _lines = null!;
    private VBoxContainer _choices = null!;

    private Action<string>? _onChoice;
    private Action? _onClose;

    public static TalkPanel Create(NpcDefinition npc, Action<string> onChoice, Action onClose)
    {
        var panel = new TalkPanel { _npc = npc, _onChoice = onChoice, _onClose = onClose };
        panel.Build();
        return panel;
    }

    private void Build()
    {
        Name = "SpireTalkPanel";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 200;

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.GuiInput += OnDimInput;
        AddChild(dim);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0), MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 28);
        center.AddChild(column);

        _lines = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _lines.AddThemeConstantOverride("separation", 10);
        column.AddChild(_lines);

        _choices = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _choices.AddThemeConstantOverride("separation", 4);
        column.AddChild(_choices);
    }

    public void SetThinking()
    {
        ClearChoices();
        ShowLines(new[] { Loc.EventText("thinking") });
    }

    public void SetTurn(IReadOnlyList<string> lines, IReadOnlyList<string> choices, int linesLeft, bool showInput, string footer)
    {
        ShowLines(lines);
        ClearChoices();
        if (linesLeft > 0)
        {
            foreach (var choice in choices.Take(3))
            {
                var text = choice;
                AddOption("", Loc.Sanitize(text), () => _onChoice?.Invoke(text));
            }
            if (showInput)
            {
                var input = TalkInputBox.Create(string.Format(Loc.EventText("typePlaceholder"), linesLeft), t => _onChoice?.Invoke(t), ButtonColor);
                input.CustomMinimumSize = new Vector2(ColumnWidth, 84);
                AddAnimated(input);
            }
        }
        AddOption(Loc.EventText(linesLeft > 0 ? "leaveShop" : "leaveShopDone"), Loc.Sanitize(footer), () => _onClose?.Invoke());
    }

    /// <summary>The merchant proposed a deal: the answer comes before the usual choices.</summary>
    public void SetDeal(IReadOnlyList<string> lines, string dealText, string acceptLabel, string declineLabel, Action<bool> onAnswer, string footer)
    {
        ShowLines(lines);
        ClearChoices();
        AddOption(Loc.Sanitize(dealText), Loc.Sanitize(acceptLabel), () => onAnswer(true));
        AddOption("", Loc.Sanitize(declineLabel), () => onAnswer(false));
    }

    public void SetError(string message)
    {
        ShowLines(new[] { message });
        ClearChoices();
        AddOption(Loc.EventText("leaveShopDone"), null, () => _onClose?.Invoke());
    }

    private void ShowLines(IEnumerable<string> lines)
    {
        foreach (var child in _lines.GetChildren())
        {
            _lines.RemoveChild(child);
            child.QueueFree();
        }
        var i = 0;
        foreach (var text in lines)
        {
            var line = NativeDialogue.ForNpc(_npc, Loc.Sanitize(text));
            if (line == null) continue;
            line.Modulate = new Color(1, 1, 1, 0);
            _lines.AddChild(line);
            // NAncientDialogueLine.OnAnimInSetVisible: a 0.1s fade, one line after another.
            var tween = line.CreateTween();
            tween.TweenInterval(0.15 * i++);
            tween.TweenProperty(line, "modulate:a", 1f, 0.1);
        }
    }

    private void AddOption(string title, string? description, Action onPressed)
    {
        AddAnimated(NativeOption.Create(title, description, ButtonColor, onPressed));
    }

    // NEventOptionButton.AnimateIn: fade in, staggered by index.
    private void AddAnimated(Control option)
    {
        var index = _choices.GetChildCount();
        option.Modulate = new Color(1, 1, 1, 0);
        _choices.AddChild(option);
        var tween = option.CreateTween();
        tween.TweenInterval(0.25 + 0.1 * index);
        tween.TweenProperty(option, "modulate", Colors.White, 0.25);
    }

    private void ClearChoices()
    {
        foreach (var child in _choices.GetChildren())
        {
            _choices.RemoveChild(child);
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
