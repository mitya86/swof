using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Swof.Game;

internal sealed class RenderPrimitives : IDisposable
{
    private const int ShapeTextureSize = 64;
    private readonly GraphicsDevice _graphicsDevice;

    public RenderPrimitives(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        Batch = new SpriteBatch(graphicsDevice);
        Pixel = CreatePixelTexture();
        Circle = CreateCircleTexture(ring: false);
        Ring = CreateCircleTexture(ring: true);
    }

    public SpriteBatch Batch { get; }
    public Texture2D Pixel { get; }
    public Texture2D Circle { get; }
    public Texture2D Ring { get; }

    public void FillRectangle(Rectangle rectangle, Color color)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        Batch.Draw(Pixel, rectangle, color);
    }

    public void DrawLine(Vector2 from, Vector2 to, Color color, float thickness)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length <= 0.001f || thickness <= 0f)
        {
            return;
        }

        Batch.Draw(
            Pixel,
            from,
            sourceRectangle: null,
            color,
            rotation: MathF.Atan2(delta.Y, delta.X),
            origin: new Vector2(0f, 0.5f),
            scale: new Vector2(length, thickness),
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }

    public void FillCircle(Vector2 center, float radius, Color color)
    {
        DrawShape(Circle, center, radius, radius, color);
    }

    public void FillEllipse(Vector2 center, float radiusX, float radiusY, Color color)
    {
        DrawShape(Circle, center, radiusX, radiusY, color);
    }

    public void DrawRing(Vector2 center, float radius, Color color)
    {
        DrawShape(Ring, center, radius, radius, color);
    }

    public void Dispose()
    {
        Ring.Dispose();
        Circle.Dispose();
        Pixel.Dispose();
        Batch.Dispose();
    }

    private void DrawShape(Texture2D texture, Vector2 center, float radiusX, float radiusY, Color color)
    {
        if (radiusX <= 0f || radiusY <= 0f)
        {
            return;
        }

        Batch.Draw(
            texture,
            center,
            sourceRectangle: null,
            color,
            rotation: 0f,
            origin: new Vector2(ShapeTextureSize * 0.5f),
            scale: new Vector2(radiusX * 2f / ShapeTextureSize, radiusY * 2f / ShapeTextureSize),
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }

    private Texture2D CreatePixelTexture()
    {
        var texture = new Texture2D(_graphicsDevice, 1, 1);
        texture.SetData(new[] { Color.White });
        return texture;
    }

    private Texture2D CreateCircleTexture(bool ring)
    {
        var texture = new Texture2D(_graphicsDevice, ShapeTextureSize, ShapeTextureSize);
        var pixels = new Color[ShapeTextureSize * ShapeTextureSize];
        var center = (ShapeTextureSize - 1) * 0.5f;
        var outerRadius = ShapeTextureSize * 0.5f - 1f;
        var innerRadius = outerRadius - 3f;

        for (var y = 0; y < ShapeTextureSize; y++)
        {
            for (var x = 0; x < ShapeTextureSize; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distanceSquared = dx * dx + dy * dy;
                var insideOuter = distanceSquared <= outerRadius * outerRadius;
                var insideInner = distanceSquared < innerRadius * innerRadius;
                var visible = ring ? insideOuter && !insideInner : insideOuter;
                pixels[y * ShapeTextureSize + x] = visible ? Color.White : Color.Transparent;
            }
        }

        texture.SetData(pixels);
        return texture;
    }
}
