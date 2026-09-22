namespace SpireTalk.Npc;

/// <summary>
/// The written-down rules of a conversation. Pure functions, no game or network access, so they
/// are the same whether the NPC is driven by Yumina, a player's own key or a local model, and
/// whether affection lives in Ancient Affection's store or ours.
///
/// Affection scale: −100…100 for Ancients (Ancient Affection's scale), −100…500 for the Merchant.
/// Tiers follow Ancient Affection: below 0 hostile, 0–33 stranger, 34–66 friendly, 67–99 close,
/// 100 (or married) devoted.
/// </summary>
public static class ConversationRules
{
    public const int LinesPerVisit = 3;
    public const int MaxRewardsPerVisit = 1;

    // ── Temperament: the one knob the player turns ──────────────────────
    // Tiers stay where Ancient Affection put them; what changes is how fast the number moves,
    // how much a visit can swing it, what it takes to earn a reward, and how hard the deals are.

    private static Temperament Mood => SpireTalkConfig.Temperament;

    public static int MaxDeltaPerLine => Mood == Temperament.Strict ? 2 : 3;

    public static int MaxNetDeltaPerVisit => Mood switch
    {
        Temperament.Lenient => 10,
        Temperament.Strict => 5,
        _ => 8,
    };

    /// <summary>Lowest tier at which an NPC hands anything out.</summary>
    public static Tier RewardFloor => Mood == Temperament.Strict ? Tier.Close : Tier.Friendly;

    /// <summary>"You came to see me" gain when the mod keeps its own ledger.</summary>
    public static int VisitBonus => Mood switch
    {
        Temperament.Lenient => 3,
        Temperament.Strict => 1,
        _ => 2,
    };

    public const int MinAffection = -100;
    public const int MaxAncientAffection = 100;
    public const int MaxMerchantAffection = 500;

    public enum Tier
    {
        Hostile = -1,
        Stranger = 0,
        Friendly = 1,
        Close = 2,
        Devoted = 3,
    }

    public static Tier TierOf(int affection, bool married)
    {
        if (married) return Tier.Devoted;
        if (affection < 0) return Tier.Hostile;
        if (affection < 34) return Tier.Stranger;
        if (affection < 67) return Tier.Friendly;
        if (affection < 100) return Tier.Close;
        return Tier.Devoted;
    }

    public static string TierName(Tier tier) => tier switch
    {
        Tier.Hostile => "hostile",
        Tier.Stranger => "stranger",
        Tier.Friendly => "friendly",
        Tier.Close => "close",
        _ => "devoted",
    };

    /// <summary>Clamp an AI-proposed delta to the per-line and per-visit limits.</summary>
    public static int ClampDelta(int proposed, int netSoFarThisVisit, string playerLine, string? previousPlayerLine)
    {
        var d = Math.Clamp(proposed, -MaxDeltaPerLine, MaxDeltaPerLine);
        var trimmed = (playerLine ?? "").Trim();
        // No farming: repeating yourself or grunting earns nothing.
        if (trimmed.Length < 2) return 0;
        if (previousPlayerLine != null && string.Equals(trimmed, previousPlayerLine.Trim(), StringComparison.OrdinalIgnoreCase)) return 0;
        // Per-visit net cap in both directions.
        var room = d > 0 ? MaxNetDeltaPerVisit - netSoFarThisVisit : MaxNetDeltaPerVisit + netSoFarThisVisit;
        if (room <= 0) return 0;
        return d > 0 ? Math.Min(d, room) : Math.Max(d, -room);
    }

    public static int ClampAffection(int value, NpcKind kind)
    {
        var max = kind == NpcKind.Merchant ? MaxMerchantAffection : MaxAncientAffection;
        return Math.Clamp(value, MinAffection, max);
    }

    /// <summary>State the rules need to decide what an NPC may offer right now.</summary>
    public sealed class RewardState
    {
        public bool RerollUsedThisVisit;
        public bool DiscountUsedThisVisit;
        public int RewardsThisVisit;
        public bool GoldGivenThisRun;
        public bool HealGivenThisRun;
        public bool PotionGivenThisRun;
        public bool GiftGivenThisRun;
        public bool PlayerHurt;
        public bool PotionSlotFree;
        /// <summary>Ancient Affection is installed, so its bond relics can be handed out through talk.</summary>
        public bool AffectionRelicsAvailable;
        public bool AffectionRelicGivenThisRun;
    }

