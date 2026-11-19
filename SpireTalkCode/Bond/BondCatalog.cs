using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace SpireTalk.Bond;

/// <summary>
/// Hands our 28 pairs to Ancient Affection. That mod "awakens" an Ancient's relic options into its
/// Devoted / Beloved versions by affection, looking each vanilla relic type up in its private
/// <c>AncientAffectionRelicUpgrades.UpgradeTypes</c> table; the relics it decided not to upgrade are
/// simply missing from that table, so adding them there is all it takes for its own roll, reveal
/// effect and co-op plan to include them. Without that mod nothing here runs: there is no relic
/// awakening to plug into. Everything is by reflection and fails quietly.
/// </summary>
public static class BondCatalog
{
    /// <summary>Vanilla relic class name → (Devoted, Beloved).</summary>
    public static readonly (string Original, Type Devoted, Type Beloved)[] Pairs =
    {
        ("DowsingRod", typeof(DevotedDowsingRod), typeof(BelovedDowsingRod)),
        ("NeowsSacrifice", typeof(DevotedNeowsSacrifice), typeof(BelovedNeowsSacrifice)),
        ("Astrolabe", typeof(DevotedAstrolabe), typeof(BelovedAstrolabe)),
        ("BlackStar", typeof(DevotedBlackStar), typeof(BelovedBlackStar)),
        ("CallingBell", typeof(DevotedCallingBell), typeof(BelovedCallingBell)),
        ("DustyTome", typeof(DevotedDustyTome), typeof(BelovedDustyTome)),
        ("Ectoplasm", typeof(DevotedEctoplasm), typeof(BelovedEctoplasm)),
        ("PhilosophersStone", typeof(DevotedPhilosophersStone), typeof(BelovedPhilosophersStone)),
        ("RunicPyramid", typeof(DevotedRunicPyramid), typeof(BelovedRunicPyramid)),
        ("SneckoEye", typeof(DevotedSneckoEye), typeof(BelovedSneckoEye)),
        ("BrilliantScarf", typeof(DevotedBrilliantScarf), typeof(BelovedBrilliantScarf)),
        ("DelicateFrond", typeof(DevotedDelicateFrond), typeof(BelovedDelicateFrond)),
        ("FurCoat", typeof(DevotedFurCoat), typeof(BelovedFurCoat)),
        ("Glitter", typeof(DevotedGlitter), typeof(BelovedGlitter)),
        ("SignetRing", typeof(DevotedSignetRing), typeof(BelovedSignetRing)),
        ("PaelsClaw", typeof(DevotedPaelsClaw), typeof(BelovedPaelsClaw)),
        ("PaelsEye", typeof(DevotedPaelsEye), typeof(BelovedPaelsEye)),
        ("PaelsGrowth", typeof(DevotedPaelsGrowth), typeof(BelovedPaelsGrowth)),
        ("PaelsLegion", typeof(DevotedPaelsLegion), typeof(BelovedPaelsLegion)),
        ("Driftwood", typeof(DevotedDriftwood), typeof(BelovedDriftwood)),
        ("PrismaticGem", typeof(DevotedPrismaticGem), typeof(BelovedPrismaticGem)),
        ("SeaGlass", typeof(DevotedSeaGlass), typeof(BelovedSeaGlass)),
        ("TouchOfOrobas", typeof(DevotedTouchOfOrobas), typeof(BelovedTouchOfOrobas)),
        ("LordsParasol", typeof(DevotedLordsParasol), typeof(BelovedLordsParasol)),
        ("MusicBox", typeof(DevotedMusicBox), typeof(BelovedMusicBox)),
        ("WhisperingEarring", typeof(DevotedWhisperingEarring), typeof(BelovedWhisperingEarring)),
        ("GoldenCompass", typeof(DevotedGoldenCompass), typeof(BelovedGoldenCompass)),
        ("MeatCleaver", typeof(DevotedMeatCleaver), typeof(BelovedMeatCleaver)),
    };

    private static readonly ConcurrentDictionary<string, Type?> Vanilla = new();

    /// <summary>The vanilla relic class, or null on a game branch that doesn't have it.</summary>
    public static Type? VanillaType(string name) =>
        Vanilla.GetOrAdd(name, n => AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.Relics." + n));

    private static bool _injected;

    /// <summary>Put our pairs into Ancient Affection's table (once; relics it upgrades itself keep its version).</summary>
    public static void EnsureInjected()
    {
        if (_injected) return;
        _injected = true;
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "AncientAffection", StringComparison.OrdinalIgnoreCase));
            var upgrades = asm?.GetType("AncientAffection.AncientAffectionRelicUpgrades", throwOnError: false);
            if (upgrades == null) return;
            if (AccessTools.Field(upgrades, "UpgradeTypes")?.GetValue(null) is not IDictionary<Type, Type[]> table)
            {
                MainFile.Logger.Warn("[Bond] Ancient Affection's upgrade table not found; its relic awakening won't include ours", 1);
                return;
            }
            int added = 0, skipped = 0;
            foreach (var (original, devoted, beloved) in Pairs)
            {
                var type = VanillaType(original);
                if (type == null) { skipped++; continue; } // not on this game branch
                if (table.ContainsKey(type)) { skipped++; continue; } // a later Ancient Affection made its own
                table[type] = new[] { devoted, beloved };
                added++;
            }
            MainFile.Logger.Info($"[Bond] added {added} Devoted/Beloved pairs to Ancient Affection ({skipped} skipped)", 1);

