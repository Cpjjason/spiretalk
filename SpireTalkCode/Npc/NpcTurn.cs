namespace SpireTalk.Npc;

/// <summary>A reward the NPC proposes. The mod validates it against <see cref="ConversationRules"/> before anything happens.</summary>
public sealed record NpcAction(string Type, int Amount)
{
    public static readonly NpcAction None = new("none", 0);
    public bool IsNone => string.IsNullOrEmpty(Type) || Type == "none";
}

/// <summary>What one exchange with an NPC produced, whichever backend produced it.</summary>
public sealed record NpcTurn(
    IReadOnlyList<string> Lines,
    IReadOnlyList<string> Choices,
    int AffectionDelta,
    string? Reason,
    NpcAction Action,
    string? ModelUsed)
{
    public string JoinedText => string.Join("\n\n", Lines);

    /// <summary>
    /// What goes into the NPC's memory: the lines plus the three proposal directives in the card's
    /// canonical form. The model reads its own past turns as examples, so a history that carries the
    /// directives keeps small models writing them; a history of bare prose teaches them to stop.
    /// </summary>
    public string ToTranscript()
    {
        var choices = new System.Text.Json.Nodes.JsonArray();
        foreach (var c in Choices) choices.Add(c);
        var action = new System.Text.Json.Nodes.JsonObject { ["type"] = Action.IsNone ? "none" : Action.Type, ["amount"] = Action.Amount };
        return JoinedText
               + "\n[affection_delta: set " + AffectionDelta + "]"
               + "\n[choices: set " + choices.ToJsonString(PlainJson) + "]"
               + "\n[action: set " + action.ToJsonString(PlainJson) + "]";
    }

    // Keep Chinese/Japanese/Korean readable in the transcript: the default encoder turns them into \uXXXX.
    private static readonly System.Text.Json.JsonSerializerOptions PlainJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

/// <summary>Where an NPC conversation happens.</summary>
public enum NpcKind
{
    Ancient,
    Merchant,
}
