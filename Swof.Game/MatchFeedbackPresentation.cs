using Microsoft.Xna.Framework;
using Swof.Engine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

internal sealed class MatchFeedbackPresentation
{
    private const float MaxBurstLifetimeSeconds = 0.72f;
    private const float ReactionLifetimeSeconds = 0.34f;
    private const float ReactionRadiusWorld = 18f;
    private const float ConnectedTrailLifetimeSeconds = 0.22f;
    private const float ArcadeTrailLifetimeSeconds = 0.78f;
    private const float PassEffectLifetimeSeconds = 1.05f;
    private const float ShotEffectLifetimeSeconds = 1.35f;
    private const int MaxTrailSamples = 48;

    private readonly List<FeedbackBurst> _bursts = new();
    private readonly List<BallTrailSample> _ballTrail = new();
    private long _lastObservedSequence;
    private long _lastObservedTick;
    private long _ballEffectSequence;
    private BallEffectKind _activeBallEffect;
    private BallEffectKind _trailEffect;
    private float _ballEffectAgeSeconds;

    public void Advance(MatchSnapshot snapshot, float elapsedSeconds, MatchFeedbackMode mode)
    {
        var frameSeconds = Math.Clamp(elapsedSeconds, 0f, 0.05f);
        AgeBursts(frameSeconds);
        AgeTrail(frameSeconds, mode);

        // A new match numbers its feedback from the start again.
        if (snapshot.Tick < _lastObservedTick)
        {
            _lastObservedSequence = 0;
        }

        _lastObservedTick = snapshot.Tick;
        foreach (var feedbackEvent in snapshot.FeedbackEvents)
        {
            if (feedbackEvent.Sequence <= _lastObservedSequence)
            {
                continue;
            }

            _lastObservedSequence = feedbackEvent.Sequence;
            if (mode != MatchFeedbackMode.Dry && feedbackEvent.Type != GameplayFeedbackType.None)
            {
                _bursts.Add(new FeedbackBurst(feedbackEvent, 0f));
            }

            if (mode != MatchFeedbackMode.Dry)
            {
                switch (feedbackEvent.Type)
                {
                    case GameplayFeedbackType.PassContact:
                        BeginBallEffect(BallEffectKind.Pass, feedbackEvent);
                        break;
                    case GameplayFeedbackType.ShotContact:
                        BeginBallEffect(BallEffectKind.Shot, feedbackEvent);
                        break;
                }
            }
        }

        if (mode == MatchFeedbackMode.Dry)
        {
            _bursts.Clear();
            EndBallEffect(clearTrail: true);
            return;
        }

        if (_bursts.Count > 32)
        {
            _bursts.RemoveRange(0, _bursts.Count - 32);
        }

        if (_activeBallEffect == BallEffectKind.None)
        {
            return;
        }

        _ballEffectAgeSeconds += frameSeconds;
        var maximumAge = _activeBallEffect == BallEffectKind.Pass
            ? PassEffectLifetimeSeconds
            : ShotEffectLifetimeSeconds;
        var minimumSpeedSquared = _activeBallEffect == BallEffectKind.Pass ? 0.0025f : 0.0144f;
        var possessionEndedFlight = snapshot.Ball.PossessorId is not null;
        var restartEndedFlight = snapshot.IsBallOutOfPlay;
        if (possessionEndedFlight
            || restartEndedFlight
            || _ballEffectAgeSeconds >= maximumAge
            || snapshot.Ball.Velocity.LengthSquared() < minimumSpeedSquared)
        {
            EndBallEffect(clearTrail: possessionEndedFlight || restartEndedFlight);
            return;
        }

        AppendTrailSample(snapshot.Ball);
    }

    public NumericsVector2 GetCameraOffset(float intensity)
    {
        var latest = _bursts.LastOrDefault(burst =>
            burst.Event.Type is GameplayFeedbackType.ShotContact or GameplayFeedbackType.GoalkeeperCatch);
        if (latest is null || latest.AgeSeconds >= 0.18f)
        {
            return NumericsVector2.Zero;
        }

        var direction = SafeNormalize(latest.Event.Direction);
        var decay = 1f - latest.AgeSeconds / 0.18f;
        return -direction * (0.07f * decay * Math.Clamp(intensity, 0f, 1f));
    }

