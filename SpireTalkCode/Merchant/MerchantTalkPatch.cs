using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Affection;
using SpireTalk.Ancients;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;
using SpireTalk.Ui;
using SpireTalk.Yumina;

namespace SpireTalk.Merchant;

/// <summary>
/// The Merchant has no dialogue flow in vanilla. We add two things: a "Talk" button that opens
/// a conversation panel (same 3-line rules as the Ancients; the AI's first line is fetched when
/// the panel opens), and a per-visit discount that the merchant can grant as a reward.
/// </summary>
internal static class MerchantTalk
{
    public const string NpcKey = "MERCHANT";

    private static readonly MethodInfo? ShowRandomMethod = AccessTools.Method(typeof(NMerchantDialogue), "ShowRandom");
    private static readonly MethodInfo? UpdateVisualMethod = AccessTools.Method(typeof(NMerchantSlot), "UpdateVisual");
    private static readonly PropertyInfo? RunStateProperty = typeof(RunManager).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Discount granted this shop visit, applied by the price hook below.</summary>
    public static int DiscountPercent { get; private set; }
    public static bool DiscountUsedThisVisit { get; private set; }

    private static MerchantConversation? _conversation;
    private static NMerchantDialogue? _dialogueNode;
    private static NMerchantInventory? _inventoryNode;

    public static Player? ResolveLocalPlayer()
    {
        try
        {
            var run = RunManager.Instance.DebugOnlyGetState()
                ?? RunStateProperty?.GetValue(RunManager.Instance) as RunState;
            if (run != null) return LocalContext.GetMe(run);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Merchant] no local player: {ex.Message}", 1);
        }
        return null;
    }

    /// <summary>New shop visit: reset the per-visit state and start the greeting.</summary>
    public static void OnShopEntered(NMerchantDialogue? node)
    {
        _dialogueNode = node;
        DiscountPercent = 0;
        DiscountUsedThisVisit = false;
        _conversation?.Dispose();
        _conversation = null;
        if (!SpireTalkConfig.EnableMerchant) return;
        var owner = ResolveLocalPlayer();
        if (owner == null || owner.RunState.Players.Count > 1)
        {
            if (SpireTalkConfig.DebugLogging) MainFile.Logger.Info("[Merchant] shop entered but no solo local player; skipping", 1);
            return;
        }
        // The vanilla merchant already says hello; the AI only speaks once the player opens the
        // panel, so a shop visit without a chat costs nothing.
        if (SpireTalkConfig.DebugLogging) MainFile.Logger.Info("[Merchant] shop entered; talk via " + MainFile.Backend.DisplayName, 1);
        _conversation = new MerchantConversation(owner);
    }

    /// <summary>Vanilla path: the dialogue node is initialised on room entry. Just remember it; Open starts the visit.</summary>
    public static void OnDialogueInitialized(NMerchantDialogue node)
    {
        _dialogueNode = node;
        _conversation?.Dispose();
        _conversation = null;
        _inventoryNode = null;
    }

    public static void OnInventoryOpened(NMerchantInventory inventory)
    {
        if (!SpireTalkConfig.EnableMerchant) return;
        // A new inventory node means a new shop visit. Some skin mods replace the vanilla
        // Initialize path (so NMerchantDialogue.Initialize never fires); Open is the reliable hook.
        if (!ReferenceEquals(_inventoryNode, inventory) || _conversation == null)
        {
            _inventoryNode = inventory;
            var dialogue = inventory.GetNodeOrNull<NMerchantDialogue>("%Dialogue") ?? inventory.GetNodeOrNull<NMerchantDialogue>("Dialogue");
            OnShopEntered(dialogue);
        }
        if (_conversation == null) return;
        Callable.From(() => InstallTalkButton(inventory)).CallDeferred();
    }

    public static void OnInventoryClosed(NMerchantInventory inventory)
    {
        var button = inventory.GetNodeOrNull<Control>("SpireTalkMerchantButton");
        if (button != null) button.Visible = false;
        _conversation?.ClosePanel();
    }

    public static void ApplyDiscount(int percent)
    {
        DiscountPercent = Math.Clamp(percent, 0, 90);
        DiscountUsedThisVisit = true;
        RefreshPrices();
    }

    private static void RefreshPrices()
    {
        var inv = _inventoryNode;
        if (inv == null || !GodotObject.IsInstanceValid(inv) || UpdateVisualMethod == null) return;
        try
        {
            foreach (var slot in inv.GetAllSlots())
            {
                UpdateVisualMethod.Invoke(slot, null);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Merchant] price refresh failed: {ex.Message}", 1);
        }
    }

    public static void ShowBubble(string text)
    {
        var node = _dialogueNode;
        if (node == null || !GodotObject.IsInstanceValid(node) || !node.IsInsideTree() || ShowRandomMethod == null) return;
        try
        {
            ShowRandomMethod.Invoke(node, new object[] { new List<LocString> { Loc.Dynamic(text) } });
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Merchant] bubble failed: {ex.Message}", 1);
        }
    }

    private static void InstallTalkButton(NMerchantInventory inventory)
    {
        if (!GodotObject.IsInstanceValid(inventory)) return;
        var existing = inventory.GetNodeOrNull<Control>("SpireTalkMerchantButton");
        if (existing != null)
        {
            existing.Visible = true;
            return;
        }
        var button = new Button
        {
            Name = "SpireTalkMerchantButton",
            Text = Loc.EventText("merchantTalkButton"),
            CustomMinimumSize = new Vector2(220, 64),
            ZIndex = 50,
        };
        button.AddThemeFontSizeOverride("font_size", 26);
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.08f, 0.85f),
            BorderColor = new Color(1f, 0.85f, 0.45f, 0.7f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
        };
        normal.SetBorderWidthAll(2);
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(1f, 0.85f, 0.45f, 0.25f);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("focus", hover);
        button.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.7f));
        // Top-right of the shop screen, clear of the shelves and the top bar.
        button.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        button.Position = new Vector2(-260, 130);
        button.Pressed += () => _conversation?.OpenPanel();
        inventory.AddChild(button);
    }
}

