using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Npc;
using SpireTalk.Store;

namespace SpireTalk.Rewards;

/// <summary>
/// Turns a validated <see cref="NpcAction"/> into a real game effect through the game's own
/// command layer, records it as spent, and shows a toast. Never called with an unvalidated action.
/// </summary>
public static class RewardExecutor
{
    public sealed record Outcome(bool Applied, string ToastText, string Summary);

    // RunRngSet.Seed is uint on 0.107 (public) and ulong on 0.111 (public-beta). Reading it by name keeps one
    // DLL loadable on both branches, and the key text is the same number either way.
    private static readonly System.Reflection.PropertyInfo? SeedProperty = HarmonyLib.AccessTools.Property(typeof(RunRngSet), "Seed");

    /// <summary>Stable key for "this run": seed + character, so a new run resets one-per-run rewards.</summary>
    public static string RunKey(Player player)
    {
        try
        {
            return $"{SeedProperty!.GetValue(player.RunState.Rng)}:{player.Character.Id.Entry}";
        }
        catch
        {
            return "run:" + DateTime.UtcNow.ToString("yyyyMMdd");
        }
    }

    public static ConversationRules.RewardState BuildState(Player player, string npcKey, string runKey, int rewardsThisVisit, bool rerollUsed, bool discountUsed)
    {
        var run = TalkStore.Instance.GetRun(runKey);
        var potionFree = false;
        try
        {
            potionFree = player.PotionSlots.Any(p => p == null);
        }
        catch { /* ignore */ }
        var hurt = false;
        try
        {
            hurt = player.Creature.CurrentHp < player.Creature.MaxHp;
        }
        catch { /* ignore */ }
        return new ConversationRules.RewardState
        {
            RewardsThisVisit = rewardsThisVisit,
            RerollUsedThisVisit = rerollUsed,
            DiscountUsedThisVisit = discountUsed,
            GoldGivenThisRun = run.GoldGiven.Contains(npcKey),
            HealGivenThisRun = run.HealGiven.Contains(npcKey),
            PotionGivenThisRun = run.PotionGiven.Contains(npcKey),
            GiftGivenThisRun = run.GiftGiven.Contains(npcKey),
            PlayerHurt = hurt,
            PotionSlotFree = potionFree,
            AffectionRelicsAvailable = SpireTalk.Affection.AncientAffectionBridge.Enabled,
            AffectionRelicGivenThisRun = run.AffectionRelicGiven.Contains(npcKey),
        };
    }

