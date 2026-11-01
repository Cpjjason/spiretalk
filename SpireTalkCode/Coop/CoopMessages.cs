using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Affection;
using SpireTalk.Npc;
using SpireTalk.Rewards;

namespace SpireTalk.Coop;

/// <summary>
/// Talk between teammates' machines. None of it changes the run (that is <see cref="Sync"/>'s job):
/// it carries the conversation itself, so the party can watch each other flirt, fight and
/// backstab, and so an NPC can bring up what a teammate just said to it.
/// </summary>
public static class CoopMessages
{
    private static bool Ready(Player? me) => me != null && Party.IsCoop(me);

    /// <summary>A line of an NPC conversation, for every teammate's feed and their NPC's memory of the room.</summary>
    public static void SendLine(Player me, string npcKey, bool fromNpc, string text, int affection, ConversationRules.Tier tier, bool married, int talks)
    {
        if (!Ready(me) || string.IsNullOrWhiteSpace(text)) return;
        Party.AddLine(new Party.Line(me.NetId, Party.Name(me), npcKey, fromNpc, text, DateTime.UtcNow));
        Send(new TalkLineMessage
        {
            netId = me.NetId, npc = npcKey, fromNpc = fromNpc, text = text,
            affection = affection, tier = ConversationRules.TierName(tier), married = married, talks = talks,
        });
    }

    /// <summary>Where I stand with this NPC, so a teammate's NPC can compare us.</summary>
    public static void SendStanding(Player me, string npcKey, int affection, ConversationRules.Tier tier, bool married, int talks)
    {
        if (!Ready(me)) return;
        Send(new StandingMessage { netId = me.NetId, npc = npcKey, affection = affection, tier = ConversationRules.TierName(tier), married = married, talks = talks });
    }

    /// <summary>The NPC changed its mind about a teammate because of something I said.</summary>
    public static void SendNudge(Player me, Player target, string npcKey, int delta)
    {
        if (!Ready(me) || delta == 0) return;
        Send(new NudgeMessage { byNetId = me.NetId, targetNetId = target.NetId, npc = npcKey, delta = delta });
    }

    private static void Send(ICustomMessage msg)
    {
        try
        {
            CustomMessageWrapper.Send(msg);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Coop] send {msg.GetType().Name} failed: {ex.Message}", 1);
        }
    }

    /// <summary>A teammate's NPC handed me something; tell me who to thank.</summary>
    internal static void AnnounceGiftToMe(Player owner, Effect e)
    {
        var npc = NpcCatalog.Get(e.Npc)?.DisplayName ?? e.Npc;
        var by = Party.Name(owner);
        var text = e.Kind switch
        {
            "gold" => string.Format(Loc.EventText("coopGiftGold"), npc, e.Amount, by),
            "heal" => string.Format(Loc.EventText("coopGiftHeal"), npc, e.Amount, by),
            "potion" => string.Format(Loc.EventText("coopGiftPotion"), npc, by),
            _ => null,
        };
        if (text != null) RewardExecutor.Toast(text);
    }

    internal static Player? LocalPlayer()
    {
        try
        {
            return Merchant.MerchantTalk.ResolveLocalPlayer();
        }
        catch
        {
            return null;
        }
    }
}

public sealed class TalkLineMessage : ICustomMessage
{
    public ulong netId;
    public string npc = "";
    public bool fromNpc;
    public string text = "";
    public int affection;
    public string tier = "";
    public bool married;
    public int talks;

    public bool ShouldBroadcast => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(netId);
        writer.WriteString(npc);
        writer.WriteBool(fromNpc);
        writer.WriteString(text);
        writer.WriteInt(affection);
        writer.WriteString(tier);
        writer.WriteBool(married);
        writer.WriteInt(talks);
    }

    public void Deserialize(PacketReader reader)
    {
        netId = reader.ReadULong();
        npc = reader.ReadString();
        fromNpc = reader.ReadBool();
        text = reader.ReadString();
        affection = reader.ReadInt();
        tier = reader.ReadString();
        married = reader.ReadBool();
        talks = reader.ReadInt();
    }

    public void HandleMessage(ulong senderId)
    {
        if (netId == Party.LocalNetId) return;
        var name = Party.Name(netId);
        Party.SetStanding(netId, npc, new Party.Standing(affection, tier, married, talks));
        Party.AddLine(new Party.Line(netId, name, npc, fromNpc, text, DateTime.UtcNow));
        Ui.PartyFeed.Show(name, NpcCatalog.Get(npc)?.DisplayName ?? npc, fromNpc, text);
    }
}

public sealed class StandingMessage : ICustomMessage
{
    public ulong netId;
    public string npc = "";
    public int affection;
    public string tier = "";
    public bool married;
    public int talks;

    public bool ShouldBroadcast => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(netId);
        writer.WriteString(npc);
        writer.WriteInt(affection);
        writer.WriteString(tier);
        writer.WriteBool(married);
        writer.WriteInt(talks);
    }

    public void Deserialize(PacketReader reader)
    {
        netId = reader.ReadULong();
        npc = reader.ReadString();
        affection = reader.ReadInt();
        tier = reader.ReadString();
        married = reader.ReadBool();
        talks = reader.ReadInt();
    }

    public void HandleMessage(ulong senderId)
    {
        if (netId == Party.LocalNetId) return;
        Party.SetStanding(netId, npc, new Party.Standing(affection, tier, married, talks));
    }
}

public sealed class NudgeMessage : ICustomMessage
{
    public ulong byNetId;
    public ulong targetNetId;
    public string npc = "";
    public int delta;

    public bool ShouldBroadcast => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(byNetId);
        writer.WriteULong(targetNetId);
        writer.WriteString(npc);
        writer.WriteInt(delta);
    }

    public void Deserialize(PacketReader reader)
    {
        byNetId = reader.ReadULong();
        targetNetId = reader.ReadULong();
        npc = reader.ReadString();
        delta = reader.ReadInt();
    }

    public void HandleMessage(ulong senderId)
    {
        if (targetNetId != Party.LocalNetId) return;
        var d = Math.Clamp(delta, -ConversationRules.MaxDeltaPerLine, ConversationRules.MaxDeltaPerLine);
        if (d == 0) return;
        var kind = npc == "MERCHANT" ? NpcKind.Merchant : NpcKind.Ancient;
        var now = AffectionService.Apply(npc, kind, d);
        var display = NpcCatalog.Get(npc)?.DisplayName ?? npc;
        RewardExecutor.Toast(string.Format(Loc.EventText(d > 0 ? "coopNudgeUp" : "coopNudgeDown"), display, Party.Name(byNetId)));
        // Remember it: the next time this climber talks to the NPC, it can bring it up.
        Store.TalkStore.Instance.AddMemory(npc, $"{Party.Name(byNetId)} talked to me about this climber; it changed how I feel about them ({(d > 0 ? "+" : "")}{d}).", "coop", null);
        Store.TalkStore.Instance.Save();
        var me = CoopMessages.LocalPlayer();
        if (me != null)
        {
            var (_, married) = AffectionService.Read(npc);
            CoopMessages.SendStanding(me, npc, now, ConversationRules.TierOf(now, married), married, Store.TalkStore.Instance.Get(npc).Talks);
        }
    }
}
