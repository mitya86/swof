namespace Swof.Engine;

/// <summary>
/// Stores goalkeeper-only skills separately from outfield football skills.
/// </summary>
public sealed record GoalkeeperAttributes(
    byte Handling,
    byte Reflexes,
    byte Positioning,
    byte Distribution)
{
    public static GoalkeeperAttributes Default { get; } = new(
        Handling: 10,
        Reflexes: 10,
        Positioning: 10,
        Distribution: 10);

    public float Handling01 => PlayerAttributes.Effective(Handling) / 10f;
    public float Reflexes01 => PlayerAttributes.Effective(Reflexes) / 10f;
    public float Positioning01 => PlayerAttributes.Effective(Positioning) / 10f;
    public float Distribution01 => PlayerAttributes.Effective(Distribution) / 10f;
}
