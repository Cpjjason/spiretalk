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
        if (!SpireTalkConfig.ModeNoticeSeen)
        {
            SpireTalkConfig.ModeNoticeSeen = true;
            config.SaveDebounced(200);
            MainMenuButton.RefreshDot();
        }
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

    private VBoxContainer _column = null!;

    private Control BuildWell()
    {
        var well = new PanelContainer();
        well.AddThemeStyleboxOverride("panel", GameArt.Slate(34, 16));

        _column = new VBoxContainer();
        _column.AddThemeConstantOverride("separation", 10);
        well.AddChild(_column);
        FillWell();
        return well;
    }

    /// <summary>Mode first; the AI connection only when the AI is wanted; then the conversation switches.</summary>
    private void FillWell()
    {
        foreach (var child in _column.GetChildren()) child.QueueFree();
        _modelFetch?.Cancel();

        _column.AddChild(BuildModeBlock());
        _column.AddChild(GameArt.Spacer(2));
        if (SpireTalkConfig.UseAi)
        {
            _column.AddChild(BuildOwnKeyCard());
        }
        else
        {
            // Nothing to show; the reset button still reports into this label.
            _ownStatus = GameArt.Label("", 22, GameArt.Muted);
            _ownStatus.Visible = false;
            _column.AddChild(_ownStatus);
        }

        _column.AddChild(GameArt.Spacer(4));
        _column.AddChild(GameArt.Rule(new Color(1f, 1f, 1f, 0.12f)));
        _column.AddChild(GameArt.Spacer(4));

        _column.AddChild(BuildConversationBlock());
    }

    // ── Conversation mode: the AI writes the lines, or the written script plays them ──

    private Control BuildModeBlock()
    {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 8);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 24);
        head.AddChild(SectionTitle(Loc.Ui("ModeTitle")));
        block.AddChild(head);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        row.AddChild(ModeOption(true));
        row.AddChild(ModeOption(false));
        block.AddChild(row);

        // Only worth saying when the choice is not in effect: the AI is picked but not connected.
        if (SpireTalkConfig.UseAi && !MainFile.Backend.IsConfigured)
        {
            var missing = GameArt.Label(Loc.Ui("ModeNowAiMissing"), 21, new Color("f0c080"), wrap: false);
            missing.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            head.AddChild(missing);
        }
        return block;
    }

    private Control ModeOption(bool ai)
    {
        var selected = SpireTalkConfig.UseAi == ai;
        var b = GameArt.HexButton("", 24, 64);
        b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (selected)
        {
            b.AddThemeStyleboxOverride("normal", GameArt.EventButton(new Color(1.35f, 1.3f, 1.05f)));
        }
        else
        {
            b.Modulate = new Color(1, 1, 1, 0.62f);
        }
        // One line: the name, then what it means in smaller type (the sheet is already near full height).
        var text = GameArt.Label($"[center][b]{Loc.Ui(ai ? "ModeAi" : "ModeScript")}[/b][/center]", 25, selected ? GameArt.Gold : GameArt.Cream, wrap: false);
        text.MouseFilter = MouseFilterEnum.Ignore;
        text.SetAnchorsPreset(LayoutPreset.FullRect);
        text.OffsetLeft = 30; text.OffsetRight = -24; text.OffsetTop = 14; text.OffsetBottom = -6;
        b.AddChild(text);
        b.Pressed += () =>
        {
            if (SpireTalkConfig.UseAi == ai) return;
            SpireTalkConfig.StashKey();
            SpireTalkConfig.UseAi = ai;
            _config.SaveDebounced(200);
            Callable.From(FillWell).CallDeferred();
        };
        return b;
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
        var tutorial = GameArt.LinkButton(Loc.Ui("KeyTutorialLink"), GameArt.Gold, 21);
        tutorial.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        tutorial.Alignment = HorizontalAlignment.Left;
        tutorial.Pressed += () =>
        {
            Close();
            MemoryScreen.OpenLater(() => GuideScreen.OpenKeyTutorial(backToSettings: true));
        };
        if (ApiSources.PresetOf(source) is { KeysPage: { } keysPage } preset)
        {
            var get = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
            get.AddChild(tutorial);
            var getLabel = GameArt.Label(Loc.Ui("GetKey"), 21, GameArt.Muted, wrap: false);
            getLabel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            getLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            get.AddChild(getLabel);
            var link = GameArt.LinkButton(preset.KeysSite, GameArt.Gold, 21);
            link.Pressed += () => { try { OS.ShellOpen(keysPage); } catch { /* best effort */ } };
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

        // The hint (later the test result) and the test button share a row.
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 24);
        _ownStatus = GameArt.Label(SourceHint(source), 21, GameArt.Muted);
        _ownStatus.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _ownStatus.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        actions.AddChild(_ownStatus);
        var test = _config.MakeButton(Loc.Ui("TestBtn"), () => _ = RunOwnKeyTest(), "#4a9d7a");
        test.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        actions.AddChild(test);
        _connection.AddChild(actions);

        // Ollama / custom have no "get a key" row; the tutorial link goes under the hint instead.
        if (tutorial.GetParent() == null) _connection.AddChild(tutorial);

        // A custom endpoint has nothing to ask until its address is in; Test connection asks later.
        if (ApiSources.ShouldFetch(source)) _ = LoadModels(source);
    }

    /// <summary>OpenRouter, Ollama and "other" have their own words; the provider presets share one line.</summary>
    private static string SourceHint(ApiSource source)
    {
        var hint = source is ApiSource.OpenRouter or ApiSource.Ollama or ApiSource.Custom
            ? Loc.Ui("SourceHint" + source)
            : string.Format(Loc.Ui("SourceHintPreset"), Loc.Ui("Source" + source), ApiSources.PresetOf(source)?.KeysSite ?? "");
        // The two sources with a model that costs nothing say which one.
        return source is ApiSource.Zhipu or ApiSource.Google ? hint + " " + Loc.Ui("FreeModel" + source) : hint;
    }

    /// <summary>The model list for the current source, plus "other" for typing an id by hand.</summary>
    private static IEnumerable<ChoiceDropdown.Choice> ModelChoices(List<(string Id, string Label)>? fetched)
    {
        var list = ApiSources.FixedModels(SpireTalkConfig.Source) ?? fetched ?? new();
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
        if (models.Count == 0 && IsInstanceValid(_ownStatus))
        {
            var why = source is ApiSource.Ollama or ApiSource.Custom ? Loc.Ui("ModelsUnavailable" + source) : Loc.Ui("ModelsUnavailablePreset");
            SetStatus(_ownStatus, why, ok: false);
        }
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
        // Detailed logging stays in the config file for bug reports; players never need the switch.
        // The Ancient Affection switch only means something when that mod is installed.
        var toggles = new List<(string key, string prop)>
        {
            ("EnableAncients", nameof(SpireTalkConfig.EnableAncients)),
            ("EnableMerchant", nameof(SpireTalkConfig.EnableMerchant)),
            ("FreeTextInput", nameof(SpireTalkConfig.FreeTextInput)),
            ("RememberAcrossRuns", nameof(SpireTalkConfig.RememberAcrossRuns)),
        };
        if (Affection.AncientAffectionBridge.Available) toggles.Add(("UseAncientAffection", nameof(SpireTalkConfig.UseAncientAffection)));
        foreach (var (key, prop) in toggles)
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
            if (key == "FreeTextInput" && !SpireTalkConfig.UseAi) cell.Modulate = new Color(1, 1, 1, 0.4f);
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
        block.AddChild(bottom);

        // The buttons get their own row; next to the dropdowns they pushed the sheet off-screen.
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 20);
        buttons.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        bottom = buttons;
        bottom.AddChild(_config.MakeButton(Loc.Ui("GuideBtn"), () =>
        {
            Close();
            MemoryScreen.OpenLater(GuideScreen.OpenTour);
        }, "#7d6a4a"));
        bottom.AddChild(_config.MakeButton(Loc.Ui("BookBtn"), () =>
        {
            Close();
            MemoryScreen.OpenLater(KeepsakeBook.Open);
        }, "#8a6a2e"));
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
        var dot = GameArt.Label("·", 21, GameArt.InkSoft, wrap: false);
        dot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(dot);
        var site = GameArt.LinkButton("yumina.io", GameArt.Ink, 21);
        site.Pressed += () => { try { OS.ShellOpen("https://yumina.io"); } catch { /* best effort */ } };
        row.AddChild(site);
        return row;
    }

    // ── Behaviour ────────────────────────────────────────────────────────

    private static void SetStatus(MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel label, string text, bool ok)
    {
        if (!GodotObject.IsInstanceValid(label)) return;
        label.Visible = true;
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
        if (ok && ApiSources.ShouldFetch(SpireTalkConfig.Source)) _ = LoadModels(SpireTalkConfig.Source);
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
