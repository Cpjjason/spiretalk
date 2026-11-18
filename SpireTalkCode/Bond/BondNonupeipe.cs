using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SpireTalk.Rewards;

namespace SpireTalk.Bond;

// Nonupeipe dresses you for the occasion: a little more polish on everything she gives.

/// <summary>Brilliant Scarf: the free card also gives Block (Devoted) and draws a card (Beloved).</summary>
public abstract class BondBrilliantScarf : BondRelic
{
    private int _played;

    public override string OriginalName => "BrilliantScarf";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(6), new BlockVar(3m, ValueProp.Unpowered) };

    private int Played
    {
        get => _played;
        set
        {
            AssertMutable();
            _played = value;
            Status = _played == DynamicVars.Cards.IntValue - 1 ? RelicStatus.Active : RelicStatus.Normal;
            InvokeDisplayAmountChanged();
        }
    }

    public override bool ShowCounter => CombatManager.Instance.IsInProgress && Played < DynamicVars.Cards.IntValue;
    public override int DisplayAmount => Played;

    private bool Discounts(CardModel card) =>
        CombatManager.Instance.IsInProgress && card.Owner.Creature == Owner.Creature && Played == DynamicVars.Cards.IntValue - 1
        && card.Pile?.Type is PileType.Hand or PileType.Play;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!Discounts(card)) return false;
        modifiedCost = 0m;
        return true;
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!Discounts(card)) return false;
        modifiedCost = 0m;
        return true;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner.Creature)) Played = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!CombatManager.Instance.IsInProgress || cardPlay.IsAutoPlay || cardPlay.Card.Owner != Owner) return;
        Played++;
        if (Played != DynamicVars.Cards.IntValue) return;
        // the free card has just been played: her extra touch
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Unpowered, null);
        if (Beloved) await CardPileCmd.Draw(choiceContext, 1m, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        Played = 0;
        return Task.CompletedTask;
    }
}

public sealed class DevotedBrilliantScarf : BondBrilliantScarf { public override int Tier => 2; }
public sealed class BelovedBrilliantScarf : BondBrilliantScarf { public override int Tier => 3; }

/// <summary>Delicate Frond: room for one (Devoted) or two (Beloved) more potions, filled before every fight.</summary>
public abstract class BondDelicateFrond : BondDelegatingRelic<MegaCrit.Sts2.Core.Models.Relics.DelicateFrond>
{
    public override string OriginalName => "DelicateFrond";
    protected abstract int Slots { get; }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("PotionSlots", Slots) };

    public override async Task AfterObtained() => await PlayerCmd.GainMaxPotionCount(Slots, Owner);

    // the filling itself is the vanilla relic's (which random stream it uses differs by branch)
    public override async Task BeforeCombatStart()
    {
        Flash();
        await Vanilla.BeforeCombatStart();
    }
}

public sealed class DevotedDelicateFrond : BondDelicateFrond { public override int Tier => 2; protected override int Slots => 1; }
public sealed class BelovedDelicateFrond : BondDelicateFrond { public override int Tier => 3; protected override int Slots => 2; }

/// <summary>Fur Coat: the marked fights also heal you when they're won.</summary>
public abstract class BondFurCoat : BondRelic
{
    private int _act = -1;

