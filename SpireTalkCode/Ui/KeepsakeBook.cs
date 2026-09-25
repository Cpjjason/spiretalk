using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using SpireTalk.Affection;
using SpireTalk.Keepsakes;
using SpireTalk.Npc;
using SpireTalk.Store;

namespace SpireTalk.Ui;

/// <summary>
/// The keepsake book: the nine keepsakes on this profile, each at the best level it ever reached.
/// Never received shows as a dark silhouette; a vow gets a gold frame and the day it happened.
/// Same parchment and slate as the settings sheet.
/// </summary>
public sealed partial class KeepsakeBook : Control, IScreenContext
{
    private const float SheetWidth = 1500f;
    private const float CardWidth = 440f;
    private const float Portrait = 150f;
    private static readonly string[] Stages = { "I", "II", "III", "" };

    public Control? DefaultFocusedControl => null;

    public static void Open()
    {
        if (NModalContainer.Instance == null || NModalContainer.Instance.OpenModal != null) return;
        var screen = new KeepsakeBook();
        screen.Build();
        NModalContainer.Instance.Add(screen, true);
    }

    private void Close(bool backToSettings)
    {
        NModalContainer.Instance?.Clear();
        if (backToSettings) MemoryScreen.OpenLater(SpireTalkScreen.Open);
    }

    private void Build()
    {
        Name = "SpireTalkKeepsakeBook";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var sheet = new PanelContainer { CustomMinimumSize = new Vector2(SheetWidth, 0) };
        sheet.AddThemeStyleboxOverride("panel", GameArt.Parchment(60, 24, 50));
        center.AddChild(sheet);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 14);
        sheet.AddChild(page);

        var npcs = NpcCatalog.All.ToList();
        var got = npcs.Count(n => TalkStore.Instance.Get(n.Key).BestKeepsakeLevel > 0);
        var vows = npcs.Count(n => TalkStore.Instance.Get(n.Key).BestKeepsakeLevel >= KeepsakeRelic.MaxLevel);
        page.AddChild(BuildHeader(string.Format(Loc.Ui("BookSubtitle"), got, npcs.Count, vows)));

        var well = new PanelContainer();
        well.AddThemeStyleboxOverride("panel", GameArt.Slate(26, 20));
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 18);
        foreach (var npc in npcs) grid.AddChild(Card(npc));
        well.AddChild(grid);
        page.AddChild(well);
    }

    private Control BuildHeader(string subtitle)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 20);
        row.AddChild(GameArt.Icon(GameArt.MushieSitPath, 64));
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        titles.AddThemeConstantOverride("separation", 0);
        titles.AddChild(GameArt.Label("[b]" + Loc.Ui("BookTitle") + "[/b]", 38, GameArt.Ink, wrap: false));
        titles.AddChild(GameArt.Label(subtitle, 23, GameArt.InkSoft, wrap: false));
        row.AddChild(titles);
        var back = GameArt.LinkButton(Loc.Ui("MemoryBack"), GameArt.Ink, 24);
        back.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        back.Pressed += () => Close(backToSettings: true);
        row.AddChild(back);
        var close = GameArt.LinkButton("✕", GameArt.Ink, 34);
        close.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        close.Pressed += () => Close(backToSettings: false);
        row.AddChild(close);
        return row;
    }

    private static Control Card(NpcDefinition npc)
    {
        var record = TalkStore.Instance.Get(npc.Key);
        var best = Math.Clamp(record.BestKeepsakeLevel, 0, KeepsakeRelic.MaxLevel);
        var vow = best >= KeepsakeRelic.MaxLevel;
        var merchant = npc.Kind == NpcKind.Merchant;

        var card = new PanelContainer { CustomMinimumSize = new Vector2(CardWidth, 0) };
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.28f),
            BorderColor = vow ? GameArt.GoldDeep : new Color(1f, 1f, 1f, 0.08f),
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12,
        };
        box.SetBorderWidthAll(vow ? 3 : 1);
        box.SetCornerRadiusAll(6);
        card.AddThemeStyleboxOverride("panel", box);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        card.AddChild(row);

        var icon = Tokens.RelicArt.Big($"keepsake_{npc.Key.ToLowerInvariant()}_{Math.Max(1, best)}", merchant);
        var pic = GameArt.Icon(icon ?? GameArt.MushieSitPath, Portrait);
        if (best == 0) pic.SelfModulate = new Color(0.05f, 0.05f, 0.07f, 0.9f);
        pic.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(pic);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        text.AddThemeConstantOverride("separation", 6);
        row.AddChild(text);

        var name = best == 0 ? "???" : (vow ? KeepsakeVowName(npc) : KeepsakeService.KeepsakeName(npc.Key));
        text.AddChild(GameArt.Label("[b]" + name + "[/b]", 26, vow ? GameArt.Gold : GameArt.Cream));
        text.AddChild(GameArt.Label(npc.DisplayName, 20, GameArt.Muted, wrap: false));

        // I · II · III · vow, the reached ones lit
        var stages = string.Join("  ", Enumerable.Range(1, KeepsakeRelic.MaxLevel).Select(i =>
        {
            var label = i == KeepsakeRelic.MaxLevel ? Loc.Ui("BookVow") : Stages[i - 1];
            return i <= best ? $"[color=#f0d68c]{label}[/color]" : $"[color=#ffffff40]{label}[/color]";
        }));
        text.AddChild(GameArt.Label(stages, 22, GameArt.Cream, wrap: false));

        string status;
        if (best == 0)
        {
            status = Loc.Ui("BookNotYet");
        }
        else if (vow && DateTime.TryParse(record.VowUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var when))
        {
            status = string.Format(Loc.Ui("BookVowOn"), when.ToLocalTime().ToString("yyyy-MM-dd"));
        }
        else
        {
            var (affection, married) = AffectionService.Read(npc.Key);
            status = Loc.EventText("tier." + ConversationRules.TierName(ConversationRules.TierOf(affection, married)));
        }
        var engr = Math.Min(KeepsakeRelic.MaxEngravings, record.Engravings);
        if (best > 0 && engr > 0) status += "   " + string.Format(Loc.Ui("BookEngravings"), engr, KeepsakeRelic.MaxEngravings);
        text.AddChild(GameArt.Label(status, 19, GameArt.Muted, wrap: false));
        return card;
    }

    private static string KeepsakeVowName(NpcDefinition npc)
    {
        try
        {
            var type = Type.GetType($"SpireTalk.Keepsakes.Keepsake{char.ToUpperInvariant(npc.Key[0])}{npc.Key[1..].ToLowerInvariant()}");
            if (type == null) return KeepsakeService.KeepsakeName(npc.Key);
            var id = MegaCrit.Sts2.Core.Models.ModelDb.GetId(type);
            return new MegaCrit.Sts2.Core.Localization.LocString("relics", id.Entry + ".vowTitle").GetFormattedText();
        }
        catch
        {
            return KeepsakeService.KeepsakeName(npc.Key);
        }
    }

    // Esc closes on the release, like the other sheets.
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
            CallDeferred(nameof(CloseOnly));
        }
    }

    private void CloseOnly() => Close(backToSettings: false);

    public override void _EnterTree()
    {
        try { NHotkeyManager.Instance?.AddBlockingScreen(this); } catch { /* optional */ }
    }

    public override void _ExitTree()
    {
        try { NHotkeyManager.Instance?.RemoveBlockingScreen(this); } catch { /* optional */ }
    }
}
