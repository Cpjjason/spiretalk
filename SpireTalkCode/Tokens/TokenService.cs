using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace SpireTalk.Tokens;

/// <summary>Hands out and retires the relics that stand for deals and road events.</summary>
public static class TokenService
{
    private static readonly Dictionary<string, Type> DealTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NEOW"] = typeof(DealNeow),
        ["OROBAS"] = typeof(DealOrobas),
        ["NONUPEIPE"] = typeof(DealNonupeipe),
        ["PAEL"] = typeof(DealPael),
        ["DARV"] = typeof(DealDarv),
        ["TANX"] = typeof(DealTanx),
        ["TEZCATARA"] = typeof(DealTezcatara),
        ["VAKUU"] = typeof(DealVakuu),
        ["MERCHANT"] = typeof(DealMerchant),
    };

    public static T? Owned<T>(Player? player) where T : RelicModel
    {
        try
        {
            return player?.Relics.OfType<T>().FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static DealRelic? DealRelicOf(Player? player, string npcKey)
    {
        try
        {
            return player?.Relics.OfType<DealRelic>().FirstOrDefault(r => string.Equals(r.NpcKey, npcKey, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The deal was just accepted: its relic joins the bar (one per NPC; an old greyed one is replaced).</summary>
    public static async Task GiveDealAsync(Player? player, string npcKey)
    {
        if (player == null || !DealTypes.TryGetValue(npcKey, out var type)) return;
        var old = DealRelicOf(player, npcKey);
        if (old != null) await RemoveAsync(old);
        await GiveAsync(type, player);
    }

    /// <summary>A kept deal leaves the bar with a flash; a broken one greys out and stays.</summary>
    public static async Task SettleDealAsync(Player? player, string npcKey, bool kept)
    {
        var relic = DealRelicOf(player, npcKey);
        if (relic == null) return;
        relic.Flash();
        if (kept) await RemoveAsync(relic);
        else relic.Refresh();
    }

    /// <summary>Progress moved (gold spent): redraw the counter and the tooltip.</summary>
    public static void RefreshDeals(Player? player)
    {
        try
        {
            foreach (var r in player?.Relics.OfType<DealRelic>() ?? Enumerable.Empty<DealRelic>()) r.Refresh();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Relic] deal refresh failed: {ex.Message}", 1);
        }
    }

    public static async Task GiveFavorAsync(Player player)
    {
        var grudge = Owned<MerchantGrudge>(player);
        if (grudge != null) await RemoveAsync(grudge);
        var favor = Owned<MerchantFavor>(player);
        if (favor != null) await RemoveAsync(favor);
        await GiveAsync(typeof(MerchantFavor), player);
    }

    public static async Task GiveGrudgeAsync(Player player)
    {
        var favor = Owned<MerchantFavor>(player);
        if (favor != null) await RemoveAsync(favor);
        if (Owned<MerchantGrudge>(player) == null) await GiveAsync(typeof(MerchantGrudge), player);
    }

    /// <summary>The shop just used the favour up.</summary>
    public static void FavorSpent(Player? player)
    {
        var favor = Owned<MerchantFavor>(player);
        if (favor == null) return;
        favor.Flash();
        favor.Refresh();
    }

    public static void GrudgeCharged(Player? player) => Owned<MerchantGrudge>(player)?.Flash();

    private static async Task GiveAsync(Type type, Player player)
    {
        try
        {
            var relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(type)).ToMutable();
            relic.Owner = player;
            await RelicCmd.Obtain(relic, player);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Relic] give {type.Name} failed: {ex.GetType().Name}: {ex.Message}", 1);
        }
    }

    private static async Task RemoveAsync(RelicModel relic)
    {
        try
        {
            await RelicCmd.Remove(relic);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Relic] remove {relic.Id.Entry} failed: {ex.GetType().Name}: {ex.Message}", 1);
        }
    }
}
