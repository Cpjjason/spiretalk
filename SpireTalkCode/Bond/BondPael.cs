using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SpireTalk.Rewards;
using PaelsLegionPet = MegaCrit.Sts2.Core.Models.Monsters.PaelsLegion;

namespace SpireTalk.Bond;

// Pael's gifts are their own flesh: they keep growing on you.

/// <summary>Pael's Claw: cards you add later are Goopy too (Devoted); Beloved, twice as Goopy.</summary>
public abstract class BondPaelsClaw : BondRelic
{
    public override string OriginalName => "PaelsClaw";
    public override bool HasUponPickupEffect => true;
    private decimal Amount => Beloved ? 2m : 1m;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(3), new StringVar("EnchantmentName", ModelDb.Enchantment<Goopy>().Title.GetFormattedText()), new("Goopy", Amount),
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromEnchantment<Goopy>((int)Amount);

    public override Task AfterObtained()
    {
        foreach (var card in PileType.Deck.GetPile(Owner).Cards.ToList())
        {
            if (!ModelDb.Enchantment<Goopy>().CanEnchant(card)) continue;
            CardCmd.Enchant<Goopy>(card, Amount);
            NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(NCardEnchantVfx.Create(card));
        }
        return Task.CompletedTask;
    }

    public override bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel? newCard)
    {
        newCard = null;
        if (card.Owner != Owner || !ModelDb.Enchantment<Goopy>().CanEnchant(card)) return false;
        newCard = Owner.RunState.CloneCard(card);
        CardCmd.Enchant<Goopy>(newCard, Amount);
        return true;
    }
}

public sealed class DevotedPaelsClaw : BondPaelsClaw { public override int Tier => 2; }
public sealed class BelovedPaelsClaw : BondPaelsClaw { public override int Tier => 3; }

/// <summary>Pael's Eye: the extra turn comes with an energy (Devoted); Beloved, the hand isn't Exhausted for it.</summary>
public abstract class BondPaelsEye : BondRelic
{
    private bool _used;
    private bool _wasPart = true;
    private bool _energyDue;

    public override string OriginalName => "PaelsEye";
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(1) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromKeyword(CardKeyword.Exhaust), HoverTipFactory.ForEnergy(this) };

    private bool Used { get => _used; set { AssertMutable(); _used = value; } }
    private bool WasPart { get => _wasPart; set { AssertMutable(); _wasPart = value; } }

    public override Task AfterObtained()
    {
        WasPart = CombatManager.Instance.IsPartOfPlayerTurn(Owner);
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (CombatManager.Instance.IsInProgress && !Used && !cardPlay.IsAutoPlay && cardPlay.Card.Owner == Owner) Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Creature.Side || Used) return Task.CompletedTask;
        WasPart = participants.Contains(Owner.Creature);
        Status = WasPart ? RelicStatus.Active : RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override bool ShouldTakeExtraTurn(Player player) => !Used && !AnyPlayedThisTurn() && WasPart && player == Owner;

    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Beloved || !participants.Contains(Owner.Creature) || Used || AnyPlayedThisTurn() || !WasPart) return;
        foreach (var card in CardPile.GetCards(Owner, PileType.Hand).ToList()) await BondRewards.Exhaust(choiceContext, card);
    }

    public override Task AfterTakingExtraTurn(Player player)
    {
        if (player != Owner) return Task.CompletedTask;
        Flash();
        Status = RelicStatus.Normal;
        Used = true;
        _energyDue = true;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !_energyDue) return;
        _energyDue = false;
        Flash();
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        Status = RelicStatus.Normal;
        Used = false;
        _energyDue = false;
        return Task.CompletedTask;
    }

    private bool AnyPlayedThisTurn()
    {
        var state = Owner.PlayerCombatState;
        // Whispering Earring's autoplay on turn 1 counts as playing (vanilla checks for it; ours too)
        if (state != null && state.TurnNumber == 1 && Owner.Relics.Any(r => r.GetType().Name.EndsWith("WhisperingEarring"))) return true;
        return CombatManager.Instance.History.CardPlaysFinished.Any(e =>
            e.Actor == Owner.Creature && e.HappenedThisTurn(Owner.Creature.CombatState) && !e.CardPlay.IsAutoPlay);
    }
}

public sealed class DevotedPaelsEye : BondPaelsEye { public override int Tier => 2; }
public sealed class BelovedPaelsEye : BondPaelsEye { public override int Tier => 3; }

/// <summary>Pael's Growth: two cards take the Clone (Devoted); Beloved, a deeper Clone on both.</summary>
public abstract class BondPaelsGrowth : BondRelic
{
    public override string OriginalName => "PaelsGrowth";
    public override bool HasUponPickupEffect => true;
    private decimal Amount => Beloved ? 5m : 4m;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new StringVar("EnchantmentName", ModelDb.Enchantment<Clone>().Title.GetFormattedText()), new CardsVar(2), new("Clone", Amount),
    };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromEnchantment<Clone>();

    public override async Task AfterObtained()
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, DynamicVars.Cards.IntValue);
        foreach (var card in await CardSelectCmd.FromDeckForEnchantment(prefs: prefs, player: Owner, enchantment: ModelDb.Enchantment<Clone>(), amount: 1))
        {
            CardCmd.Enchant<Clone>(card, Amount);
            CardCmd.Preview(card);
        }
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner) return false;
        options.Add(new CloneRestSiteOption(player));
        return true;
    }
}

