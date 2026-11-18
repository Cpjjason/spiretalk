using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SpireTalk.Bond;

// Darv, the collector of tomes and stars: more to choose from, and the bad page torn out for you.

/// <summary>Astrolabe: one more card; Beloved, each result picked from three.</summary>
public abstract class BondAstrolabe : BondRelic
{
    public override string OriginalName => "Astrolabe";
    public override bool HasUponPickupEffect => true;
    protected abstract int Cards { get; }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(Cards), new("Choices", 3m) };

    public override async Task AfterObtained()
    {
        var chosen = (await CardSelectCmd.FromDeckForTransformation(prefs: new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, DynamicVars.Cards.IntValue), player: Owner)).ToList();
        var rng = Owner.PlayerRng.Transformations;
        foreach (var card in chosen)
        {
            CardModel result;
            if (!Beloved)
            {
                result = CardFactory.CreateRandomCardForTransform(card, isInCombat: false, rng);
            }
            else
            {
                var options = new List<CardModel>();
                for (var tries = 0; options.Count < 3 && tries < 12; tries++)
                {
                    var option = CardFactory.CreateRandomCardForTransform(card, isInCombat: false, rng);
                    if (options.All(o => o.Id != option.Id)) options.Add(option);
                }
                foreach (var o in options) CardCmd.Upgrade(o);
                result = await CardSelectCmd.FromChooseACardScreen(new BlockingPlayerChoiceContext(), options, Owner) ?? options[0];
            }
            if (!result.IsUpgraded) CardCmd.Upgrade(result);
            await CardCmd.Transform(card, result);
        }
    }
}

public sealed class DevotedAstrolabe : BondAstrolabe { public override int Tier => 2; protected override int Cards => 4; }
public sealed class BelovedAstrolabe : BondAstrolabe { public override int Tier => 3; protected override int Cards => 4; }

/// <summary>Black Star: elites leave gold too; Beloved, the extra relic is at least Uncommon.</summary>
public abstract class BondBlackStar : BondRelic
{
    public override string OriginalName => "BlackStar";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new GoldVar(25) };

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner || room == null || room.RoomType != RoomType.Elite) return false;
        if (Beloved)
        {
            var rarity = Owner.PlayerRng.Rewards.NextDouble() < 0.4 ? RelicRarity.Rare : RelicRarity.Uncommon;
            rewards.Add(BondRewards.Relic(rarity, player));
        }
        else rewards.Add(new RelicReward(player));
        return true;
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (room.RoomType != RoomType.Elite || Owner.Creature.IsDead) return;
        Flash();
        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner);
    }
}

public sealed class DevotedBlackStar : BondBlackStar { public override int Tier => 2; }
public sealed class BelovedBlackStar : BondBlackStar { public override int Tier => 3; }

/// <summary>Calling Bell: the Common relic rings Uncommon; Beloved, no curse at all.</summary>
public abstract class BondCallingBell : BondRelic
{
    public override string OriginalName => "CallingBell";
    public override bool HasUponPickupEffect => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("Relics", 3m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => Beloved ? Array.Empty<IHoverTip>() : HoverTipFactory.FromCardWithCardHoverTips<CurseOfTheBell>();

    public override async Task AfterObtained()
    {
        if (!Beloved)
        {
            await CardPileCmd.AddCurseToDeck<CurseOfTheBell>(Owner);
            await Cmd.Wait(0.75f);
        }
        var rewards = new List<Reward>
        {
            BondRewards.Relic(RelicRarity.Uncommon, Owner),
            BondRewards.Relic(RelicRarity.Uncommon, Owner),
            BondRewards.Relic(RelicRarity.Rare, Owner),
        };
        await RewardsCmd.OfferCustom(Owner, rewards);
    }
}

public sealed class DevotedCallingBell : BondCallingBell { public override int Tier => 2; }
public sealed class BelovedCallingBell : BondCallingBell { public override int Tier => 3; }

/// <summary>Dusty Tome: choose the Ancient card from two (Devoted) or three (Beloved).</summary>
public abstract class BondDustyTome : BondRelic
{
    private ModelId? _offered;

    public override string OriginalName => "DustyTome";
    public override bool HasUponPickupEffect => true;
    protected abstract int Choices { get; }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("Choices", Choices), new StringVar("AncientCard") };

    /// <summary>The card the Ancient's page already rolled for the vanilla tome: always one of the choices.</summary>
    [SavedProperty]
    public ModelId? Offered
    {
        get => _offered;
        set { AssertMutable(); _offered = value; }
    }

    public override void SetupFrom(RelicModel original) => Offered = Read<ModelId>(original, "AncientCard");

    public override async Task AfterObtained()
    {
        bool Eligible(CardModel c) => c.Rarity == CardRarity.Ancient && !ArchaicTooth.TranscendenceCards.Contains(c);
        var pool = Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint).Where(Eligible).ToList();
        // a profile that hasn't unlocked any yet still gets one: the vanilla tome would give nothing
        if (pool.Count == 0) pool = Owner.Character.CardPool.AllCards.Where(Eligible).ToList();
        MainFile.Logger.Info($"[Bond] {GetType().Name}: {pool.Count} Ancient cards to choose from", 1);
        if (pool.Count == 0) return;
        var picks = new List<CardModel>();
        if (Offered != null && pool.FirstOrDefault(c => c.Id == Offered) is { } first) picks.Add(first);
        var rng = Owner.PlayerRng.Rewards;
        for (var tries = 0; picks.Count < Choices && tries < 20; tries++)
        {
            var c = rng.NextItem(pool);
            if (c != null && picks.All(p => p.Id != c.Id)) picks.Add(c);
        }
        var cards = picks.Select(p => { var card = Owner.RunState.CreateCard(p, Owner); CardCmd.Upgrade(card); return card; }).ToList();
        var chosen = cards.Count == 1 ? cards[0] : await CardSelectCmd.FromChooseACardScreen(new BlockingPlayerChoiceContext(), cards, Owner) ?? cards[0];
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(chosen, PileType.Deck), 2f);
    }
}

