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
    /// </summary>
    public static void VisitBonus(string npcKey, NpcKind kind)
    {
        if (UsingAncientAffection) return;
        Apply(npcKey, kind, ConversationRules.VisitBonus);
    }

    /// <summary>Apply an already-clamped delta. Returns the new value.</summary>
    public static int Apply(string npcKey, NpcKind kind, int delta)
    {
        var record = TalkStore.Instance.Get(npcKey);
        if (delta == 0) return Read(npcKey).value;

        if (UsingAncientAffection)
        {
            var written = AncientAffectionBridge.Increment(npcKey, delta);
            if (written.HasValue)
            {
                record.Affection = written.Value;
                TalkStore.Instance.Save();
                return written.Value;
            }
            // Bridge broke mid-run: continue on our own store from the last known value.
        }
        record.Affection = ConversationRules.ClampAffection(record.Affection + delta, kind);
        TalkStore.Instance.Save();
        return record.Affection;
    }
}