    public bool TryGetReaction(PlayerSnapshot player, out NumericsVector2 direction, out float strength)
    {
        var latest = _bursts.LastOrDefault(burst =>
            burst.AgeSeconds < ReactionLifetimeSeconds
            && burst.Event.PlayerId != player.Id
            && burst.Event.Type is GameplayFeedbackType.PassContact
                or GameplayFeedbackType.ShotContact
                or GameplayFeedbackType.PossessionChanged
                or GameplayFeedbackType.TackleStarted
                or GameplayFeedbackType.GoalkeeperCatch);
        if (latest is null)
        {
            direction = NumericsVector2.Zero;
            strength = 0f;
            return false;
        }

        var offset = latest.Event.Position - player.Position;
        var distance = offset.Length();
        if (distance <= 0.001f || distance >= ReactionRadiusWorld)
        {
            direction = NumericsVector2.Zero;
            strength = 0f;
            return false;
        }

        direction = offset / distance;
        var timeStrength = 1f - latest.AgeSeconds / ReactionLifetimeSeconds;
        var distanceStrength = 1f - distance / ReactionRadiusWorld;
        strength = Math.Clamp(timeStrength * distanceStrength * 1.4f, 0f, 1f);
        return strength > 0.05f;
    }

    public void Draw(
        RenderPrimitives primitives,
        MatchSnapshot snapshot,
        WorldCamera camera,
        Rectangle viewport,
        MatchFeedbackMode mode)
    {
        if (mode == MatchFeedbackMode.Dry)
        {
            return;
        }

        foreach (var burst in _bursts)
        {
            DrawBurst(primitives, burst, camera, viewport, mode);
        }

        DrawBallMotionEffect(primitives, snapshot, camera, viewport, mode);
    }

    private void AgeBursts(float frameSeconds)
    {
        for (var i = _bursts.Count - 1; i >= 0; i--)
        {
            var aged = _bursts[i] with { AgeSeconds = _bursts[i].AgeSeconds + frameSeconds };
            if (aged.AgeSeconds >= MaxBurstLifetimeSeconds)
            {
                _bursts.RemoveAt(i);
            }
            else
            {
                _bursts[i] = aged;
            }
        }
    }

    private void AgeTrail(float frameSeconds, MatchFeedbackMode mode)
    {
        var lifetime = mode == MatchFeedbackMode.Arcade
            ? ArcadeTrailLifetimeSeconds
            : ConnectedTrailLifetimeSeconds;
        for (var i = _ballTrail.Count - 1; i >= 0; i--)
        {
            var aged = _ballTrail[i] with { AgeSeconds = _ballTrail[i].AgeSeconds + frameSeconds };
            if (aged.AgeSeconds >= lifetime)
            {
                _ballTrail.RemoveAt(i);
            }
            else
            {
                _ballTrail[i] = aged;
            }
        }

        if (_ballTrail.Count == 0 && _activeBallEffect == BallEffectKind.None)
        {
            _trailEffect = BallEffectKind.None;
        }
    }

    private void BeginBallEffect(BallEffectKind effect, GameplayFeedbackEvent feedbackEvent)
    {
        _activeBallEffect = effect;
        _trailEffect = effect;
        _ballEffectSequence = feedbackEvent.Sequence;
        _ballEffectAgeSeconds = 0f;
        _ballTrail.Clear();
        _ballTrail.Add(new BallTrailSample(feedbackEvent.Position, Height: 0f, AgeSeconds: 0f));
    }

    private void EndBallEffect(bool clearTrail)
    {
        _activeBallEffect = BallEffectKind.None;
        _ballEffectAgeSeconds = 0f;
        if (clearTrail)
        {
            _trailEffect = BallEffectKind.None;
            _ballTrail.Clear();
        }
    }

    private void AppendTrailSample(BallSnapshot ball)
    {
        if (_ballTrail.Count > 0)
        {
            var latest = _ballTrail[^1];
            var positionDelta = ball.Position - latest.Position;
            var heightDelta = ball.Height - latest.Height;
            if (positionDelta.LengthSquared() < 0.0025f && MathF.Abs(heightDelta) < 0.025f)
            {
                return;
            }
        }

        _ballTrail.Add(new BallTrailSample(ball.Position, ball.Height, AgeSeconds: 0f));
        if (_ballTrail.Count > MaxTrailSamples)
        {
            _ballTrail.RemoveRange(0, _ballTrail.Count - MaxTrailSamples);
        }
    }

