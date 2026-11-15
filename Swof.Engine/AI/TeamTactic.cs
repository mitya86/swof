namespace Swof.Engine.AI;

/// <summary>How one team's computer players are told to play. Data only: the players read it from their context.</summary>
/// <param name="Name">The name shown to people.</param>
/// <param name="PressLineMetres">
/// How far from its own goal line the team goes to meet an opposing carrier. Beyond it the presser and its cover wait
/// on the line instead of following the carrier.
/// </param>
public sealed record TeamTactic(string Name, float PressLineMetres)
{
    /// <summary>Press the carrier anywhere on the court.</summary>
    public static TeamTactic HighPress { get; } = new("high press", float.PositiveInfinity);

    /// <summary>Wait at the halfway line and press only in the team's own half.</summary>
    public static TeamTactic HalfCourt { get; } = new("half court", 20f);
}
