using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Encounters;
using SpireTalk.Rewards;
using SpireTalk.Store;

namespace SpireTalk.Tokens;

/// <summary>
/// What the overturned cart leaves behind, carried in the relic bar so the climber can see it:
/// helping earns the Merchant's favour (one cheaper shop, then spent); pocketing a relic earns his
/// grudge (every shop costs more for the rest of the run). The shop reads the run record; these
/// relics only show it.
/// </summary>
[Pool(typeof(EventRelicPool))]
public abstract class MerchantToken : CustomRelicModel
{
    protected abstract string IconName { get; }
    protected abstract string BorrowedIcon { get; }

    protected override string IconBaseName => BorrowedIcon;
    public override string PackedIconPath => RelicArt.Icon(IconName) ?? base.PackedIconPath;
    protected override string PackedIconOutlinePath => RelicArt.Outline(IconName) ?? base.PackedIconOutlinePath;
    protected override string BigIconPath => RelicArt.Big(IconName) ?? base.BigIconPath;

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => runState.Players.Count == 1;

    protected TalkStore.RunRecord? Run
    {
        get
        {
            try
            {
                return TalkStore.Instance.GetRun(RewardExecutor.RunKey(Owner));
            }
            catch
            {
                return null;
            }
        }
    }

    public void Refresh()
    {
        if (!IsMutable) return;
        var status = IsUsedUp ? RelicStatus.Disabled : RelicStatus.Normal;
        if (Status != status) Status = status;
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

/// <summary>Helped at the cart: the next shop is cheaper. Greys out once that shop has been visited.</summary>
public sealed class MerchantFavor : MerchantToken
{
    protected override string IconName => "merchant_favor";
    protected override string BorrowedIcon => "membership_card";

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("Discount", MerchantWreck.FavorDiscount) };

    public override bool IsUsedUp => Run is { MerchantFavor: false };
}

/// <summary>Pocketed a relic at the cart: every shop this run costs more. Never wears off.</summary>
public sealed class MerchantGrudge : MerchantToken
{
    protected override string IconName => "merchant_grudge";
    protected override string BorrowedIcon => "jeweled_mask";

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new("Markup", MerchantWreck.GrudgeMarkup) };
}
