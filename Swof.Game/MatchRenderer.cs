using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Swof.Engine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

internal sealed class MatchRenderer : IDisposable
{
    private const int CleanHudHeight = 44;
    private const int DebugHudHeight = 62;
    private const int ViewPadding = 10;
    private const int PlayerAtlasColumns = 2;
    private const int PlayerRunFrameCount = 4;
    private const int PlayerRunTeamRows = 2;
    private const float PlayerSpriteHeightInRadii = 3.4f;
    private const float PlayerReferenceSpeedPerTick = 0.34f;
    private const float PlayerAnimationSpeedThreshold = 0.04f;
    private const float CameraResponsePerSecond = 8f;
    private const float MaxCameraFrameSeconds = 0.05f;

    private static readonly Color ActiveColor = new(255, 230, 80);
    private static readonly Color HudColor = new(16, 18, 22, 235);
    private static readonly Color ShadowColor = new(0, 0, 0, 105);
    private static readonly Color PlayerShadowColor = new(0, 0, 0, 82);
    private static readonly Color HomeGoalkeeperTint = new(145, 255, 175);
    private static readonly Color AwayGoalkeeperTint = new(255, 220, 130);

    private readonly GraphicsDevice _graphicsDevice;
    private readonly RenderPrimitives _primitives;
    private readonly PitchSurface _pitchSurface;
    private readonly Texture2D _playerAtlas;
    private readonly Texture2D _playerRunAtlas;
    private readonly Dictionary<int, float> _playerAnimationPhases = new();
    private readonly RasterizerState _gameplayRasterizerState;
    private readonly MatchFeedbackPresentation _feedback = new();
    private WorldCamera? _camera;

