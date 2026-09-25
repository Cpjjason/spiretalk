using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace SpireTalk.Ui;

/// <summary>
/// How to play: a few short pages on what the mod adds and where to find it, on the same parchment
/// as the settings sheet. Opens by itself the first time the main menu shows after install, and
/// from the settings sheet any time after. The first page ends in a button to the AI setup, since
/// nothing talks until that is done.
/// </summary>
public sealed partial class GuideScreen : Control, IScreenContext
{
    private const float SheetWidth = 1380f;
    private const float PictureSize = 300f;
    private static readonly string[] Pages = { "welcome", "freekey", "talk", "deals", "keepsakes", "road", "memory" };

    /// <summary>The picture beside each page: the mod's own chibi icons (keepsake art falls back to the mushie).</summary>
    private static readonly Dictionary<string, string> Pictures = new()
    {
        ["welcome"] = "keepsake_neow_3",
        ["talk"] = "keepsake_neow_2",
        ["deals"] = "deal_neow",
        ["keepsakes"] = "keepsake_pael_3",
        ["road"] = "merchant_favor",
        ["memory"] = "keepsake_orobas_2",
    };

    private int _page;
    private VBoxContainer _body = null!;
    private HBoxContainer _dots = null!;
    private Button _prev = null!;
    private Button _next = null!;

    public Control? DefaultFocusedControl => null;

    public static void Open(int page = 0)
    {
        if (NModalContainer.Instance == null || NModalContainer.Instance.OpenModal != null) return;
        var screen = new GuideScreen { _page = Math.Clamp(page, 0, Pages.Length - 1) };
        screen.Build();
        NModalContainer.Instance.Add(screen, true);
        if (!SpireTalkConfig.GuideSeen)
        {
            SpireTalkConfig.GuideSeen = true;
            SpireTalkConfig.Instance?.SaveDebounced(200);
        }
    }

    /// <summary>First main menu after install: show the guide once, when nothing else is on screen.</summary>
    public static void OpenOnceWhenIdle()
    {
        if (SpireTalkConfig.GuideSeen) return;
        if (Godot.Engine.GetMainLoop() is not SceneTree tree) return;
        tree.CreateTimer(1.2).Timeout += () =>
        {
            if (SpireTalkConfig.GuideSeen) return;
            if (NModalContainer.Instance?.OpenModal != null) return;
            Open();
        };
    }

    private void Close() => NModalContainer.Instance?.Clear();

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

        var nav = new HBoxContainer();
        nav.AddThemeConstantOverride("separation", 18);
        _prev = GameArt.HexButton(Loc.Ui("GuidePrev"), 24, 60);
        _prev.CustomMinimumSize = new Vector2(220, 60);
        _prev.Pressed += () => Go(_page - 1);
        nav.AddChild(_prev);
        nav.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _dots = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _dots.AddThemeConstantOverride("separation", 12);
        nav.AddChild(_dots);
        nav.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _next = GameArt.HexButton(Loc.Ui("GuideNext"), 24, 60);
        _next.CustomMinimumSize = new Vector2(220, 60);
        _next.Pressed += () =>
        {
            if (_page >= Pages.Length - 1) Close();
            else Go(_page + 1);
        };
        nav.AddChild(_next);
        page.AddChild(nav);

        Fill();
    }

    private Control BuildHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 20);
        row.AddChild(GameArt.Icon(GameArt.MushieSitPath, 64));
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        titles.AddThemeConstantOverride("separation", 0);
        titles.AddChild(GameArt.Label("[b]" + Loc.Ui("GuideTitle") + "[/b]", 38, GameArt.Ink, wrap: false));
        var subtitle = Loc.Ui("GuideSubtitle");
        if (!string.IsNullOrWhiteSpace(subtitle) && subtitle != "GuideSubtitle") titles.AddChild(GameArt.Label(subtitle, 23, GameArt.InkSoft, wrap: false));
        row.AddChild(titles);
        var close = GameArt.LinkButton("✕", GameArt.Ink, 34);
        close.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        close.Pressed += Close;
        row.AddChild(close);
        return row;
    }

    private void Go(int page)
    {
        _page = Math.Clamp(page, 0, Pages.Length - 1);
        Fill();
    }

    private void Fill()
    {
        foreach (var child in _body.GetChildren()) child.QueueFree();
        var key = Pages[_page];

        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 44);
        _body.AddChild(row);

        var picture = PictureFor(key);
        picture.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(picture);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        text.AddThemeConstantOverride("separation", 18);
        text.AddChild(GameArt.Label("[b]" + Loc.Ui($"Guide.{key}.title") + "[/b]", 36, GameArt.Gold));
        foreach (var para in Loc.Ui($"Guide.{key}.body").Split("\n\n"))
        {
            text.AddChild(GameArt.Label(para.Trim(), 25, GameArt.Cream));
        }
        if (key is "welcome" or "freekey")
        {
            var setup = GameArt.HexButton(Loc.Ui("GuideSetup"), 24, 60);
            setup.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            setup.CustomMinimumSize = new Vector2(300, 60);
            setup.Pressed += () =>
            {
                Close();
                MemoryScreen.OpenLater(SpireTalkScreen.Open);
            };
            text.AddChild(setup);
        }
        row.AddChild(text);

        foreach (var child in _dots.GetChildren()) child.QueueFree();
        for (var i = 0; i < Pages.Length; i++)
        {
            var dot = new ColorRect
            {
                CustomMinimumSize = new Vector2(14, 14),
                Color = i == _page ? GameArt.GoldDeep : new Color(GameArt.Ink, 0.35f),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _dots.AddChild(dot);
        }
        _prev.Visible = _page > 0;
        _next.Text = _page >= Pages.Length - 1 ? Loc.Ui("GuideDone") : Loc.Ui("GuideNext");
    }

    private static Control PictureFor(string key)
    {
        var name = Pictures.GetValueOrDefault(key, "");
        var path = Tokens.RelicArt.Icon(name, merchant: name.StartsWith("merchant")) ?? GameArt.MushieSitPath;
        var tex = GameArt.Tex(path);
        return new TextureRect
        {
            Texture = tex,
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
            CallDeferred(nameof(Close));
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
