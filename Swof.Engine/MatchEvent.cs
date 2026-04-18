using System.Numerics;

namespace Swof.Engine;

public sealed record MatchEvent(
    MatchEventType Type,
    RestartKind Restart,
    TeamSide? Team,
    Vector2 Position,
    string Description)
{
    public static MatchEvent None { get; } = new(
        MatchEventType.None,
        RestartKind.None,
        Team: null,
        Position: Vector2.Zero,
        Description: string.Empty);
}
