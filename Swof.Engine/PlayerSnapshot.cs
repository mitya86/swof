using System.Numerics;

namespace Swof.Engine;

public sealed record PlayerSnapshot(
    int Id,
    string Name,
    byte ShirtNumber,
    TeamSide Team,
    PlayerRole Role,
    FutsalOutfieldRole FutsalRole,
    Vector2 Position,
    Vector2 Velocity,
    Azimuth Facing,
    bool HasPossession,
    float Radius,
    float ActionCharge01,
    float KickAnimation01)
{
    public static PlayerSnapshot FromState(PlayerState player)
    {
        return new PlayerSnapshot(
            player.Id,
            player.Name,
            player.ShirtNumber,
            player.Team,
            player.Role,
            player.FutsalRole,
            player.Position,
            player.Velocity,
            player.Facing,
            player.HasPossession,
            player.Radius,
            player.ActionCharge01,
            player.KickAnimation01);
    }

    public static PlayerSnapshot Interpolate(PlayerSnapshot from, PlayerSnapshot to, float alpha)
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
