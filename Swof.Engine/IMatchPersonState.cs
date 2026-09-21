using System.Numerics;

namespace Swof.Engine;

/// <summary>
/// Exposes the spatial state shared by footballers and match officials.
/// </summary>
public interface IMatchPersonState
{
    int Id { get; }
    string Name { get; }
    Vector2 Position { get; }
    Vector2 Velocity { get; }
    Azimuth Facing { get; }
    float Radius { get; }
}
