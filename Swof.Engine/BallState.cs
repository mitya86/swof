using System.Numerics;

namespace Swof.Engine;

public sealed class BallState
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Radius { get; init; } = PhysicsTuning.Default.BallRadius;
    public int? PossessorId { get; set; }

    public bool IsPossessed => PossessorId.HasValue;
}