/// <summary>Patches that hand shop lifecycle events to <see cref="MerchantTalk"/>.</summary>
[HarmonyPatch(typeof(NMerchantDialogue), nameof(NMerchantDialogue.Initialize))]
internal static class MerchantDialogueInitPatch
{
    [HarmonyPostfix]
    private static void OnInit(NMerchantDialogue __instance, MerchantDialogueSet dialogueSet)
    {
        try
        {
            MerchantTalk.OnDialogueInitialized(__instance);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Merchant] init hook failed: {ex.Message}", 1);
        }
    }
}

[HarmonyPatch(typeof(NMerchantInventory), "Open")]
internal static class MerchantInventoryOpenPatch
{
    [HarmonyPostfix]
    private static void OnOpen(NMerchantInventory __instance)
    {
        try
        {
            MerchantTalk.OnInventoryOpened(__instance);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Merchant] open hook failed: {ex.Message}", 1);
        }
    }
}

[HarmonyPatch(typeof(NMerchantInventory), "Close")]
internal static class MerchantInventoryClosePatch
{
    [HarmonyPostfix]
    private static void OnClose(NMerchantInventory __instance)
    {
        try
        {
            MerchantTalk.OnInventoryClosed(__instance);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Merchant] close hook failed: {ex.Message}", 1);
        }
    }
}

/// <summary>The discount reward: every ordinary price this visit is reduced by the granted percent.</summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyMerchantPrice))]
internal static class MerchantPricePatch
{
    [HarmonyPostfix]
    private static void ApplyTalkDiscount(MerchantEntry entry, ref decimal __result)
    {
        var pct = MerchantTalk.DiscountPercent;
        if (pct <= 0) return;
        if (entry is MerchantCardRemovalEntry) return;
        __result = Math.Max(0m, Math.Round(__result * (100m - pct) / 100m));
    }
}

/// <summary>One shop visit's conversation with the Merchant, rendered in <see cref="TalkPanel"/>.</summary>
internal sealed class MerchantConversation
{
    private readonly Player _owner;
    private readonly NpcDefinition _npc;
    private readonly TalkStore.NpcRecord _record;
    private readonly string _runKey;
    private VisitContext? _ctx;
    private TalkPanel? _panel;
    private bool _busy;
    private bool _greeted;
    private bool _visitBonusGiven;
    private int _linesUsed;
    private int _netDeltaThisVisit;
    private int _rewardsThisVisit;
    private string? _previousPlayerLine;
    private NpcTurn? _lastTurn;

    public MerchantConversation(Player owner)
    {
        _owner = owner;
        _npc = NpcCatalog.Get(MerchantTalk.NpcKey) ?? new NpcDefinition { Key = "MERCHANT", Name = "The Merchant", KindName = "merchant" };
        _record = TalkStore.Instance.Get(MerchantTalk.NpcKey);
        _runKey = RewardExecutor.RunKey(owner);
    }

