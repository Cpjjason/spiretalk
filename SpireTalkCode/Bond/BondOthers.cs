using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace SpireTalk.Bond;

/// <summary>
/// For the relics whose vanilla logic is long or differs between game branches (Lord's Parasol,
/// Whispering Earring, Golden Compass, Sea Glass, Touch of Orobas): keep a private vanilla copy
/// owned by the same climber and hand it the hooks that do the vanilla part, so that part is
/// always the game's own code for whichever branch is running. The copy is not in the relic bar
/// (its Flash goes nowhere) and not saved; anything it must remember lives on our relic.
/// </summary>
public abstract class BondDelegatingRelic<T> : BondRelic where T : RelicModel
{
    private T? _vanilla;

    protected T Vanilla
    {
        get
        {
            if (_vanilla != null) return _vanilla;
            _vanilla = (T)ModelDb.Relic<T>().ToMutable();
            _vanilla.Owner = Owner;
            return _vanilla;
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _vanilla = null;
    }
}

// ---------------- Orobas: shiny, and more of it ----------------

/// <summary>Driftwood: card rewards can be rerolled and offer one more card; Beloved, one of them is at least Uncommon.</summary>
public abstract class BondDriftwood : BondRelic
{
    public override string OriginalName => "Driftwood";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(1) };

    public override bool TryModifyRewardsLate(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner) return false;
        foreach (var r in rewards.OfType<CardReward>()) r.CanReroll = true;
        return true;
    }

    [ThreadStatic] private static bool _rolling;

    public override bool TryModifyCardRewardOptions(Player player, List<CardCreationResult> options, CardCreationOptions creationOptions)
    {
        // CardFactory.CreateForReward runs this very hook on what it makes: without this guard, rolling
        // the extra card recursed until the game crashed.
        if (_rolling || player != Owner || options.Count == 0) return false;
        _rolling = true;
        try
        {
            CardCreationResult? extra = null;
            var wantsUncommon = Beloved && options.All(o => o.Card.Rarity is not (CardRarity.Uncommon or CardRarity.Rare));
            // (WithFlags changes the options in place, so the reward's own options are never passed through it)
            var make = wantsUncommon
                ? CardCreationOptions.ForNonCombatWithUniformOdds(creationOptions.CardPools, c => c.Rarity == CardRarity.Uncommon)
                    .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications)
                : creationOptions;
            for (var tries = 0; extra == null && tries < 6; tries++)
            {
                var roll = CardFactory.CreateForReward(player, 1, make).FirstOrDefault();
                if (roll != null && options.All(o => o.Card.Id != roll.Card.Id)) extra = roll;
            }
            if (extra == null) return false;
            options.Add(extra);
            return true;
        }
        finally { _rolling = false; }
    }
}

public sealed class DevotedDriftwood : BondDriftwood { public override int Tier => 2; }
public sealed class BelovedDriftwood : BondDriftwood { public override int Tier => 3; }

/// <summary>Prismatic Gem: other climbers' cards you take come upgraded; Beloved, one more card on turn 1.</summary>
public abstract class BondPrismaticGem : BondRelic
{
    public override string OriginalName => "PrismaticGem";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(1), new CardsVar(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.ForEnergy(this) };

    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player != Owner ? amount : amount + DynamicVars.Energy.IntValue;

    public override CardCreationOptions ModifyCardRewardCreationOptions(Player player, CardCreationOptions options)
    {
        if (Owner != player || options.Flags.HasFlag(CardCreationFlags.NoCardPoolModifications) || !options.Flags.HasFlag(CardCreationFlags.IsCardReward)) return options;
        if (options.CardPools.All(p => p.IsColorless)) return options;
        return options.WithCardPools(player.UnlockState.CharacterCardPools.Union(options.CardPools));
    }

