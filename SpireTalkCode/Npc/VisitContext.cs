using System.Text.Json.Nodes;

namespace SpireTalk.Npc;

/// <summary>One remembered line of dialogue (both directions), kept per NPC in the profile store.</summary>
public sealed record HistoryLine(string Role, string Text, string? WhenUtc);

/// <summary>
/// Everything a backend needs to produce one NPC turn. Built by the conversation controller and
/// refreshed between lines (affection, lines_left, allowed_actions change as the visit goes on).
/// </summary>
public sealed class VisitContext
{
    public required string NpcKey { get; init; }
    public required NpcDefinition Npc { get; init; }
    /// <summary>seed:character of the current run — scopes per-run memory and one-per-run rewards.</summary>
    public string RunKey { get; init; } = "no-run";
    public NpcKind Kind => Npc.Kind;

    /// <summary>Run snapshot + relationship state, same keys in both backends.</summary>
    public JsonObject Variables { get; } = new();

    public string Language { get; set; } = "English";

    public int Affection { get; set; }
    public bool Married { get; set; }
    public ConversationRules.Tier Tier => ConversationRules.TierOf(Affection, Married);
    public int LinesLeft { get; set; } = ConversationRules.LinesPerVisit;
    public List<string> AllowedActions { get; set; } = new();
    public int Talks { get; set; }

    /// <summary>Push the relationship fields into <see cref="Variables"/> so both backends see the same thing.</summary>
    public void SyncRelationshipVariables()
    {
        Variables["affection"] = Affection;
        Variables["affection_tier"] = ConversationRules.TierName(Tier);
        Variables["married"] = Married;
        Variables["lines_left"] = LinesLeft;
        Variables["allowed_actions"] = AllowedActions.Count == 0 ? "none" : string.Join(", ", AllowedActions);
        Variables["talks"] = Talks;
        Variables["language"] = Language;
    }
}
