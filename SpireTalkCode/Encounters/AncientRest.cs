using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SpireTalk.Affection;
using SpireTalk.Keepsakes;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;

namespace SpireTalk.Encounters;

/// <summary>
/// An Ancient the climber knows, sitting by the road outside their own domain, worn out. For
/// once they are the one who needs something: a stranger asks for gold, a friend for a potion,
/// and someone close asks for nothing but company. Giving (or sitting with them) earns
/// affection and a blessing on one card; refusing costs a little. Only Ancients the climber
/// has talked to turn up; the fonder they are, the likelier.
/// </summary>
public sealed class AncientRest : EncounterEvent
{
    private const int StrangerGold = 40;
    private const int FriendGold = 25;
    private const int GiveAffection = 6;
    private const int SitAffection = 4;
    private const int RefuseAffection = -2;

    private enum Ask { Nothing, Gold, Potion }

    protected override string Key => "SPIRETALK-ANCIENT_REST";
    protected override string Scene => "ancient_rest_" + (Npc?.Key.ToLowerInvariant() ?? "unknown");
    protected override bool Waifu => EncounterArt.AncientWaifus;
    protected override string FallbackPortrait => ImageHelper.GetImagePath("events/wellspring.png");

    protected override NpcDefinition? NpcFor(Player? player)
    {
        if (player == null) return null;
        var run = TalkStore.Instance.GetRun(RewardExecutor.RunKey(player));
        if (run.RestAncient != null) return NpcCatalog.Get(run.RestAncient);
        return Pick(player);
    }

    /// <summary>An Ancient this climber has talked to, weighted by how fond they are; the same one all run.</summary>
    private static NpcDefinition? Pick(Player player)
    {
        var known = NpcCatalog.All
            .Where(n => n.Kind == NpcKind.Ancient && Knows(n.Key))
            .Select(n => (npc: n, weight: Math.Max(1, AffectionService.Read(n.Key).value + 60)))
            .ToList();
        if (known.Count == 0) return null;
        var roll = new Random(RewardExecutor.RunKey(player).GetHashCode()).Next(known.Sum(k => k.weight));
        foreach (var (npc, weight) in known)
        {
            if (roll < weight) return npc;
            roll -= weight;
        }
        return known[^1].npc;
    }

    /// <summary>The climber has met this Ancient through the mod: they talked, or there is affection either way.</summary>
    private static bool Knows(string key)
    {
        var record = TalkStore.Instance.Get(key);
        return record.Talks > 0 || record.History.Count > 0 || AffectionService.Read(key).value != 0;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (Owner != null && Npc is { } npc)
        {
            var run = TalkStore.Instance.GetRun(RewardExecutor.RunKey(Owner));
            run.RestAncient ??= npc.Key;
            TalkStore.Instance.Save();
        }
        return base.GenerateInitialOptions();
    }

    private ConversationRules.Tier Tier
    {
        get
        {
            var (affection, married) = AffectionService.Read(Npc?.Key ?? "");
            return ConversationRules.TierOf(affection, married);
        }
    }

    /// <summary>What they ask of this climber: the closer they are, the less they need.</summary>
    private (Ask ask, int gold) Request
    {
        get
        {
            var tier = Tier;
            if (tier is ConversationRules.Tier.Close or ConversationRules.Tier.Devoted || Owner == null) return (Ask.Nothing, 0);
            var hasPotion = Owner.Potions.Any();
            var gold = tier == ConversationRules.Tier.Friendly ? FriendGold : StrangerGold;
            if (tier == ConversationRules.Tier.Friendly && hasPotion) return (Ask.Potion, 0);
            if (Owner.Gold >= gold) return (Ask.Gold, gold);
            return hasPotion ? (Ask.Potion, 0) : (Ask.Nothing, 0);
        }
    }

    protected override string Situation
    {
        get
        {
            var (ask, gold) = Request;
            var need = ask switch
            {
                Ask.Gold => $"You need {gold} gold from the climber and will ask for it plainly.",
                Ask.Potion => "You need one of the climber's potions and will ask for it plainly.",
                _ => "You need nothing from the climber; you are simply glad of the company.",
            };
            return "You are far from your own domain, resting on a stone by the road between floors, worn out and weaker than the climber has ever seen you. " + need;
        }
    }