    private static void DrawBurst(
        RenderPrimitives primitives,
        FeedbackBurst burst,
        WorldCamera camera,
        Rectangle viewport,
        MatchFeedbackMode mode)
    {
        var progress = Math.Clamp(burst.AgeSeconds / MaxBurstLifetimeSeconds, 0f, 1f);
        var visibility = 1f - progress;
        var center = camera.ToScreen(burst.Event.Position, viewport);
        var direction = ToFramework(SafeNormalize(burst.Event.Direction));
        if (direction.LengthSquared() <= 0.0001f)
        {
            direction = Vector2.UnitX;
        }

        var right = new Vector2(-direction.Y, direction.X);
        var arcade = mode == MatchFeedbackMode.Arcade;
        switch (burst.Event.Type)
        {
            case GameplayFeedbackType.ActionAccepted:
                primitives.DrawRing(center, 5f + progress * (arcade ? 12f : 8f), new Color(255, 229, 100) * (0.48f * visibility));
                break;
            case GameplayFeedbackType.PassContact:
                DrawContact(primitives, center, direction, right, burst, new Color(170, 238, 255), visibility, arcade ? 1.1f : 0.75f);
                break;
            case GameplayFeedbackType.ShotContact:
                DrawContact(primitives, center, direction, right, burst, new Color(255, 175, 55), visibility, arcade ? 1.45f : 1f);
                break;
            case GameplayFeedbackType.PossessionChanged:
                primitives.DrawRing(center, 7f + progress * (arcade ? 18f : 12f), new Color(175, 255, 196) * (0.52f * visibility));
                break;
            case GameplayFeedbackType.TackleStarted:
                DrawTackleBurst(primitives, center, direction, right, visibility, arcade);
                break;
            case GameplayFeedbackType.GoalkeeperCatch:
                DrawCatchBurst(primitives, center, right, progress, visibility, arcade);
                break;
            case GameplayFeedbackType.GoalScored:
                DrawGoalBurst(primitives, center, progress, visibility, arcade);
                break;
        }
    }

    private static void DrawContact(
        RenderPrimitives primitives,
        Vector2 center,
        Vector2 direction,
        Vector2 right,
        FeedbackBurst burst,
        Color color,
        float visibility,
        float scale)
    {
        var travel = Math.Clamp(burst.AgeSeconds / 0.22f, 0f, 1f);
        primitives.DrawLine(center - direction * (10f * scale), center + direction * (5f * scale), color * (0.8f * visibility), 2.2f * scale);
        primitives.DrawLine(center - right * (5f * scale), center + right * (5f * scale), Color.White * (0.72f * visibility), 1.7f * scale);

        for (var i = 0; i < 5; i++)
        {
            var side = ((burst.Event.Sequence + i * 3) % 7 - 3) / 3f;
            var distance = (6f + i * 2.3f) * travel * scale;
            var particle = center - direction * distance + right * side * (4f + travel * 8f);
            primitives.FillRectangle(
                new Rectangle((int)particle.X, (int)particle.Y, i % 2 == 0 ? 3 : 2, 2),
                color * (0.7f * visibility));
        }
    }

    private static void DrawTackleBurst(
        RenderPrimitives primitives,
        Vector2 center,
        Vector2 direction,
        Vector2 right,
        float visibility,
        bool arcade)
    {
        var color = new Color(215, 241, 255) * (0.62f * visibility);
        primitives.DrawLine(center - direction * 5f, center + direction * (arcade ? 14f : 8f), color, arcade ? 3f : 2f);
        if (arcade)
        {
            primitives.DrawLine(center - right * 7f, center + direction * 10f + right * 4f, new Color(98, 211, 255) * (0.48f * visibility), 2f);
            primitives.DrawLine(center + right * 7f, center + direction * 10f - right * 4f, Color.White * (0.4f * visibility), 1.5f);
        }
    }

    private static void DrawCatchBurst(
        RenderPrimitives primitives,
        Vector2 center,
        Vector2 right,
        float progress,
        float visibility,
        bool arcade)
    {
        primitives.DrawRing(center, 8f + progress * (arcade ? 24f : 15f), new Color(130, 232, 255) * (0.68f * visibility));
        primitives.DrawLine(center - right * 7f, center + right * 7f, Color.White * (0.55f * visibility), 2f);
        if (arcade)
        {
            primitives.DrawRing(center, 15f + progress * 34f, Color.White * (0.28f * visibility));
        }
    }