            // Patched here rather than by attribute: Ancient Affection may load after us.
            var create = AccessTools.Method(upgrades, "CreateForTier", new[] { typeof(RelicModel), typeof(int) });
            if (create != null)
                new Harmony("io.yumina.spiretalk.bond").Patch(create,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(BondSetupFromOriginal), nameof(BondSetupFromOriginal.Postfix))));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Bond] could not reach Ancient Affection: {ex.GetType().Name}: {ex.Message}", 1);
        }
    }
}

/// <summary>Before any Ancient builds its page (and so before Ancient Affection looks at it), our pairs are in.</summary>
[HarmonyPatch(typeof(AncientEventModel), "GenerateInitialOptionsWrapper")]
internal static class BondInjectBeforeAncient
{
    [HarmonyPrefix]
    private static void Prefix() => BondCatalog.EnsureInjected();
}

/// <summary>
/// Ancient Affection builds the upgraded relic fresh from the model database; the vanilla one it
/// replaces may carry what the Ancient's page set up (Sea Glass's character and so on). Pass it on.
/// </summary>
internal static class BondSetupFromOriginal
{
    public static void Postfix(RelicModel original, RelicModel __result)
    {
        if (__result is not BondRelic bond || original == null || original is BondRelic) return;
        try { bond.SetupFrom(original); }
        catch (Exception ex) { MainFile.Logger.Warn($"[Bond] setup of {bond.GetType().Name} failed: {ex.Message}", 1); }
    }
}

/// <summary>
/// RelicReward(RelicRarity, Player) on 0.111; 0.113 added an optional Rng, so the two-argument
/// constructor a 0.111 build calls is gone there. Find whichever exists.
/// </summary>
internal static class BondRewards
{
    private static readonly System.Reflection.ConstructorInfo? ByRarity =
        typeof(MegaCrit.Sts2.Core.Rewards.RelicReward).GetConstructors().FirstOrDefault(c =>
        {
            var p = c.GetParameters();
            return p.Length >= 2 && p[0].ParameterType == typeof(MegaCrit.Sts2.Core.Entities.Relics.RelicRarity)
                && p[1].ParameterType == typeof(MegaCrit.Sts2.Core.Entities.Players.Player) && p.Skip(2).All(x => x.HasDefaultValue);
        });

    public static MegaCrit.Sts2.Core.Rewards.Reward Relic(MegaCrit.Sts2.Core.Entities.Relics.RelicRarity rarity, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (ByRarity == null) return new MegaCrit.Sts2.Core.Rewards.RelicReward(player);
        var ps = ByRarity.GetParameters();
        var args = new object?[ps.Length];
        args[0] = rarity;
        args[1] = player;
        for (var i = 2; i < ps.Length; i++) args[i] = ps[i].DefaultValue;
        return (MegaCrit.Sts2.Core.Rewards.Reward)ByRarity.Invoke(args);
    }

    // CardCmd.Exhaust returns Task<CardPileAddResult?> from 0.111; 0.107 has a different signature.
    private static readonly System.Reflection.MethodInfo? ExhaustMethod =
        typeof(MegaCrit.Sts2.Core.Commands.CardCmd).GetMethods().FirstOrDefault(m =>
        {
            if (m.Name != "Exhaust") return false;
            var p = m.GetParameters();
            return p.Length >= 2 && p[0].ParameterType == typeof(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext)
                && p[1].ParameterType == typeof(MegaCrit.Sts2.Core.Models.CardModel) && p.Skip(2).All(x => x.HasDefaultValue);
        });

    public static Task Exhaust(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext ctx, MegaCrit.Sts2.Core.Models.CardModel card)
    {
        if (ExhaustMethod == null) return Task.CompletedTask;
        var ps = ExhaustMethod.GetParameters();
        var args = new object?[ps.Length];
        args[0] = ctx;
        args[1] = card;
        for (var i = 2; i < ps.Length; i++) args[i] = ps[i].DefaultValue;
        return ExhaustMethod.Invoke(null, args) as Task ?? Task.CompletedTask;
    }
}
