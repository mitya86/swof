namespace Swof.Engine.AI;

/// <summary>How one team's computer players are told to play. Data only: the players read it from their context.</summary>
/// <param name="Name">The name shown to people.</param>
/// <param name="PressLineMetres">
/// How far from its own goal line the team goes to meet an opposing carrier. Beyond it the presser and its cover wait
/// on the line instead of following the carrier.
/// </param>
/// <param name="MarkersUpMetres">
/// How much further from their own goal the players who mark (everyone but the presser and its cover) stand and may
/// go. At 0 they stay within 9 to 15 metres of their own goal line, whatever the press does.
/// </param>
public sealed record TeamTactic(string Name, float PressLineMetres, float MarkersUpMetres = 0f)
{
    /// <summary>Press the carrier anywhere on the court while the markers stay near their own goal.</summary>
    public static TeamTactic HighPress { get; } = new("high press", float.PositiveInfinity);

    /// <summary>Wait at the halfway line and press only in the team's own half.</summary>
    public static TeamTactic HalfCourt { get; } = new("half court", 20f);

    /// <summary>Press the carrier anywhere, with the markers six metres further up the court behind the press.</summary>
    public static TeamTactic PushUp { get; } = new("push up", float.PositiveInfinity, 6f);
}
