using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Swof.Engine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

internal sealed class MatchRenderer : IDisposable
{
    private const int HudHeight = 62;
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

    private readonly GraphicsDevice _graphicsDevice;
    private readonly RenderPrimitives _primitives;
    private readonly PitchSurface _pitchSurface;
    private readonly Texture2D _playerAtlas;
    private readonly Texture2D _playerRunAtlas;
    private readonly Dictionary<int, float> _playerAnimationPhases = new();
    private readonly RasterizerState _gameplayRasterizerState;
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
        float elapsedSeconds)
    {
        var bounds = _graphicsDevice.Viewport.Bounds;
        var gameView = new Rectangle(
            ViewPadding,
            HudHeight + ViewPadding,
            Math.Max(1, bounds.Width - ViewPadding * 2),
            Math.Max(1, bounds.Height - HudHeight - ViewPadding * 2));
        var targetCamera = WorldCamera.Create(snapshot, gameView.Width / (float)gameView.Height);
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
        DrawPlayers(snapshot, camera, gameView, elapsedSeconds);
        DrawBall(snapshot, camera, gameView);
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
        _primitives.FillRectangle(new Rectangle(0, 0, bounds.Width, HudHeight), HudColor);
        _primitives.Batch.DrawString(font, primaryStatus, new Vector2(12f, 7f), Color.White);
        _primitives.Batch.DrawString(font, secondaryStatus, new Vector2(12f, 32f), new Color(190, 211, 235));

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
            const string restartText = "PLAY STOPPED - PRESS R TO RESUME";
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
        if (_camera is not WorldCamera current
            || MathF.Abs(current.Width - target.Width) > 0.001f
            || MathF.Abs(current.Height - target.Height) > 0.001f)
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
        float elapsedSeconds)
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

            if (player.HasPossession)
            {
                _primitives.DrawRing(center, radius + 5f, Color.White);
            }

            if (player.Id == snapshot.ActivePlayerId)
            {
                _primitives.DrawRing(center, radius + 9f, ActiveColor);
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
            var bodyPosition = center + forward * (radius * 0.08f * speed01) + sway - Vector2.UnitY * lift;

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
            var breathing = MathF.Sin(snapshot.ClockSeconds * 2.2f + player.Id) * 0.006f;
            var spriteScale = new Vector2(
                baseScale * (1f + breathing + stride * 0.018f * speed01),
                baseScale * (1f - breathing - stride * 0.012f * speed01));
            var rotation = MathF.Atan2(forward.Y, forward.X)
                + MathHelper.PiOver2
                + stride * 0.012f * speed01;

            _primitives.Batch.Draw(
                spriteAtlas,
                bodyPosition,
                sourceRectangle,
                Color.White,
                rotation,
                sourceOrigin,
                spriteScale,
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
            var framesPerSecond = MathHelper.Lerp(5f, 11f, speed01);
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

    private static bool IsVisible(Vector2 center, float margin, Rectangle viewport)
    {
        return center.X >= viewport.Left - margin
            && center.X <= viewport.Right + margin
            && center.Y >= viewport.Top - margin
            && center.Y <= viewport.Bottom + margin;
    }
}