    public MatchRenderer(GraphicsDevice graphicsDevice, Texture2D playerAtlas, Texture2D playerRunAtlas)
    {
        _graphicsDevice = graphicsDevice;
        _primitives = new RenderPrimitives(graphicsDevice);
        _pitchSurface = new PitchSurface(graphicsDevice, _primitives);
        _playerAtlas = playerAtlas;
        _playerRunAtlas = playerRunAtlas;
        _gameplayRasterizerState = new RasterizerState
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true
        };
    }

    public void Draw(
        MatchSnapshot snapshot,
        SpriteFont font,
        string primaryStatus,
        string secondaryStatus,
        string? notice,
        float elapsedSeconds,
        MatchPresentationSettings settings)
    {
        var bounds = _graphicsDevice.Viewport.Bounds;
        var hudHeight = settings.ShowDebugHud ? DebugHudHeight : CleanHudHeight;
        var gameView = new Rectangle(
            ViewPadding,
            hudHeight + ViewPadding,
            Math.Max(1, bounds.Width - ViewPadding * 2),
            Math.Max(1, bounds.Height - hudHeight - ViewPadding * 2));
        _feedback.Advance(snapshot, elapsedSeconds, settings.FeedbackMode);
        var targetCamera = WorldCamera.Create(snapshot, gameView.Width / (float)gameView.Height);
        if (settings.CameraEffectsEnabled)
        {
            targetCamera = targetCamera.Translate(_feedback.GetCameraOffset(settings.FeedbackIntensity));
        }

        var camera = UpdateCamera(targetCamera, elapsedSeconds);
        // Building the cached pitch uses the shared SpriteBatch, so it must finish before the
        // gameplay batch begins. Prepare is cheap after the first frame unless the field changes.
        _pitchSurface.Prepare(snapshot.Field);

        _graphicsDevice.Clear(new Color(11, 12, 15));
        _graphicsDevice.ScissorRectangle = gameView;
        _primitives.Batch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            _gameplayRasterizerState);
        _pitchSurface.Draw(camera, gameView);
        _primitives.Batch.End();

        // Keep the deliberately low-resolution player pixels crisp. Their world and screen
        // positions remain floating point, so point sampling does not quantize movement.
        _primitives.Batch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            _gameplayRasterizerState);
        DrawPlayers(snapshot, camera, gameView, elapsedSeconds, settings.FeedbackIntensity);
        _primitives.Batch.End();

        _primitives.Batch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            _gameplayRasterizerState);
        DrawBall(snapshot, camera, gameView);
        _feedback.Draw(_primitives, snapshot, camera, gameView, settings.FeedbackMode);
        DrawActivePlayerIdentity(snapshot, font, camera, gameView);
        _primitives.DrawLine(
            new Vector2(gameView.Left, gameView.Top),
            new Vector2(gameView.Right, gameView.Top),
            Color.Black,
            2f);
        _primitives.Batch.End();

        _primitives.Batch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);
        _primitives.FillRectangle(new Rectangle(0, 0, bounds.Width, hudHeight), HudColor);
        if (settings.ShowDebugHud)
        {
            _primitives.Batch.DrawString(font, primaryStatus, new Vector2(12f, 7f), Color.White);
            _primitives.Batch.DrawString(font, secondaryStatus, new Vector2(12f, 32f), new Color(190, 211, 235));
        }
        else
        {
            DrawMatchHud(snapshot, font, bounds, settings);
        }

        if (!string.IsNullOrWhiteSpace(notice))
        {
            var size = font.MeasureString(notice);
            var noticeRectangle = new Rectangle(
                gameView.Center.X - (int)size.X / 2 - 12,
                gameView.Top + 14,
                (int)size.X + 24,
                (int)size.Y + 12);
            _primitives.FillRectangle(noticeRectangle, new Color(10, 12, 16, 215));
            _primitives.Batch.DrawString(
                font,
                notice,
                new Vector2(noticeRectangle.X + 12, noticeRectangle.Y + 6),
                ActiveColor);
        }

        if (snapshot.IsBallOutOfPlay)
        {
            var restartLabel = snapshot.LastEvent.Restart switch
            {
                RestartKind.Kickoff => "GOAL",
                RestartKind.KickIn => "KICK-IN",
                RestartKind.Corner => "CORNER",
                RestartKind.GoalClearance => "GOAL CLEARANCE",
                _ => "PLAY STOPPED"
            };
            var restartText = snapshot.LastEvent.Team is TeamSide restartTeam
                ? $"{restartLabel} - {restartTeam.ToString().ToUpperInvariant()}"
                : restartLabel;
            var size = font.MeasureString(restartText);
            var position = new Vector2(
                gameView.Center.X - size.X * 0.5f,
                gameView.Center.Y - size.Y * 0.5f);
            _primitives.FillRectangle(
                new Rectangle(
                    (int)position.X - 16,
                    (int)position.Y - 10,
                    (int)size.X + 32,
                    (int)size.Y + 20),
                new Color(10, 12, 16, 225));
            _primitives.Batch.DrawString(font, restartText, position, ActiveColor);
        }

        _primitives.Batch.End();
    }

    public void Dispose()
    {
        _gameplayRasterizerState.Dispose();
        _pitchSurface.Dispose();
        _primitives.Dispose();
    }

    private WorldCamera UpdateCamera(WorldCamera target, float elapsedSeconds)
    {
        if (_camera is not WorldCamera current)
        {
            _camera = target;
            return target;
        }

        var frameSeconds = Math.Clamp(elapsedSeconds, 0f, MaxCameraFrameSeconds);
        var response = 1f - MathF.Exp(-CameraResponsePerSecond * frameSeconds);
        _camera = WorldCamera.Interpolate(current, target, response);
        return _camera.Value;
    }

    private void DrawPlayers(
        MatchSnapshot snapshot,
        WorldCamera camera,
        Rectangle viewport,
        float elapsedSeconds,
        float feedbackIntensity)
    {
        var scale = camera.ScaleFor(viewport);
        var animationFrameSeconds = Math.Clamp(elapsedSeconds, 0f, MaxCameraFrameSeconds);

        // Back-to-front ordering lets the full-body silhouettes overlap naturally on the pitch.
        foreach (var player in snapshot.Players.OrderBy(player => player.Position.Y))
        {
            var center = camera.ToScreen(player.Position, viewport);
            var radius = MathF.Max(5f, player.Radius * scale);
            if (!IsVisible(center, radius * 3f, viewport))
            {
                continue;
            }

            var facing = player.Facing.ToUnitVector();
            var forward = GetVisualForward(player, new Vector2(facing.X, facing.Y));
            var reactionOffset = Vector2.Zero;
            if (_feedback.TryGetReaction(player, out var reactionDirection, out var reactionStrength))
            {
                reactionStrength *= Math.Clamp(feedbackIntensity, 0f, 1f);
                var reactionForward = new Vector2(reactionDirection.X, reactionDirection.Y);
                var reacted = Vector2.Lerp(forward, reactionForward, reactionStrength * 0.48f);
                if (reacted.LengthSquared() > 0.0001f)
                {
                    reacted.Normalize();
                    forward = reacted;
                }

                reactionOffset = reactionForward * (radius * 0.11f * reactionStrength);
            }

            if (player.HasPossession)
            {
                _primitives.DrawRing(center, radius + 5f, Color.White);
            }

            if (player.Id == snapshot.ActivePlayerId)
            {
                _primitives.DrawRing(center, radius + 8f, ActiveColor);
                var markerCenter = center - Vector2.UnitY * (radius * 2.15f);
                _primitives.DrawLine(markerCenter - new Vector2(6f, 5f), markerCenter, ActiveColor, 2.2f);
                _primitives.DrawLine(markerCenter + new Vector2(6f, -5f), markerCenter, ActiveColor, 2.2f);
            }

            var speed01 = Math.Clamp(player.Velocity.Length() / PlayerReferenceSpeedPerTick, 0f, 1f);
            var isMoving = speed01 > PlayerAnimationSpeedThreshold;
            var animationPhase = GetPlayerAnimationPhase(player.Id, speed01, animationFrameSeconds, isMoving);
            var gaitRadians = isMoving
                ? animationPhase / PlayerRunFrameCount * MathHelper.TwoPi
                : snapshot.ClockSeconds * 2.2f + player.Id;
            var stride = MathF.Sin(gaitRadians);
            var lift = MathF.Abs(stride) * radius * 0.075f * speed01;
            var right = new Vector2(-forward.Y, forward.X);
            var sway = right * (stride * radius * 0.045f * speed01);
            var windup01 = Math.Clamp(player.ActionCharge01, 0f, 1f);
            var followThrough01 = Math.Clamp(player.KickAnimation01, 0f, 1f);
            var actionOffset = forward * radius * (followThrough01 * 0.24f - windup01 * 0.16f);
            var bodyPosition = center
                + forward * (radius * 0.08f * speed01)
                + actionOffset
                + reactionOffset
                + sway
                - Vector2.UnitY * lift;

            var shadowStretch = 1f + speed01 * 0.12f;
            _primitives.FillEllipse(
                center + new Vector2(0f, radius * 0.28f),
                radius * 0.92f * shadowStretch,
                radius * 0.42f,
                PlayerShadowColor);

            var spriteAtlas = isMoving ? _playerRunAtlas : _playerAtlas;
            var spriteEffects = SpriteEffects.None;
            Rectangle sourceRectangle;
            if (isMoving)
            {
                var frameWidth = _playerRunAtlas.Width / PlayerRunFrameCount;
                var frameHeight = _playerRunAtlas.Height / PlayerRunTeamRows;
                var frameIndex = (int)MathF.Floor(animationPhase) % PlayerRunFrameCount;
                var teamRow = player.Team == TeamSide.Home ? 0 : 1;
                if (frameIndex >= PlayerRunFrameCount / 2)
                {
                    spriteEffects = SpriteEffects.FlipHorizontally;
                }

                sourceRectangle = new Rectangle(
                    frameIndex * frameWidth,
                    teamRow * frameHeight,
                    frameWidth,
                    frameHeight);
            }
            else
            {
                var frameWidth = _playerAtlas.Width / PlayerAtlasColumns;
                var sourceX = player.Team == TeamSide.Home ? 0 : frameWidth;
                sourceRectangle = new Rectangle(sourceX, 0, frameWidth, _playerAtlas.Height);
            }

            var sourceOrigin = new Vector2(sourceRectangle.Width * 0.5f, sourceRectangle.Height * 0.5f);
            var sourceHeight = sourceRectangle.Height;
            var baseScale = radius * PlayerSpriteHeightInRadii / sourceHeight;
            var actionScale = new Vector2(
                baseScale * (1f + windup01 * 0.07f),
                baseScale * (1f - windup01 * 0.09f + followThrough01 * 0.08f));
            var rotation = MathF.Atan2(forward.Y, forward.X)
                + MathHelper.PiOver2;
            var spriteTint = player.Role switch
            {
                PlayerRole.Goalkeeper when player.Team == TeamSide.Home => HomeGoalkeeperTint,
                PlayerRole.Goalkeeper => AwayGoalkeeperTint,
                _ => Color.White
            };

            _primitives.Batch.Draw(
                spriteAtlas,
                bodyPosition,
                sourceRectangle,
                spriteTint,
                rotation,
                sourceOrigin,
                actionScale,
                spriteEffects,
                layerDepth: 0f);
        }
    }

    private float GetPlayerAnimationPhase(int playerId, float speed01, float elapsedSeconds, bool isMoving)
    {
        if (!_playerAnimationPhases.TryGetValue(playerId, out var phase))
        {
            phase = playerId * 0.73f % PlayerRunFrameCount;
        }

        if (isMoving)
        {
            var framesPerSecond = MathHelper.Lerp(8f, 14f, speed01);
            phase = (phase + elapsedSeconds * framesPerSecond) % PlayerRunFrameCount;
            _playerAnimationPhases[playerId] = phase;
        }

        return phase;
    }

    private static Vector2 GetVisualForward(PlayerSnapshot player, Vector2 facing)
    {
        var velocity = new Vector2(player.Velocity.X, player.Velocity.Y);
        if (velocity.LengthSquared() <= 0.0001f)
        {
            return facing;
        }

        velocity.Normalize();
        var blended = Vector2.Lerp(velocity, facing, 0.65f);
        if (blended.LengthSquared() <= 0.0001f)
        {
            return facing;
        }

        blended.Normalize();
        return blended;
    }

    private void DrawBall(MatchSnapshot snapshot, WorldCamera camera, Rectangle viewport)
    {
        var ball = snapshot.Ball;
        var scale = camera.ScaleFor(viewport);
        var shadowCenter = camera.ToScreen(ball.Position, viewport);
        var baseRadius = MathF.Max(4f, ball.Radius * scale);
        var heightPixels = ball.Height * scale;
        var ballCenter = shadowCenter - Vector2.UnitY * heightPixels;
        var ballRadius = baseRadius * (1f + MathF.Min(0.5f, ball.Height * 0.045f));
        var shadowRadiusX = baseRadius * MathF.Max(0.45f, 1f - ball.Height * 0.035f);
        var shadowRadiusY = baseRadius * MathF.Max(0.25f, 0.55f - ball.Height * 0.025f);

        _primitives.FillEllipse(shadowCenter, shadowRadiusX, shadowRadiusY, ShadowColor);
        _primitives.FillCircle(ballCenter, ballRadius + 1.3f, Color.Black);
        _primitives.FillCircle(ballCenter, ballRadius, new Color(245, 245, 245));
        _primitives.FillCircle(
            ballCenter + new Vector2(ballRadius * 0.28f, -ballRadius * 0.2f),
            MathF.Max(1f, ballRadius * 0.18f),
            new Color(55, 55, 60));
    }

    private void DrawActivePlayerIdentity(
        MatchSnapshot snapshot,
        SpriteFont font,
        WorldCamera camera,
        Rectangle viewport)
    {
        var player = snapshot.ActivePlayer;
        var center = camera.ToScreen(player.Position, viewport);
        if (!IsVisible(center, 40f, viewport))
        {
            return;
        }

        var label = $"{player.ShirtNumber}  {player.Name.ToUpperInvariant()}";
        const float textScale = 0.62f;
        var size = font.MeasureString(label) * textScale;
        var position = center + new Vector2(-size.X * 0.5f, 17f);
        var rectangle = new Rectangle(
            (int)position.X - 5,
            (int)position.Y - 2,
            (int)MathF.Ceiling(size.X) + 10,
            (int)MathF.Ceiling(size.Y) + 4);
        _primitives.FillRectangle(rectangle, new Color(10, 12, 16, 185));
        _primitives.Batch.DrawString(
            font,
            label,
            position,
            Color.White,
            rotation: 0f,
            origin: Vector2.Zero,
            scale: textScale,
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }

    private void DrawMatchHud(
        MatchSnapshot snapshot,
        SpriteFont font,
        Rectangle bounds,
        MatchPresentationSettings settings)
    {
        var score = $"HOME  {snapshot.HomeScore}  -  {snapshot.AwayScore}  AWAY";
        var minutes = (int)(snapshot.ClockSeconds / 60f);
        var seconds = (int)snapshot.ClockSeconds % 60;
        var clock = $"{minutes:00}:{seconds:00}";
        var active = snapshot.ActivePlayer;
        var activeText = $"{active.ShirtNumber}  {active.Name.ToUpperInvariant()}";

        var scoreSize = font.MeasureString(score);
        var clockSize = font.MeasureString(clock);
        var scorePosition = new Vector2(bounds.Center.X - scoreSize.X * 0.5f, 5f);
        _primitives.Batch.DrawString(font, score, scorePosition, Color.White);
        _primitives.Batch.DrawString(font, clock, new Vector2(scorePosition.X + scoreSize.X + 20f, 5f), new Color(190, 211, 235));
        _primitives.Batch.DrawString(font, activeText, new Vector2(14f, 5f), ActiveColor);

        var help = $"F2 {settings.FeedbackLabel}   F3 DEBUG   F4 CAMERA";
        var helpSize = font.MeasureString(help);
        _primitives.Batch.DrawString(font, help, new Vector2(bounds.Right - helpSize.X - 14f, 5f), new Color(130, 145, 162));
    }

    private static bool IsVisible(Vector2 center, float margin, Rectangle viewport)
    {
        return center.X >= viewport.Left - margin
            && center.X <= viewport.Right + margin
            && center.Y >= viewport.Top - margin
            && center.Y <= viewport.Bottom + margin;
    }
}