    /// <summary>Actions an NPC of this kind may offer at this tier, given what was already given.</summary>
    public static List<string> AllowedActions(NpcKind kind, Tier tier, RewardState s)
    {
        var allowed = new List<string>();
        if (s.RewardsThisVisit >= MaxRewardsPerVisit) return allowed;
        var floor = RewardFloor;
        var closeFloor = (Tier)Math.Max((int)Tier.Close, (int)floor);
        if (kind == NpcKind.Ancient)
        {
            if (tier >= floor && !s.RerollUsedThisVisit) allowed.Add("reroll_relics");
            if (tier >= floor && !s.GoldGivenThisRun) allowed.Add("gold");
            if (tier >= floor && !s.HealGivenThisRun && s.PlayerHurt) allowed.Add("heal");
            if (tier >= closeFloor && !s.PotionGivenThisRun && s.PotionSlotFree) allowed.Add("potion");
            if (tier >= closeFloor && s.AffectionRelicsAvailable && !s.AffectionRelicGivenThisRun) allowed.Add("affection_relic");
        }
        else
        {
            if (tier >= floor && !s.DiscountUsedThisVisit) allowed.Add("discount");
            if (tier >= Tier.Devoted && !s.GiftGivenThisRun && s.PotionSlotFree) allowed.Add("gift_potion");
        }
        return allowed;
    }

    /// <summary>Validate and normalise a proposed action. Returns None when it is not allowed.</summary>
    public static NpcAction Validate(NpcAction proposed, NpcKind kind, Tier tier, RewardState s, int maxHp)
    {
        if (proposed.IsNone) return NpcAction.None;
        var allowed = AllowedActions(kind, tier, s);
        if (!allowed.Contains(proposed.Type)) return NpcAction.None;
        return proposed.Type switch
        {
            "gold" => new NpcAction("gold", Math.Clamp(proposed.Amount <= 0 ? GoldForTier(tier) : proposed.Amount, 15, 75)),
            "heal" => new NpcAction("heal", Math.Clamp(proposed.Amount <= 0 ? HealForTier(tier, maxHp) : proposed.Amount, 5, Math.Max(5, maxHp / 3))),
            "discount" => new NpcAction("discount", DiscountPercentForTier(tier)),
            "potion" => new NpcAction("potion", 1),
            "gift_potion" => new NpcAction("gift_potion", 1),
            "reroll_relics" => new NpcAction("reroll_relics", 1),
            "affection_relic" => new NpcAction("affection_relic", 1),
            _ => NpcAction.None,
        };
    }

    public static int GoldForTier(Tier tier) => tier switch
    {
        Tier.Devoted => 75,
        Tier.Close => 50,
        _ => 30,
    };

    public static int HealForTier(Tier tier, int maxHp) => tier switch
    {
        Tier.Devoted => (int)Math.Round(maxHp * 0.25),
        Tier.Close => (int)Math.Round(maxHp * 0.20),
        _ => (int)Math.Round(maxHp * 0.12),
    };

    public static int DiscountPercentForTier(Tier tier) => tier switch
    {
        Tier.Devoted => 30,
        Tier.Close => 20,
        Tier.Friendly => 10,
        _ => 0,
    };

    /// <summary>The rules as prose, for the prompt. Same text the Yumina worlds carry (npcs.json).</summary>
    public static string PromptText(NpcKind kind)
    {
        var scale = kind == NpcKind.Merchant ? "-100 to 500" : "-100 to 100";
        var text = NpcCatalog.AffectionRules;
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "AFFECTION (scale {scale}): after every player line set affection_delta to an integer -3..3 judged by your values; 0 is normal. At most {lines} lines per visit. REWARDS: at most one per visit, only from allowed_actions; set action.type and action.amount (0 = game decides).";
        }
        return text.Replace("{scale}", scale).Replace("{lines}", LinesPerVisit.ToString());
    }
}
