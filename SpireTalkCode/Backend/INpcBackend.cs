using SpireTalk.Npc;

namespace SpireTalk.Backend;

/// <summary>
/// Something that can play an NPC. Three implementations: Yumina (account), Direct (the
/// player's own OpenAI-compatible key: OpenRouter, OpenAI, local Ollama…), and Mock (offline).
/// All of them must honour the same contract: read the context, answer as the NPC, propose
/// choices / an affection delta / an action. The controller applies the rules afterwards.
/// </summary>
public interface INpcBackend
{
    string DisplayName { get; }

    /// <summary>Cheap readiness check used before showing the Talk option / merchant greeting.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// One exchange. <paramref name="isOpener"/> marks the engine's stage direction that starts a
    /// visit; the backend may need to (re)establish a session on it.
    /// </summary>
    Task<NpcTurn> ExchangeAsync(VisitContext ctx, string playerLine, bool isOpener, CancellationToken ct);
}
