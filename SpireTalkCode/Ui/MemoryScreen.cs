using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using SpireTalk.Affection;
using SpireTalk.Npc;
using SpireTalk.Store;

namespace SpireTalk.Ui;

/// <summary>
/// What each NPC remembers, laid open: the memory notes the model reads before every visit
/// (editable, deletable, and the player can add their own), the recent conversation, and a way
/// to make one NPC forget. Same parchment and slate as the settings sheet.
/// </summary>
public sealed partial class MemoryScreen : Control, IScreenContext
{
    private const float SheetWidth = 1640f;
    private const float BodyHeight = 700f;
    private const int LogLines = 20;

    private static string? _lastSelected;

    private VBoxContainer _list = null!;
    private VBoxContainer _detail = null!;
    private ScrollContainer _scroll = null!;
    private string _selected = "NEOW";
    private TextEdit? _focusAfterBuild;

    public Control? DefaultFocusedControl => null;

    public static void Open()
    {
        if (NModalContainer.Instance == null || NModalContainer.Instance.OpenModal != null) return;
        var screen = new MemoryScreen();
        screen.Build();
        NModalContainer.Instance.Add(screen, true);
    }

    private void Close(bool backToSettings)
    {
        foreach (var npc in NpcCatalog.All) TalkStore.Instance.Get(npc.Key).Memories.RemoveAll(m => string.IsNullOrWhiteSpace(m.Text) && m.Source == "player");
        TalkStore.Instance.Save();
        NModalContainer.Instance?.Clear();
        if (backToSettings) OpenLater(SpireTalkScreen.Open);
    }

    /// <summary>The modal container frees the old sheet at the end of the frame; open the next one after that.</summary>
    internal static void OpenLater(Action open)
    {
        if (Godot.Engine.GetMainLoop() is SceneTree tree) tree.CreateTimer(0.05).Timeout += open;
    }

