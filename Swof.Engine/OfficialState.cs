using System.Numerics;

namespace Swof.Engine;

/// <summary>
/// Holds spatial state for a match official. Officials are deliberately excluded
/// from team possession, player selection, and football actions.
/// </summary>
public sealed class OfficialState : IMatchPersonState
{
    public OfficialState(int id, string name, MatchOfficialRole role, Vector2 position)
    {
        Id = id;
        Name = name;
        Role = role;
        Position = position;
    }

    public int Id { get; }
    public string Name { get; }
    public MatchOfficialRole Role { get; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Azimuth Facing { get; set; }
    public float Radius { get; set; } = PhysicsTuning.Default.PlayerRadius;
}
