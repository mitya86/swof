using System.Numerics;

namespace Swof.Engine;

/// <summary>
/// Describes the futsal court and its law-defined markings in metres.
/// </summary>
public sealed record FieldDimensions(
    float Length,
    float Width,
    float GoalWidth,
    float PenaltyAreaRadius,
    float CenterCircleRadius,
    float PenaltyMarkDistance,
    float SecondPenaltyMarkDistance,
    float CornerArcRadius,
    float GoalDepth)
{
    /// <summary>Gets the international-sized 40m by 20m futsal court used by default.</summary>
    public static FieldDimensions Futsal { get; } = new(
        Length: 40f,
        Width: 20f,
        GoalWidth: 3f,
        PenaltyAreaRadius: 6f,
        CenterCircleRadius: 3f,
        PenaltyMarkDistance: 6f,
        SecondPenaltyMarkDistance: 10f,
        CornerArcRadius: 0.25f,
        GoalDepth: 1f);

    /// <summary>Gets the foundational court used by new matches.</summary>
    public static FieldDimensions Default => Futsal;

    public float Left => -Length / 2f;
    public float Right => Length / 2f;
    public float Top => -Width / 2f;
    public float Bottom => Width / 2f;
    public float GoalTop => -GoalWidth / 2f;
    public float GoalBottom => GoalWidth / 2f;

    /// <summary>
    /// Determines whether a position is inside a team's D-shaped futsal penalty area.
    /// </summary>
    /// <param name="team">The team defending the area.</param>
    /// <param name="position">The world position to test.</param>
    /// <returns><see langword="true"/> when the position is inside or on the penalty-area boundary.</returns>
    public bool ContainsInPenaltyArea(TeamSide team, Vector2 position)
    {
        var depth = team == TeamSide.Home
            ? position.X - Left
            : Right - position.X;
        if (depth < 0f || depth > PenaltyAreaRadius)
        {
            return false;
        }

        var distancePastNearestPost = MathF.Max(0f, MathF.Abs(position.Y) - GoalWidth * 0.5f);
        return depth * depth + distancePastNearestPost * distancePastNearestPost
            <= PenaltyAreaRadius * PenaltyAreaRadius;
    }
}
