using System.Numerics;

namespace Swof.Engine;

public sealed class PlayerState
{
    public PlayerState(int id, TeamSide team, Vector2 position)
    {
        Id = id;
        Team = team;
        Position = position;
    }

    public int Id { get; }
    public TeamSide Team { get; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Azimuth Facing { get; set; }
    public bool HasPossession { get; set; }
    public PlayerAttributes Attributes { get; init; } = PlayerAttributes.DefaultOutfield;
    public float DribbleStepDistance { get; set; }
    public int SlideTicksRemaining { get; set; }
    public int SlideRecoveryTicksRemaining { get; set; }
    public float Radius { get; set; } = PhysicsTuning.Default.PlayerRadius;
    public float MaxSpeedPerTick { get; set; } = PhysicsTuning.Default.PlayerMaxSpeedPerTick;
    public float AccelerationPerTick { get; set; } = PhysicsTuning.Default.PlayerAccelerationPerTick;
    public float FrictionPerTick { get; set; } = PhysicsTuning.Default.PlayerFrictionPerTick;

    public bool IsSliding => SlideTicksRemaining > 0;
    public bool IsRecoveringFromSlide => SlideRecoveryTicksRemaining > 0;
}