    public override bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel? newCard)
    {
        newCard = null;
        if (card.Owner != Owner || card.IsUpgraded || !card.IsUpgradable) return false;
        var pool = card.Pool;
        if (pool == null || pool.IsColorless || pool == Owner.Character.CardPool) return false;
        if (!Owner.UnlockState.CharacterCardPools.Contains(pool)) return false;
        newCard = Owner.RunState.CloneCard(card);
        CardCmd.Upgrade(newCard);
        return true;
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (!Beloved || player != Owner || Owner.PlayerCombatState.TurnNumber > 1) return count;
        return count + DynamicVars.Cards.BaseValue;
    }
}

public sealed class DevotedPrismaticGem : BondPrismaticGem { public override int Tier => 2; }
public sealed class BelovedPrismaticGem : BondPrismaticGem { public override int Tier => 3; }

/// <summary>Sea Glass: more cards to pick from (18, Beloved 21) and Beloved, the Rare ones come upgraded.</summary>
public abstract class BondSeaGlass : BondDelegatingRelic<SeaGlass>
{
    private ModelId? _character;

    public override string OriginalName => "SeaGlass";
    public override bool HasUponPickupEffect => true;
    protected abstract int Cards { get; }
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(Cards), new StringVar("Character") };

    [SavedProperty]
    public ModelId? CharacterId
    {
        get => _character;
        set
        {
            AssertMutable();
            _character = value;
            if (value != null && DynamicVars.TryGetValue("Character", out var v) && v is StringVar s)
                s.StringValue = ModelDb.GetById<CharacterModel>(value).Title.GetFormattedText();
        }
    }

    public override void SetupFrom(RelicModel original) => CharacterId = Read<ModelId>(original, "CharacterId");

    public override async Task AfterObtained()
    {
        if (CharacterId == null)
        {
            var others = ModelDb.AllCharacters.Where(c => c.Id != Owner.Character.Id).ToList();
            CharacterId = (others.Count > 0 ? Owner.PlayerRng.Rewards.NextItem(others) : Owner.Character)?.Id;
        }
        var glass = Vanilla;
        glass.CharacterId = CharacterId;
        glass.DynamicVars.Cards.BaseValue = DynamicVars.Cards.BaseValue;
        var before = PileType.Deck.GetPile(Owner).Cards.ToHashSet();
        await glass.AfterObtained();
        if (!Beloved) return;
        foreach (var card in PileType.Deck.GetPile(Owner).Cards.Where(c => !before.Contains(c) && c.Rarity == CardRarity.Rare && c.IsUpgradable && !c.IsUpgraded).ToList())
            CardCmd.Upgrade(card);
    }
}

public sealed class DevotedSeaGlass : BondSeaGlass { public override int Tier => 2; protected override int Cards => 18; }
public sealed class BelovedSeaGlass : BondSeaGlass { public override int Tier => 3; protected override int Cards => 21; }

/// <summary>Touch of Orobas: a pocketful of gold too; Beloved, a relic as well.</summary>
public abstract class BondTouchOfOrobas : BondRelic
{
    private ModelId? _starter;
    private ModelId? _upgraded;

    public override string OriginalName => "TouchOfOrobas";
    public override bool HasUponPickupEffect => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new StringVar("StarterRelic"), new StringVar("UpgradedRelic"), new GoldVar(100) };

    [SavedProperty] public ModelId? StarterRelic { get => _starter; set { AssertMutable(); _starter = value; Name("StarterRelic", value); } }
    [SavedProperty] public ModelId? UpgradedRelic { get => _upgraded; set { AssertMutable(); _upgraded = value; Name("UpgradedRelic", value); } }

    private void Name(string key, ModelId? id)
    {
        if (id != null && DynamicVars.TryGetValue(key, out var v) && v is StringVar s)
            s.StringValue = ModelDb.GetById<RelicModel>(id).Title.GetFormattedText();
    }

    public override void SetupFrom(RelicModel original)
    {
        StarterRelic = Read<ModelId>(original, "StarterRelic");
        UpgradedRelic = Read<ModelId>(original, "UpgradedRelic");
    }

    public override async Task AfterObtained()
    {
        // The vanilla relic does the swap itself: its table of starter upgrades differs by branch.
        var touch = (TouchOfOrobas)ModelDb.Relic<TouchOfOrobas>().ToMutable();
        touch.Owner = Owner;
        if (StarterRelic != null && UpgradedRelic != null) touch.SetupForTests(StarterRelic, UpgradedRelic);
        await touch.AfterObtained();
        Flash();
        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner);
        if (Beloved) await RewardsCmd.OfferCustom(Owner, new List<Reward> { new RelicReward(Owner) });
    }
}

