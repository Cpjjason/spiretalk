using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using SpireTalk.Backend;

namespace SpireTalk.Ui;

/// <summary>
/// The mod's guide, on the same parchment as the settings sheet. Three ways in:
///  - the welcome (first main menu after install): what the mod is, then one question — do you
///    have an AI key, do you want help getting a free one, or play with the written lines;
///  - the key tutorial: pick where you are, follow the steps, paste the key right here and test it;
///  - the tour: four short pages on what to look for in a run (also the settings' "How to play").
/// </summary>
public sealed partial class GuideScreen : Control, IScreenContext
{
    private const float SheetWidth = 1380f;
    private const float PictureSize = 300f;
    private static readonly string[] TourPages = { "talk", "deals", "keepsakes", "road" };

    /// <summary>The picture beside each page: the mod's own chibi icons (keepsake art falls back to the mushie).</summary>
    private static readonly Dictionary<string, string> Pictures = new()
    {
        ["welcome"] = "keepsake_neow_3",
        ["talk"] = "keepsake_darv_2",
        ["deals"] = "deal_neow",
        ["keepsakes"] = "keepsake_pael_3",
        ["road"] = "merchant_favor",
    };

    private List<string> _pages = new();
    private int _page;
    private bool _backToSettings;
    private VBoxContainer _body = null!;
    private HBoxContainer _nav = null!;
    private HBoxContainer _dots = null!;
    private Button _prev = null!;
    private Button _next = null!;

    public Control? DefaultFocusedControl => null;

    /// <summary>First main menu after install: the welcome with its three choices.</summary>
    public static void OpenWelcome()
    {
        SpireTalkConfig.GuideSeen = true;
        SpireTalkConfig.ModeNoticeSeen = true;
        SpireTalkConfig.Instance?.SaveDebounced(200);
        MainMenuButton.RefreshDot();
        Show(new List<string> { "welcome" }, false);
    }

    /// <summary>Settings → How to play: the four tour pages.</summary>
    public static void OpenTour() => Show(TourPages.ToList(), false);

    /// <summary>Getting a free key, then back to the settings (from there) or on into the tour (from the welcome).</summary>
    public static void OpenKeyTutorial(bool backToSettings) => Show(new List<string> { "keyregion" }, backToSettings);

    private static void Show(List<string> pages, bool backToSettings)
    {
        if (NModalContainer.Instance == null || NModalContainer.Instance.OpenModal != null) return;
        var screen = new GuideScreen { _pages = pages, _backToSettings = backToSettings };
        screen.Build();
        NModalContainer.Instance.Add(screen, true);
    }

    /// <summary>First main menu after install: show the welcome once, when nothing else is on screen.</summary>
    public static void OpenOnceWhenIdle()
    {
        if (SpireTalkConfig.GuideSeen) return;
        if (Godot.Engine.GetMainLoop() is not SceneTree tree) return;
        tree.CreateTimer(1.2).Timeout += () =>
        {
            if (SpireTalkConfig.GuideSeen) return;
            if (NModalContainer.Instance?.OpenModal != null) return;
            OpenWelcome();
        };
    }

    private void Close() => NModalContainer.Instance?.Clear();

    private void Finish()
    {
        Close();
        if (_backToSettings) MemoryScreen.OpenLater(SpireTalkScreen.Open);
    }

    private void Build()
    {
        Name = "SpireTalkGuideScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var sheet = new PanelContainer { CustomMinimumSize = new Vector2(SheetWidth, 0) };
        sheet.AddThemeStyleboxOverride("panel", GameArt.Parchment(68, 26, 58));
        center.AddChild(sheet);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 16);
        sheet.AddChild(page);
        page.AddChild(BuildHeader());

        var well = new PanelContainer { CustomMinimumSize = new Vector2(0, 430) };
        well.AddThemeStyleboxOverride("panel", GameArt.Slate(40, 30));
        _body = new VBoxContainer();
        well.AddChild(_body);
        page.AddChild(well);

