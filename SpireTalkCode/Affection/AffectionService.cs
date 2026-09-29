using SpireTalk.Npc;
using SpireTalk.Store;

namespace SpireTalk.Affection;

/// <summary>
/// One place to read and change affection. Two modes, same rules:
///  - Ancient Affection installed and enabled: its store is the truth (scale −100…100, merchant to 500).
///  - Otherwise: our own per-profile store on the same scale.
/// The mod always keeps a mirror in its own store so switching modes never loses the number.
/// </summary>
public static class AffectionService
{
    public static bool UsingAncientAffection => AncientAffectionBridge.Enabled;

    public static (int value, bool married) Read(string npcKey)
    {
        var record = TalkStore.Instance.Get(npcKey);
        if (UsingAncientAffection)
        {
            var v = AncientAffectionBridge.Get(npcKey);
            if (v.HasValue)
            {
                record.Affection = v.Value;
                return (v.Value, AncientAffectionBridge.IsMarried(npcKey));
            }
        }
        return (record.Affection, false);
    }

    /// <summary>
    /// A small "you came to see me" gain, only when the mod keeps its own ledger. Ancient
    /// Affection has its own sources of affection, so its number is left alone here.
    /// Paid once per room per run, and only after the NPC actually answered: a failed call or
    /// quitting and reloading into the same room must not farm it.
    /// </summary>
    public static void VisitBonus(string npcKey, NpcKind kind, string runKey, int floor)
    {
        if (UsingAncientAffection) return;
        var paid = TalkStore.Instance.GetRun(runKey).VisitBonusPaid;
        var mark = $"{npcKey}@{floor}";
        if (paid.Contains(mark, StringComparer.OrdinalIgnoreCase)) return;
        paid.Add(mark);
        Apply(npcKey, kind, ConversationRules.VisitBonus);
    }

    /// <summary>Put one NPC back to zero, in whichever store holds the number.</summary>
    public static void ResetToZero(string npcKey, NpcKind kind)
    {
        var current = Read(npcKey).value;
        if (UsingAncientAffection && current != 0) AncientAffectionBridge.Increment(npcKey, -current);
        TalkStore.Instance.Get(npcKey).Affection = 0;
        TalkStore.Instance.Save();
    }

    /// <summary>Apply an already-clamped delta. Returns the new value.</summary>
    public static int Apply(string npcKey, NpcKind kind, int delta)
    {
        var record = TalkStore.Instance.Get(npcKey);
        if (delta == 0) return Read(npcKey).value;

        var before = record.Affection;
        if (UsingAncientAffection)
        {
            before = AncientAffectionBridge.Get(npcKey) ?? before;
            var written = AncientAffectionBridge.Increment(npcKey, delta);
            if (written.HasValue)
            {
                record.Affection = written.Value;
                TalkStore.Instance.Save();
                Ui.AffectionHud.Show(npcKey, kind, written.Value, written.Value - before);
                return written.Value;
            }
            // Bridge broke mid-run: continue on our own store from the last known value.
        }
        record.Affection = ConversationRules.ClampAffection(record.Affection + delta, kind);
        TalkStore.Instance.Save();
        Ui.AffectionHud.Show(npcKey, kind, record.Affection, record.Affection - before);
        return record.Affection;
    }
}
