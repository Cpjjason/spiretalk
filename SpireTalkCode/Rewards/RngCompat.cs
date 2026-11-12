using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace SpireTalk.Rewards;

/// <summary>
/// The game's <see cref="Rng"/> differs between branches: on 0.107 (public) the seeded constructor is
/// Rng(Player, ModelId, uint mixin, int counter) and the state is a seed plus a counter; on 0.111
/// (public-beta) it is Rng(Player, ModelId, ulong mixin) and the state round-trips through
/// SerializableRng. A direct call compiled against one branch throws MissingMethodException on the
/// other the moment the calling method is JIT-compiled, taking the whole method down with it (an
/// Orobas keepsake froze the end of every fight on 0.107, reported on Bilibili 2026-11-11). So every
/// such call goes through here, by reflection.
/// </summary>
public static class RngCompat
{
    private static readonly ConstructorInfo? SeededCtor = typeof(Rng).GetConstructors().FirstOrDefault(c =>
    {
        var p = c.GetParameters();
        return p.Length >= 2 && p[0].ParameterType == typeof(Player) && p[1].ParameterType == typeof(ModelId);
    });

    // 0.111: Rng.ToSerializable() / Rng.LoadFromSerializable(SerializableRng)
    private static readonly MethodInfo? ToSerializable = AccessTools.Method(typeof(Rng), "ToSerializable");
    private static readonly MethodInfo? LoadFromSerializable = AccessTools.Method(typeof(Rng), "LoadFromSerializable");

    // 0.107: Rng.Counter, and PlayerRngSet keeps its Rngs in a dictionary keyed by PlayerRngType
    private static readonly PropertyInfo? CounterProperty = AccessTools.Property(typeof(Rng), "Counter");

    /// <summary>The same seeded Rng the game would make for this player and model, on either branch.</summary>
    public static Rng For(Player player, ModelId id, ulong mixin = 0)
    {
        if (SeededCtor == null) throw new MissingMethodException("Rng(Player, ModelId, ...) not found");
        var ps = SeededCtor.GetParameters();
        var args = new object?[ps.Length];
        args[0] = player;
        args[1] = id;
        for (var i = 2; i < ps.Length; i++)
        {
            var t = ps[i].ParameterType;
            if (i == 2) args[i] = t == typeof(uint) ? unchecked((uint)mixin) : t == typeof(ulong) ? mixin : Convert.ChangeType(mixin, t);
            else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (t.IsValueType ? Activator.CreateInstance(t) : null);
        }
        return (Rng)SeededCtor.Invoke(args);
    }

    /// <summary>Remember where the player's reward Rng is, to put it back with <see cref="RestoreRewards"/>.</summary>
    public static object? SnapshotRewards(Player player)
    {
        var rng = player.PlayerRng.Rewards;
        if (ToSerializable != null) return ToSerializable.Invoke(rng, null);
        if (CounterProperty != null) return (int)CounterProperty.GetValue(rng)!;
        return null;
    }

    /// <summary>Put the player's reward Rng back where <see cref="SnapshotRewards"/> found it.</summary>
    public static void RestoreRewards(Player player, object? snapshot)
    {
        if (snapshot == null) return;
        var rng = player.PlayerRng.Rewards;
        if (LoadFromSerializable != null)
        {
            LoadFromSerializable.Invoke(rng, new[] { snapshot });
            return;
        }
        // 0.107 can only count forward: replace the reward Rng with a fresh one fast-forwarded to the
        // remembered position, the way PlayerRngSet.FromSerializable rebuilds it from a save.
        if (snapshot is not int counter || CounterProperty == null) return;
        if ((int)CounterProperty.GetValue(rng)! == counter) return;
        var set = player.PlayerRng;
        var rngs = AccessTools.Field(set.GetType(), "_rngs")?.GetValue(set) as System.Collections.IDictionary;
        var create = AccessTools.Method(set.GetType(), "CreateRng");
        var fastForward = AccessTools.Method(typeof(Rng), "FastForwardCounter");
        var key = rngs?.Keys.Cast<object>().FirstOrDefault(k => k.ToString() == "Rewards");
        if (rngs == null || create == null || fastForward == null || key == null)
        {
            MainFile.Logger.Warn("[Rng] could not rewind the reward Rng on this game version", 1);
            return;
        }
        var fresh = (Rng)create.Invoke(set, new[] { key })!;
        fastForward.Invoke(fresh, new object[] { counter });
        rngs[key] = fresh;
    }
}
