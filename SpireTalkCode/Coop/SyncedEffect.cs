using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Keepsakes;

namespace SpireTalk.Coop;

/// <summary>
/// One change to the game that a conversation earned: gold, healing, a potion, a relic, a keepsake
/// growing, a relic reroll. Everything random is decided on the speaker's machine first and
/// travels as concrete ids, so applying it is the same on every peer.
/// </summary>
public sealed record Effect(string Kind, int Amount = 0, ModelId? Id = null, int Extra = -1, ulong Target = 0, string Npc = "");

/// <summary>
/// The single door every SpireTalk game change goes through. Solo: applied right here, as before.
/// Co-op: wrapped in a <see cref="SpireTalkEffectAction"/> and handed to the game's own action
/// queue, which the host orders and every peer executes, so the run stays identical everywhere
/// (the game checksums it after every event and drops a client that drifts).
/// </summary>
public static class Sync
{
    private static readonly Dictionary<uint, TaskCompletionSource<bool>> Pending = new();
    private static uint _nonce = (uint)Random.Shared.Next(1, int.MaxValue);

    /// <summary>Called on the owner's machine after a co-op reroll, with the fresh initial page.</summary>
    public static event Action<Player, IReadOnlyList<EventOption>>? Rerolled;

    private static readonly AsyncLocal<bool> Everywhere_ = new();

    /// <summary>
    /// Code that every peer is already running in lockstep (an event option the game synced by index,
    /// a relic hook): apply effects right here on each peer instead of queueing one per peer.
    /// </summary>
    public static Scope Everywhere() => new(true);

    /// <summary>Inside <see cref="Everywhere"/>, a step only the owner's machine decides (it reads their store): queue it.</summary>
    public static Scope Queued() => new(false);

    public readonly struct Scope : IDisposable
    {
        private readonly bool _previous;
        internal Scope(bool everywhere)
        {
            _previous = Everywhere_.Value;
            Everywhere_.Value = everywhere;
        }
        public void Dispose() => Everywhere_.Value = _previous;
    }

    public static async Task<bool> RunAsync(Player owner, Effect effect)
    {
        if (!Party.IsCoop(owner))
        {
            return await Effects.ApplyAsync(owner, effect, local: true);
        }
        if (Everywhere_.Value)
        {
            return await Effects.ApplyAsync(owner, effect, local: Party.IsLocal(owner));
        }

        uint nonce;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (Pending)
        {
            nonce = ++_nonce;
            Pending[nonce] = tcs;
        }
        try
        {
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new SpireTalkEffectAction(owner, effect, nonce));
        }
        catch (Exception ex)
        {
            lock (Pending) Pending.Remove(nonce);
            MainFile.Logger.Warn($"[Coop] could not enqueue {effect.Kind}: {ex.Message}", 1);
            return false;
        }
        // The host may take a moment; a lost request must not hang the conversation forever.
        var done = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        lock (Pending) Pending.Remove(nonce);
        if (done != tcs.Task)
        {
            MainFile.Logger.Warn($"[Coop] {effect.Kind} not confirmed by the host in time", 1);
            return false;
        }
        return tcs.Task.Result;
    }

    internal static void Complete(uint nonce, bool applied)
    {
        TaskCompletionSource<bool>? tcs;
        lock (Pending) Pending.TryGetValue(nonce, out tcs);
        tcs?.TrySetResult(applied);
    }

    internal static void RaiseRerolled(Player owner, IReadOnlyList<EventOption> options) => Rerolled?.Invoke(owner, options);
}

/// <summary>
/// Event options that only this machine runs (a chat, never a game change): the click must not be
/// broadcast, because the other peers' copies of the event would run their option at that index.
/// </summary>
public static class LocalOnly
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EventOption, object> Marked = new();

    public static EventOption Mark(EventOption option)
    {
        Marked.AddOrUpdate(option, Marker);
        return option;
    }

    public static bool Is(EventOption option) =>
        Marked.TryGetValue(option, out _) || (option.TextKey?.StartsWith("SPIRETALK_", StringComparison.Ordinal) ?? false);

    private static readonly object Marker = new();
}

/// <summary>Applies an <see cref="Effect"/>. Runs on every peer in co-op, so it must not read anything only one machine knows.</summary>
public static class Effects
{
    private static readonly MethodInfo GenerateOptions = AccessTools.Method(typeof(EventModel), "GenerateInitialOptionsWrapper");
    private static readonly MethodInfo SetEventState = AccessTools.Method(typeof(EventModel), "SetEventState", new[] { typeof(LocString), typeof(IEnumerable<EventOption>) });

