using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;

namespace SpireTalk.Bond;

// Neow's two newest relics (game 0.111+). Neow looks after you by carrying part of the price herself.

/// <summary>Dowsing Rod: the Dowsing card starts part of the way there.</summary>
public abstract class BondDowsingRod : BondRelic
{
    public override string OriginalName => "DowsingRod";
    public override bool HasUponPickupEffect => true;

    /// <summary>"?" rooms the Dowsing still needs (vanilla 4).</summary>
    protected abstract int Rooms { get; }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("Rooms", Rooms) };
    // Dowsing is new in 0.111: on 0.107 the relic never drops, but the compendium may still ask for its tips
    protected override IEnumerable<IHoverTip> ExtraHoverTips { get { try { return Tips(); } catch (TypeLoadException) { return Array.Empty<IHoverTip>(); } } }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static IEnumerable<IHoverTip> Tips() => HoverTipFactory.FromCardWithCardHoverTips<Dowsing>();

    public override async Task AfterObtained()
    {
        var card = (Dowsing)Owner.RunState.CreateCard<Dowsing>(Owner);
        card.RoomsEntered = Dowsing.maxRooms - Rooms;
        CardCmd.PreviewCardPileAdd(new[] { await CardPileCmd.Add(card, PileType.Deck) }, 2f);
    }
}

public sealed class DevotedDowsingRod : BondDowsingRod
{
    public override int Tier => 2;
    protected override int Rooms => 3;
}

public sealed class BelovedDowsingRod : BondDowsingRod
{
    public override int Tier => 3;
    protected override int Rooms => 2;
}

/// <summary>Neow's Sacrifice: the Guilty leaves sooner; Beloved, Neow keeps it.</summary>
public abstract class BondNeowsSacrifice : BondRelic
{
    public override string OriginalName => "NeowsSacrifice";
    public override bool HasUponPickupEffect => true;

    /// <summary>Combats the Guilty stays (vanilla 5); 0 = no Guilty at all.</summary>
    protected abstract int Combats { get; }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("Combats", Combats) };

    // Ambergris is new in 0.111: on 0.107 the relic never drops, but the compendium may still ask for its tips
    protected override IEnumerable<IHoverTip> ExtraHoverTips { get { try { return Tips(Combats); } catch (TypeLoadException) { return Array.Empty<IHoverTip>(); } } }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static IEnumerable<IHoverTip> Tips(int combats)
    {
        var tips = new List<IHoverTip> { HoverTipFactory.FromPotion<Ambergris>() };
        if (combats > 0) tips.AddRange(HoverTipFactory.FromCardWithCardHoverTips<Guilty>());
        return tips;
    }

    public override async Task AfterObtained()
    {
        await PotionCmd.TryToProcure<Ambergris>(Owner);
        if (Combats <= 0) return;
        var card = (Guilty)Owner.RunState.CreateCard<Guilty>(Owner);
        card.CombatsSeen = Guilty.maxCombats - Combats;
        CardCmd.PreviewCardPileAdd(new[] { await CardPileCmd.Add(card, PileType.Deck) }, 2f);
    }
}

public sealed class DevotedNeowsSacrifice : BondNeowsSacrifice
{
    public override int Tier => 2;
    protected override int Combats => 3;
}

public sealed class BelovedNeowsSacrifice : BondNeowsSacrifice
{
    public override int Tier => 3;
    protected override int Combats => 0;
}