    private void Build()
    {
        Name = "SpireTalkMemoryScreen";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _selected = _lastSelected ?? NpcCatalog.All.First().Key;

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var sheet = new PanelContainer { CustomMinimumSize = new Vector2(SheetWidth, 0) };
        sheet.AddThemeStyleboxOverride("panel", GameArt.Parchment(60, 24, 54));
        center.AddChild(sheet);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 12);
        sheet.AddChild(page);
        page.AddChild(BuildHeader());

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 18);
        page.AddChild(body);

        var left = new PanelContainer { CustomMinimumSize = new Vector2(420, BodyHeight) };
        left.AddThemeStyleboxOverride("panel", GameArt.Slate(18, 16));
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 6);
        left.AddChild(_list);
        body.AddChild(left);

        var right = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, BodyHeight) };
        right.AddThemeStyleboxOverride("panel", GameArt.Slate(30, 18));
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, BodyHeight - 36) };
        _detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _detail.AddThemeConstantOverride("separation", 10);
        _scroll.AddChild(_detail);
        right.AddChild(_scroll);
        body.AddChild(right);

        FillList();
        FillDetail();
    }

    private Control BuildHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 20);
        row.AddChild(GameArt.Icon(GameArt.MushieSitPath, 64));
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        titles.AddThemeConstantOverride("separation", 0);
        titles.AddChild(GameArt.Label("[b]" + Loc.Ui("MemoryTitle") + "[/b]", 38, GameArt.Ink, wrap: false));
        titles.AddChild(GameArt.Label(Loc.Ui("MemorySubtitle"), 23, GameArt.InkSoft, wrap: false));
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

    // ── Left: one row per NPC ────────────────────────────────────────────

    private void FillList()
    {
        foreach (var child in _list.GetChildren()) child.QueueFree();
        foreach (var npc in NpcCatalog.All)
        {
            var selected = npc.Key == _selected;
            var button = new Button { CustomMinimumSize = new Vector2(0, 70), FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var tint = selected ? new Color(1.35f, 1.3f, 1.1f) : new Color(1f, 1f, 1f, 0.75f);
            button.AddThemeStyleboxOverride("normal", NavyTinted(tint));
            button.AddThemeStyleboxOverride("hover", NavyTinted(new Color(1.3f, 1.3f, 1.25f)));
            button.AddThemeStyleboxOverride("pressed", NavyTinted(tint));
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = 14;
            row.OffsetRight = -10;
            row.AddThemeConstantOverride("separation", 12);
            row.AddChild(NpcIcon(npc, 46));
            var texts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            texts.AddThemeConstantOverride("separation", -6);
            texts.AddChild(Quiet(GameArt.Label(Loc.NpcName(npc), 25, selected ? GameArt.Gold : GameArt.Cream, wrap: false)));
            texts.AddChild(Quiet(GameArt.Label(Summary(npc), 18, GameArt.Muted, wrap: false)));
            row.AddChild(texts);
            button.AddChild(row);

            var key = npc.Key;
            button.Pressed += () =>
            {
                if (_selected == key) return;
                _selected = _lastSelected = key;
                FillList();
                FillDetail();
            };
            _list.AddChild(button);
        }
    }

    private static string Summary(NpcDefinition npc)
    {
        var (affection, married) = AffectionService.Read(npc.Key);
        var tier = Loc.EventText("tier." + ConversationRules.TierName(ConversationRules.TierOf(affection, married)));
        var talks = TalkStore.Instance.Get(npc.Key).Talks;
        return string.Format(Loc.Ui("MemoryAffection"), affection, tier) + "  ·  " + string.Format(Loc.Ui("MemoryVisits"), talks);
    }

    // ── Right: notes, recent conversation, forget ────────────────────────

    private void FillDetail()
    {
        foreach (var child in _detail.GetChildren()) child.QueueFree();
        var npc = NpcCatalog.All.First(n => n.Key == _selected);
        var store = TalkStore.Instance;
        store.SummarizeForViewing(npc.Key);
        var record = store.Get(npc.Key);
        var name = Loc.NpcName(npc);

        var title = new HBoxContainer();
        title.AddThemeConstantOverride("separation", 14);
        title.AddChild(NpcIcon(npc, 56));
        var titleText = GameArt.Label("[b]" + name + "[/b]", 34, GameArt.Gold, wrap: false);
        titleText.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        title.AddChild(titleText);
        var summary = GameArt.Label(Summary(npc), 21, GameArt.Muted, wrap: false);
        summary.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        summary.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        title.AddChild(summary);
        _detail.AddChild(title);

        // What the model reads.
        _detail.AddChild(Section(Loc.Ui("MemoryNotesTitle")));
        _detail.AddChild(GameArt.Label(string.Format(Loc.Ui("MemoryNotesHint"), name), 20, GameArt.Muted));
        var notes = record.Memories.Where(m => !string.IsNullOrWhiteSpace(m.Text) || m.Source == "player").ToList();
        if (notes.Count == 0) _detail.AddChild(GameArt.Label(Loc.Ui("MemoryNotesEmpty"), 22, GameArt.Cream));
        var runKey = CurrentRunKey();
        foreach (var note in notes) _detail.AddChild(NoteRow(npc, note, runKey));

        var add = GameArt.LinkButton(Loc.Ui("MemoryAdd"), GameArt.Gold, 23);
        add.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        add.Pressed += () =>
        {
            TalkStore.Instance.AddMemory(npc.Key, "", "player", null);
            _pendingFocusNew = true;
            FillDetail();
        };
        _detail.AddChild(add);

        _detail.AddChild(GameArt.Spacer(6));
        _detail.AddChild(GameArt.Rule(new Color(1f, 1f, 1f, 0.12f)));

        // What was said lately.
        _detail.AddChild(Section(Loc.Ui("MemoryLogTitle")));
        _detail.AddChild(GameArt.Label(Loc.Ui("MemoryLogHint"), 20, GameArt.Muted));
        var log = record.History.Where(h => !MemoryText.IsStageNote(h.Text)).TakeLast(LogLines).ToList();
        if (log.Count == 0) _detail.AddChild(GameArt.Label(Loc.Ui("MemoryLogEmpty"), 22, GameArt.Cream));
        foreach (var entry in log) _detail.AddChild(LogRow(npc, entry));

        _detail.AddChild(GameArt.Spacer(8));
        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 16);
        var status = GameArt.Label("", 21, new Color("9fd8a8"), wrap: false);
        status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var config = SpireTalkConfig.Instance;
        if (config != null)
        {
            bottom.AddChild(config.MakeButton(Loc.Ui("MemoryForget"), () =>
            {
                TalkStore.Instance.ForgetConversation(npc.Key);
                FillDetail();
                FillList();
            }, "#b03f3f"));
            bottom.AddChild(config.MakeButton(Loc.Ui("MemoryResetAffection"), () =>
            {
                Affection.AffectionService.ResetToZero(npc.Key, npc.Kind);
                FillDetail();
                FillList();
                if (_lastStatus != null && IsInstanceValid(_lastStatus)) _lastStatus.Text = Loc.Ui("MemoryResetAffectionDone");
            }, "#8a6a2e"));
        }
        bottom.AddChild(status);
        _lastStatus = status;
        _detail.AddChild(bottom);

        if (_pendingFocusNew)
        {
            _pendingFocusNew = false;
            Callable.From(() =>
            {
                if (_focusAfterBuild != null && IsInstanceValid(_focusAfterBuild)) _focusAfterBuild.GrabFocus();
                _scroll.ScrollVertical = 0;
            }).CallDeferred();
        }
    }

    private bool _pendingFocusNew;
    private MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel? _lastStatus;

    private Control NoteRow(NpcDefinition npc, TalkStore.MemoryNote note, string? runKey)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var tag = note.Source == "player"
            ? Loc.Ui("MemoryTag.player")
            : (note.Run != null && note.Run == runKey ? Loc.Ui("MemoryRunThis") : Loc.Ui("MemoryRunEarlier")) + " · " + Loc.Ui("MemoryTag." + note.Source);
        var chip = new PanelContainer { CustomMinimumSize = new Vector2(132, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        chip.AddThemeStyleboxOverride("panel", NavyTinted(TagColor(note.Source)));
        var chipText = GameArt.Label(tag, 18, GameArt.Cream, wrap: false);
        chipText.HorizontalAlignment = HorizontalAlignment.Center;
        chip.AddChild(chipText);
        row.AddChild(chip);

        var edit = new TextEdit
        {
            Text = note.Text,
            PlaceholderText = Loc.Ui("MemoryNewNote"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 50),
            ContextMenuEnabled = false,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            ScrollFitContentHeight = true,
        };
        StyleEdit(edit);
        edit.TextChanged += () => note.Text = edit.Text.Replace('\n', ' ');
        edit.FocusExited += () => TalkStore.Instance.Save();
        row.AddChild(edit);
        if (note.Source == "player" && string.IsNullOrEmpty(note.Text)) _focusAfterBuild = edit;

        var delete = GameArt.LinkButton("✕", GameArt.Muted, 24);
        delete.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        delete.Pressed += () =>
        {
            TalkStore.Instance.DeleteMemory(npc.Key, note.Id);
            TalkStore.Instance.Save();
            FillDetail();
        };
        row.AddChild(delete);
        return row;
    }

    private Control LogRow(NpcDefinition npc, TalkStore.HistoryEntry entry)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var isNpc = entry.Role == "npc";
        var who = GameArt.Label(isNpc ? Loc.NpcName(npc) : Loc.Ui("MemoryYou"), 21, isNpc ? GameArt.Gold : GameArt.Muted, wrap: false);
        who.CustomMinimumSize = new Vector2(132, 0);
        who.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        who.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        row.AddChild(who);
        var text = isNpc ? MemoryText.CleanNpcText(entry.Text).Replace("\n\n", "\n") : entry.Text;
        var body = GameArt.Label(Loc.Sanitize(text), 21, GameArt.Cream);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(body);
        var delete = GameArt.LinkButton("✕", GameArt.Muted, 22);
        delete.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        delete.Pressed += () =>
        {
            TalkStore.Instance.DeleteHistoryEntry(npc.Key, entry);
            TalkStore.Instance.Save();
            FillDetail();
        };
        row.AddChild(delete);
        return row;
    }

    // ── Bits ─────────────────────────────────────────────────────────────

    private static string? CurrentRunKey()
    {
        try
        {
            var player = SpireTalk.Merchant.MerchantTalk.ResolveLocalPlayer();
            return player != null ? SpireTalk.Rewards.RewardExecutor.RunKey(player) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Control Section(string text) => GameArt.Label("[b]" + text + "[/b]", 27, GameArt.Gold, wrap: false);

    private static T Quiet<T>(T control) where T : Control
    {
        control.MouseFilter = MouseFilterEnum.Ignore;
        return control;
    }

    private static Color TagColor(string source) => source switch
    {
        "player" => new Color(1.5f, 1.25f, 0.6f),
        "road" => new Color(1.35f, 0.95f, 0.7f),
        _ => new Color(0.85f, 1.25f, 1.25f),
    };

    private static StyleBox NavyTinted(Color tint)
    {
        var box = GameArt.Navy(14, 6);
        if (box is StyleBoxTexture t) t.ModulateColor = tint;
        return box;
    }

    private static TextureRect NpcIcon(NpcDefinition npc, float size)
    {
        var name = npc.Kind == NpcKind.Merchant ? "shop" : npc.Key.ToLowerInvariant();
        return GameArt.Icon($"res://images/ui/run_history/{name}.png", size);
    }

    private static void StyleEdit(TextEdit edit)
    {
        var normal = GameArt.Navy(16, 8);
        var focus = GameArt.Navy(16, 8);
        if (focus is StyleBoxTexture ft) ft.ModulateColor = new Color(1.35f, 1.35f, 1.3f);
        edit.AddThemeStyleboxOverride("normal", normal);
        edit.AddThemeStyleboxOverride("focus", focus);
        var font = GameArt.Font("res://themes/kreon_regular_shared.tres");
        if (font != null) edit.AddThemeFontOverride("font", font);
        edit.AddThemeFontSizeOverride("font_size", 21);
        edit.AddThemeColorOverride("font_color", GameArt.Cream);
        edit.AddThemeColorOverride("font_placeholder_color", new Color(GameArt.Cream, 0.45f));
        edit.AddThemeColorOverride("caret_color", GameArt.Cream);
    }

    // Esc closes on release, like the settings sheet, so the menu underneath never sees it.
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
            Callable.From(() => Close(backToSettings: true)).CallDeferred();
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
