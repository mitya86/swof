using Microsoft.Xna.Framework;
using Swof.Engine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

internal readonly record struct WorldCamera(float Left, float Right, float Top, float Bottom)
{
    public const float BoundaryMargin = 8f;

    private const float LengthRatio = 0.44f;
    private const float WidthRatio = 0.58f;
    private const float BallWeight = 0.72f;
    private const float LookaheadTicks = 14f;
    private const float MaxLookaheadRatio = 0.24f;

    public float Width => Right - Left;
    public float Height => Bottom - Top;

    public static WorldCamera Interpolate(WorldCamera from, WorldCamera to, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        return new WorldCamera(
            MathHelper.Lerp(from.Left, to.Left, alpha),
            MathHelper.Lerp(from.Right, to.Right, alpha),
            MathHelper.Lerp(from.Top, to.Top, alpha),
            MathHelper.Lerp(from.Bottom, to.Bottom, alpha));
    }

    public static WorldCamera Create(MatchSnapshot snapshot, float screenAspect)
    {
        var field = snapshot.Field;
        var worldWidth = field.Length + BoundaryMargin * 2f;
        var worldHeight = field.Width + BoundaryMargin * 2f;
        var aspect = MathF.Max(0.1f, screenAspect);
        var spanX = field.Length * LengthRatio;
        var spanY = field.Width * WidthRatio;

        if (spanX / spanY < aspect)
        {
            spanX = spanY * aspect;
        }
        else
        {
            spanY = spanX / aspect;
        }

        if (spanX > worldWidth)
        {
            spanX = worldWidth;
            spanY = spanX / aspect;
        }

        if (spanY > worldHeight)
        {
            spanY = worldHeight;
            spanX = spanY * aspect;
        }

        var active = snapshot.ActivePlayer;
        var center = snapshot.Ball.Position * BallWeight + active.Position * (1f - BallWeight);
        var lookahead = snapshot.Ball.Velocity * LookaheadTicks;
        lookahead = new NumericsVector2(
            Math.Clamp(lookahead.X, -spanX * MaxLookaheadRatio, spanX * MaxLookaheadRatio),
            Math.Clamp(lookahead.Y, -spanY * MaxLookaheadRatio, spanY * MaxLookaheadRatio));
        center += lookahead;

        var halfX = spanX * 0.5f;
        var halfY = spanY * 0.5f;
        center = new NumericsVector2(
            Math.Clamp(center.X, field.Left - BoundaryMargin + halfX, field.Right + BoundaryMargin - halfX),
            Math.Clamp(center.Y, field.Top - BoundaryMargin + halfY, field.Bottom + BoundaryMargin - halfY));

        return new WorldCamera(
            center.X - halfX,
            center.X + halfX,
            center.Y - halfY,
            center.Y + halfY);
    }

    public Vector2 ToScreen(NumericsVector2 world, Rectangle viewport)
    {
        var x01 = (world.X - Left) / MathF.Max(0.0001f, Width);
        var y01 = (world.Y - Top) / MathF.Max(0.0001f, Height);
        return new Vector2(
            viewport.X + x01 * viewport.Width,
            viewport.Y + y01 * viewport.Height);
    }

    public float ScaleFor(Rectangle viewport)
    {
        return viewport.Width / MathF.Max(0.0001f, Width);
    }
}
