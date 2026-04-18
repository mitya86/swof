using System.Numerics;

namespace Swof.Engine;

public readonly record struct Azimuth(float Degrees)
{
    public static Azimuth North { get; } = new(0f);
    public static Azimuth East { get; } = new(90f);
    public static Azimuth South { get; } = new(180f);
    public static Azimuth West { get; } = new(270f);

    public float NormalizedDegrees => NormalizeDegrees(Degrees);

    public Vector2 ToUnitVector()
    {
        var radians = NormalizedDegrees * MathF.PI / 180f;
        return new Vector2(MathF.Sin(radians), -MathF.Cos(radians));
    }

    public Azimuth Quantized(int sectors)
    {
        if (sectors <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sectors), "Sector count must be positive.");
        }

        var sectorSize = 360f / sectors;
        var sector = MathF.Round(NormalizedDegrees / sectorSize) % sectors;
        return new Azimuth(sector * sectorSize);
    }

    public static Azimuth FromVector(Vector2 direction)
    {
        if (direction.LengthSquared() <= 0.0001f)
        {
            return North;
        }

        var normalized = Vector2.Normalize(direction);
        var radians = MathF.Atan2(normalized.X, -normalized.Y);
        return new Azimuth(radians * 180f / MathF.PI);
    }

    private static float NormalizeDegrees(float degrees)
    {
        var normalized = degrees % 360f;
        return normalized < 0f ? normalized + 360f : normalized;
    }
}