    protected override void FillInitial(LocString loc)
    {
        loc.Add("Ask", AskText());
    }

    private string AskText()
    {
        var (ask, gold) = Request;
        var text = Text(ask switch { Ask.Gold => "askGold", Ask.Potion => "askPotion", _ => "askNothing" });
        text.Add("Gold", (decimal)gold);
        AddNames(text);
        return text.GetFormattedText();
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new GoldVar(StrangerGold) };

    public override void CalculateVars()
    {
        DynamicVars.Gold.BaseValue = Request.gold;
    }

    protected override List<EventOption> Choices()
    {
        var options = new List<EventOption>();
        switch (Request.ask)
        {
            case Ask.Gold: options.Add(Option(GiveGold, "INITIAL")); break;
            case Ask.Potion: options.Add(Option(GivePotion, "INITIAL")); break;
            default: options.Add(Option(SitWith, "INITIAL")); break;
        }
        // Walking away from someone who asked for nothing costs nothing.
        options.Add(Request.ask == Ask.Nothing ? Option(Leave, "INITIAL") : Option(Decline, "INITIAL"));
        return options;
    }

    private async Task GiveGold()
    {
        if (Owner == null || Finished) return;
        var gold = Request.gold;
        await PlayerCmd.LoseGold(gold, Owner, GoldLossType.Lost);
        await Grateful(GiveAffection, $"The climber gave you {gold} gold without arguing.", "memory.restGold", gold);
    }

    private async Task GivePotion()
    {
        if (Owner == null || Finished) return;
        var potion = Owner.Potions.FirstOrDefault();
        var name = "";
        if (potion != null)
        {
            name = potion.Title.GetFormattedText();
            await PotionCmd.Discard(potion);
        }
        await Grateful(GiveAffection, $"The climber gave you their {name}.", "memory.restPotion", name);
    }

    private async Task SitWith()
    {
        if (Owner == null || Finished) return;
        await Grateful(SitAffection, "The climber sat down beside you on the stone for a while and asked for nothing.", "memory.restSat");
    }

    /// <summary>The thanks: affection, a blessing on one card (if any can still be upgraded), the keepsake catching up, and their reply.</summary>
    private async Task Grateful(int affection, string whatHappened, string memoryKey, params object[] memoryArgs)
    {
        var npc = Npc!;
        AffectionService.Apply(npc.Key, NpcKind.Ancient, affection);
        var card = BlessOneCard(Owner!);
        try
        {
            await KeepsakeService.RefreshAsync(npc, Owner!, quiet: false);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Encounter] keepsake refresh failed: {ex.Message}", 1);
        }
        EncounterVoice.Remember(npc, Owner!, memoryKey, memoryArgs);
        if (card != null)
        {
            await ReactAndFinish(
                whatHappened + $" You touched their deck and strengthened {card} in thanks.",
                "pages.THANKS.description",
                page => page.Add("Card", card));
            return;
        }
        // Every card already upgraded: nothing left to strengthen, so the thanks is only the words.
        await ReactAndFinish(
            whatHappened + " You reached for their deck to strengthen a card in thanks, but every card in it is already as strong as it can be.",
            "pages.THANKS_NOCARD.description");
    }

    private async Task Decline()
    {
        if (Owner == null || Finished) return;
        var npc = Npc!;
        if (Request.ask != Ask.Nothing) AffectionService.Apply(npc.Key, NpcKind.Ancient, RefuseAffection);
        EncounterVoice.Remember(npc, Owner, "memory.restDeclined");
        await ReactAndFinish("The climber said no and moved on.", "pages.DECLINE.description");
    }

    private async Task Leave()
    {
        if (Owner == null || Finished) return;
        EncounterVoice.Remember(Npc!, Owner, "memory.restLeft");
        await ReactAndFinish("The climber nodded and moved on.", "pages.DECLINE.description");
    }

    /// <summary>Upgrade one random card that can still be upgraded. Returns its name, or null.</summary>
    private static string? BlessOneCard(Player player)
    {
        try
        {
            var candidates = player.Deck.Cards.Where(c => c.IsUpgradable).ToList();
            if (candidates.Count == 0) return null;
            var card = candidates[new Random().Next(candidates.Count)];
            CardCmd.Upgrade(card);
            return card.Title;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Encounter] blessing failed: {ex.Message}", 1);
            return null;
        }
    }
}