    private static void DrawGoalBurst(
        RenderPrimitives primitives,
        Vector2 center,
        float progress,
        float visibility,
        bool arcade)
    {
        primitives.DrawRing(center, 14f + progress * (arcade ? 74f : 55f), new Color(255, 229, 100) * (0.65f * visibility));
        if (!arcade)
        {
            return;
        }

        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathHelper.TwoPi / 8f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var start = center + direction * (18f + progress * 25f);
            var end = start + direction * (9f + progress * 18f);
            primitives.DrawLine(start, end, new Color(255, 176, 48) * (0.5f * visibility), 2f);
        }
    }

    private void DrawBallMotionEffect(
        RenderPrimitives primitives,
        MatchSnapshot snapshot,
        WorldCamera camera,
        Rectangle viewport,
        MatchFeedbackMode mode)
    {
        var lifetime = mode == MatchFeedbackMode.Arcade
            ? ArcadeTrailLifetimeSeconds
            : ConnectedTrailLifetimeSeconds;
        for (var i = 1; i < _ballTrail.Count; i++)
        {
            var previous = _ballTrail[i - 1];
            var current = _ballTrail[i];
            var visibility = Math.Clamp(1f - MathF.Max(previous.AgeSeconds, current.AgeSeconds) / lifetime, 0f, 1f);
            if (visibility <= 0f)
            {
                continue;
            }

            var from = ToScreen(previous, camera, viewport);
            var to = ToScreen(current, camera, viewport);
            if (mode == MatchFeedbackMode.Arcade)
            {
                DrawArcadeTrailSegment(primitives, from, to, visibility, _trailEffect, i);
            }
            else
            {
                var color = _trailEffect == BallEffectKind.Shot
                    ? new Color(255, 220, 155)
                    : new Color(210, 242, 255);
                primitives.DrawLine(from, to, color * (visibility * 0.42f), 1.8f);
            }
        }

        if (mode == MatchFeedbackMode.Arcade && _activeBallEffect == BallEffectKind.Shot)
        {
            DrawArcadeFireball(primitives, snapshot.Ball, camera, viewport);
        }
    }

    private static void DrawArcadeTrailSegment(
        RenderPrimitives primitives,
        Vector2 from,
        Vector2 to,
        float visibility,
        BallEffectKind effect,
        int sampleIndex)
    {
        if (effect == BallEffectKind.Shot)
        {
            primitives.DrawLine(from, to, new Color(198, 43, 20) * (visibility * 0.48f), 9f * visibility + 1f);
            primitives.DrawLine(from, to, new Color(255, 113, 24) * (visibility * 0.68f), 5f * visibility + 1f);
            primitives.DrawLine(from, to, new Color(255, 235, 125) * (visibility * 0.88f), 1.5f + visibility);
            return;
        }

        primitives.DrawLine(from, to, new Color(42, 166, 255) * (visibility * 0.42f), 5f * visibility + 1f);
        primitives.DrawLine(from, to, new Color(202, 246, 255) * (visibility * 0.8f), 1.6f + visibility);
        if (sampleIndex % 3 == 0)
        {
            primitives.FillRectangle(
                new Rectangle((int)from.X - 1, (int)from.Y - 1, 3, 3),
                Color.White * (visibility * 0.72f));
        }
    }

    private void DrawArcadeFireball(
        RenderPrimitives primitives,
        BallSnapshot ball,
        WorldCamera camera,
        Rectangle viewport)
    {
        var scale = camera.ScaleFor(viewport);
        var center = camera.ToScreen(ball.Position, viewport) - Vector2.UnitY * ball.Height * scale;
        var ballRadius = MathF.Max(4f, ball.Radius * scale);
        var direction = ToFramework(SafeNormalize(ball.Velocity));
        if (direction.LengthSquared() <= 0.0001f)
        {
            direction = Vector2.UnitX;
        }

        var right = new Vector2(-direction.Y, direction.X);
        var pulse = 0.5f + MathF.Sin(_ballEffectAgeSeconds * 52f) * 0.5f;
        primitives.FillCircle(center, ballRadius + 8f + pulse * 2f, new Color(255, 72, 18) * 0.22f);
        primitives.FillCircle(center, ballRadius + 5f + pulse, new Color(255, 145, 28) * 0.55f);
        primitives.FillCircle(center, ballRadius + 2f, new Color(255, 236, 128) * 0.9f);

        var phase = (int)(_ballEffectAgeSeconds * 42f);
        for (var i = 0; i < 9; i++)
        {
            var sideSeed = (int)((_ballEffectSequence + i * 11 + phase * 5) % 9);
            var side = (sideSeed - 4) / 4f;
            var distance = ballRadius + 3f + (i % 5) * 3.2f;
            var particle = center - direction * distance + right * side * (3f + (i % 3) * 2f);
            var color = (i % 3) switch
            {
                0 => new Color(255, 241, 140),
                1 => new Color(255, 137, 26),
                _ => new Color(214, 49, 18)
            };
            var size = i % 2 == 0 ? 3 : 2;
            primitives.FillRectangle(new Rectangle((int)particle.X, (int)particle.Y, size, size), color * 0.86f);
        }
    }

    private static Vector2 ToScreen(BallTrailSample sample, WorldCamera camera, Rectangle viewport)
    {
        return camera.ToScreen(sample.Position, viewport) - Vector2.UnitY * sample.Height * camera.ScaleFor(viewport);
    }

    private static NumericsVector2 SafeNormalize(NumericsVector2 value)
    {
        return value.LengthSquared() <= 0.0001f ? NumericsVector2.Zero : NumericsVector2.Normalize(value);
    }

    private static Vector2 ToFramework(NumericsVector2 value)
    {
        return new Vector2(value.X, value.Y);
    }

    private enum BallEffectKind
    {
        None = 0,
        Pass = 1,
        Shot = 2
    }

    private sealed record FeedbackBurst(GameplayFeedbackEvent Event, float AgeSeconds);

    private sealed record BallTrailSample(NumericsVector2 Position, float Height, float AgeSeconds);
}
