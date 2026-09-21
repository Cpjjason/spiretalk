using System.Reflection;

namespace SpireTalk.Affection;

/// <summary>
/// Optional two-way bridge to the Ancient Affection mod (Workshop id 3750930021). When that mod is
/// loaded we read and write ITS affection store, so conversations move the same number that
/// unlocks its bond relics, red options and marriage. Everything is resolved by reflection and
/// wrapped in try/catch: if the mod is missing or its internals change, we silently fall back
/// to our own store and the game never notices.
/// </summary>
public static class AncientAffectionBridge
{
    private static bool _probed;
    private static Type? _store;
    private static MethodInfo? _get;
    private static MethodInfo? _increment;
    private static MethodInfo? _isMarried;
    private static MethodInfo? _getMerchant;
    private static MethodInfo? _incrementMerchant;

    /// <summary>True when Ancient Affection is loaded and its store API looks like what we expect.</summary>
    public static bool Available
    {
        get
        {
            Probe();
            return _store != null && _get != null && _increment != null;
        }
    }

    public static bool Enabled => SpireTalkConfig.UseAncientAffection && Available;

    private static void Probe()
    {
        if (_probed) return;
        _probed = true;
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(asm.GetName().Name, "AncientAffection", StringComparison.OrdinalIgnoreCase)) continue;
                _store = asm.GetType("AncientAffection.AncientAffectionStore", throwOnError: false);
                if (_store == null) continue;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                _get = _store.GetMethod("Get", flags, new[] { typeof(string) });
                _increment = _store.GetMethod("Increment", flags, new[] { typeof(string), typeof(int) });
                _isMarried = _store.GetMethod("IsMarried", flags, new[] { typeof(string) });
                _getMerchant = _store.GetMethod("GetMerchantAffection", flags, Type.EmptyTypes);
                _incrementMerchant = _store.GetMethod("IncrementMerchantAffection", flags, new[] { typeof(int) });
                MainFile.Logger.Info($"[AA] Ancient Affection detected: get={_get != null} inc={_increment != null} married={_isMarried != null} merchant={_getMerchant != null}", 1);
                return;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[AA] probe failed: {ex.Message}", 1);
            _store = null;
        }
    }

    public static int? Get(string npcKey)
    {
        try
        {
            if (!Available) return null;
            if (npcKey == "MERCHANT")
            {
                return _getMerchant?.Invoke(null, null) is int m ? m : null;
            }
            return _get!.Invoke(null, new object[] { npcKey }) is int v ? v : null;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[AA] Get({npcKey}) failed: {ex.Message}", 1);
            return null;
        }
    }

    public static bool IsMarried(string npcKey)
    {
        try
        {
            if (!Available || npcKey == "MERCHANT" || _isMarried == null) return false;
            return _isMarried.Invoke(null, new object[] { npcKey }) is true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ask Ancient Affection to pick one of ITS bond relics for this ancient (the same pool and
    /// upgrade tiers its own "show affection" option uses), without granting it. Null when the mod
    /// is missing or its internals moved. The caller obtains the relic through RelicCmd.
    /// </summary>
    public static object? PickBondRelic(object ancientEventModel, string npcKey, int affection)
    {
        try
        {
            if (!Available || _store == null) return null;
            var asm = _store.Assembly;
            var factory = asm.GetType("AncientAffection.AncientAffectionOptionFactory", throwOnError: false);
            var rngHelper = asm.GetType("AncientAffection.AncientAffectionSeededRng", throwOnError: false);
            var multiplayer = asm.GetType("AncientAffection.AncientAffectionMultiplayer", throwOnError: false);
            if (factory == null || rngHelper == null) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var forRng = rngHelper.GetMethod("For", flags);
            var select = factory.GetMethod("CreateBondRewardSelection", flags);
            if (forRng == null || select == null) return null;

            var rng = forRng.Invoke(null, new object?[] { ancientEventModel, "spiretalk_gift", $"affection={affection};utc={DateTime.UtcNow.Ticks}" });
            var owner = ancientEventModel.GetType().GetProperty("Owner")?.GetValue(ancientEventModel);
            var playerKey = "spiretalk";
            var stableKey = multiplayer?.GetMethod("StablePlayerKey", flags);
            if (stableKey != null) playerKey = stableKey.Invoke(null, new[] { owner }) as string ?? playerKey;

            var selection = select.Invoke(null, new object?[] { ancientEventModel, npcKey, affection, rng, playerKey });
            return selection?.GetType().GetProperty("Reward")?.GetValue(selection);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[AA] PickBondRelic({npcKey}) failed: {ex.Message}", 1);
            return null;
        }
    }

    /// <summary>Apply a delta to Ancient Affection's store. Returns the new value, or null when the write failed.</summary>
    public static int? Increment(string npcKey, int delta)
    {
        try
        {
            if (!Available || delta == 0) return Get(npcKey);
            if (npcKey == "MERCHANT")
            {
                if (_incrementMerchant == null) return null;
                _incrementMerchant.Invoke(null, new object[] { delta });
                return Get(npcKey);
            }
            _increment!.Invoke(null, new object[] { npcKey, delta });
            return Get(npcKey);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[AA] Increment({npcKey},{delta}) failed: {ex.Message}", 1);
            return null;
        }
    }
}