public sealed class DevotedPaelsGrowth : BondPaelsGrowth { public override int Tier => 2; }
public sealed class BelovedPaelsGrowth : BondPaelsGrowth { public override int Tier => 3; }

/// <summary>Pael's Legion: it greets each fight with Block; Beloved, it also wakes every turn.</summary>
public abstract class BondPaelsLegion : BondRelic
{
    private string _skin = MegaCrit.Sts2.Core.Models.Relics.PaelsLegion.SkinOptions[0];
    private int _cooldown;
    private bool _blockedLastTurn;
    private CardPlay? _affected;

    public override string OriginalName => "PaelsLegion";
    public override bool AddsPet => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("Turns", Beloved ? 1m : 2m), new BlockVar(6m, ValueProp.Unpowered) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.Static(StaticHoverTip.Block) };

    [SavedProperty]
    public string Skin
    {
        get => _skin;
        set { AssertMutable(); _skin = value; }
    }

    public override bool ShowCounter => DisplayAmount > 0;
    public override int DisplayAmount => !CombatManager.Instance.IsInProgress || IsCanonical || _cooldown <= 0 ? -1 : _cooldown;

    private int Cooldown { get => _cooldown; set { AssertMutable(); _cooldown = value; InvokeDisplayAmountChanged(); } }
    private bool BlockedLastTurn { get => _blockedLastTurn; set { AssertMutable(); _blockedLastTurn = value; } }
    private CardPlay? Affected { get => _affected; set { AssertMutable(); _affected = value; } }

    private PaelsLegionPet? Pet => Owner.PlayerCombatState?.GetPet<PaelsLegionPet>()?.Monster as PaelsLegionPet;

    public override async Task AfterObtained()
    {
        Skin = RngCompat.For(Owner, Id).NextItem(MegaCrit.Sts2.Core.Models.Relics.PaelsLegion.SkinOptions) ?? Skin;
        if (CombatManager.Instance.IsInProgress) await PlayerCmd.AddPet<PaelsLegionPet>(Owner);
    }

    public override async Task BeforeCombatStart()
    {
        await PlayerCmd.AddPet<PaelsLegionPet>(Owner);
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Unpowered, null);
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!props.IsCardOrMonsterMove() || cardSource == null || cardSource.Owner != Owner || Cooldown > 0) return 1m;
        return Affected == null || Affected == cardPlay ? 2m : 1m;
    }

    public override Task AfterModifyingBlockAmount(decimal modifiedAmount, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (modifiedAmount <= 0m || cardPlay == null || (Affected != null && Affected != cardPlay)) return Task.CompletedTask;
        Affected = cardPlay;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Affected == null || Affected != cardPlay) return;
        Flash();
        Affected = null;
        Cooldown = DynamicVars["Turns"].IntValue;
        Status = RelicStatus.Normal;
        if (Pet is { } pet) await CreatureCmd.TriggerAnim(pet.Creature, "BlockTrigger", 0.15f);
        BlockedLastTurn = true;
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner.Creature)) return;
        var wasCooling = Cooldown > 0;
        Cooldown--;
        if (Cooldown <= 0)
        {
            Status = RelicStatus.Active;
            InvokeDisplayAmountChanged();
        }
        if (Pet is { } pet)
        {
            if (Cooldown > 0 && BlockedLastTurn) await CreatureCmd.TriggerAnim(pet.Creature, "SleepTrigger", 0.15f);
            else if (Cooldown <= 0 && wasCooling) await CreatureCmd.TriggerAnim(pet.Creature, "WakeUpTrigger", 0.15f);
        }
        BlockedLastTurn = false;
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        Status = RelicStatus.Normal;
        Cooldown = 0;
        BlockedLastTurn = false;
        Affected = null;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}

public sealed class DevotedPaelsLegion : BondPaelsLegion { public override int Tier => 2; }
public sealed class BelovedPaelsLegion : BondPaelsLegion { public override int Tier => 3; }

/// <summary>The Legion pet reads its skin from the vanilla relic; with ours instead, read it from ours.</summary>
[HarmonyPatch(typeof(PaelsLegionPet), nameof(PaelsLegionPet.SetupSkins))]
internal static class BondPaelsLegionSkin
{
    [HarmonyPrefix]
    private static bool Prefix(PaelsLegionPet __instance, MegaSkeleton skeleton)
    {
        try
        {
            if (!__instance.IsMutable) return true;
            var owner = __instance.Creature?.PetOwner;
            if (owner == null || owner.GetRelic<MegaCrit.Sts2.Core.Models.Relics.PaelsLegion>() != null) return true;
            if (owner.Relics.OfType<BondPaelsLegion>().FirstOrDefault() is not { } bond) return true;
            skeleton.SetSkin(skeleton.GetData().FindSkin(bond.Skin));
            skeleton.SetSlotsToSetupPose();
            return false;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Bond] Legion skin: {ex.Message}", 1);
            return true;
        }
    }
}
