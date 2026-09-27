using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace SpireTalk.Ancients;

/// <summary>
/// Snapshot of the run that the NPC should know about. Sent to Yumina as session variables so the
/// world prompt can reference them ({{hp}}, {{gold}}, {{deck}} …). Only variables the world declares
/// are kept server-side; extra keys are dropped harmlessly.
/// </summary>
public static class GameContext
{
    private const int MaxRelics = 14;
    private const int MaxDeckCards = 18;

    public static JsonObject Collect(AncientEventModel ancient)
    {
        var vars = new JsonObject
        {
            ["npc"] = ancient.Id.Entry,
            ["language"] = Loc.ReplyLanguageName(),
            ["canon_language"] = Loc.CanonLanguage(),
        };

        var owner = ancient.Owner;
        if (owner != null)
        {
            AddPlayer(vars, owner);
        }

        try
        {
            var stats = SaveManager.Instance.Progress.GetStatsForAncient(ancient.Id);
            vars["visits"] = stats?.TotalVisits ?? 0;
            vars["ancient_wins"] = stats?.TotalWins ?? 0;
            vars["ancient_losses"] = stats?.TotalLosses ?? 0;
        }
        catch
        {
            vars["visits"] = 0;
        }

        return vars;
    }

    public static JsonObject CollectForMerchant(Player owner)
    {
        var vars = new JsonObject
        {
            ["npc"] = "MERCHANT",
            ["language"] = Loc.ReplyLanguageName(),
            ["canon_language"] = Loc.CanonLanguage(),
        };
        AddPlayer(vars, owner);
        return vars;
    }

    private static void AddPlayer(JsonObject vars, Player owner)
    {
        try
        {
            vars["character"] = owner.Character.Title.GetFormattedText();
            vars["character_id"] = owner.Character.Id.Entry;
        }
        catch { /* keep going */ }

        try
        {
            var creature = owner.Creature;
            vars["hp"] = (int)creature.CurrentHp;
            vars["max_hp"] = (int)creature.MaxHp;
        }
        catch { /* keep going */ }

        try
        {
            vars["gold"] = owner.Gold;
        }
        catch { /* keep going */ }

        try
        {
            var run = owner.RunState;
            vars["act"] = run.Act.Title.GetFormattedText();
            vars["act_index"] = run.CurrentActIndex + 1;
            vars["floor_in_act"] = run.ActFloor;
            vars["floor_total"] = run.TotalFloor;
            vars["ascension"] = run.AscensionLevel;
            vars["players"] = run.Players.Count;
        }
        catch { /* keep going */ }

        try
        {
            var relics = owner.Relics.Select(r => r.Title.GetFormattedText()).ToList();
            vars["relic_count"] = relics.Count;
            vars["relics"] = Join(relics, MaxRelics);
        }
        catch { /* keep going */ }

        try
        {
            var cards = owner.Deck.Cards;
            vars["deck_size"] = cards.Count;
            var titles = cards
                .GroupBy(c => c.Title + (c.IsUpgraded ? "+" : ""))
                .OrderByDescending(g => g.Count())
                .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key)
                .ToList();
            vars["deck"] = Join(titles, MaxDeckCards);
        }
        catch { /* keep going */ }

        try
        {
            var potions = owner.Potions.Select(p => p.Title.GetFormattedText()).ToList();
            vars["potions"] = potions.Count == 0 ? "none" : Join(potions, 6);
        }
        catch { /* keep going */ }
    }

    private static string Join(IReadOnlyList<string> items, int max)
    {
        if (items.Count == 0) return "none";
        if (items.Count <= max) return string.Join(", ", items);
        return string.Join(", ", items.Take(max)) + $" (+{items.Count - max} more)";
    }
}
