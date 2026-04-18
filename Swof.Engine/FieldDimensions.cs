namespace Swof.Engine;

public sealed record FieldDimensions(
    float Length,
    float Width,
    float GoalWidth,
    float PenaltyAreaLength,
    float PenaltyAreaWidth)
{
    public static FieldDimensions Default { get; } = new(
        Length: 100f,
        Width: 64f,
        GoalWidth: 14f,
        PenaltyAreaLength: 16.5f,
        PenaltyAreaWidth: 40f);

    public float Left => -Length / 2f;
    public float Right => Length / 2f;
    public float Top => -Width / 2f;
    public float Bottom => Width / 2f;
    public float GoalTop => -GoalWidth / 2f;
    public float GoalBottom => GoalWidth / 2f;
}
