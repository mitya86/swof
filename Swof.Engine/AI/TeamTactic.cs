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

/// <summary>
/// A rule that swaps a team's tactic by the score and the time left. It reads only the match snapshot, so a seed
/// still replays the same match. It is not a coach: it observes nothing but the scoreboard and acts at once.
/// </summary>
/// <param name="MatchLengthSeconds">Both halves together, which the snapshot does not carry.</param>
/// <param name="WhenBehind">The tactic while the team is losing, or null to keep its usual one.</param>
/// <param name="WhenAheadLate">The tactic while the team is winning late in the match, or null to keep its usual one.</param>
/// <param name="LateFraction">How much of the match, counted back from full time, is late.</param>
public sealed record TacticByScore(
    float MatchLengthSeconds,
    TeamTactic? WhenBehind = null,
    TeamTactic? WhenAheadLate = null,
    float LateFraction = 0.25f)
{
    /// <summary>Push up while losing, wait at halfway while winning in the last quarter.</summary>
    public static TacticByScore ChaseAndHold(float matchLengthSeconds) =>
        new(matchLengthSeconds, TeamTactic.PushUp, TeamTactic.HalfCourt);

    /// <summary>The tactic the team should play now.</summary>
    public TeamTactic Choose(MatchSnapshot match, TeamSide team, TeamTactic usual)
    {
        ArgumentNullException.ThrowIfNull(match);
        var lead = team == TeamSide.Home ? match.HomeScore - match.AwayScore : match.AwayScore - match.HomeScore;
        var isLate = match.ClockSeconds >= MatchLengthSeconds * (1f - LateFraction);
        return lead < 0 ? WhenBehind ?? usual
            : lead > 0 && isLate ? WhenAheadLate ?? usual
            : usual;
    }
}
