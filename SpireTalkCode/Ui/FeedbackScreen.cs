using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using SpireTalk.Backend;

namespace SpireTalk.Ui;

/// <summary>
/// Settings → Feedback: a box to write us in, an optional copy of the last conversation, and the
/// anonymous-counts switch. Sent to the mod's own service (<see cref="ModService"/>), never with a key.
/// </summary>
public sealed partial class FeedbackScreen : Control, IScreenContext
{
    private TextEdit _text = null!;
    private CheckBox _attach = null!;
    private MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel _status = null!;
    private Control _send = null!;
    private bool _sending;

    public Control? DefaultFocusedControl => null;

    public static void Open()
    {
        if (NModalContainer.Instance == null || NModalContainer.Instance.OpenModal != null) return;
        var screen = new FeedbackScreen();
        screen.Build();
        NModalContainer.Instance.Add(screen, true);
    }

    private void Back()
    {
        NModalContainer.Instance?.Clear();
        MemoryScreen.OpenLater(SpireTalkScreen.Open);
    }

    private void Build()
    {
        Name = "SpireTalkFeedbackScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var sheet = new PanelContainer { CustomMinimumSize = new Vector2(1060, 0) };
        sheet.AddThemeStyleboxOverride("panel", GameArt.Parchment(68, 26, 58));
        center.AddChild(sheet);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 14);
        sheet.AddChild(page);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 22);
        head.AddChild(GameArt.Icon(GameArt.MushieSitPath, 64));
        var title = GameArt.Label("[b]" + Loc.Ui("FbTitle") + "[/b]", 38, GameArt.Ink, wrap: false);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(title);
        var close = GameArt.LinkButton("✕", GameArt.Ink, 34);
        close.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        close.Pressed += Back;
        head.AddChild(close);
        page.AddChild(head);

        var well = new PanelContainer();
        well.AddThemeStyleboxOverride("panel", GameArt.Slate(34, 18));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        well.AddChild(column);
        page.AddChild(well);

        column.AddChild(GameArt.Label(Loc.Ui("FbIntro"), 22, GameArt.Cream));

        _text = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 230),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            PlaceholderText = Loc.Ui("FbPlaceholder"),
        };
        _text.AddThemeFontSizeOverride("font_size", 22);
        _text.AddThemeColorOverride("font_color", GameArt.Cream);
        _text.AddThemeColorOverride("font_placeholder_color", new Color(1, 1, 1, 0.4f));
        var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.35f), ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10 };
        box.SetCornerRadiusAll(6);
        _text.AddThemeStyleboxOverride("normal", box);
        _text.AddThemeStyleboxOverride("focus", box);
        column.AddChild(_text);

        _attach = Tick(Loc.Ui("FbAttach"), false);
        column.AddChild(_attach);
        var stats = Tick(Loc.Ui("FbStats"), SpireTalkConfig.SendStats);
        stats.Toggled += on =>
        {
            SpireTalkConfig.SendStats = on;
            SpireTalkConfig.Instance?.SaveDebounced(200);
        };
        column.AddChild(stats);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 20);
        _status = GameArt.Label(Loc.Ui("FbPrivacy"), 20, GameArt.Muted);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        actions.AddChild(_status);
        _send = SpireTalkConfig.Instance!.MakeButton(Loc.Ui("FbSend"), () => _ = Send(), "#4a9d7a");
        actions.AddChild(_send);
        column.AddChild(actions);
    }

    private static CheckBox Tick(string text, bool on)
    {
        var tick = new CheckBox { Text = text, ButtonPressed = on, FocusMode = FocusModeEnum.None };
        tick.AddThemeFontSizeOverride("font_size", 22);
        tick.AddThemeColorOverride("font_color", GameArt.Cream);
        tick.AddThemeColorOverride("font_hover_color", GameArt.Gold);
        tick.AddThemeColorOverride("font_pressed_color", GameArt.Cream);
        return tick;
    }

    private async Task Send()
    {
        var message = _text.Text.Trim();
        if (_sending) return;
        if (message.Length == 0)
        {
            SetStatus(Loc.Ui("FbEmpty"), ok: false);
            return;
        }
        _sending = true;
        SetStatus(Loc.Ui("FbSending"), ok: true);
        try
        {
            await ModService.SendFeedbackAsync(message, _attach.ButtonPressed);
            if (!IsInstanceValid(this)) return;
            _text.Text = "";
            SetStatus(Loc.Ui("FbSent"), ok: true);
        }
        catch (Exception ex)
        {
            if (!IsInstanceValid(this)) return;
            SetStatus(string.Format(Loc.Ui("FbFailed"), ex.Message), ok: false);
        }
        finally
        {
            _sending = false;
        }
    }

    private void SetStatus(string text, bool ok)
    {
        if (!IsInstanceValid(_status)) return;
        _status.Text = Loc.Sanitize(text);
        _status.AddThemeColorOverride("default_color", ok ? new Color("9fd8a8") : new Color("f0a0a0"));
    }

    // Esc goes back to the settings, on the release (see SpireTalkScreen).
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
            CallDeferred(nameof(Back));
        }
    }

    public override void _EnterTree()
    {
        try { NHotkeyManager.Instance?.AddBlockingScreen(this); } catch { /* optional */ }
    }

    public override void _ExitTree()
    {
        try { NHotkeyManager.Instance?.RemoveBlockingScreen(this); } catch { /* optional */ }
    }
}
