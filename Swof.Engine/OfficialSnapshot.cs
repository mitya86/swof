using System.Numerics;

namespace Swof.Engine;

/// <summary>
/// Immutable render-facing state for a match official.
/// </summary>
public sealed record OfficialSnapshot(
    int Id,
    MatchOfficialRole Role,
    Vector2 Position,
    Vector2 Velocity,
    Azimuth Facing,
    float Radius)
{
    public static OfficialSnapshot FromState(OfficialState official)
    {
        return new OfficialSnapshot(
            official.Id,
            official.Role,
            official.Position,
            official.Velocity,
            official.Facing,
            official.Radius);
    }

    public static OfficialSnapshot Interpolate(OfficialSnapshot from, OfficialSnapshot to, float alpha)
    {
        return to with
        {
            Position = Vector2.Lerp(from.Position, to.Position, alpha),
            Velocity = Vector2.Lerp(from.Velocity, to.Velocity, alpha),
            Facing = new Azimuth(LerpDegrees(from.Facing.NormalizedDegrees, to.Facing.NormalizedDegrees, alpha))
        };
    }

    private static float LerpDegrees(float from, float to, float alpha)
    {
        var delta = ((to - from + 540f) % 360f) - 180f;
        var value = from + delta * alpha;
        return value < 0f ? value + 360f : value % 360f;
    }
}
