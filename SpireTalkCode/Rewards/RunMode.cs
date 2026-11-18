using MegaCrit.Sts2.Core.Entities.Players;

namespace SpireTalk.Rewards;

/// <summary>
/// The run's game mode, read by name: Blitz and Dread exist from game 0.113 (the GameMode enum
/// before that has neither), and one DLL runs on every branch.
/// </summary>
public static class RunMode
{
    public static bool IsBlitz(Player? player)
    {
        try
        {
            var state = player?.RunState;
            return state?.GetType().GetProperty("GameMode")?.GetValue(state)?.ToString() == "Blitz";
        }
        catch { return false; }
    }
}
