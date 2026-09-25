using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Deals;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;

namespace SpireTalk.Tokens;

/// <summary>
/// A deal in the relic bar. Accepting a deal hands the climber this relic; its tooltip carries the
/// terms, the reward and — for a shop deal — the gold still to spend, shown on the icon's counter.
/// Kept: it flashes, pays out and leaves the bar. Broken: it greys out and stays until the run ends,
/// the way a spent relic does. One class per NPC so each deal wears that NPC's picture.
///
/// The relic holds no state of its own: it reads the NPC's open (or broken) deal for this run from
/// the store, so a reloaded run shows exactly what the store says.
/// </summary>
[Pool(typeof(EventRelicPool))]
public abstract class DealRelic : CustomRelicModel
{
    public abstract string NpcKey { get; }

    /// <summary>Vanilla sprite used until the mod ships its own picture.</summary>
    protected abstract string BorrowedIcon { get; }

    protected override string IconBaseName => BorrowedIcon;

    private string IconName => "deal_" + NpcKey.ToLowerInvariant();
    private bool IsMerchant => NpcKey == "MERCHANT";

    public override string PackedIconPath => RelicArt.Icon(IconName, IsMerchant) ?? base.PackedIconPath;
    protected override string PackedIconOutlinePath => RelicArt.Outline(IconName, IsMerchant) ?? base.PackedIconOutlinePath;
    protected override string BigIconPath => RelicArt.Big(IconName, IsMerchant) ?? base.BigIconPath;

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => runState.Players.Count == 1;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new StringVar("Terms"),
        new StringVar("State"),
        new("Affection", DealService.KeptAffection),
        new("Gold", DealService.KeptGold),
    };

    /// <summary>The deal this relic stands for: this NPC's open or broken deal in the current run.</summary>
    public TalkStore.DealRecord? Deal
    {
        get
        {
            try
            {
                var run = RewardExecutor.RunKey(Owner);
                return TalkStore.Instance.Deals.LastOrDefault(d => d.Npc == NpcKey && d.Run == run && (d.Status == "pending" || d.Status == "broken"));
            }
            catch
            {
                return null;
            }
        }
    }

    public bool Broken => Deal?.Status == "broken";

    public override bool IsUsedUp => Broken;

    public override bool ShowCounter => Deal is { Status: "pending", Type: "spend_at_shop" };

    public override int DisplayAmount
    {
        get
        {
            var d = Deal;
            return d == null ? 0 : Math.Max(0, d.Param - d.Progress);
        }
    }

    public override LocString Title
    {
        get
        {
            Refresh(); // the tooltip reads the vars right after the title; keep them current
            var title = new LocString("relics", Id.Entry + ".title");
            title.Add("Npc", NpcCatalog.Get(NpcKey)?.DisplayName ?? NpcKey);
            return title;
        }
    }

    /// <summary>Pull the terms and state from the store into the description, counter and grey-out.</summary>
    public void Refresh()
    {
        if (!IsMutable) return;
        var d = Deal;
        ((StringVar)DynamicVars["Terms"]).StringValue = d != null ? DealService.Describe(d) : "";
        var state = d switch
        {
            null => "",
            { Status: "broken" } => Loc.EventText("dealRelic.broken"),
            { Type: "spend_at_shop" } => string.Format(Loc.EventText("dealRelic.spent"), d.Progress, d.Param),
            _ => "",
        };
        ((StringVar)DynamicVars["State"]).StringValue = state;
        var status = d?.Status == "broken" ? RelicStatus.Disabled : RelicStatus.Normal;
        if (Status != status) Status = status;
        InvokeDisplayAmountChanged();
    }

    public override async Task AfterObtained()
    {
        Refresh();
        await base.AfterObtained();
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        Refresh();
        return Task.CompletedTask;
    }
}

public sealed class DealNeow : DealRelic
{
    public override string NpcKey => "NEOW";
    protected override string BorrowedIcon => "neows_talisman";
}

public sealed class DealOrobas : DealRelic
{
    public override string NpcKey => "OROBAS";
    protected override string BorrowedIcon => "radiant_pearl";
}

public sealed class DealNonupeipe : DealRelic
{
    public override string NpcKey => "NONUPEIPE";
    protected override string BorrowedIcon => "jewelry_box";
}

public sealed class DealPael : DealRelic
{
    public override string NpcKey => "PAEL";
    protected override string BorrowedIcon => "paels_eye";
}

public sealed class DealDarv : DealRelic
{
    public override string NpcKey => "DARV";
    protected override string BorrowedIcon => "dusty_tome";
}

public sealed class DealTanx : DealRelic
{
    public override string NpcKey => "TANX";
    protected override string BorrowedIcon => "tanxs_whistle";
}

public sealed class DealTezcatara : DealRelic
{
    public override string NpcKey => "TEZCATARA";
    protected override string BorrowedIcon => "storybook";
}

public sealed class DealVakuu : DealRelic
{
    public override string NpcKey => "VAKUU";
    protected override string BorrowedIcon => "jeweled_mask";
}

public sealed class DealMerchant : DealRelic
{
    public override string NpcKey => "MERCHANT";
    protected override string BorrowedIcon => "membership_card";
}
