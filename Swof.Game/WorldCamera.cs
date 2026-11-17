using Microsoft.Xna.Framework;
using Swof.Engine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

internal readonly record struct WorldCamera(float Left, float Right, float Top, float Bottom)
{
    public const float BoundaryMargin = 3f;

    private const float VisibleCourtLengthRatio = 0.5f;
    private const float BallWeight = 0.72f;
    private const float FocusPadding = 2.25f;
    private const float LookaheadTicks = 14f;
    private const float MaxLookaheadRatio = 0.24f;

    public float Width => Right - Left;
    public float Height => Bottom - Top;

    public WorldCamera Translate(NumericsVector2 offset)
    {
        return new WorldCamera(Left + offset.X, Right + offset.X, Top + offset.Y, Bottom + offset.Y);
    }

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
        var spanX = field.Length * VisibleCourtLengthRatio;
        var spanY = spanX / aspect;
        var active = snapshot.ActivePlayer;
        var requiredSpanX = MathF.Abs(snapshot.Ball.Position.X - active.Position.X) + FocusPadding * 2f;
        var requiredSpanY = MathF.Abs(snapshot.Ball.Position.Y - active.Position.Y) + FocusPadding * 2f;
        spanX = MathF.Max(spanX, MathF.Max(requiredSpanX, requiredSpanY * aspect));
        spanY = spanX / aspect;

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

        var center = snapshot.Ball.Position * BallWeight + active.Position * (1f - BallWeight);
        var lookahead = snapshot.Ball.Velocity * LookaheadTicks;
        lookahead = new NumericsVector2(
            Math.Clamp(lookahead.X, -spanX * MaxLookaheadRatio, spanX * MaxLookaheadRatio),
            Math.Clamp(lookahead.Y, -spanY * MaxLookaheadRatio, spanY * MaxLookaheadRatio));
        center += lookahead;

        var halfX = spanX * 0.5f;
        var halfY = spanY * 0.5f;
        var focusMinX = MathF.Min(snapshot.Ball.Position.X, active.Position.X) - FocusPadding;
        var focusMaxX = MathF.Max(snapshot.Ball.Position.X, active.Position.X) + FocusPadding;
        var focusMinY = MathF.Min(snapshot.Ball.Position.Y, active.Position.Y) - FocusPadding;
        var focusMaxY = MathF.Max(snapshot.Ball.Position.Y, active.Position.Y) + FocusPadding;
        var minimumCenterX = MathF.Max(
            field.Left - BoundaryMargin + halfX,
            focusMaxX - halfX);
        var maximumCenterX = MathF.Min(
            field.Right + BoundaryMargin - halfX,
            focusMinX + halfX);
        var minimumCenterY = MathF.Max(
            field.Top - BoundaryMargin + halfY,
            focusMaxY - halfY);
        var maximumCenterY = MathF.Min(
            field.Bottom + BoundaryMargin - halfY,
            focusMinY + halfY);
        center = new NumericsVector2(
            minimumCenterX <= maximumCenterX
                ? Math.Clamp(center.X, minimumCenterX, maximumCenterX)
                : Math.Clamp(center.X, field.Left - BoundaryMargin + halfX, field.Right + BoundaryMargin - halfX),
            minimumCenterY <= maximumCenterY
                ? Math.Clamp(center.Y, minimumCenterY, maximumCenterY)
                : Math.Clamp(center.Y, field.Top - BoundaryMargin + halfY, field.Bottom + BoundaryMargin - halfY));

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
