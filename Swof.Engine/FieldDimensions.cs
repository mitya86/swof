namespace Swof.Engine;

public sealed record FieldDimensions(
    float Length,
    float Width,
    float GoalWidth,
    float PenaltyAreaLength,
    float PenaltyAreaWidth)
{
    public static FieldDimensions Default { get; } = new(
        Length: 105f,
        Width: 68f,
        GoalWidth: 7.32f,
        PenaltyAreaLength: 16.5f,
        PenaltyAreaWidth: 40.32f);

    public float Left => -Length / 2f;
    public float Right => Length / 2f;
    public float Top => -Width / 2f;
    public float Bottom => Width / 2f;
    public float GoalTop => -GoalWidth / 2f;
    public float GoalBottom => GoalWidth / 2f;
}