    /// <summary>First AI line of the visit; runs when the panel is opened for the first time.</summary>
    public async Task GreetAsync()
    {
        if (_greeted) return;
        _greeted = true;
        var backend = MainFile.Backend;
        if (!backend.IsConfigured) return;
        try
        {
            if (!_visitBonusGiven)
            {
                _visitBonusGiven = true;
                AffectionService.VisitBonus(MerchantTalk.NpcKey, NpcKind.Merchant);
            }
            _ctx = BuildContext();
            _record.Talks++;
            _record.LastTalkUtc = DateTime.UtcNow.ToString("o");
            _ctx.Talks = _record.Talks;
            _ctx.SyncRelationshipVariables();
            TalkStore.Instance.Save();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var opener = BuildOpener();
            var turn = await backend.ExchangeAsync(_ctx, opener, true, cts.Token);
            _lastTurn = turn;
            TalkStore.Instance.AppendHistory(MerchantTalk.NpcKey, "player", opener, _runKey);
            if (!string.IsNullOrWhiteSpace(turn.JoinedText))
            {
                TalkStore.Instance.AppendHistory(MerchantTalk.NpcKey, "npc", turn.JoinedText, _runKey);
                _record.LastLine = turn.JoinedText.Length > 300 ? turn.JoinedText[..300] : turn.JoinedText;
            }
            TalkStore.Instance.Save();

            if (SpireTalkConfig.DebugLogging) MainFile.Logger.Info($"[Merchant] greeting shown: {turn.Lines.FirstOrDefault() ?? ""}", 1);
            if (_panel != null && GodotObject.IsInstanceValid(_panel)) Present(turn);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Merchant] greeting skipped: {ex.GetType().Name}: {ex.Message}", 1);
            _ctx = null;
            _greeted = false; // reopening the panel tries again
            // A player already staring at the "…" panel gets the reason and a way out.
            if (_panel != null && GodotObject.IsInstanceValid(_panel)) _panel.SetError(Loc.DescribeError(ex));
        }
    }

    public void OpenPanel()
    {
        if (_panel != null && GodotObject.IsInstanceValid(_panel))
        {
            _panel.Visible = true;
            return;
        }
        var title = _npc.DisplayName;
        _panel = TalkPanel.Create(title, line => _ = TurnAsync(line), ClosePanel);
        var host = (Node?)NGame.Instance;
        if (host == null) return;
        host.AddChild(_panel);
        if (!MainFile.Backend.IsConfigured)
        {
            _panel.SetError(Loc.Ui("ErrNotConfigured"));
            return;
        }
        if (!_greeted)
        {
            _panel.SetThinking();
            _ = GreetAsync();
            return;
        }
        if (_ctx == null)
        {
            _panel.SetError(Loc.EventText("silence"));
            return;
        }
        if (_lastTurn != null) Present(_lastTurn);
        else _panel.SetThinking();
    }

    public void ClosePanel()
    {
        if (_panel != null && GodotObject.IsInstanceValid(_panel))
        {
            _panel.QueueFree();
        }
        _panel = null;
    }

    public void Dispose()
    {
        ClosePanel();
    }

    private async Task TurnAsync(string playerLine)
    {
        if (_busy || _ctx == null) return;
        if (_linesUsed >= ConversationRules.LinesPerVisit) return;
        _busy = true;
        try
        {
            _linesUsed++;
            _ctx.LinesLeft = ConversationRules.LinesPerVisit - _linesUsed;
            _ctx.AllowedActions = ConversationRules.AllowedActions(NpcKind.Merchant, _ctx.Tier, RewardState());
            _ctx.SyncRelationshipVariables();
            _panel?.SetThinking();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var turn = await MainFile.Backend.ExchangeAsync(_ctx, playerLine, false, cts.Token);
            _lastTurn = turn;

            TalkStore.Instance.AppendHistory(MerchantTalk.NpcKey, "player", playerLine, _runKey);
            if (!string.IsNullOrWhiteSpace(turn.JoinedText))
            {
                TalkStore.Instance.AppendHistory(MerchantTalk.NpcKey, "npc", turn.JoinedText, _runKey);
                _record.LastLine = turn.JoinedText.Length > 300 ? turn.JoinedText[..300] : turn.JoinedText;
            }

            var delta = ConversationRules.ClampDelta(turn.AffectionDelta, _netDeltaThisVisit, playerLine, _previousPlayerLine);
            _netDeltaThisVisit += delta;
            _ctx.Affection = AffectionService.Apply(MerchantTalk.NpcKey, NpcKind.Merchant, delta);
            _previousPlayerLine = playerLine;

            var reward = ConversationRules.Validate(turn.Action, NpcKind.Merchant, _ctx.Tier, RewardState(), _owner.Creature.MaxHp);
            var summary = "none";
            if (!reward.IsNone)
            {
                if (reward.Type == "discount")
                {
                    MerchantTalk.ApplyDiscount(reward.Amount);
                    _rewardsThisVisit++;
                    summary = $"discount {reward.Amount}%";
                    RewardExecutor.Toast(string.Format(Loc.EventText("rewardDiscount"), reward.Amount));
                }
                else
                {
                    var outcome = await RewardExecutor.ApplyAsync(reward, _owner, MerchantTalk.NpcKey, _npc.DisplayName, _runKey);
                    if (outcome.Applied) _rewardsThisVisit++;
                    summary = outcome.Summary;
                }
            }
            TalkStore.Instance.Save();

            if (SpireTalkConfig.DebugLogging)
            {
                MainFile.Logger.Info($"[Merchant] line {_linesUsed}/{ConversationRules.LinesPerVisit}: delta {turn.AffectionDelta}→{delta} (now {_ctx.Affection}) action={turn.Action.Type}→{summary}", 1);
            }
            Present(turn);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Merchant] turn failed: {ex.GetType().Name}: {ex.Message}", 1);
            _panel?.SetError(Loc.DescribeError(ex));
        }
        finally
        {
            _busy = false;
        }
    }

    private void Present(NpcTurn turn)
    {
        if (_panel == null || !GodotObject.IsInstanceValid(_panel) || _ctx == null) return;
        var linesLeft = ConversationRules.LinesPerVisit - _linesUsed;
        var lines = turn.Lines.Count > 0 ? turn.Lines : new[] { Loc.EventText("silence") };
        var choices = turn.Choices.Count > 0 ? turn.Choices : new[] { Loc.EventText("defaultChoice1"), Loc.EventText("defaultChoice2"), Loc.EventText("defaultChoice3") };
        var footer = string.Format(Loc.EventText("merchantFooter"), _ctx.Affection, Loc.EventText("tier." + ConversationRules.TierName(_ctx.Tier)), linesLeft,
            MerchantTalk.DiscountPercent > 0 ? string.Format(Loc.EventText("merchantDiscountNote"), MerchantTalk.DiscountPercent) : "");
        _panel.SetTurn(lines, choices, linesLeft, SpireTalkConfig.FreeTextInput, footer);
    }

    private ConversationRules.RewardState RewardState()
    {
        return RewardExecutor.BuildState(_owner, MerchantTalk.NpcKey, _runKey, _rewardsThisVisit, rerollUsed: true, MerchantTalk.DiscountUsedThisVisit);
    }

    private VisitContext BuildContext()
    {
        var ctx = new VisitContext { NpcKey = MerchantTalk.NpcKey, Npc = _npc, Language = Loc.ReplyLanguageName(), RunKey = _runKey };
        foreach (var kv in GameContext.CollectForMerchant(_owner))
        {
            ctx.Variables[kv.Key] = kv.Value?.DeepClone();
        }
        var (affection, married) = AffectionService.Read(MerchantTalk.NpcKey);
        ctx.Affection = affection;
        ctx.Married = married;
        ctx.LinesLeft = ConversationRules.LinesPerVisit;
        ctx.AllowedActions = ConversationRules.AllowedActions(NpcKind.Merchant, ctx.Tier, RewardState());
        ctx.History.AddRange(TalkStore.Instance.HistoryOf(MerchantTalk.NpcKey, _runKey));
        ctx.Variables["affection_source"] = AffectionService.UsingAncientAffection ? "Ancient Affection mod" : "SpireTalk";
        return ctx;
    }

    private string BuildOpener()
    {
        var character = _owner.Character.Title.GetFormattedText();
        return $"[{character} walks into your shop{(_record.Talks <= 1 ? " for the first time" : $" again (visit #{_record.Talks})")}. Greet them in one or two short sentences; they may say up to {ConversationRules.LinesPerVisit} lines this visit. Reply in {Loc.ReplyLanguageName()}.]";
    }
}