    public override string OriginalName => "FurCoat";
    protected abstract int Heal { get; }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("Combats", 8m), new HealVar(Heal) };

    [SavedProperty]
    public int FurCoatActIndex
    {
        get => _act;
        set { AssertMutable(); _act = value; }
    }

    [SavedProperty] public int[] FurCoatCoordCols { get; set; } = Array.Empty<int>();
    [SavedProperty] public int[] FurCoatCoordRows { get; set; } = Array.Empty<int>();
    [SavedProperty] public bool FurCoatCoordsSet { get; set; }

    public override Task AfterObtained()
    {
        FurCoatActIndex = Owner.RunState.CurrentActIndex;
        AddMarkedRooms(Owner.RunState.Map);
        return Task.CompletedTask;
    }

    public override ActMap ModifyGeneratedMapLate(IRunState runState, ActMap map, int actIndex) => AddMarkedRooms(map);

    private ActMap AddMarkedRooms(ActMap map)
    {
        if (Owner.RunState.CurrentActIndex != FurCoatActIndex) return map;
        var marked = Marked();
        if (marked == null || !marked.TrueForAll(c => map.HasPoint(c) && map.GetPoint(c).PointType is MapPointType.Monster or MapPointType.Elite))
        {
            var rng = RngCompat.For(Owner, Id);
            var points = map.GetAllMapPoints()
                .Where(p => p.PointType is MapPointType.Monster or MapPointType.Elite && !p.Quests.Any(q => q is BondFurCoat))
                .ToList();
            points.UnstableShuffle(rng);
            var take = points.Take(DynamicVars["Combats"].IntValue).ToList();
            FurCoatCoordCols = take.Select(p => p.coord.col).ToArray();
            FurCoatCoordRows = take.Select(p => p.coord.row).ToArray();
            FurCoatCoordsSet = true;
            foreach (var p in take) p.AddQuest(this);
        }
        else
        {
            foreach (var c in marked) map.GetPoint(c)?.AddQuest(this);
        }
        return map;
    }

    // (no map point at all in a fight started from the console or by some events: never throw here, it would stop the fight)
    private bool HereIsMarked() => Owner.RunState.CurrentMapPoint is { } here && Marked() is { } m && m.Contains(here.coord);

    public override async Task BeforeCombatStart()
    {
        if (!HereIsMarked()) return;
        Flash();
        var enemies = Owner.Creature.CombatState.HittableEnemies;
        VfxCmd.PlayOnCreatureCenters(enemies, "vfx/vfx_bite");
        foreach (var e in enemies) await CreatureCmd.SetCurrentHp(e, 1m);
    }

    public override async Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (creature.Side != CombatSide.Enemy || !HereIsMarked()) return;
        Flash();
        VfxCmd.PlayOnCreatureCenters(new[] { creature }, "vfx/vfx_bite"); // the single-creature overload is gone on 0.113
        await CreatureCmd.SetCurrentHp(creature, 1m);
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (Owner.Creature.IsDead || !HereIsMarked()) return;
        Flash();
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
    }

    private List<MapCoord>? Marked()
    {
        if (!FurCoatCoordsSet) return null;
        var list = new List<MapCoord>();
        for (var i = 0; i < FurCoatCoordCols.Length; i++) list.Add(new MapCoord { col = FurCoatCoordCols[i], row = FurCoatCoordRows[i] });
        return list;
    }
}

public sealed class DevotedFurCoat : BondFurCoat { public override int Tier => 2; protected override int Heal => 5; }
public sealed class BelovedFurCoat : BondFurCoat { public override int Tier => 3; protected override int Heal => 8; }

/// <summary>Glitter: card rewards can be rerolled once (Devoted); the shop's cards shine too (Beloved).</summary>
public abstract class BondGlitter : BondRelic
{
    public override string OriginalName => "Glitter";
    protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromEnchantment<Glam>();

    private void Shine(List<CardCreationResult> cards)
    {
        var glam = ModelDb.Enchantment<Glam>();
        foreach (var result in cards)
        {
            if (!glam.CanEnchant(result.Card)) continue;
            var copy = Owner.RunState.CloneCard(result.Card);
            CardCmd.Enchant<Glam>(copy, 1m);
            result.ModifyCard(copy, this);
        }
    }

    public override bool TryModifyCardRewardOptionsLate(Player player, List<CardCreationResult> cardRewards, CardCreationOptions options)
    {
        if (player != Owner) return false;
        Shine(cardRewards);
        return true;
    }

    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner) return false;
        foreach (var reward in rewards.OfType<CardReward>()) reward.CanReroll = true;
        return true;
    }

    public override void ModifyMerchantCardCreationResults(Player player, List<CardCreationResult> cards)
    {
        if (Beloved && player == Owner) Shine(cards);
    }
}

public sealed class DevotedGlitter : BondGlitter { public override int Tier => 2; }
public sealed class BelovedGlitter : BondGlitter { public override int Tier => 3; }

/// <summary>Signet Ring: the Merchant knows the seal — a standing discount, and Beloved, the first removal is free.</summary>
public abstract class BondSignetRing : BondRelic
{
    private bool _removalUsed;

    public override string OriginalName => "SignetRing";
    public override bool HasUponPickupEffect => true;
    protected abstract int Discount { get; }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new GoldVar(888), new("Discount", Discount) };

    [SavedProperty]
    public bool RemovalUsed
    {
        get => _removalUsed;
        set { AssertMutable(); _removalUsed = value; }
    }

    public override async Task AfterObtained() => await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner);

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal cost)
    {
        if (player != Owner) return cost;
        if (Beloved && !RemovalUsed && entry is MerchantCardRemovalEntry) return 0m;
        return Math.Floor(cost * (100 - Discount) / 100m);
    }

    public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        if (player == Owner && Beloved && itemPurchased is MerchantCardRemovalEntry) RemovalUsed = true;
        return Task.CompletedTask;
    }
}

public sealed class DevotedSignetRing : BondSignetRing { public override int Tier => 2; protected override int Discount => 10; }
public sealed class BelovedSignetRing : BondSignetRing { public override int Tier => 3; protected override int Discount => 20; }
