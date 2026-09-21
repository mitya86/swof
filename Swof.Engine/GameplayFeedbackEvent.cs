using System.Numerics;

namespace Swof.Engine;

public sealed record GameplayFeedbackEvent(
    long Sequence,
    long Tick,
    GameplayFeedbackType Type,
    int? PlayerId,
    TeamSide? Team,
    Vector2 Position,
    Vector2 Direction,
    float Intensity);
