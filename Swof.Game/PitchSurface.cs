using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Swof.Engine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

internal sealed class PitchSurface : IDisposable
{
    private const int SurfaceWidth = 2048;
    private const float CenterCircleRadius = 9.15f;
    private const float GoalNetDepth = 2.1f;
    private const float TrackWidth = 1.25f;

    private static readonly Color Grass = new(33, 126, 55);
    private static readonly Color GrassStripe = new(29, 112, 49);
    private static readonly Color Surround = new(50, 54, 48);
    private static readonly Color Track = new(93, 97, 90);
    private static readonly Color Line = new(242, 242, 242);
    private static readonly Color CrowdDark = new(34, 34, 41);
    private static readonly Color CrowdLight = new(105, 111, 130);
    private static readonly Color Bench = new(42, 47, 58);
    private static readonly Color BenchRoof = new(78, 88, 108);
    private static readonly Color Net = new(225, 225, 225, 120);

    private readonly GraphicsDevice _graphicsDevice;
    private readonly RenderPrimitives _primitives;
    private RenderTarget2D? _target;
    private FieldDimensions? _field;
    private float _worldLeft;
    private float _worldTop;
    private float _worldWidth;
    private float _worldHeight;
    private float _scaleX;
    private float _scaleY;

    public PitchSurface(GraphicsDevice graphicsDevice, RenderPrimitives primitives)
    {
        _graphicsDevice = graphicsDevice;
        _primitives = primitives;
    }

    public void Prepare(FieldDimensions field)
    {
        if (_target is null || _target.IsDisposed || _field != field)
        {
            Build(field);
        }
    }