    public static async Task<bool> ApplyAsync(Player owner, Effect e, bool local)
    {
        var target = e.Target != 0 ? Party.Find(owner.RunState, e.Target) ?? owner : owner;
        switch (e.Kind)
        {
            case "gold":
                await PlayerCmd.GainGold(e.Amount, target);
                return true;
            case "lose_gold":
                await PlayerCmd.LoseGold(Math.Min(e.Amount, target.Gold), target);
                return true;
            case "heal":
                await CreatureCmd.Heal(target.Creature, e.Amount);
                return true;
            case "potion":
            {
                if (e.Id == null) return false;
                var potion = ModelDb.GetById<PotionModel>(e.Id).ToMutable();
                var result = await PotionCmd.TryToProcure(potion, target);
                return result.success;
            }
            case "relic":
            {
                if (e.Id == null) return false;
                var relic = ModelDb.GetById<RelicModel>(e.Id).ToMutable();
                relic.Owner = target;
                if (relic is KeepsakeRelic keepsake)
                {
                    if (e.Extra >= 0) keepsake.EngravedCount = e.Extra;
                    if (e.Amount > 0) keepsake.Level = e.Amount;
                }
                await RelicCmd.Obtain(relic, target);
                if (relic is KeepsakeTezcatara tez) await tez.GrantSlotIfDue();
                return true;
            }
            case "relic_remove":
            {
                var relic = target.Relics.FirstOrDefault(r => e.Id != null && r.Id == e.Id);
                if (relic == null) return false;
                await RelicCmd.Remove(relic);
                return true;
            }
            case "keepsake":
            {
                // Level and engravings of a keepsake the target already carries.
                var keepsake = target.Relics.OfType<KeepsakeRelic>().FirstOrDefault(r => e.Id != null && r.Id == e.Id);
                if (keepsake == null) return false;
                if (e.Extra >= 0) keepsake.EngravedCount = e.Extra;
                if (e.Amount > 0 && e.Amount != keepsake.Level) keepsake.Level = e.Amount;
                else keepsake.ApplyValues();
                if (keepsake is KeepsakeTezcatara tez) await tez.GrantSlotIfDue();
                return true;
            }
            case "reroll":
                return Reroll(owner, local);
            default:
                MainFile.Logger.Warn($"[Coop] unknown effect {e.Kind}", 1);
                return false;
        }
    }

    /// <summary>
    /// Ask the owner's Ancient for a fresh page. Every peer holds an identical copy of that event
    /// (same seed, same Rng position), so each one lands on the same relics.
    /// </summary>
    private static bool Reroll(Player owner, bool local)
    {
        EventModel ev;
        try
        {
            ev = RunManager.Instance.EventSynchronizer.GetEventForPlayer(owner);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Coop] reroll: no event for {owner.NetId}: {ex.Message}", 1);
            return false;
        }
        if (GenerateOptions.Invoke(ev, null) is not IReadOnlyList<EventOption> fresh || fresh.Count == 0) return false;
        if (local)
        {
            // The owner's page is showing the conversation; it restores these when the talk ends.
            Sync.RaiseRerolled(owner, fresh);
        }
        else
        {
            SetEventState.Invoke(ev, new object[] { ev.Description ?? ev.InitialDescription, fresh });
        }
        return true;
    }
}

/// <summary>The queued form of an <see cref="Effect"/>; see <see cref="Sync"/>.</summary>
public sealed class SpireTalkEffectAction : GameAction
{
    public Player Player { get; }
    public Effect Effect { get; }
    public uint Nonce { get; }

    public SpireTalkEffectAction(Player player, Effect effect, uint nonce)
    {
        Player = player;
        Effect = effect;
        Nonce = nonce;
    }

    public override ulong OwnerId => Player.NetId;

    public override GameActionType ActionType => GameActionType.Any;

    protected override async Task ExecuteAction()
    {
        var local = Party.IsLocal(Player);
        var applied = false;
        try
        {
            applied = await Effects.ApplyAsync(Player, Effect, local);
            MainFile.Logger.Info($"[Coop] effect {Effect.Kind} {Effect.Amount} {Effect.Id?.Entry} for {Player.NetId}→{(Effect.Target == 0 ? Player.NetId : Effect.Target)}: {(applied ? "applied" : "skipped")}", 1);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Coop] effect {Effect.Kind} failed: {ex.GetType().Name}: {ex.Message}", 1);
        }
        if (local) Sync.Complete(Nonce, applied);
        else if (applied && Effect.Target != 0 && Effect.Target == Party.LocalNetId) CoopMessages.AnnounceGiftToMe(Player, Effect);
    }

    public override INetAction ToNetAction() => new NetSpireTalkEffectAction
    {
        kind = Effect.Kind,
        amount = Effect.Amount,
        extra = Effect.Extra,
        idCategory = Effect.Id?.Category ?? "",
        idEntry = Effect.Id?.Entry ?? "",
        target = Effect.Target,
        npc = Effect.Npc,
        nonce = Nonce,
    };

    public override string ToString() => $"SpireTalkEffectAction {Effect.Kind} {Effect.Amount} {Effect.Id?.Entry} player {Player.NetId}";
}

public struct NetSpireTalkEffectAction : INetAction, IPacketSerializable
{
    public string kind;
    public int amount;
    public int extra;
    public string idCategory;
    public string idEntry;
    public ulong target;
    public string npc;
    public uint nonce;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(kind ?? "");
        writer.WriteInt(amount);
        writer.WriteInt(extra);
        writer.WriteString(idCategory ?? "");
        writer.WriteString(idEntry ?? "");
        writer.WriteULong(target);
        writer.WriteString(npc ?? "");
        writer.WriteUInt(nonce);
    }

    public void Deserialize(PacketReader reader)
    {
        kind = reader.ReadString();
        amount = reader.ReadInt();
        extra = reader.ReadInt();
        idCategory = reader.ReadString();
        idEntry = reader.ReadString();
        target = reader.ReadULong();
        npc = reader.ReadString();
        nonce = reader.ReadUInt();
    }

    public GameAction ToGameAction(Player player)
    {
        var id = string.IsNullOrEmpty(idEntry) ? null : new ModelId(idCategory, idEntry);
        return new SpireTalkEffectAction(player, new Effect(kind, amount, id, extra, target, npc), nonce);
    }

    public override string ToString() => $"NetSpireTalkEffectAction {kind} {amount} {idEntry}";
}
