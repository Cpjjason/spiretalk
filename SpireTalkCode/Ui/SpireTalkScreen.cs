using BaseLib.Config.UI;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using SpireTalk.Yumina;

namespace SpireTalk.Ui;

/// <summary>
/// The mod's own settings sheet: a parchment page in the game's modal layer. What the player sees
/// first is the part everyone needs — an API endpoint, a key, a model — and the conversation
/// switches. The Yumina account lives behind a small link in the corner of the sheet.
/// </summary>
public sealed partial class SpireTalkScreen : Control, IScreenContext
{
    private const float SheetWidth = 1180f;

    private SpireTalkConfig _config = null!;
    private VBoxContainer _providerCard = null!;
    private VBoxContainer _ownKeyCard = null!;
    private VBoxContainer _yuminaCard = null!;
    private Button _cornerLink = null!;
    private MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel _ownStatus = null!;
    private MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel _yuminaStatus = null!;
    private Button _reopenLink = null!;
    private NConfigButton _loginButton = null!;

    public Control? DefaultFocusedControl => null;

    /// <summary>Open the sheet in the game's modal container (main menu, mod list, or mid-run pause).</summary>
    public static void Open()
    {
        var config = SpireTalkConfig.Instance;
        if (config == null || NModalContainer.Instance == null) return;
        if (NModalContainer.Instance.OpenModal != null) return;
        var screen = new SpireTalkScreen { _config = config };
        screen.Build();
        NModalContainer.Instance.Add(screen, true);
    }

    private void Close()
    {
        DeviceLogin.Cancel();
        _config.SaveDebounced(200);
        NModalContainer.Instance?.Clear();
    }

    private void Build()
    {
        Name = "SpireTalkScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var sheet = new PanelContainer { CustomMinimumSize = new Vector2(SheetWidth, 0) };
        sheet.AddThemeStyleboxOverride("panel", GameArt.Parchment(68, 26, 58));
        center.AddChild(sheet);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 14);
        sheet.AddChild(page);

