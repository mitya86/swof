using System.Numerics;

namespace Swof.Engine;

/// <summary>
/// Decides whether a slide that reaches the ball carrier is a foul. The engine and the computer
/// players both ask this, so a computer never slides into what the engine would punish.
/// </summary>
/// <param name="BehindDot">
/// How closely the slide must follow the way the carrier faces to count as from behind
/// (1 is exactly behind). Any value above 1 switches the rule off.
/// </param>
/// <param name="HitDistance">How far beyond the two radii a slide reaches the ball.</param>
public sealed record SlideFoulRule(float BehindDot, float HitDistance)
{
    public static SlideFoulRule Off { get; } = new(2f, 0f);

    /// <summary>True when the slide comes from behind the carrier and the ball is out of its reach.</summary>
    public bool IsFoul(
        Vector2 slideDirection,
        Vector2 sliderPosition,
        float sliderRadius,
        Vector2 carrierFacing,
        Vector2 ballPosition,
        float ballRadius)
    {
        return Vector2.Dot(slideDirection, carrierFacing) > BehindDot
            && Vector2.Distance(sliderPosition, ballPosition) > sliderRadius + ballRadius + HitDistance;
    }
}
