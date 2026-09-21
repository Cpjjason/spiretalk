using SpireTalk.Npc;

namespace SpireTalk.Backend;

/// <summary>
/// Something that can play an NPC: read the context, answer as the NPC, propose choices / an
/// affection delta / an action. The controller applies the rules afterwards. The one
/// implementation is <see cref="EngineBackend"/> (Yumina engine in-process + the player's key);
/// the interface stays so tests and future hosts can swap it.
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