        page.AddChild(BuildHeader());
        page.AddChild(BuildWell());
        page.AddChild(BuildFooter());
    }

    // ── Header: mushie, title, version, close ────────────────────────────

    private Control BuildHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 22);

        row.AddChild(GameArt.Icon(GameArt.MushieSitPath, 72));

        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        titles.AddThemeConstantOverride("separation", 0);
        titles.AddChild(GameArt.Label("[b]" + Loc.Ui("ScreenTitle") + "[/b]", 40, GameArt.Ink, wrap: false));
        titles.AddChild(GameArt.Label(Loc.Ui("ScreenSubtitle") + "  ·  v" + MainFile.Version, 24, GameArt.InkSoft, wrap: false));
        row.AddChild(titles);

        var close = GameArt.LinkButton("✕", GameArt.Ink, 34);
        close.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        close.Pressed += Close;
        row.AddChild(close);
        return row;
    }

    // ── The slate well: provider card + conversation switches ────────────

    private Control BuildWell()
    {
        var well = new PanelContainer();
        well.AddThemeStyleboxOverride("panel", GameArt.Slate(34, 16));

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        well.AddChild(column);

        _providerCard = new VBoxContainer();
        _providerCard.AddThemeConstantOverride("separation", 12);
        _ownKeyCard = BuildOwnKeyCard();
        _yuminaCard = BuildYuminaCard();
        _providerCard.AddChild(_ownKeyCard);
        _providerCard.AddChild(_yuminaCard);
        column.AddChild(_providerCard);

        column.AddChild(GameArt.Spacer(4));
        column.AddChild(GameArt.Rule(new Color(1f, 1f, 1f, 0.12f)));
        column.AddChild(GameArt.Spacer(4));

        column.AddChild(BuildConversationBlock());

        ShowProvider(SpireTalkConfig.Mode == BackendMode.Yumina ? BackendMode.Yumina : BackendMode.OwnKey, save: false);
        return well;
    }

    private static Control SectionTitle(string text)
    {
        return GameArt.Label("[b]" + text + "[/b]", 28, GameArt.Gold, wrap: false);
    }

    private static HBoxContainer Row(string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 20);
        var text = GameArt.Label(label, 25, GameArt.Cream, wrap: false);
        text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(text);
        control.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(control);
        return row;
    }

    private VBoxContainer BuildOwnKeyCard()
    {
        var card = new VBoxContainer();
        card.AddThemeConstantOverride("separation", 8);

        var head = new HBoxContainer();
        var title = SectionTitle(Loc.Ui("OwnKeyTitle"));
        head.AddChild(title);
        _cornerLink = GameArt.LinkButton(Loc.Ui("CornerYumina"), GameArt.Muted, 22);
        _cornerLink.Pressed += () => ShowProvider(BackendMode.Yumina, save: true);
        head.AddChild(_cornerLink);
        card.AddChild(head);

        card.AddChild(Row(Loc.Ui("OwnBaseUrl"), _config.MakeLineEdit(nameof(SpireTalkConfig.OwnBaseUrl))));
        card.AddChild(Row(Loc.Ui("OwnKey"), _config.MakeLineEdit(nameof(SpireTalkConfig.OwnKey), secret: true)));
        card.AddChild(Row(Loc.Ui("OwnModel"), _config.MakeLineEdit(nameof(SpireTalkConfig.OwnModel))));

        var toggles = new HBoxContainer();
        toggles.AddThemeConstantOverride("separation", 16);
        var jsonLabel = GameArt.Label(Loc.Ui("OwnJsonMode"), 23, GameArt.Muted, wrap: false);
        jsonLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        jsonLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        toggles.AddChild(jsonLabel);
        var tick = _config.MakeTickbox(nameof(SpireTalkConfig.OwnJsonMode));
        tick.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        toggles.AddChild(tick);
        toggles.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var test = _config.MakeButton(Loc.Ui("TestBtn"), () => _ = RunOwnKeyTest(), "#4a9d7a");
        toggles.AddChild(test);
        card.AddChild(toggles);

        _ownStatus = GameArt.Label(Loc.Ui("OwnKeyHint"), 22, GameArt.Muted);
        card.AddChild(_ownStatus);
        return card;
    }

    private VBoxContainer BuildYuminaCard()
    {
        var card = new VBoxContainer();
        card.AddThemeConstantOverride("separation", 10);

        var head = new HBoxContainer();
        head.AddChild(SectionTitle(Loc.Ui("YuminaTitle")));
        var back = GameArt.LinkButton(Loc.Ui("CornerOwnKey"), GameArt.Muted, 22);
        back.Pressed += () => ShowProvider(BackendMode.OwnKey, save: true);
        head.AddChild(back);
        card.AddChild(head);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 16);
        _loginButton = _config.MakeButton(Loc.Ui("LoginBtn"), () => _ = RunLogin(), "#c9a227");
        actions.AddChild(_loginButton);
        var logout = _config.MakeButton(Loc.Ui("LogoutBtn"), () =>
        {
            DeviceLogin.Cancel();
            SpireTalkConfig.Logout();
            SetStatus(_yuminaStatus, Loc.Ui("LoggedOutBody"), ok: true);
        }, "#b03f3f");
        actions.AddChild(logout);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var test = _config.MakeButton(Loc.Ui("TestBtn"), () => _ = RunYuminaTest(), "#4a9d7a");
        actions.AddChild(test);
        card.AddChild(actions);

        _yuminaStatus = GameArt.Label(Loc.Ui("LoginHint"), 22, GameArt.Muted);
        card.AddChild(_yuminaStatus);
        _reopenLink = GameArt.LinkButton(Loc.Ui("ReopenBrowser"), GameArt.Gold, 22);
        _reopenLink.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _reopenLink.Visible = false;
        _reopenLink.Pressed += DeviceLogin.ReopenBrowser;
        card.AddChild(_reopenLink);

        card.AddChild(Row(Loc.Ui("ApiToken"), _config.MakeLineEdit(nameof(SpireTalkConfig.ApiToken), secret: true)));
        card.AddChild(Row(Loc.Ui("ServerUrl"), _config.MakeLineEdit(nameof(SpireTalkConfig.ServerUrl))));

        // Live account block: who is signed in, credits, plan, model picker.
        AccountPanel.Install(_config, card, compact: true);
        return card;
    }

    private Control BuildConversationBlock()
    {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 10);
        block.AddChild(SectionTitle(Loc.Ui("ConversationTitle")));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 40);
        grid.AddThemeConstantOverride("v_separation", 2);
        foreach (var (key, prop) in new[]
                 {
                     ("EnableAncients", nameof(SpireTalkConfig.EnableAncients)),
                     ("EnableMerchant", nameof(SpireTalkConfig.EnableMerchant)),
                     ("FreeTextInput", nameof(SpireTalkConfig.FreeTextInput)),
                     ("RememberAcrossRuns", nameof(SpireTalkConfig.RememberAcrossRuns)),
                     ("UseAncientAffection", nameof(SpireTalkConfig.UseAncientAffection)),
                     ("DebugLogging", nameof(SpireTalkConfig.DebugLogging)),
                 })
        {
            var cell = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cell.AddThemeConstantOverride("separation", 14);
            var tick = _config.MakeTickbox(prop);
            tick.Scale = new Vector2(0.82f, 0.82f);
            tick.CustomMinimumSize = new Vector2(54f, 54f);
            cell.AddChild(tick);
            var label = GameArt.Label(Loc.Ui(key), 24, GameArt.Cream, wrap: false);
            label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            cell.AddChild(label);
            grid.AddChild(cell);
        }
        block.AddChild(grid);

        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 20);
        var langLabel = GameArt.Label(Loc.Ui("Language"), 24, GameArt.Cream, wrap: false);
        langLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        langLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bottom.AddChild(langLabel);
        var dropdown = _config.MakeDropdown(nameof(SpireTalkConfig.Language));
        dropdown.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bottom.AddChild(dropdown);
        bottom.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var reset = _config.MakeButton(Loc.Ui("ResetMemoriesBtn"), () =>
        {
            SpireTalkConfig.ResetMemories();
            SetStatus(_ownStatus, Loc.Ui("ResetDoneBody"), ok: true);
            SetStatus(_yuminaStatus, Loc.Ui("ResetDoneBody"), ok: true);
        }, "#b03f3f");
        bottom.AddChild(reset);
        block.AddChild(bottom);
        return block;
    }

    // ── Footer: thanks + link ────────────────────────────────────────────

    private Control BuildFooter()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.Alignment = BoxContainer.AlignmentMode.End;
        row.AddChild(GameArt.Icon(GameArt.MushieCoinPath, 30));
        var thanks = GameArt.Label(Loc.Ui("ThanksYumina"), 21, GameArt.InkSoft, wrap: false);
        thanks.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        thanks.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(thanks);
        var link = GameArt.LinkButton("github.com/lovetimo0421/yumina-oss", GameArt.Ink, 21);
        link.Pressed += () => { try { OS.ShellOpen("https://github.com/lovetimo0421/yumina-oss"); } catch { /* best effort */ } };
        row.AddChild(link);
        return row;
    }

    // ── Behaviour ────────────────────────────────────────────────────────

    private void ShowProvider(BackendMode mode, bool save)
    {
        var yumina = mode == BackendMode.Yumina;
        _ownKeyCard.Visible = !yumina;
        _yuminaCard.Visible = yumina;
        if (save) SpireTalkConfig.SetMode(mode);
        if (yumina) AccountPanel.Refresh();
    }

    private static void SetStatus(MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel label, string text, bool ok)
    {
        if (!GodotObject.IsInstanceValid(label)) return;
        label.Text = Loc.Sanitize(text);
        label.AddThemeColorOverride("default_color", ok ? new Color("9fd8a8") : new Color("f0a0a0"));
    }

    private async Task RunOwnKeyTest()
    {
        SetStatus(_ownStatus, Loc.Ui("Testing"), ok: true);
        var (ok, text) = await SpireTalkConfig.TestOwnKeyAsync();
        SetStatus(_ownStatus, text, ok);
    }

    private async Task RunYuminaTest()
    {
        SetStatus(_yuminaStatus, Loc.Ui("Testing"), ok: true);
        var (ok, text) = await SpireTalkConfig.TestConnectionAsync();
        SetStatus(_yuminaStatus, text, ok);
    }

    private async Task RunLogin()
    {
        if (DeviceLogin.InProgress) return;
        _loginButton.Disable();
        try
        {
            await DeviceLogin.StartAsync(update =>
            {
                if (!IsInsideTree()) return;
                switch (update.Phase)
                {
                    case DeviceLogin.Phase.Waiting:
                        SetStatus(_yuminaStatus, update.Text, ok: true);
                        _reopenLink.Visible = true;
                        break;
                    case DeviceLogin.Phase.Done:
                        SetStatus(_yuminaStatus, update.Text, ok: true);
                        _reopenLink.Visible = false;
                        break;
                    case DeviceLogin.Phase.Failed:
                        SetStatus(_yuminaStatus, update.Text, ok: false);
                        _reopenLink.Visible = false;
                        break;
                    default:
                        _reopenLink.Visible = false;
                        break;
                }
            });
        }
        finally
        {
            if (IsInsideTree() && GodotObject.IsInstanceValid(_loginButton)) _loginButton.Enable();
        }
    }

    // Esc: the press is swallowed and the sheet closes on the RELEASE, so neither half of the key
    // stroke reaches the hotkey manager underneath (the main menu opens the profile screen on Esc).
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
            CallDeferred(nameof(Close));
        }
    }

    public override void _EnterTree()
    {
        // The game's own popups block every hotkey while open; do the same.
        try { NHotkeyManager.Instance?.AddBlockingScreen(this); } catch { /* optional */ }
    }

    public override void _ExitTree()
    {
        try { NHotkeyManager.Instance?.RemoveBlockingScreen(this); } catch { /* optional */ }
        DeviceLogin.Cancel();
    }
}
