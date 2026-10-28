using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Affection;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;

namespace SpireTalk.Deals;

/// <summary>
/// Deals: an NPC asks something of the climber, the climber accepts or declines, the game settles it.
///
/// The NPC never invents the terms. The mod picks a deal from the templates the NPC's npcs.json
/// entry allows, sized by the current run and the temperament setting; the AI only voices it in
/// character and later reacts to the outcome. Settlement happens on game hooks (entering an act,
/// winning a fight, drinking a potion, removing a card, buying at the shop) — the NPC does not need
/// to be present. Keeping a deal earns affection and gold; breaking one costs nothing beyond the
/// NPC's disappointment and no further deals that run.
/// </summary>
public static class DealService
{
    public const int MaxDealsPerRun = 3;
    public const int KeptAffection = 6;
    public const int KeptGold = 50;
    private const int MinSpendDeal = 60;

    public enum DealType
    {
        ReachNextAct,
        KillElite,
        HpAtLeast,
        GoldAtLeast,
        NoPotion,
        NoCardRemoval,
        SpendAtShop,
    }

    /// <summary>A deal the NPC is about to propose; becomes a <see cref="TalkStore.DealRecord"/> when accepted.</summary>
    public sealed record Offer(string Npc, DealType Type, int Param, int TargetAct, string RunKey, int MadeAct)
    {
        public string Key => TypeKey(Type);
        /// <summary>Player-facing, in the game's language.</summary>
        public string Describe() => DealService.Describe(Type, Param, TargetAct);
        /// <summary>For the prompt: plain English, the model rephrases it in character and in the reply language.</summary>
        public string PromptText() => DealService.PromptText(Type, Param, TargetAct);
    }

    public static string TypeKey(DealType t) => t switch
    {
        DealType.ReachNextAct => "reach_next_act",
        DealType.KillElite => "kill_elite",
        DealType.HpAtLeast => "hp_at_least",
        DealType.GoldAtLeast => "gold_at_least",
        DealType.NoPotion => "no_potion",
        DealType.NoCardRemoval => "no_card_removal",
        DealType.SpendAtShop => "spend_at_shop",
        _ => "unknown",
    };

    public static DealType? ParseType(string key) => key switch
    {
        "reach_next_act" => DealType.ReachNextAct,
        "kill_elite" => DealType.KillElite,
        "hp_at_least" => DealType.HpAtLeast,
        "gold_at_least" => DealType.GoldAtLeast,
        "no_potion" => DealType.NoPotion,
        "no_card_removal" => DealType.NoCardRemoval,
        "spend_at_shop" => DealType.SpendAtShop,
        _ => null,
    };

    // ── Picking ──────────────────────────────────────────────────────────

    /// <summary>
    /// The deal this NPC proposes on this visit, or null when none fits: hostile climber, a deal
    /// already running with this NPC, three already made this run, a broken one this run, or the
    /// final act (act-scoped deals need a next act to settle in).
    /// </summary>
    public static Offer? PickOffer(NpcDefinition npc, Player player, ConversationRules.Tier tier, string runKey)
    {
        if (tier <= ConversationRules.Tier.Hostile) return null;
        var store = TalkStore.Instance;
        var runDeals = store.Deals.Where(d => d.Run == runKey).ToList();
        if (runDeals.Count >= MaxDealsPerRun) return null;
        if (runDeals.Any(d => d.Npc == npc.Key && (d.Status == "pending" || d.Status == "broken"))) return null;

        IRunState run;
        int act, actCount;
        try
        {
            run = player.RunState;
            act = run.CurrentActIndex;
            actCount = run.Acts.Count;
        }
        catch
        {
            return null;
        }
        if (run.Players.Count > 1) return null;
        var lastAct = act >= actCount - 1;

        var gold = SafeInt(() => player.Gold, 0);
        var allowed = npc.Deals.Select(ParseType).Where(t => t.HasValue).Select(t => t!.Value)
            .Where(t => t != DealType.SpendAtShop || gold >= MinSpendDeal).ToList();
        if (allowed.Count == 0) return null;
        var lastType = store.Get(npc.Key).LastDealType;
        var usedThisRun = runDeals.Select(d => d.Type).ToHashSet();
        var candidates = allowed.Where(t => !lastAct || t == DealType.SpendAtShop)
            .Where(t => TypeKey(t) != lastType && !usedThisRun.Contains(TypeKey(t))).ToList();
        if (candidates.Count == 0) candidates = allowed.Where(t => !lastAct || t == DealType.SpendAtShop).Where(t => TypeKey(t) != lastType).ToList();
        if (candidates.Count == 0) candidates = allowed.Where(t => !lastAct || t == DealType.SpendAtShop).ToList();
        if (candidates.Count == 0) return null;

        var type = candidates[Random.Shared.Next(candidates.Count)];
        var param = ParamFor(type, player);
        var target = type == DealType.SpendAtShop ? act : act + 1;
        return new Offer(npc.Key, type, param, target, runKey, act);
    }