public sealed class DevotedDustyTome : BondDustyTome { public override int Tier => 2; protected override int Choices => 2; }
public sealed class BelovedDustyTome : BondDustyTome { public override int Tier => 3; protected override int Choices => 3; }

/// <summary>Ectoplasm: the gold that never reaches you turns into Max HP.</summary>
public abstract class BondEctoplasm : BondRelic
{
    private int _stored;
    private decimal _pending;

    public override string OriginalName => "Ectoplasm";
    protected abstract int Rate { get; }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(1), new("Rate", Rate) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.ForEnergy(this) };

    /// <summary>Gold kept back so far, toward the next point of Max HP.</summary>
    [SavedProperty]
    public int Stored
    {
        get => _stored;
        set { AssertMutable(); _stored = value; InvokeDisplayAmountChanged(); }
    }

    public override bool ShowCounter => Stored > 0;
    public override int DisplayAmount => Stored;

    public override decimal ModifyGoldGained(Player player, decimal amount)
    {
        if (player != Owner) return amount;
        if (amount > 0) _pending += amount;
        return 0m;
    }

    public override async Task AfterModifyingGoldGained(Player player, decimal amount)
    {
        if (player != Owner) return;
        Flash();
        var total = Stored + (int)_pending;
        _pending = 0;
        var hp = total / Rate;
        Stored = total % Rate;
        if (hp > 0) await CreatureCmd.GainMaxHp(Owner.Creature, hp);
    }

    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player != Owner ? amount : amount + DynamicVars.Energy.IntValue;
}

public sealed class DevotedEctoplasm : BondEctoplasm { public override int Tier => 2; protected override int Rate => 40; }
public sealed class BelovedEctoplasm : BondEctoplasm { public override int Tier => 3; protected override int Rate => 25; }

/// <summary>Philosopher's Stone: ordinary fights stay calm; Beloved, the elites' and bosses' Strength wears off on turn 3.</summary>
public abstract class BondPhilosophersStone : BondRelic
{
    public override string OriginalName => "PhilosophersStone";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<StrengthPower>(1m), new EnergyVar(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.ForEnergy(this) };

    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player != Owner ? amount : amount + DynamicVars.Energy.IntValue;

    /// <summary>Only elites and bosses get the Strength.</summary>
    private static bool Applies(AbstractRoom? room) => room?.RoomType is RoomType.Elite or RoomType.Boss;

    public override Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (creature.Side == Owner.Creature.Side || !Applies(Owner.RunState.CurrentRoom)) return Task.CompletedTask;
        Flash();
        return PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), creature, DynamicVars["StrengthPower"].BaseValue, null, null);
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom || !Applies(room)) return;
        var targets = Owner.Creature.CombatState.GetOpponentsOf(Owner.Creature).Where(c => c.IsAlive);
        Flash();
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), targets, DynamicVars["StrengthPower"].BaseValue, null, null);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!Beloved || player != Owner || Owner.PlayerCombatState?.TurnNumber != 3 || !Applies(Owner.RunState.CurrentRoom)) return;
        var targets = Owner.Creature.CombatState.GetOpponentsOf(Owner.Creature).Where(c => c.IsAlive).ToList();
        if (targets.Count == 0) return;
        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, targets, -DynamicVars["StrengthPower"].BaseValue, null, null);
    }
}

public sealed class DevotedPhilosophersStone : BondPhilosophersStone { public override int Tier => 2; }
public sealed class BelovedPhilosophersStone : BondPhilosophersStone { public override int Tier => 3; }

/// <summary>Runic Pyramid: curses and statuses still leave your hand; Beloved, two more cards on turn 1.</summary>
public abstract class BondRunicPyramid : BondRelic
{
    public override string OriginalName => "RunicPyramid";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(2) };

    public override bool ShouldFlush(Player player) => player != Owner;

    public override async Task BeforeFlushLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner) return;
        var junk = PileType.Hand.GetPile(Owner).Cards.Where(c => c.Type is CardType.Curse or CardType.Status).ToList();
        if (junk.Count == 0) return;
        Flash();
        await CardCmd.Discard(choiceContext, junk);
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (!Beloved || player != Owner || Owner.PlayerCombatState.TurnNumber > 1) return count;
        return count + DynamicVars.Cards.BaseValue;
    }
}

public sealed class DevotedRunicPyramid : BondRunicPyramid { public override int Tier => 2; }
public sealed class BelovedRunicPyramid : BondRunicPyramid { public override int Tier => 3; }

/// <summary>Snecko Eye: the confusion sets in on turn 2 (Devoted) or turn 3 (Beloved).</summary>
public abstract class BondSneckoEye : BondRelic
{
    public override string OriginalName => "SneckoEye";
    protected abstract int ClearTurns { get; }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(2), new("Turn", ClearTurns + 1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<ConfusedPower>() };

    public override decimal ModifyHandDraw(Player player, decimal count) => player != Owner ? count : count + DynamicVars.Cards.BaseValue;

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner || Owner.PlayerCombatState.TurnNumber != ClearTurns + 1) return;
        if (Owner.Creature.GetPower<ConfusedPower>() != null) return;
        Flash();
        await PowerCmd.Apply<ConfusedPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
    }
}

public sealed class DevotedSneckoEye : BondSneckoEye { public override int Tier => 2; protected override int ClearTurns => 1; }
public sealed class BelovedSneckoEye : BondSneckoEye { public override int Tier => 3; protected override int ClearTurns => 2; }
