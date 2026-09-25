using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SpireTalk.Affection;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;

namespace SpireTalk.Keepsakes;

/// <summary>
/// Hands out, levels and engraves keepsakes. The NPC gives theirs the first time the climber
/// visits at friendly or better in a run (the AI is told so it can say so); every affection change
/// afterwards may raise its level; every kept deal engraves it. Nothing here asks the model anything.
/// </summary>
public static class KeepsakeService
{
    private static readonly Dictionary<string, Type> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NEOW"] = typeof(KeepsakeNeow),
        ["OROBAS"] = typeof(KeepsakeOrobas),
        ["NONUPEIPE"] = typeof(KeepsakeNonupeipe),
        ["PAEL"] = typeof(KeepsakePael),
        ["DARV"] = typeof(KeepsakeDarv),
        ["TANX"] = typeof(KeepsakeTanx),
        ["TEZCATARA"] = typeof(KeepsakeTezcatara),
        ["VAKUU"] = typeof(KeepsakeVakuu),
        ["MERCHANT"] = typeof(KeepsakeMerchant),
    };

    public static int LevelFor(ConversationRules.Tier tier) => tier switch
    {
        ConversationRules.Tier.Devoted => 4,
        ConversationRules.Tier.Close => 3,
        ConversationRules.Tier.Friendly => 2,
        _ => 1,
    };

    public static KeepsakeRelic? Owned(Player player, string npcKey)
    {
        try
        {
            return player.Relics.OfType<KeepsakeRelic>().FirstOrDefault(r => string.Equals(r.NpcKey, npcKey, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Start of a visit: give the keepsake if the relationship has reached friendly and the climber
    /// does not carry it yet; otherwise raise its level if the relationship moved. Returns the line
    /// for the `keepsake` prompt variable so the NPC can speak to what just happened.
    /// </summary>
    public static async Task<string> OnVisitAsync(NpcDefinition npc, Player player, ConversationRules.Tier tier)
    {
        if (!Types.TryGetValue(npc.Key, out var type)) return "none";
        var owned = Owned(player, npc.Key);
        var name = KeepsakeName(npc.Key);
        if (owned == null)
        {
            if (tier < ConversationRules.Tier.Stranger)
            {
                return $"You have a keepsake for the climbers you meet (\"{name}\"); this one has soured you, and will not get it while that lasts.";
            }
            var relic = await GiveAsync(type, player, tier, npc);
            if (relic == null) return "none";
            var level = relic.Level;
            return $"You are giving them your keepsake right now: \"{name}\" (level {level} of 4). Say so in this reply, in your own way — it is a small token for now, and it will grow as they earn your trust; at the end it becomes your vow.";
        }

        var raised = await RefreshAsync(npc, player, quiet: false);
        var now = owned.Level;
        var engr = owned.Engravings;
        var engraved = engr > 0 ? $" It carries {engr} engraving(s) from deals they kept with you." : "";
        return raised
            ? $"They carry your keepsake \"{name}\" and it has just {Grew(now)} because of how far you two have come. Mention it.{engraved}"
            : $"They carry your keepsake \"{name}\" at level {now}.{engraved}";
    }

    private static async Task<KeepsakeRelic?> GiveAsync(Type type, Player player, ConversationRules.Tier tier, NpcDefinition npc)
    {
        try
        {
            var canonical = ModelDb.GetById<RelicModel>(ModelDb.GetId(type));
            if (canonical.ToMutable() is not KeepsakeRelic relic) return null;
            relic.Owner = player;
            relic.Level = LevelFor(tier);
            await RelicCmd.Obtain(relic, player);
            if (relic is KeepsakeTezcatara tez) await tez.GrantSlotIfDue();
            RecordBest(npc.Key, relic.Level);
            RewardExecutor.Toast(string.Format(Loc.EventText("keepsakeGiven"), npc.DisplayName, KeepsakeName(npc.Key)));
            MainFile.Logger.Info($"[Keepsake] {npc.Key} gave {relic.Id.Entry} at level {relic.Level}", 1);
            return relic;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Keepsake] give failed: {ex.GetType().Name}: {ex.Message}", 1);
            return null;
        }
    }

    /// <summary>Raise the keepsake's level to the relationship's tier if it is behind. Never lowers it.</summary>
    public static async Task<bool> RefreshAsync(NpcDefinition npc, Player player, bool quiet)
    {
        var owned = Owned(player, npc.Key);
        if (owned == null) return false;
        try
        {
            var (affection, married) = AffectionService.Read(npc.Key);
            var level = LevelFor(ConversationRules.TierOf(affection, married));
            if (level <= owned.Level)
            {
                owned.ApplyValues(); // engravings may have moved
                return false;
            }
            owned.Level = level;
            RecordBest(npc.Key, level);
            if (level >= KeepsakeRelic.MaxLevel) owned.CelebrateVow();
            else owned.Flash();
            if (owned is KeepsakeTezcatara tez) await tez.GrantSlotIfDue();
            if (!quiet)
            {
                RewardExecutor.Toast(level >= KeepsakeRelic.MaxLevel
                    ? string.Format(Loc.EventText("keepsakeVow"), KeepsakeName(npc.Key))
                    : string.Format(Loc.EventText("keepsakeLevel"), KeepsakeName(npc.Key), Roman(level)));
            }
            MainFile.Logger.Info($"[Keepsake] {npc.Key} keepsake → level {level}", 1);
            return true;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Keepsake] refresh failed: {ex.GetType().Name}: {ex.Message}", 1);
            return false;
        }
    }

    /// <summary>A kept deal leaves its mark on the keepsake, for good.</summary>
    public static async Task EngraveAsync(string npcKey, Player? player)
    {
        var record = TalkStore.Instance.Get(npcKey);
        if (record.Engravings < KeepsakeRelic.MaxEngravings) record.Engravings++;
        TalkStore.Instance.Save();
        var npc = NpcCatalog.Get(npcKey);
        if (player == null || npc == null) return;
        var owned = Owned(player, npcKey);
        if (owned == null) return;
        await RefreshAsync(npc, player, quiet: true);
        owned.ApplyValues();
        RewardExecutor.Toast(string.Format(Loc.EventText("keepsakeEngraved"), KeepsakeName(npcKey), owned.Engravings));
    }

    /// <summary>The keepsake book remembers the best level ever reached and the day it became a vow.</summary>
    private static void RecordBest(string npcKey, int level)
    {
        var record = TalkStore.Instance.Get(npcKey);
        if (level <= record.BestKeepsakeLevel) return;
        record.BestKeepsakeLevel = level;
        if (level >= KeepsakeRelic.MaxLevel && record.VowUtc == null) record.VowUtc = DateTime.UtcNow.ToString("o");
        TalkStore.Instance.Save();
    }

    public static string KeepsakeName(string npcKey)
    {
        if (!Types.TryGetValue(npcKey, out var type)) return npcKey;
        try
        {
            var canonical = ModelDb.GetById<RelicModel>(ModelDb.GetId(type));
            var title = new MegaCrit.Sts2.Core.Localization.LocString("relics", canonical.Id.Entry + ".title");
            title.Add("Level", "");
            return title.GetFormattedText().Trim();
        }
        catch
        {
            return npcKey;
        }
    }

    /// <summary>How the prompt says the keepsake just grew; the last step is the vow.</summary>
    public static string Grew(int level) => level >= KeepsakeRelic.MaxLevel
        ? "become your vow keepsake, the highest bond you give. Say one short line that sounds like a vow, in your own voice"
        : $"grown to level {Roman(level)}";

    private static string Roman(int level) => level switch { >= 4 => "IV", 3 => "III", 2 => "II", _ => "I" };
}
