using System.Numerics;

namespace Swof.Engine;

public sealed class PlayerState : IMatchPersonState
{
    public PlayerState(int id, string name, TeamSide team, Vector2 position)
    {
        Id = id;
        Name = name;
        Team = team;
        Position = position;
        KickoffPosition = position;
    }

    public int Id { get; }
    public string Name { get; }
    public byte ShirtNumber { get; init; }
    public TeamSide Team { get; }
    public PlayerRole Role { get; init; } = PlayerRole.Outfield;
    public FutsalOutfieldRole FutsalRole { get; init; }
    public GoalkeeperAttributes? GoalkeeperAttributes { get; init; }
    public Vector2 Position { get; set; }

    /// <summary>Gets where this player lines up for a kickoff: the position the match was created with.</summary>
    public Vector2 KickoffPosition { get; }
    public Vector2 Velocity { get; set; }
    public Azimuth Facing { get; set; }
    public bool HasPossession { get; set; }
    public PlayerAttributes Attributes { get; init; } = PlayerAttributes.DefaultOutfield;
    public float DribbleStepDistance { get; set; }
    public float ActionCharge01 { get; set; }
    public float KickAnimation01 { get; set; }
    public int SlideTicksRemaining { get; set; }
    public int SlideRecoveryTicksRemaining { get; set; }
    public float Radius { get; set; } = PhysicsTuning.Default.PlayerRadius;
    public float MaxSpeedPerTick { get; set; } = PhysicsTuning.Default.PlayerMaxSpeedPerTick;
    public float AccelerationPerTick { get; set; } = PhysicsTuning.Default.PlayerAccelerationPerTick;
    public float FrictionPerTick { get; set; } = PhysicsTuning.Default.PlayerFrictionPerTick;

    public bool IsSliding => SlideTicksRemaining > 0;
    public bool IsRecoveringFromSlide => SlideRecoveryTicksRemaining > 0;
    public bool IsGoalkeeper => Role == PlayerRole.Goalkeeper;
}
