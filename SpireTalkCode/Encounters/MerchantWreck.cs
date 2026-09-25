using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
using SpireTalk.Affection;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;

namespace SpireTalk.Encounters;

/// <summary>
/// The Merchant's cart has tipped over on the road. Help him gather it (a few HP; he gives a
/// potion and the next shop this run is cheaper), pocket something while he isn't looking (a
/// relic; he saw, and every shop this run costs more), or walk on. He remembers either way.
/// </summary>
public sealed class MerchantWreck : EncounterEvent
{
    public const int HelpHpLoss = 5;
    public const int FavorDiscount = 15;
    public const int GrudgeMarkup = 15;
    private const int HelpAffection = 30;
    private const int StealAffection = -60;
    private const int WalkAffection = -5;

    protected override string Key => "SPIRETALK-MERCHANT_WRECK";
    protected override string Scene => "merchant_wreck";
    protected override bool Waifu => EncounterArt.MerchantWaifu;
    protected override string FallbackPortrait => ImageHelper.GetImagePath("events/potion_courier.png");
    protected override NpcDefinition? NpcFor(Player? player) => NpcCatalog.Get("MERCHANT");

    protected override string Situation =>
        "Your cart has overturned on the road between floors. Your goods are scattered in the dirt and you are on your knees gathering them when the climber walks up.";

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new HpLossVar(HelpHpLoss) };

    protected override List<EventOption> Choices() => new()
    {
        Option(Help, "INITIAL"),
        Option(Pocket, "INITIAL"),
        Option(WalkOn, "INITIAL"),
    };

    private async Task Help()
    {
        if (Owner == null || Finished) return;
        var npc = Npc!;
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner.Creature, HelpHpLoss, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        AffectionService.Apply(npc.Key, NpcKind.Merchant, HelpAffection);
        var run = TalkStore.Instance.GetRun(RewardExecutor.RunKey(Owner));
        run.MerchantFavor = true;
        run.MerchantGrudge = false;
        TalkStore.Instance.Save();
        await Tokens.TokenService.GiveFavorAsync(Owner);

        var potionName = "";
        try
        {
            var potion = PotionFactory.CreateRandomPotionOutOfCombat(Owner, Rng.Chaotic).ToMutable();
            var got = await PotionCmd.TryToProcure(potion, Owner);
            if (got.success) potionName = potion.Title.GetFormattedText();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Encounter] wreck potion failed: {ex.Message}", 1);
        }

        EncounterVoice.Remember(npc, Owner, "memory.wreckHelped");
        await ReactAndFinish(
            $"The climber just helped you pick everything up and cut their hand doing it. You pressed a potion on them and promised them exactly {FavorDiscount}% off everything at your next shop - say that number if you mention it.",
            "pages.HELP.description",
            page => page.Add("Potion", potionName));
    }

    private async Task Pocket()
    {
        if (Owner == null || Finished) return;
        var npc = Npc!;
        var relicName = "";
        try
        {
            var relic = RelicFactory.PullNextRelicFromFront(Owner).ToMutable();
            relic.Owner = Owner;
            await RelicCmd.Obtain(relic, Owner, -1);
            relicName = relic.Title.GetFormattedText();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Encounter] wreck relic failed: {ex.Message}", 1);
        }
        AffectionService.Apply(npc.Key, NpcKind.Merchant, StealAffection);
        var run = TalkStore.Instance.GetRun(RewardExecutor.RunKey(Owner));
        run.MerchantGrudge = true;
        run.MerchantFavor = false;
        TalkStore.Instance.Save();
        await Tokens.TokenService.GiveGrudgeAsync(Owner);

        EncounterVoice.Remember(npc, Owner, "memory.wreckStole", relicName);
        await ReactAndFinish(
            $"While you were gathering your goods you looked up and saw the climber slip {relicName} from the pile into their pack. You will charge them {GrudgeMarkup}% more for the rest of this climb.",
            "pages.POCKET.description",
            page => page.Add("Relic", relicName));
    }

    private async Task WalkOn()
    {
        if (Owner == null || Finished) return;
        var npc = Npc!;
        AffectionService.Apply(npc.Key, NpcKind.Merchant, WalkAffection);
        EncounterVoice.Remember(npc, Owner, "memory.wreckWalked");
        await ReactAndFinish("The climber looked at the mess and walked on without stopping.", "pages.WALK_ON.description");
    }
}
