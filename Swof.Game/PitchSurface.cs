using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Swof.Engine;
using NumericsVector2 = System.Numerics.Vector2;

namespace Swof.Game;

internal sealed class PitchSurface : IDisposable
{
    private const int SurfaceWidth = 2048;
    private const float CourtApronWidth = 0.7f;

    private static readonly Color Court = new(35, 123, 157);
    private static readonly Color CourtPanel = new(32, 116, 149);
    private static readonly Color CourtApron = new(24, 77, 98);
    private static readonly Color Surround = new(17, 25, 32);
    private static readonly Color Line = new(242, 242, 242);
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

        FillWorldRectangle(
            field.Left - CourtApronWidth,
            field.Top - CourtApronWidth,
            field.Length + CourtApronWidth * 2f,
            field.Width + CourtApronWidth * 2f,
            CourtApron);
        FillWorldRectangle(field.Left, field.Top, field.Length, field.Width, Court);
        DrawCourtPanels(field);
        DrawGoalNets(field);
        DrawBenches(field);
        DrawFieldLines(field);

        _primitives.Batch.End();
        _graphicsDevice.SetRenderTarget(null);
    }

    private void DrawCourtPanels(FieldDimensions field)
    {
        const int panelCount = 8;
        var panelWidth = field.Length / panelCount;
        for (var i = 0; i < panelCount; i++)
        {
            if (i % 2 == 0)
            {
                continue;
            }

            FillWorldRectangle(
                field.Left + i * panelWidth,
                field.Top,
                panelWidth,
                field.Width,
                CourtPanel);
        }
    }

    private void DrawFieldLines(FieldDimensions field)
    {
        const float thickness = 0.08f;
        DrawWorldRectangle(field.Left, field.Top, field.Length, field.Width, Line, thickness);
        DrawWorldLine(new NumericsVector2(0f, field.Top), new NumericsVector2(0f, field.Bottom), Line, thickness);
        DrawWorldRing(NumericsVector2.Zero, field.CenterCircleRadius, Line, thickness);
        DrawWorldSpot(NumericsVector2.Zero, 0.09f, Line);
        DrawPenaltyArea(field, leftSide: true, thickness);
        DrawPenaltyArea(field, leftSide: false, thickness);
        DrawPenaltyMarks(field);
        DrawCornerArcs(field, thickness);
        DrawSubstitutionZoneMarks(field, thickness);

        DrawWorldLine(
            new NumericsVector2(field.Left, field.GoalTop),
            new NumericsVector2(field.Left, field.GoalBottom),
            Line,
            thickness * 2f);
        DrawWorldLine(
            new NumericsVector2(field.Right, field.GoalTop),
            new NumericsVector2(field.Right, field.GoalBottom),
            Line,
            thickness * 2f);
    }

    private void DrawPenaltyArea(FieldDimensions field, bool leftSide, float thickness)
    {
        var goalX = leftSide ? field.Left : field.Right;
        var insideX = leftSide
            ? goalX + field.PenaltyAreaRadius
            : goalX - field.PenaltyAreaRadius;
        var topPost = new NumericsVector2(goalX, field.GoalTop);
        var bottomPost = new NumericsVector2(goalX, field.GoalBottom);

        if (leftSide)
        {
            DrawWorldArc(topPost, field.PenaltyAreaRadius, -MathHelper.PiOver2, 0f, Line, thickness);
            DrawWorldArc(bottomPost, field.PenaltyAreaRadius, 0f, MathHelper.PiOver2, Line, thickness);
        }
        else
        {
            DrawWorldArc(topPost, field.PenaltyAreaRadius, MathHelper.Pi, MathHelper.Pi * 1.5f, Line, thickness);
            DrawWorldArc(bottomPost, field.PenaltyAreaRadius, MathHelper.PiOver2, MathHelper.Pi, Line, thickness);
        }

        DrawWorldLine(
            new NumericsVector2(insideX, field.GoalTop),
            new NumericsVector2(insideX, field.GoalBottom),
            Line,
            thickness);
    }

    private void DrawPenaltyMarks(FieldDimensions field)
    {
        foreach (var side in new[] { -1f, 1f })
        {
            var goalX = side < 0f ? field.Left : field.Right;
            var inward = side < 0f ? 1f : -1f;
            DrawWorldSpot(
                new NumericsVector2(goalX + inward * field.PenaltyMarkDistance, 0f),
                0.09f,
                Line);
            var secondMark = new NumericsVector2(
                goalX + inward * field.SecondPenaltyMarkDistance,
                0f);
            DrawWorldSpot(secondMark, 0.09f, Line);
            DrawWorldSpot(secondMark + new NumericsVector2(0f, -5f), 0.07f, Line);
            DrawWorldSpot(secondMark + new NumericsVector2(0f, 5f), 0.07f, Line);
        }
    }

    private void DrawCornerArcs(FieldDimensions field, float thickness)
    {
        DrawWorldArc(
            new NumericsVector2(field.Left, field.Top),
            field.CornerArcRadius,
            0f,
            MathHelper.PiOver2,
            Line,
            thickness);
        DrawWorldArc(
            new NumericsVector2(field.Right, field.Top),
            field.CornerArcRadius,
            MathHelper.PiOver2,
            MathHelper.Pi,
            Line,
            thickness);
        DrawWorldArc(
            new NumericsVector2(field.Right, field.Bottom),
            field.CornerArcRadius,
            MathHelper.Pi,
            MathHelper.Pi * 1.5f,
            Line,
            thickness);
        DrawWorldArc(
            new NumericsVector2(field.Left, field.Bottom),
            field.CornerArcRadius,
            MathHelper.Pi * 1.5f,
            MathHelper.TwoPi,
            Line,
            thickness);
    }

    private void DrawSubstitutionZoneMarks(FieldDimensions field, float thickness)
    {
        var zoneEnds = new[] { -10f, -5f, 5f, 10f };
        foreach (var x in zoneEnds)
        {
            DrawWorldLine(
                new NumericsVector2(x, field.Top - 0.4f),
                new NumericsVector2(x, field.Top + 0.4f),
                Line,
                thickness);
        }
    }

    private void DrawGoalNets(FieldDimensions field)
    {
        DrawGoalNet(field, leftGoal: true);
        DrawGoalNet(field, leftGoal: false);
    }

    private void DrawGoalNet(FieldDimensions field, bool leftGoal)
    {
        var frontX = leftGoal ? field.Left : field.Right;
        var backX = leftGoal ? frontX - field.GoalDepth : frontX + field.GoalDepth;
        var minX = MathF.Min(frontX, backX);
        FillWorldRectangle(minX, field.GoalTop, field.GoalDepth, field.GoalWidth, new Color(235, 235, 235, 32));
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
        const float benchLength = 5f;
        const float benchDepth = 1f;
        const float offset = 1.2f;
        var topY = field.Top - offset - benchDepth;
        DrawBench(-10f, topY, benchLength, benchDepth);
        DrawBench(5f, topY, benchLength, benchDepth);
    }

    private void DrawBench(float x, float y, float width, float height)
    {
        FillWorldRectangle(x, y, width, height, Bench);
        FillWorldRectangle(x, y, width, 0.16f, BenchRoof);
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

    private void DrawWorldArc(
        NumericsVector2 center,
        float radius,
        float startAngle,
        float endAngle,
        Color color,
        float thickness)
    {
        const int segments = 32;
        var previous = center + new NumericsVector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * radius;
        for (var i = 1; i <= segments; i++)
        {
            var amount = i / (float)segments;
            var angle = MathHelper.Lerp(startAngle, endAngle, amount);
            var current = center + new NumericsVector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            DrawWorldLine(previous, current, color, thickness);
            previous = current;
        }
    }

    private void DrawWorldSpot(NumericsVector2 center, float radius, Color color)
    {
        _primitives.FillCircle(WorldToSurface(center), radius * AverageScale, color);
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