public sealed class DevotedTouchOfOrobas : BondTouchOfOrobas { public override int Tier => 2; }
public sealed class BelovedTouchOfOrobas : BondTouchOfOrobas { public override int Tier => 3; }

// ---------------- Vakuu: a lord's courtesies ----------------

/// <summary>Lord's Parasol: pocket money with the spree; Beloved, a rest before it.</summary>
public abstract class BondLordsParasol : BondDelegatingRelic<LordsParasol>
{
    public override string OriginalName => "LordsParasol";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new GoldVar(75), new HealVar(15m) };

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not MerchantRoom) return;
        Flash();
        if (Beloved) await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        // the spree is free, so the pocket money can come first
        await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner);
        await Vanilla.AfterRoomEntered(room);
    }
}

public sealed class DevotedLordsParasol : BondLordsParasol { public override int Tier => 2; }
public sealed class BelovedLordsParasol : BondLordsParasol { public override int Tier => 3; }

/// <summary>Music Box: the echo costs nothing; Beloved, the first two Attacks each turn echo.</summary>
public abstract class BondMusicBox : BondRelic
{
    private int _echoes;
    private CardModel? _playing;

    public override string OriginalName => "MusicBox";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("Echoes", Beloved ? 2m : 1m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromKeyword(CardKeyword.Ethereal) };

    private int Echoes { get => _echoes; set { AssertMutable(); _echoes = value; } }
    private CardModel? Playing { get => _playing; set { AssertMutable(); _playing = value; } }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Playing != null || cardPlay.Card.Owner != Owner || Echoes >= DynamicVars["Echoes"].IntValue || cardPlay.Card.Type != CardType.Attack)
            return Task.CompletedTask;
        Playing = cardPlay.Card;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card != Playing) return;
        Flash();
        var echo = cardPlay.Card.CreateClone();
        CardCmd.ApplyKeyword(echo, CardKeyword.Ethereal);
        echo.EnergyCost.SetThisCombat(0);
        await CardPileCmd.AddGeneratedCardToCombat(echo, PileType.Hand, Owner);
        Echoes++;
        Playing = null;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner.Creature)) return Task.CompletedTask;
        Echoes = 0;
        Playing = null;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        Echoes = 0;
        Playing = null;
        return Task.CompletedTask;
    }
}

public sealed class DevotedMusicBox : BondMusicBox { public override int Tier => 2; }
public sealed class BelovedMusicBox : BondMusicBox { public override int Tier => 3; }

/// <summary>Whispering Earring: after Vakuu plays your first turn, a card (Devoted); Beloved, an energy and two cards.</summary>
public abstract class BondWhisperingEarring : BondDelegatingRelic<WhisperingEarring>
{
    public override string OriginalName => "WhisperingEarring";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(1), new CardsVar(Beloved ? 2 : 1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.ForEnergy(this) };

    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player != Owner ? amount : amount + DynamicVars.Energy.BaseValue;

    public override async Task AfterAutoPrePlayPhaseEnteredLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Owner.PlayerCombatState.TurnNumber > 1) return;
        await Vanilla.AfterAutoPrePlayPhaseEnteredLate(choiceContext, player);
        if (CombatManager.Instance.IsOverOrEnding) return;
        Flash();
        if (Beloved) await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }
}

