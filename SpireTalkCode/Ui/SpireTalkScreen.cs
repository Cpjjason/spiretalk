using BaseLib.Config.UI;
using SpireTalk.Backend;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace SpireTalk.Ui;

/// <summary>
/// The mod's own settings sheet: a parchment page in the game's modal layer — an API endpoint, a
/// key, a model, and the conversation switches. That is all the mod needs.
/// </summary>
public sealed partial class SpireTalkScreen : Control, IScreenContext
{
    private const float SheetWidth = 1180f;

    private SpireTalkConfig _config = null!;
    private MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel _ownStatus = null!;

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
        _modelFetch?.Cancel();
        SpireTalkConfig.StashKey();
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

        column.AddChild(BuildOwnKeyCard());

        column.AddChild(GameArt.Spacer(4));
        column.AddChild(GameArt.Rule(new Color(1f, 1f, 1f, 0.12f)));
        column.AddChild(GameArt.Spacer(4));

        column.AddChild(BuildConversationBlock());
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

    // ── Connection: source → key (or address) → model, like Yumina's own "private API" card ──

    private const string OtherModel = "\u0001other";
    private const float FieldWidth = 620f;

    private VBoxContainer _connection = null!;
    private ChoiceDropdown? _modelDropdown;
    private Control? _modelIdRow;
    private CancellationTokenSource? _modelFetch;

    private VBoxContainer BuildOwnKeyCard()
    {
        SpireTalkConfig.EnsureSource();
        _connection = new VBoxContainer();
        _connection.AddThemeConstantOverride("separation", 8);
        FillConnection();
        return _connection;
    }

    private void FillConnection()
    {
        foreach (var child in _connection.GetChildren()) child.QueueFree();
        _modelFetch?.Cancel();
        var source = SpireTalkConfig.Source;

        _connection.AddChild(SectionTitle(Loc.Ui("OwnKeyTitle")));

        var sources = Enum.GetValues<ApiSource>().Select(s => new ChoiceDropdown.Choice(Loc.Ui("Source" + s), s.ToString()));
        _connection.AddChild(Row(Loc.Ui("SourceLabel"), ChoiceDropdown.Create(FieldWidth, sources, source.ToString(), c =>
        {
            var next = Enum.Parse<ApiSource>(c.Value);
            if (next == SpireTalkConfig.Source) return;
            SpireTalkConfig.SwitchSource(next);
            _config.SaveDebounced(200);
            Callable.From(FillConnection).CallDeferred();
        }, out _)));

        if (source == ApiSource.Custom)
        {
            _connection.AddChild(Row(Loc.Ui("OwnBaseUrl"), _config.MakeLineEdit(nameof(SpireTalkConfig.CustomBaseUrl), width: FieldWidth)));
        }
        if (source != ApiSource.Ollama)
        {
            _connection.AddChild(Row(Loc.Ui("OwnKey"), _config.MakeLineEdit(nameof(SpireTalkConfig.OwnKey), secret: true, width: FieldWidth)));
        }
        if (source == ApiSource.OpenRouter)
        {
            var get = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
            var getLabel = GameArt.Label(Loc.Ui("GetKey"), 21, GameArt.Muted, wrap: false);
            getLabel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            getLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            get.AddChild(getLabel);
            var link = GameArt.LinkButton("OpenRouter", GameArt.Gold, 21);
            link.Pressed += () => { try { OS.ShellOpen(ApiSources.OpenRouterKeysPage); } catch { /* best effort */ } };
            get.AddChild(link);
            _connection.AddChild(get);
        }

        _connection.AddChild(Row(Loc.Ui("OwnModel"), ChoiceDropdown.Create(FieldWidth, ModelChoices(null), SpireTalkConfig.OwnModel, c =>
        {
            ShowModelId(c.Value == OtherModel);
            if (c.Value != OtherModel)
            {
                SpireTalkConfig.OwnModel = c.Value;
                _config.SaveDebounced(200);
            }
        }, out var modelDropdown)));
        _modelDropdown = modelDropdown;
        _modelIdRow = Row(Loc.Ui("ModelId"), _config.MakeLineEdit(nameof(SpireTalkConfig.OwnModel), width: FieldWidth));
        _connection.AddChild(_modelIdRow);
        ShowModelId(modelDropdown.SelectedValue == OtherModel);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 16);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        actions.AddChild(_config.MakeButton(Loc.Ui("TestBtn"), () => _ = RunOwnKeyTest(), "#4a9d7a"));
        _connection.AddChild(actions);

