using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using SpireTalk.Affection;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;

namespace SpireTalk.Merchant;

/// <summary>
/// An Ancient the climber is close to may leave one of their own relics with the Merchant.
/// Ancient-rarity relics are otherwise only handed out in the Ancients' halls (the game prices
/// them at 999,999,999 so they never enter a shop); here the third shelf slot is swapped for one,
/// priced like a rare, when an Ancient at friendly+ rolls the favour. Neow's cursed set stays out.
/// </summary>
[HarmonyPatch]
internal static class AncientRelicShelf
{
    private const int BasePrice = 340;

    private static readonly FieldInfo? CostField = AccessTools.Field(typeof(MerchantEntry), "_cost");
    private static readonly FieldInfo? RelicEntriesField = AccessTools.Field(typeof(MerchantInventory), "_relicEntries");

    /// <summary>Relics an Ancient hands out as a punishment or a trap; never "a favour".</summary>
    private static readonly HashSet<string> Blacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "CURSED_PEARL", "DOWSING_ROD", "HEFTY_TABLET", "LARGE_CAPSULE", "LEAFY_POULTICE",
        "NEOWS_BONES", "NEOWS_SACRIFICE", "PRECARIOUS_SHEARS", "SILKEN_TRESS", "SILVER_CRUCIBLE",
    };

    [HarmonyPostfix, HarmonyPatch(typeof(MerchantInventory), nameof(MerchantInventory.CreateForNormalMerchant))]
    private static void AfterCreate(Player player, MerchantInventory __result)
    {
        try
        {
            Stock(player, __result);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Shelf] failed: {ex.GetType().Name}: {ex.Message}", 1);
        }
    }

    private static void Stock(Player player, MerchantInventory inventory)
    {
        if (player.RunState.Players.Count > 1) return;
        if (RelicEntriesField?.GetValue(inventory) is not List<MerchantRelicEntry> entries || entries.Count < 3) return;

        var runKey = RewardExecutor.RunKey(player);
        var run = TalkStore.Instance.GetRun(runKey);
        if (run.ShelfRelic != null) return; // one favour per run

        // The Ancient the climber is closest to, if that is friendly or better.
        NpcDefinition? patron = null;
        var best = ConversationRules.Tier.Stranger;
        foreach (var npc in NpcCatalog.All.Where(n => n.Kind == NpcKind.Ancient))
        {
            var (affection, married) = AffectionService.Read(npc.Key);
            var tier = ConversationRules.TierOf(affection, married);
            if (tier > best)
            {
                best = tier;
                patron = npc;
            }
        }
        if (patron == null || best < ConversationRules.Tier.Friendly) return;

        var chance = best switch
        {
            ConversationRules.Tier.Devoted => 1.0,
            ConversationRules.Tier.Close => 0.5,
            _ => 0.3,
        };
        if (Random.Shared.NextDouble() > chance) return;

        var relic = PickRelic(patron.Key, player, entries);
        if (relic == null) return;

        var entry = new MerchantRelicEntry(relic, player);
        var price = (int)Math.Round(BasePrice * player.PlayerRng.Shops.NextFloat(0.9f, 1.15f));
        CostField?.SetValue(entry, price);
        entries[2] = entry;

        run.ShelfAncient = patron.Key;
        run.ShelfRelic = relic.Id.Entry;
        TalkStore.Instance.Save();
        var title = SafeTitle(relic);
        RewardExecutor.Toast(string.Format(Loc.EventText("shelfToast"), patron.DisplayName, title));
        MainFile.Logger.Info($"[Shelf] {patron.Key} left {relic.Id.Entry} ({price} gold), tier {best}", 1);
    }

    /// <summary>One of the patron's own relics the climber does not carry and the shop does not already show.</summary>
    private static RelicModel? PickRelic(string ancientKey, Player player, List<MerchantRelicEntry> entries)
    {
        var ancient = ModelDb.AllAncients.FirstOrDefault(a => string.Equals(a.Id.Entry, ancientKey, StringComparison.OrdinalIgnoreCase));
        if (ancient == null) return null;
        var owned = new HashSet<string>(player.Relics.Select(r => r.Id.Entry), StringComparer.OrdinalIgnoreCase);
        var shown = new HashSet<string>(entries.Select(e => e.Model?.Id.Entry).OfType<string>(), StringComparer.OrdinalIgnoreCase);
        var pool = ancient.AllPossibleOptions
            .Select(o => o.Relic)
            .OfType<RelicModel>()
            .Where(r => r.Rarity == RelicRarity.Ancient)
            .Where(r => !Blacklist.Contains(r.Id.Entry) && !owned.Contains(r.Id.Entry) && !shown.Contains(r.Id.Entry))
            .GroupBy(r => r.Id.Entry)
            .Select(g => g.First())
            .ToList();
        if (pool.Count == 0) return null;
        var picked = pool[Random.Shared.Next(pool.Count)];
        var mutable = picked.CanonicalInstance.ToMutable();
        mutable.Owner = player;
        return mutable;
    }

    /// <summary>What the Merchant says about it, for the `shop_special` prompt variable.</summary>
    public static string PromptFor(Player player)
    {
        try
        {
            var run = TalkStore.Instance.GetRun(RewardExecutor.RunKey(player));
            if (run.ShelfRelic == null || run.ShelfAncient == null) return "none";
            var patron = NpcCatalog.Get(run.ShelfAncient)?.Name ?? run.ShelfAncient;
            var relic = ModelDb.AllAncients
                .SelectMany(a => a.AllPossibleOptions)
                .Select(o => o.Relic)
                .OfType<RelicModel>()
                .FirstOrDefault(r => string.Equals(r.Id.Entry, run.ShelfRelic, StringComparison.OrdinalIgnoreCase));
            var title = relic != null ? SafeTitle(relic) : run.ShelfRelic;
            var owned = player.Relics.Any(r => string.Equals(r.Id.Entry, run.ShelfRelic, StringComparison.OrdinalIgnoreCase));
            return owned
                ? $"{patron} left their relic \"{title}\" with you for this climber, and they have bought it."
                : $"{patron} left their relic \"{title}\" with you for this climber; it is on your shelf now, priced as a favour.";
        }
        catch
        {
            return "none";
        }
    }

    private static string SafeTitle(RelicModel relic)
    {
        try
        {
            return relic.Title.GetFormattedText();
        }
        catch
        {
            return relic.Id.Entry;
        }
    }
}
