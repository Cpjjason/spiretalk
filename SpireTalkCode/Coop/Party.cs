using System.Text;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Npc;

namespace SpireTalk.Coop;

/// <summary>
/// Who else is in the run, and what this machine knows about them. In co-op every peer simulates
/// every player, but each player's conversations, memory and affection live on their own machine;
/// teammates learn about them only through <see cref="CoopMessages"/>. Everything here is local
/// flavour for prompts and the feed, never game state.
/// </summary>
public static class Party
{
    /// <summary>One line of talk somebody in the party had with an NPC in the current room.</summary>
    public sealed record Line(ulong NetId, string Speaker, string Npc, bool FromNpc, string Text, DateTime Utc);

    /// <summary>What a teammate last told us about their standing with an NPC.</summary>
    public sealed record Standing(int Affection, string Tier, bool Married, int Talks);

    private static readonly Dictionary<(ulong, string), Standing> Standings = new();
    private static readonly List<Line> Lines = new();
    private const int MaxLines = 40;

    public static bool IsCoop(IRunState? run) => run != null && run.Players.Count > 1;

    public static bool IsCoop(Player? player) => player != null && IsCoop(player.RunState);

    public static ulong LocalNetId => LocalContext.NetId ?? 0;

    public static bool IsLocal(Player? player) => player != null && LocalContext.NetId is { } id && player.NetId == id;

    public static string Name(ulong netId)
    {
        try
        {
            return PlatformUtil.GetPlayerName(RunManager.Instance.NetService.Platform, netId);
        }
        catch
        {
            return "#" + netId;
        }
    }

    public static string Name(Player player) => Name(player.NetId);

    public static string Character(Player player)
    {
        try
        {
            return player.Character.Title.GetFormattedText();
        }
        catch
        {
            return player.Character.Id.Entry;
        }
    }

    public static IEnumerable<Player> Teammates(Player me) => me.RunState.Players.Where(p => p.NetId != me.NetId);

    public static Player? Find(IRunState run, ulong netId) => run.Players.FirstOrDefault(p => p.NetId == netId);

    /// <summary>A teammate the NPC named in its reply: by player name or by character, case-insensitive.</summary>
    public static Player? FindByName(Player me, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var n = name.Trim();
        foreach (var p in Teammates(me))
        {
            if (string.Equals(Name(p), n, StringComparison.OrdinalIgnoreCase)) return p;
        }
        // The speaker themself (the model sometimes writes the climber's own name or class as the
        // target): nobody else gets it.
        var myName = Name(me);
        var myClass = Character(me);
        if (string.Equals(myName, n, StringComparison.OrdinalIgnoreCase)
            || (myName.Length > 0 && n.Contains(myName, StringComparison.OrdinalIgnoreCase))) return null;
        foreach (var p in Teammates(me))
        {
            var c = Character(p);
            if (n.Contains(Name(p), StringComparison.OrdinalIgnoreCase)) return p;
            // Two climbers of the same class: the class alone doesn't say which.
            if (string.Equals(c, myClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(c, n, StringComparison.OrdinalIgnoreCase)
                || c.Contains(n, StringComparison.OrdinalIgnoreCase)
                || n.Contains(c.Replace("The ", ""), StringComparison.OrdinalIgnoreCase)) return p;
        }
        if (string.Equals(myClass, n, StringComparison.OrdinalIgnoreCase)
            || myClass.Contains(n, StringComparison.OrdinalIgnoreCase)
            || n.Contains(myClass.Replace("The ", ""), StringComparison.OrdinalIgnoreCase)) return null;
        // Only one teammate and the name isn't the speaker's: it must have meant them.
        var others = Teammates(me).ToList();
        return others.Count == 1 ? others[0] : null;
    }

    public static void SetStanding(ulong netId, string npcKey, Standing s)
    {
        lock (Standings) Standings[(netId, npcKey.ToUpperInvariant())] = s;
    }

    public static Standing? StandingOf(ulong netId, string npcKey)
    {
        lock (Standings) return Standings.TryGetValue((netId, npcKey.ToUpperInvariant()), out var s) ? s : null;
    }

    public static void AddLine(Line line)
    {
        lock (Lines)
        {
            Lines.Add(line);
            if (Lines.Count > MaxLines) Lines.RemoveRange(0, Lines.Count - MaxLines);
        }
    }

    /// <summary>A new room: the talk of the last one is over.</summary>
    public static void ClearRoom()
    {
        lock (Lines) Lines.Clear();
    }

    /// <summary>What teammates said to (and heard from) this NPC since the room began.</summary>
    public static List<Line> TeammateLines(string npcKey)
    {
        lock (Lines)
        {
            return Lines.Where(l => l.NetId != LocalNetId && string.Equals(l.Npc, npcKey, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    /// <summary>
    /// A memory note the NPC keeps across runs: who this climber came with. The next time the same
    /// friends show up (or one of them is missing) the NPC can say so.
    /// </summary>
    public static void RememberCompanions(Player me, string npcKey, string runKey)
    {
        if (!IsCoop(me)) return;
        var mates = Teammates(me).Select(p => $"{Name(p)} ({Character(p)})").ToList();
        if (mates.Count == 0) return;
        var text = $"This climber came to me with friends: {string.Join(", ", mates)}.";
        var rec = Store.TalkStore.Instance.Get(npcKey);
        if (rec.Memories.Any(m => m.Source == "coop" && m.Run == runKey && m.Text == text)) return;
        Store.TalkStore.Instance.AddMemory(npcKey, text, "coop", runKey);
    }

    // ── Prompt text ──────────────────────────────────────────────────────

    /// <summary>"party" variable: who the climber came with, and how the NPC feels about each.</summary>
    public static string DescribeParty(Player me, string npcKey)
    {
        if (!IsCoop(me)) return "solo";
        var sb = new StringBuilder();
        sb.Append("This climber, ").Append(Name(me)).Append(" (").Append(Character(me)).Append("), did not come alone. Their party:");
        foreach (var p in Teammates(me))
        {
            sb.Append("\n- ").Append(Name(p)).Append(" (").Append(Character(p)).Append(", HP ");
            try { sb.Append(p.Creature.CurrentHp).Append('/').Append(p.Creature.MaxHp); } catch { sb.Append('?'); }
            sb.Append(", gold ");
            try { sb.Append(p.Gold); } catch { sb.Append('?'); }
            sb.Append(")");
            var s = StandingOf(p.NetId, npcKey);
            if (s != null)
            {
                sb.Append(": your affection for them is ").Append(s.Affection).Append(" (").Append(s.Tier).Append(s.Married ? ", married to you" : "").Append(")");
                if (s.Talks > 1) sb.Append(", you have talked ").Append(s.Talks).Append(" times");
            }
            else
            {
                sb.Append(": you do not know them yet");
            }
        }
        return sb.ToString();
    }

    /// <summary>"party_talk" variable: the teammates' side of the room, word for word.</summary>
    public static string DescribeTeammateTalk(string npcKey)
    {
        var lines = TeammateLines(npcKey);
        if (lines.Count == 0) return "none";
        var sb = new StringBuilder();
        foreach (var l in lines.TakeLast(12))
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(l.FromNpc ? "You, to " + l.Speaker + ": " : l.Speaker + ", to you: ").Append('"').Append(Trim(l.Text, 240)).Append('"');
        }
        return sb.ToString();
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