        _ownStatus = GameArt.Label(Loc.Ui("SourceHint" + source), 22, GameArt.Muted);
        _connection.AddChild(_ownStatus);

        // A custom endpoint has nothing to ask until its address is in; Test connection asks later.
        if (source == ApiSource.Ollama || (source == ApiSource.Custom && !string.IsNullOrWhiteSpace(SpireTalkConfig.CustomBaseUrl))) _ = LoadModels(source);
    }

    /// <summary>The model list for the current source, plus "other" for typing an id by hand.</summary>
    private static IEnumerable<ChoiceDropdown.Choice> ModelChoices(List<(string Id, string Label)>? fetched)
    {
        var list = SpireTalkConfig.Source == ApiSource.OpenRouter ? ApiSources.OpenRouterModels.ToList() : fetched ?? new();
        foreach (var (id, label) in list) yield return new ChoiceDropdown.Choice(label, id);
        yield return new ChoiceDropdown.Choice(Loc.Ui("ModelOther"), OtherModel);
    }

    private async Task LoadModels(ApiSource source)
    {
        _modelFetch = new CancellationTokenSource();
        var ct = _modelFetch.Token;
        var models = await ApiSources.ListModelsAsync(source, ct);
        if (ct.IsCancellationRequested || _modelDropdown == null || !IsInstanceValid(_modelDropdown)) return;
        // Pick the saved model if the source serves it; a fresh switch lands on the first one.
        var current = SpireTalkConfig.OwnModel;
        if (models.Count > 0 && string.IsNullOrWhiteSpace(current))
        {
            SpireTalkConfig.OwnModel = current = models[0].Id;
            _config.SaveDebounced(200);
        }
        _modelDropdown.SetChoices(ModelChoices(models), current);
        ShowModelId(_modelDropdown.SelectedValue == OtherModel);
        if (models.Count == 0 && IsInstanceValid(_ownStatus)) SetStatus(_ownStatus, Loc.Ui("ModelsUnavailable" + source), ok: false);
    }

    private void ShowModelId(bool show)
    {
        if (_modelIdRow != null && IsInstanceValid(_modelIdRow)) _modelIdRow.Visible = show;
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
        bottom.AddChild(GameArt.Spacer(0));
        var moodLabel = GameArt.Label(Loc.Ui("Temperament"), 24, GameArt.Cream, wrap: false);
        moodLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        moodLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bottom.AddChild(moodLabel);
        var mood = _config.MakeDropdown(nameof(SpireTalkConfig.Temperament));
        mood.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bottom.AddChild(mood);
        bottom.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        bottom.AddChild(_config.MakeButton(Loc.Ui("MemoryBtn"), () =>
        {
            Close();
            MemoryScreen.OpenLater(MemoryScreen.Open);
        }, "#4a7d9d"));
        var reset =_config.MakeButton(Loc.Ui("ResetMemoriesBtn"), () =>
        {
            SpireTalkConfig.ResetMemories();
            SetStatus(_ownStatus, Loc.Ui("ResetDoneBody"), ok: true);
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

    private static void SetStatus(MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel label, string text, bool ok)
    {
        if (!GodotObject.IsInstanceValid(label)) return;
        label.Text = Loc.Sanitize(text);
        label.AddThemeColorOverride("default_color", ok ? new Color("9fd8a8") : new Color("f0a0a0"));
    }

    private async Task RunOwnKeyTest()
    {
        SpireTalkConfig.StashKey();
        SetStatus(_ownStatus, Loc.Ui("Testing"), ok: true);
        var (ok, text) = await SpireTalkConfig.TestOwnKeyAsync();
        SetStatus(_ownStatus, text, ok);
        // A custom endpoint's list needs its address and key; they may have just been typed.
        if (ok && SpireTalkConfig.Source == ApiSource.Custom) _ = LoadModels(ApiSource.Custom);
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
    }
}