    private static int ParamFor(DealType type, Player player)
    {
        var mood = SpireTalkConfig.Temperament;
        switch (type)
        {
            case DealType.HpAtLeast:
            {
                var pct = mood switch { Temperament.Lenient => 0.30, Temperament.Strict => 0.60, _ => 0.45 };
                var maxHp = SafeInt(() => player.Creature.MaxHp, 80);
                return Math.Max(1, (int)Math.Round(maxHp * pct));
            }
            case DealType.GoldAtLeast:
            {
                var extra = mood switch { Temperament.Lenient => 75, Temperament.Strict => 175, _ => 125 };
                return SafeInt(() => player.Gold, 0) + extra;
            }
            case DealType.SpendAtShop:
            {
                var wanted = mood switch { Temperament.Lenient => 100, Temperament.Strict => 200, _ => 150 };
                var affordable = SafeInt(() => player.Gold, 0) / 10 * 10;
                return Math.Max(MinSpendDeal, Math.Min(wanted, affordable));
            }
            default:
                return 0;
        }
    }

    private static int SafeInt(Func<int> f, int fallback)
    {
        try { return f(); } catch { return fallback; }
    }

    // ── Accepting ────────────────────────────────────────────────────────

    /// <summary>The token relic of the last accepted deal being put on the bar.</summary>
    public static Task LastRelicGift { get; private set; } = Task.CompletedTask;

