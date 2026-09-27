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

    /// <summary>Apply everything except reroll_relics / discount, which the callers own.</summary>
    public static async Task<Outcome> ApplyAsync(NpcAction action, Player player, string npcKey, string npcName, string runKey, object? ancientEvent = null, int affection = 0)
    {
        var run = TalkStore.Instance.GetRun(runKey);
        try
        {
            switch (action.Type)
            {
                case "affection_relic":
                {
                    // One of Ancient Affection's own bond relics, chosen by its selector so the pool,
                    // upgrade tiers and "not already owned" filter match what its option would give.
                    if (ancientEvent == null) return new Outcome(false, "", "affection_relic needs an ancient event");
                    if (SpireTalk.Affection.AncientAffectionBridge.PickBondRelic(ancientEvent, npcKey, affection) is not MegaCrit.Sts2.Core.Models.RelicModel relic)
                    {
                        return new Outcome(false, "", "affection_relic: Ancient Affection did not offer a relic");
                    }
                    relic.Owner = player;
                    await RelicCmd.Obtain(relic, player, -1);
                    run.AffectionRelicGiven.Add(npcKey);
                    TalkStore.Instance.Save();
                    var relicTitle = SafeTitle(() => relic.Title.GetFormattedText());
                    return Done(string.Format(Loc.EventText("rewardAffectionRelic"), npcName, relicTitle), "affection relic " + relicTitle);
                }
                case "gold":
                {
                    await PlayerCmd.GainGold(action.Amount, player);
                    run.GoldGiven.Add(npcKey);
                    TalkStore.Instance.Save();
                    return Done(string.Format(Loc.EventText("rewardGold"), npcName, action.Amount), $"gold +{action.Amount}");
                }
                case "heal":
                {
                    var before = player.Creature.CurrentHp;
                    await CreatureCmd.Heal(player.Creature, action.Amount);
                    var healed = Math.Max(0, player.Creature.CurrentHp - before);
                    run.HealGiven.Add(npcKey);
                    TalkStore.Instance.Save();
                    return Done(string.Format(Loc.EventText("rewardHeal"), npcName, healed), $"heal +{healed}");
                }
                case "potion":
                case "gift_potion":
                {
                    var potion = PotionFactory.CreateRandomPotionOutOfCombat(player, Rng.Chaotic).ToMutable();
                    var result = await PotionCmd.TryToProcure(potion, player);
                    if (!result.success)
                    {
                        return new Outcome(false, "", "potion refused: " + result.failureReason);
                    }
                    if (action.Type == "potion") run.PotionGiven.Add(npcKey); else run.GiftGiven.Add(npcKey);
                    TalkStore.Instance.Save();
                    var title = SafeTitle(() => potion.Title.GetFormattedText());
                    return Done(string.Format(Loc.EventText("rewardPotion"), npcName, title), "potion " + title);
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
