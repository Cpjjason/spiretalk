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
    string? ModelUsed,
    int? CreditsBalance)
{
    public string JoinedText => string.Join("\n\n", Lines);
}

/// <summary>Where an NPC conversation happens.</summary>
public enum NpcKind
{
    Ancient,
    Merchant,
}