public sealed class DevotedWhisperingEarring : BondWhisperingEarring { public override int Tier => 2; }
public sealed class BelovedWhisperingEarring : BondWhisperingEarring { public override int Tier => 3; }

// ---------------- Tezcatara: something warm on the road ----------------

/// <summary>Golden Compass: every event on the golden path heals you; Beloved, with a little gold too.</summary>
public abstract class BondGoldenCompass : BondDelegatingRelic<GoldenCompass>
{
    private int _act = -1;

    public override string OriginalName => "GoldenCompass";
    public override bool HasUponPickupEffect => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new HealVar(5m), new GoldVar(20) };

    [SavedProperty]
    public int GoldenPathAct
    {
        get => _act;
        set { AssertMutable(); _act = value; }
    }

    private GoldenCompass Synced
    {
        get
        {
            if (Vanilla.GoldenPathAct != GoldenPathAct) Vanilla.GoldenPathAct = GoldenPathAct;
            return Vanilla;
        }
    }

    public override async Task AfterObtained()
    {
        GoldenPathAct = Owner.RunState.CurrentActIndex;
        await Synced.AfterObtained();
    }

    public override ActMap ModifyGeneratedMap(IRunState runState, ActMap map, int actIndex) => Synced.ModifyGeneratedMap(runState, map, actIndex);

    public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes) => Synced.ModifyUnknownMapPointRoomTypes(roomTypes);

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not EventRoom || Owner.RunState.CurrentActIndex != GoldenPathAct || Owner.Creature.IsDead) return;
        Flash();
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        if (Beloved) await PlayerCmd.GainGold(DynamicVars.Gold.BaseValue, Owner);
    }
}

public sealed class DevotedGoldenCompass : BondGoldenCompass { public override int Tier => 2; }
public sealed class BelovedGoldenCompass : BondGoldenCompass { public override int Tier => 3; }

// ---------------- Tanx: the hunter shares the meal ----------------

/// <summary>Meat Cleaver: resting heals 10 more; Beloved, Cook also adds 2 Max HP.</summary>
public abstract class BondMeatCleaver : BondRelic
{
    public override string OriginalName => "MeatCleaver";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new HealVar(10m), new MaxHpVar(2m) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.Static(StaticHoverTip.Cook) };

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner) return false;
        options.Add(new CookRestSiteOption(player));
        return true;
    }

    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount)
    {
        if (creature.Player != Owner) return amount;
        return amount + DynamicVars.Heal.BaseValue;
    }

    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (player == Owner) Flash();
        return Task.CompletedTask;
    }
}

public sealed class DevotedMeatCleaver : BondMeatCleaver { public override int Tier => 2; }
public sealed class BelovedMeatCleaver : BondMeatCleaver { public override int Tier => 3; }

/// <summary>Beloved Meat Cleaver: after a Cook, Tanx's share of the meal (2 Max HP).</summary>
[HarmonyPatch(typeof(CookRestSiteOption), nameof(CookRestSiteOption.OnSelect))]
internal static class BondCookExtra
{
    private static readonly System.Reflection.PropertyInfo? OwnerProp = AccessTools.Property(typeof(RestSiteOption), "Owner");

    [HarmonyPostfix]
    private static void Postfix(CookRestSiteOption __instance, ref Task<bool> __result)
    {
        if (OwnerProp?.GetValue(__instance) is not Player owner) return;
        if (owner.Relics.OfType<BelovedMeatCleaver>().FirstOrDefault() is not { } cleaver) return;
        __result = AfterCook(__result, owner, cleaver);
    }

    private static async Task<bool> AfterCook(Task<bool> cook, Player owner, BelovedMeatCleaver cleaver)
    {
        var done = await cook;
        if (done)
        {
            cleaver.Flash();
            await CreatureCmd.GainMaxHp(owner.Creature, cleaver.DynamicVars.MaxHp.BaseValue);
        }
        return done;
    }
}