    public void Draw(WorldCamera camera, Rectangle viewport)
    {
        var texture = _target
            ?? throw new InvalidOperationException("The pitch surface must be prepared before drawing.");
        var sourceWidth = MathF.Max(0.0001f, camera.Width * _scaleX);
        var sourceHeight = MathF.Max(0.0001f, camera.Height * _scaleY);
        var scale = new Vector2(viewport.Width / sourceWidth, viewport.Height / sourceHeight);
        var cameraLeft = (camera.Left - _worldLeft) * _scaleX;
        var cameraTop = (camera.Top - _worldTop) * _scaleY;
        var position = new Vector2(
            viewport.Left - cameraLeft * scale.X,
            viewport.Top - cameraTop * scale.Y);

        // Drawing the full surface with a floating-point transform avoids quantizing camera motion
        // to the integer-only source rectangle used by SpriteBatch's destination overload.
        _primitives.Batch.Draw(
            texture,
            position,
            sourceRectangle: null,
            Color.White,
            rotation: 0f,
            origin: Vector2.Zero,
            scale,
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }

    public void Dispose()
    {
        _target?.Dispose();
    }

    private void Build(FieldDimensions field)
    {
        _field = field;
        _worldLeft = field.Left - WorldCamera.BoundaryMargin;
        _worldTop = field.Top - WorldCamera.BoundaryMargin;
        _worldWidth = field.Length + WorldCamera.BoundaryMargin * 2f;
        _worldHeight = field.Width + WorldCamera.BoundaryMargin * 2f;
        var surfaceHeight = Math.Max(1, (int)MathF.Round(SurfaceWidth * (_worldHeight / _worldWidth)));
        _scaleX = SurfaceWidth / _worldWidth;
        _scaleY = surfaceHeight / _worldHeight;

        _target?.Dispose();
        _target = new RenderTarget2D(
            _graphicsDevice,
            SurfaceWidth,
            surfaceHeight,
            mipMap: false,
            _graphicsDevice.PresentationParameters.BackBufferFormat,
            DepthFormat.None,
            preferredMultiSampleCount: 0,
            RenderTargetUsage.PreserveContents);

        _graphicsDevice.SetRenderTarget(_target);
        _graphicsDevice.Clear(Surround);
        _primitives.Batch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        DrawCrowd(field);
        FillWorldRectangle(
            field.Left - TrackWidth,
            field.Top - TrackWidth,
            field.Length + TrackWidth * 2f,
            field.Width + TrackWidth * 2f,
            Track);
        FillWorldRectangle(field.Left, field.Top, field.Length, field.Width, Grass);
        DrawPitchStripes(field);
        DrawGoalNets(field);
        DrawBenches(field);
        DrawFieldLines(field);

        _primitives.Batch.End();
        _graphicsDevice.SetRenderTarget(null);
    }

    private void DrawPitchStripes(FieldDimensions field)
    {
        const int stripeCount = 10;
        var stripeWidth = field.Length / stripeCount;
        for (var i = 0; i < stripeCount; i++)
        {
            if (i % 2 == 0)
            {
                continue;
            }

            FillWorldRectangle(
                field.Left + i * stripeWidth,
                field.Top,
                stripeWidth,
                field.Width,
                GrassStripe);
        }
    }

    private void DrawFieldLines(FieldDimensions field)
    {
        const float thickness = 0.16f;
        DrawWorldRectangle(field.Left, field.Top, field.Length, field.Width, Line, thickness);
        DrawWorldLine(new NumericsVector2(0f, field.Top), new NumericsVector2(0f, field.Bottom), Line, thickness);
        DrawWorldRing(NumericsVector2.Zero, CenterCircleRadius, Line, thickness);

        var penaltyTop = -field.PenaltyAreaWidth * 0.5f;
        DrawWorldRectangle(
            field.Left,
            penaltyTop,
            field.PenaltyAreaLength,
            field.PenaltyAreaWidth,
            Line,
            thickness);
        DrawWorldRectangle(
            field.Right - field.PenaltyAreaLength,
            penaltyTop,
            field.PenaltyAreaLength,
            field.PenaltyAreaWidth,
            Line,
            thickness);

        DrawWorldLine(
            new NumericsVector2(field.Left, field.GoalTop),
            new NumericsVector2(field.Left, field.GoalBottom),
            Line,
            thickness * 2.2f);
        DrawWorldLine(
            new NumericsVector2(field.Right, field.GoalTop),
            new NumericsVector2(field.Right, field.GoalBottom),
            Line,
            thickness * 2.2f);
    }

    private void DrawGoalNets(FieldDimensions field)
    {
        DrawGoalNet(field, leftGoal: true);
        DrawGoalNet(field, leftGoal: false);
    }

    private void DrawGoalNet(FieldDimensions field, bool leftGoal)
    {
        var frontX = leftGoal ? field.Left : field.Right;
        var backX = leftGoal ? frontX - GoalNetDepth : frontX + GoalNetDepth;
        var minX = MathF.Min(frontX, backX);
        FillWorldRectangle(minX, field.GoalTop, GoalNetDepth, field.GoalWidth, new Color(235, 235, 235, 32));
        DrawWorldLine(new NumericsVector2(backX, field.GoalTop), new NumericsVector2(backX, field.GoalBottom), Net, 0.1f);
        DrawWorldLine(new NumericsVector2(frontX, field.GoalTop), new NumericsVector2(backX, field.GoalTop), Net, 0.1f);
        DrawWorldLine(new NumericsVector2(frontX, field.GoalBottom), new NumericsVector2(backX, field.GoalBottom), Net, 0.1f);

        const int meshRows = 5;
        for (var i = 1; i < meshRows; i++)
        {
            var y = MathHelper.Lerp(field.GoalTop, field.GoalBottom, i / (float)meshRows);
            DrawWorldLine(new NumericsVector2(frontX, y), new NumericsVector2(backX, y), Net, 0.06f);
        }
    }

    private void DrawBenches(FieldDimensions field)
    {
        const float benchLength = 12f;
        const float benchDepth = 1.8f;
        const float offset = 2.2f;
        var x = -benchLength * 0.5f;
        var topY = field.Top - offset - benchDepth;
        var bottomY = field.Bottom + offset;

        FillWorldRectangle(x, topY, benchLength, benchDepth, Bench);
        FillWorldRectangle(x, topY, benchLength, 0.22f, BenchRoof);
        FillWorldRectangle(x, bottomY, benchLength, benchDepth, Bench);
        FillWorldRectangle(x, bottomY, benchLength, 0.22f, BenchRoof);
    }

    private void DrawCrowd(FieldDimensions field)
    {
        const float spacing = 1.05f;
        const float radius = 0.11f;
        for (var y = _worldTop + 0.5f; y <= field.Top - TrackWidth - 0.35f; y += spacing)
        {
            DrawCrowdRow(y, radius, spacing);
        }

        for (var y = field.Bottom + TrackWidth + 0.35f; y <= _worldTop + _worldHeight - 0.5f; y += spacing)
        {
            DrawCrowdRow(y, radius, spacing);
        }

        for (var y = field.Top; y <= field.Bottom; y += spacing)
        {
            for (var x = _worldLeft + 0.5f; x <= field.Left - TrackWidth - 0.35f; x += spacing)
            {
                DrawCrowdDot(x, y, radius);
            }

            for (var x = field.Right + TrackWidth + 0.35f; x <= _worldLeft + _worldWidth - 0.5f; x += spacing)
            {
                DrawCrowdDot(x, y, radius);
            }
        }
    }

    private void DrawCrowdRow(float y, float radius, float spacing)
    {
        for (var x = _worldLeft + 0.5f; x <= _worldLeft + _worldWidth - 0.5f; x += spacing)
        {
            DrawCrowdDot(x, y, radius);
        }
    }

    private void DrawCrowdDot(float x, float y, float radius)
    {
        var parity = ((int)MathF.Round((x - _worldLeft) * 3f + (y - _worldTop) * 5f)) & 3;
        var color = parity == 0 ? Line : parity == 1 ? CrowdLight : CrowdDark;
        var center = WorldToSurface(new NumericsVector2(x, y));
        _primitives.FillCircle(center, radius * AverageScale, color);
    }

    private void FillWorldRectangle(float x, float y, float width, float height, Color color)
    {
        var topLeft = WorldToSurface(new NumericsVector2(x, y));
        var bottomRight = WorldToSurface(new NumericsVector2(x + width, y + height));
        var rectangle = new Rectangle(
            (int)MathF.Floor(topLeft.X),
            (int)MathF.Floor(topLeft.Y),
            Math.Max(1, (int)MathF.Ceiling(bottomRight.X - topLeft.X)),
            Math.Max(1, (int)MathF.Ceiling(bottomRight.Y - topLeft.Y)));
        _primitives.FillRectangle(rectangle, color);
    }

    private void DrawWorldRectangle(float x, float y, float width, float height, Color color, float thickness)
    {
        var topLeft = new NumericsVector2(x, y);
        var topRight = new NumericsVector2(x + width, y);
        var bottomLeft = new NumericsVector2(x, y + height);
        var bottomRight = new NumericsVector2(x + width, y + height);
        DrawWorldLine(topLeft, topRight, color, thickness);
        DrawWorldLine(topRight, bottomRight, color, thickness);
        DrawWorldLine(bottomRight, bottomLeft, color, thickness);
        DrawWorldLine(bottomLeft, topLeft, color, thickness);
    }

    private void DrawWorldRing(NumericsVector2 center, float radius, Color color, float thickness)
    {
        const int segments = 96;
        var previous = center + new NumericsVector2(radius, 0f);
        for (var i = 1; i <= segments; i++)
        {
            var angle = MathHelper.TwoPi * i / segments;
            var current = center + new NumericsVector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            DrawWorldLine(previous, current, color, thickness);
            previous = current;
        }
    }

    private void DrawWorldLine(NumericsVector2 from, NumericsVector2 to, Color color, float thickness)
    {
        _primitives.DrawLine(
            WorldToSurface(from),
            WorldToSurface(to),
            color,
            MathF.Max(1f, thickness * AverageScale));
    }

    private Vector2 WorldToSurface(NumericsVector2 world)
    {
        return new Vector2(
            (world.X - _worldLeft) * _scaleX,
            (world.Y - _worldTop) * _scaleY);
    }

    private float AverageScale => (_scaleX + _scaleY) * 0.5f;
}
