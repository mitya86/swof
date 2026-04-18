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
    public bool HasPossession { get; set; }
    public float Radius { get; init; } = PhysicsTuning.Default.PlayerRadius;
    public float MaxSpeedPerTick { get; init; } = PhysicsTuning.Default.PlayerMaxSpeedPerTick;
    public float AccelerationPerTick { get; init; } = PhysicsTuning.Default.PlayerAccelerationPerTick;
    public float FrictionPerTick { get; init; } = PhysicsTuning.Default.PlayerFrictionPerTick;
}
