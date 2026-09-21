using System.Numerics;

namespace Swof.Engine;

public sealed record BallSnapshot(
    Vector2 Position,
    Vector2 Velocity,
    float Height,
    float VerticalVelocity,
    float Radius,
    int? PossessorId,
    BallControlKind ControlKind)
{
    public static BallSnapshot FromState(BallState ball)
    {
        return new BallSnapshot(
            ball.Position,
            ball.Velocity,
            ball.Height,
            ball.VerticalVelocity,
            ball.Radius,
            ball.PossessorId,
            ball.ControlKind);
    }

    public static BallSnapshot Interpolate(BallSnapshot from, BallSnapshot to, float alpha)
    {
        return to with
        {
            Position = Vector2.Lerp(from.Position, to.Position, alpha),
            Velocity = Vector2.Lerp(from.Velocity, to.Velocity, alpha),
            Height = Lerp(from.Height, to.Height, alpha),
            VerticalVelocity = Lerp(from.VerticalVelocity, to.VerticalVelocity, alpha)
        };
    }

    private static float Lerp(float from, float to, float alpha)
    {
        return from + (to - from) * alpha;
    }
}