    /// <summary>
    /// Apply everything except reroll_relics / discount, which the callers own. In co-op the NPC
    /// may hand the reward to a teammate instead (<paramref name="target"/>); random picks are made
    /// here, on the speaker's machine, and the result goes through <see cref="Coop.Sync"/>.
    /// </summary>
    public static async Task<Outcome> ApplyAsync(NpcAction action, Player player, string npcKey, string npcName, string runKey, object? ancientEvent = null, int affection = 0, Player? target = null)
    {
        var run = TalkStore.Instance.GetRun(runKey);
        var to = target != null && target.NetId != player.NetId ? target : null;
        var targetId = to?.NetId ?? 0;
        var forName = to != null ? Coop.Party.Name(to) : null;
        try
        {
            switch (action.Type)
            {
                case "affection_relic":
                {
                    // One of Ancient Affection's own bond relics, chosen by its selector so the pool,
                    // upgrade tiers and "not already owned" filter match what its option would give.
                    if (ancientEvent == null) return new Outcome(false, "", "affection_relic needs an ancient event");
                    // Co-op: Ancient Affection prepares the relic it picks (Dusty Tome draws its card from
                    // the player's reward Rng) on this machine only. Put that Rng back, and let every peer
                    // prepare its own copy from the same position (Effects "relic_prepared").
                    var coop = Coop.Party.IsCoop(player);
                    var rewardsRng = coop ? RngCompat.SnapshotRewards(player) : null;
                    var picked = SpireTalk.Affection.AncientAffectionBridge.PickBondRelic(ancientEvent, npcKey, affection);
                    RngCompat.RestoreRewards(player, rewardsRng);
                    if (picked is not MegaCrit.Sts2.Core.Models.RelicModel relic)
                    {
                        return new Outcome(false, "", "affection_relic: Ancient Affection did not offer a relic");
                    }
                    if (!coop)
                    {
                        // solo: the instance Ancient Affection prepared (Dusty Tome carries its chosen card), as in 0.6.0
                        relic.Owner = player;
                        await RelicCmd.Obtain(relic, player, -1);
                    }
                    else if (!await Coop.Sync.RunAsync(player, new Coop.Effect("relic_prepared", 0, relic.Id, -1, 0, npcKey))) return new Outcome(false, "", "affection_relic not applied");
                    run.AffectionRelicGiven.Add(npcKey);
                    TalkStore.Instance.Save();
                    var relicTitle = SafeTitle(() => relic.Title.GetFormattedText());
                    return Done(string.Format(Loc.EventText("rewardAffectionRelic"), npcName, relicTitle), "affection relic " + relicTitle);
                }
                case "gold":
                {
                    if (!await Coop.Sync.RunAsync(player, new Coop.Effect("gold", action.Amount, null, -1, targetId, npcKey))) return new Outcome(false, "", "gold not applied");
                    run.GoldGiven.Add(npcKey);
                    TalkStore.Instance.Save();
                    return forName != null
                        ? Done(string.Format(Loc.EventText("rewardGoldTeammate"), npcName, forName, action.Amount), $"gold +{action.Amount} to {forName}")
                        : Done(string.Format(Loc.EventText("rewardGold"), npcName, action.Amount), $"gold +{action.Amount}");
                }
                case "heal":
                {
                    var who = to ?? player;
                    var before = who.Creature.CurrentHp;
                    if (!await Coop.Sync.RunAsync(player, new Coop.Effect("heal", action.Amount, null, -1, targetId, npcKey))) return new Outcome(false, "", "heal not applied");
                    var healed = Math.Max(0, who.Creature.CurrentHp - before);
                    run.HealGiven.Add(npcKey);
                    TalkStore.Instance.Save();
                    return forName != null
                        ? Done(string.Format(Loc.EventText("rewardHealTeammate"), npcName, forName, healed), $"heal +{healed} to {forName}")
                        : Done(string.Format(Loc.EventText("rewardHeal"), npcName, healed), $"heal +{healed}");
                }
                case "potion":
                case "gift_potion":
                {
                    var who = to ?? player;
                    var potion = PotionFactory.CreateRandomPotionOutOfCombat(who, Rng.Chaotic);
                    if (!await Coop.Sync.RunAsync(player, new Coop.Effect("potion", 1, potion.Id, -1, targetId, npcKey)))
                    {
                        return new Outcome(false, "", "potion refused");
                    }
                    if (action.Type == "potion") run.PotionGiven.Add(npcKey); else run.GiftGiven.Add(npcKey);
                    TalkStore.Instance.Save();
                    var title = SafeTitle(() => potion.Title.GetFormattedText());
                    return forName != null
                        ? Done(string.Format(Loc.EventText("rewardPotionTeammate"), npcName, forName, title), $"potion {title} to {forName}")
                        : Done(string.Format(Loc.EventText("rewardPotion"), npcName, title), "potion " + title);
                }
                default:
                    return new Outcome(false, "", "unhandled " + action.Type);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Reward] {action.Type} failed: {ex.GetType().Name}: {ex.Message}", 1);
            return new Outcome(false, "", "failed: " + ex.Message);
        }
    }

    public static void Toast(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var vfx = NFullscreenTextVfx.Create(Loc.Sanitize(text));
            if (vfx != null && NGame.Instance != null)
            {
                NGame.Instance.AddChild(vfx);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Reward] toast failed: {ex.Message}", 1);
        }
    }

    private static Outcome Done(string toast, string summary)
    {
        Toast(toast);
        return new Outcome(true, toast, summary);
    }

    private static string SafeTitle(Func<string> f)
    {
        try
        {
            return f();
        }
        catch
        {
            return "?";
        }
    }
}