    public static TalkStore.DealRecord Accept(Offer offer)
    {
        var store = TalkStore.Instance;
        var deal = new TalkStore.DealRecord
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Npc = offer.Npc,
            Type = offer.Key,
            Param = offer.Param,
            TargetAct = offer.TargetAct,
            Run = offer.RunKey,
            MadeAct = offer.MadeAct,
            Status = "pending",
            Utc = DateTime.UtcNow.ToString("o"),
        };
        store.AddDeal(deal);
        store.Get(offer.Npc).LastDealType = offer.Key;
        store.Save();
        LastRelicGift = Tokens.TokenService.GiveDealAsync(Merchant.MerchantTalk.ResolveLocalPlayer(), offer.Npc);
        if (SpireTalkConfig.DebugLogging) MainFile.Logger.Info($"[Deal] accepted {deal.Npc} {deal.Type} param={deal.Param} target={deal.TargetAct}", 1);
        return deal;
    }

    public static TalkStore.DealRecord? PendingFor(string npcKey, string runKey) =>
        TalkStore.Instance.Deals.FirstOrDefault(d => d.Npc == npcKey && d.Run == runKey && d.Status == "pending");

    public static IEnumerable<TalkStore.DealRecord> PendingInRun(string runKey) =>
        TalkStore.Instance.Deals.Where(d => d.Run == runKey && d.Status == "pending");

    /// <summary>A settled deal this NPC has not yet commented on (kept or broken), newest first.</summary>
    public static TalkStore.DealRecord? UntoldResultFor(string npcKey) =>
        TalkStore.Instance.Deals.LastOrDefault(d => d.Npc == npcKey && !d.Told && (d.Status == "kept" || d.Status == "broken"));

    // ── Settling ─────────────────────────────────────────────────────────

    private static string RunKeyOf(IRunState run)
    {
        try
        {
            var player = run.Players.FirstOrDefault();
            return player != null ? RewardExecutor.RunKey(player) : "no-run";
        }
        catch
        {
            return "no-run";
        }
    }

    public static void OnActEntered(IRunState run)
    {
        var runKey = RunKeyOf(run);
        var act = run.CurrentActIndex;
        var player = run.Players.FirstOrDefault();
        foreach (var deal in PendingInRun(runKey).ToList())
        {
            if (deal.TargetAct > act) continue;
            switch (ParseType(deal.Type))
            {
                case DealType.ReachNextAct:
                case DealType.NoPotion:
                case DealType.NoCardRemoval:
                    Settle(deal, true, player);
                    break;
                case DealType.KillElite:
                    Settle(deal, false, player); // no elite fell before the act ended
                    break;
                case DealType.HpAtLeast:
                    Settle(deal, SafeInt(() => (int)player!.Creature.CurrentHp, 0) >= deal.Param, player);
                    break;
                case DealType.GoldAtLeast:
                    Settle(deal, SafeInt(() => player!.Gold, 0) >= deal.Param, player);
                    break;
                case DealType.SpendAtShop:
                    Settle(deal, deal.Progress >= deal.Param, player);
                    break;
            }
        }
    }

    public static void OnCombatVictory(IRunState run, CombatRoom room)
    {
        if (room.RoomType != RoomType.Elite) return;
        var runKey = RunKeyOf(run);
        var player = run.Players.FirstOrDefault();
        foreach (var deal in PendingInRun(runKey).Where(d => d.Type == "kill_elite" && d.MadeAct == run.CurrentActIndex).ToList())
        {
            Settle(deal, true, player);
        }
    }

    public static void OnPotionUsed(IRunState run)
    {
        var runKey = RunKeyOf(run);
        var player = run.Players.FirstOrDefault();
        foreach (var deal in PendingInRun(runKey).Where(d => d.Type == "no_potion").ToList())
        {
            Settle(deal, false, player);
        }
    }

    private static readonly HashSet<CardModel> Stolen = new();

    public static void OnCardStolen(CardModel card)
    {
        if (card.DeckVersion != null) Stolen.Add(card.DeckVersion);
    }

    /// <summary>
    /// Removals the climber didn't choose: a Thieving Hopper's theft (the card comes back if it dies),
    /// Spoils Map spent on its treasure, Guilty wearing off after five fights, a placeholder card.
    /// </summary>
    private static bool IsInvoluntary(CardModel card) =>
        Stolen.Remove(card) || card is SpoilsMap or Guilty or DeprecatedCard;

    public static void OnCardRemoved(IRunState run, CardModel card)
    {
        if (IsInvoluntary(card)) return;
        var runKey = RunKeyOf(run);
        var player = run.Players.FirstOrDefault();
        foreach (var deal in PendingInRun(runKey).Where(d => d.Type == "no_card_removal").ToList())
        {
            Settle(deal, false, player);
        }
    }

    public static void OnItemPurchased(IRunState run, Player player, int goldSpent)
    {
        if (goldSpent <= 0) return;
        var runKey = RunKeyOf(run);
        var changed = false;
        foreach (var deal in PendingInRun(runKey).Where(d => d.Type == "spend_at_shop").ToList())
        {
            deal.Progress += goldSpent;
            changed = true;
            if (deal.Progress >= deal.Param) Settle(deal, true, player);
        }
        if (changed)
        {
            TalkStore.Instance.Save();
            Tokens.TokenService.RefreshDeals(player);
        }
    }

    /// <summary>Death breaks every open deal; a won run leaves them unjudged.</summary>
    public static void OnRunEnded(IRunState run, bool win)
    {
        var runKey = RunKeyOf(run);
        foreach (var deal in PendingInRun(runKey).ToList())
        {
            if (win)
            {
                deal.Status = "void";
                deal.Told = true;
            }
            else
            {
                Settle(deal, false, null, quiet: true);
            }
        }
        TalkStore.Instance.Save();
    }

    private static void Settle(TalkStore.DealRecord deal, bool kept, Player? player, bool quiet = false)
    {
        deal.Status = kept ? "kept" : "broken";
        deal.Told = false;
        var npc = NpcCatalog.Get(deal.Npc);
        var name = npc?.DisplayName ?? deal.Npc;
        var kind = npc?.Kind ?? NpcKind.Ancient;
        if (kept)
        {
            AffectionService.Apply(deal.Npc, kind, KeptAffection);
            if (player != null)
            {
                _ = GiveGoldAsync(player);
            }
            _ = Keepsakes.KeepsakeService.EngraveAsync(deal.Npc, player);
            if (!quiet) RewardExecutor.Toast(string.Format(Loc.EventText("dealKept"), name, Describe(deal), KeptAffection, KeptGold));
        }
        else
        {
            if (!quiet) RewardExecutor.Toast(string.Format(Loc.EventText("dealBroken"), name, Describe(deal)));
        }
        TalkStore.Instance.Save();
        _ = Tokens.TokenService.SettleDealAsync(player ?? Merchant.MerchantTalk.ResolveLocalPlayer(), deal.Npc, kept);
        if (SpireTalkConfig.DebugLogging) MainFile.Logger.Info($"[Deal] {deal.Npc} {deal.Type} → {deal.Status}", 1);
    }

    private static async Task GiveGoldAsync(Player player)
    {
        try
        {
            await PlayerCmd.GainGold(KeptGold, player);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Deal] gold reward failed: {ex.Message}", 1);
        }
    }

    // ── Text ─────────────────────────────────────────────────────────────

    public static string Describe(TalkStore.DealRecord deal) =>
        Describe(ParseType(deal.Type) ?? DealType.ReachNextAct, deal.Param, deal.TargetAct);

    /// <summary>Player-facing description in the game's language. Acts are shown 1-based.</summary>
    public static string Describe(DealType type, int param, int targetAct)
    {
        var act = targetAct + 1;
        return type switch
        {
            DealType.ReachNextAct => string.Format(Loc.EventText("deal.reach_next_act"), act),
            DealType.KillElite => Loc.EventText("deal.kill_elite"),
            DealType.HpAtLeast => string.Format(Loc.EventText("deal.hp_at_least"), param, act),
            DealType.GoldAtLeast => string.Format(Loc.EventText("deal.gold_at_least"), param, act),
            DealType.NoPotion => string.Format(Loc.EventText("deal.no_potion"), act),
            DealType.NoCardRemoval => string.Format(Loc.EventText("deal.no_card_removal"), act),
            DealType.SpendAtShop => string.Format(Loc.EventText("deal.spend_at_shop"), param),
            _ => "?",
        };
    }

    /// <summary>English wording for the prompt; the model puts it in the NPC's mouth.</summary>
    public static string PromptText(DealType type, int param, int targetAct)
    {
        var act = targetAct + 1;
        return type switch
        {
            DealType.ReachNextAct => $"reach act {act} alive",
            DealType.KillElite => "kill an elite enemy before leaving this act",
            DealType.HpAtLeast => $"arrive in act {act} with at least {param} HP",
            DealType.GoldAtLeast => $"arrive in act {act} carrying at least {param} gold",
            DealType.NoPotion => $"drink no potion until act {act}",
            DealType.NoCardRemoval => $"remove no card from the deck until act {act}",
            DealType.SpendAtShop => $"spend at least {param} gold at the shop this act",
            _ => "?",
        };
    }

    /// <summary>What the NPC knows about deals with this climber, for the `deals` prompt variable.</summary>
    public static string PromptStatus(string npcKey, string runKey)
    {
        var parts = new List<string>();
        var pending = PendingFor(npcKey, runKey);
        if (pending != null)
        {
            var type = ParseType(pending.Type) ?? DealType.ReachNextAct;
            var progress = type == DealType.SpendAtShop ? $" (so far {pending.Progress}/{pending.Param})" : "";
            parts.Add($"OPEN: the climber promised you to {PromptText(type, pending.Param, pending.TargetAct)}{progress}. Not judged yet; you may refer to it, but do not judge it yourself.");
        }
        var result = UntoldResultFor(npcKey);
        if (result != null)
        {
            var type = ParseType(result.Type) ?? DealType.ReachNextAct;
            parts.Add(result.Status == "kept"
                ? $"KEPT: last time they promised to {PromptText(type, result.Param, result.TargetAct)} — and they did. Acknowledge it in your own way this visit."
                : $"BROKEN: last time they promised to {PromptText(type, result.Param, result.TargetAct)} — and did not. Let it show this visit, in character; no punishment beyond words.");
        }
        return parts.Count == 0 ? "none" : string.Join(" ", parts);
    }

    /// <summary>After a visit's opener the outcome has been aired; it is not brought up again.</summary>
    public static void MarkResultTold(string npcKey)
    {
        var result = UntoldResultFor(npcKey);
        if (result == null) return;
        result.Told = true;
        TalkStore.Instance.Save();
    }
}