        _nav = new HBoxContainer();
        _nav.AddThemeConstantOverride("separation", 18);
        _prev = GameArt.HexButton(Loc.Ui("GuidePrev"), 24, 60);
        _prev.CustomMinimumSize = new Vector2(220, 60);
        _prev.Pressed += () => Go(_page - 1);
        _nav.AddChild(_prev);
        _nav.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _dots = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _dots.AddThemeConstantOverride("separation", 12);
        _nav.AddChild(_dots);
        _nav.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _next = GameArt.HexButton(Loc.Ui("GuideNext"), 24, 60);
        _next.CustomMinimumSize = new Vector2(220, 60);
        _next.Pressed += Next;
        _nav.AddChild(_next);
        page.AddChild(_nav);

        Fill();
    }

    private Control BuildHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 20);
        row.AddChild(GameArt.Icon(GameArt.MushieSitPath, 64));
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        titles.AddThemeConstantOverride("separation", 0);
        // The welcome introduces the mod itself; every other page is the guide.
        var heading = _pages.FirstOrDefault() == "welcome" ? Loc.Ui("ScreenTitle") : Loc.Ui("GuideTitle");
        titles.AddChild(GameArt.Label("[b]" + heading + "[/b]", 38, GameArt.Ink, wrap: false));
        row.AddChild(titles);
        var close = GameArt.LinkButton("✕", GameArt.Ink, 34);
        close.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        close.Pressed += Finish;
        row.AddChild(close);
        return row;
    }

    private void Go(int page)
    {
        _page = Math.Clamp(page, 0, _pages.Count - 1);
        Fill();
    }

    private void Next()
    {
        if (_page < _pages.Count - 1) Go(_page + 1);
        else Finish();
    }

    /// <summary>Swap the rest of the flow for another one (a choice on the welcome or region page).</summary>
    private void Continue(IEnumerable<string> pages)
    {
        _pages = _pages.Take(_page + 1).Concat(pages).ToList();
        Go(_page + 1);
    }

    private void Fill()
    {
        foreach (var child in _body.GetChildren()) child.QueueFree();
        var key = _pages[_page];

        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 44);
        _body.AddChild(row);

        var picture = PictureFor(key);
        picture.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(picture);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        text.AddThemeConstantOverride("separation", 16);
        row.AddChild(text);

        switch (key)
        {
            case "welcome":
                FillWelcome(text);
                break;
            case "keyregion":
                FillRegion(text);
                break;
            case "keyentry":
                FillKeyEntry(text);
                break;
            default:
                AddTitleAndBody(text, key);
                break;
        }

        // The question pages are answered with their own buttons; the nav row is for reading pages.
        var question = key is "welcome" or "keyregion";
        _nav.Visible = !question;
        foreach (var child in _dots.GetChildren()) child.QueueFree();
        var readable = _pages.Where(p => p is not ("welcome" or "keyregion")).ToList();
        var at = readable.IndexOf(key);
        for (var i = 0; i < readable.Count && readable.Count > 1; i++)
        {
            _dots.AddChild(new ColorRect
            {
                CustomMinimumSize = new Vector2(14, 14),
                Color = i == at ? GameArt.GoldDeep : new Color(GameArt.Ink, 0.35f),
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }
        _prev.Visible = _page > 0 && _pages[_page - 1] is not ("welcome" or "keyregion");
        _next.Text = _page >= _pages.Count - 1 ? Loc.Ui(_backToSettings ? "GuideBackToSettings" : "GuideDone") : Loc.Ui("GuideNext");
    }

    private static void AddTitleAndBody(VBoxContainer text, string key)
    {
        text.AddChild(GameArt.Label("[b]" + Loc.Ui($"Guide.{key}.title") + "[/b]", 36, GameArt.Gold));
        foreach (var para in Loc.Ui($"Guide.{key}.body").Split("\n\n"))
        {
            text.AddChild(GameArt.Label(para.Trim(), 25, GameArt.Cream));
        }
    }

    // ── Welcome: one question, three answers ─────────────────────────────

    private void FillWelcome(VBoxContainer text)
    {
        AddTitleAndBody(text, "welcome");
        text.AddChild(GameArt.Label("[b]" + Loc.Ui("WelcomeQuestion") + "[/b]", 27, GameArt.Gold));

        text.AddChild(Choice(1, "WelcomeHaveKey", () =>
        {
            SpireTalkConfig.UseAi = true;
            SpireTalkConfig.Instance?.SaveDebounced(200);
            Close();
            MemoryScreen.OpenLater(SpireTalkScreen.Open);
        }));
        text.AddChild(Choice(2, "WelcomeTeachMe", () =>
        {
            SpireTalkConfig.UseAi = true;
            SpireTalkConfig.Instance?.SaveDebounced(200);
            Continue(new[] { "keyregion" });
        }));
        text.AddChild(Choice(3, "WelcomeNoAi", () =>
        {
            SpireTalkConfig.UseAi = false;
            SpireTalkConfig.Instance?.SaveDebounced(200);
            Continue(TourPages);
        }));

        var foot = new HBoxContainer();
        var cost = GameArt.Label(Loc.Ui("WelcomeCost"), 20, GameArt.Muted);
        cost.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foot.AddChild(cost);
        var later = GameArt.LinkButton(Loc.Ui("WelcomeLater"), GameArt.Cream, 22);
        later.Pressed += Close;
        foot.AddChild(later);
        text.AddChild(foot);
    }

    /// <summary>An event-option style button: number, gold title, a line of explanation under it.</summary>
    private static Button Choice(int number, string key, Action onPressed)
    {
        var b = GameArt.HexButton("", 24, 66);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 30; row.OffsetRight = -30; row.OffsetTop = 6; row.OffsetBottom = -6;
        row.AddThemeConstantOverride("separation", 16);
        var n = GameArt.Label("[b]" + number + "[/b]", 26, GameArt.Gold, wrap: false);
        n.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        n.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        n.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(n);
        var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", -4);
        var title = GameArt.Label("[b]" + Loc.Ui(key) + "[/b]", 24, GameArt.Gold, wrap: false);
        title.MouseFilter = MouseFilterEnum.Ignore;
        lines.AddChild(title);
        var descText = Loc.Ui(key + "Desc");
        if (descText.Length > 0 && descText != key + "Desc")
        {
            var desc = GameArt.Label(descText, 20, GameArt.Cream, wrap: false);
            desc.MouseFilter = MouseFilterEnum.Ignore;
            lines.AddChild(desc);
        }
        row.AddChild(lines);
        b.AddChild(row);
        b.Pressed += onPressed;
        return b;
    }

    // ── Key tutorial: where are you, then the steps with the key box right here ──

    private void FillRegion(VBoxContainer text)
    {
        AddTitleAndBody(text, "keyregion");
        text.AddChild(Choice(1, "RegionChina", () => PickRegion(ApiSource.Zhipu)));
        text.AddChild(Choice(2, "RegionAbroad", () => PickRegion(ApiSource.Google)));
    }

    private void PickRegion(ApiSource source)
    {
        SpireTalkConfig.EnsureSource();
        SpireTalkConfig.SwitchSource(source);
        SpireTalkConfig.Instance?.SaveDebounced(200);
        var rest = new List<string> { "keyentry" };
        if (!_backToSettings) rest.AddRange(TourPages);
        Continue(rest);
    }

    private void FillKeyEntry(VBoxContainer text)
    {
        var source = SpireTalkConfig.Source;
        var region = source == ApiSource.Zhipu ? "China" : "Abroad";
        text.AddChild(GameArt.Label("[b]" + Loc.Ui("KeySteps" + region + "Title") + "[/b]", 34, GameArt.Gold));
        text.AddChild(GameArt.Label(Loc.Ui("KeySteps" + region), 23, GameArt.Cream));

        var preset = ApiSources.PresetOf(source);
        if (preset?.KeysPage is { } page)
        {
            var open = GameArt.LinkButton(Loc.Ui("KeyOpenPage") + "  " + page.Replace("https://", ""), GameArt.Gold, 22);
            open.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            open.Pressed += () => { try { OS.ShellOpen(page); } catch { /* best effort */ } };
            text.AddChild(open);
        }

        var entry = new HBoxContainer();
        entry.AddThemeConstantOverride("separation", 16);
        var box = SpireTalkConfig.Instance!.MakeLineEdit(nameof(SpireTalkConfig.OwnKey), secret: true, width: 560f);
        box.PlaceholderText = Loc.Ui("KeyPastePlaceholder");
        entry.AddChild(box);
        var status = GameArt.Label("", 22, GameArt.Muted);
        entry.AddChild(SpireTalkConfig.Instance.MakeButton(Loc.Ui("TestBtn"), () => _ = TestKey(source, status), "#4a9d7a"));
        text.AddChild(entry);
        text.AddChild(status);
    }

    /// <summary>
    /// Ask the provider for its models with the pasted key: a list means the key works. Then pick the
    /// free model on that list, so the player never has to know its name.
    /// </summary>
    private async Task TestKey(ApiSource source, MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel status)
    {
        SpireTalkConfig.StashKey();
        Status(status, Loc.Ui("Testing"), ok: true);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        List<(string Id, string Label)> models;
        try
        {
            models = await ApiSources.ListModelsAsync(source, cts.Token);
        }
        catch
        {
            models = new();
        }
        if (!IsInstanceValid(status)) return;
        if (models.Count == 0)
        {
            Status(status, Loc.Ui("KeyTestFailed"), ok: false);
            return;
        }
        SpireTalkConfig.OwnModel = FreeModel(source, models);
        SpireTalkConfig.UseAi = true;
        SpireTalkConfig.StashKey();
        SpireTalkConfig.Instance?.SaveDebounced(200);
        Status(status, string.Format(Loc.Ui("KeyTestOk"), SpireTalkConfig.OwnModel), ok: true);
    }

    /// <summary>The free model each tutorial source offers, by the names it has gone by; else the first "flash" model listed.</summary>
    private static string FreeModel(ApiSource source, List<(string Id, string Label)> models)
    {
        string[] preferred = source == ApiSource.Zhipu
            ? new[] { "glm-4.7-flash", "glm-4-flash-250414", "glm-4-flash" }
            : new[] { "gemini-3.8-flash", "gemini-3.5-flash", "gemini-2.5-flash" };
        var ids = models.Select(m => m.Id.Replace("models/", "")).ToList();
        foreach (var id in preferred)
        {
            if (ids.Contains(id)) return id;
        }
        return ids.FirstOrDefault(id => id.Contains("flash") && !id.Contains("image") && !id.Contains("tts") && !id.Contains("live")) ?? ids[0];
    }

    private static void Status(MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel label, string text, bool ok)
    {
        if (!IsInstanceValid(label)) return;
        label.Text = Loc.Sanitize(text);
        label.AddThemeColorOverride("default_color", ok ? new Color("9fd8a8") : new Color("f0a0a0"));
    }

    private static Control PictureFor(string key)
    {
        var name = key switch
        {
            "keyregion" or "keyentry" => "",
            _ => Pictures.GetValueOrDefault(key, ""),
        };
        var path = name.Length == 0
            ? (key == "keyentry" ? GameArt.MushieCoinPath : GameArt.MushieSitPath)
            : Tokens.RelicArt.Icon(name, merchant: name.StartsWith("merchant")) ?? GameArt.MushieSitPath;
        return new TextureRect
        {
            Texture = GameArt.Tex(path),
            CustomMinimumSize = new Vector2(PictureSize, PictureSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
    }

    // Esc closes on the release, like the settings sheet, so the menu underneath never sees it.
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
            CallDeferred(nameof(Finish));
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
