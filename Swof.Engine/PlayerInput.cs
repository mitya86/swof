using System.Numerics;

namespace Swof.Engine;

public readonly record struct PlayerInput(Vector2 Move, bool ActionDown)
{
    public static PlayerInput Empty { get; } = new(Vector2.Zero, ActionDown: false);

    public Vector2 NormalizedMove
    {
        get
        {
            if (Move.LengthSquared() <= 0.0001f)
            {
                return Vector2.Zero;
            }

            return Vector2.Normalize(Move);
        }
    }
}
