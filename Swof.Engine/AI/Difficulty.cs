namespace Swof.Engine.AI;

/// <summary>
/// How hard the computer opponent is. Data only. A harder opponent plays a stronger tactic and has a coach; an
/// easier one is slower on its feet. No level gives the computer better players than the person's.
/// </summary>
/// <param name="Name">The name shown to people.</param>
/// <param name="OpponentSpeedChange">Added to the speed attribute of each opposing outfielder. Never above 0.</param>
/// <param name="OpponentTactic">The opponent's usual tactic.</param>
/// <param name="OpponentCoach">Whether the opponent has a coach that changes its tactic by the score.</param>
public sealed record Difficulty(string Name, int OpponentSpeedChange, TeamTactic OpponentTactic, bool OpponentCoach)
{
    public static Difficulty Easy { get; } = new("easy", -4, TeamTactic.HighPress, OpponentCoach: false);

    public static Difficulty Normal { get; } = new("normal", 0, TeamTactic.HighPress, OpponentCoach: false);

    public static Difficulty Hard { get; } = new("hard", 0, TeamTactic.PushUp, OpponentCoach: true);

    public static IReadOnlyList<Difficulty> All { get; } = [Easy, Normal, Hard];

    /// <summary>The eight outfielders of <see cref="MatchEngine.CreateDefault"/> with the opponent's four changed.</summary>
    public IReadOnlyList<PlayerAttributes> Squad(IReadOnlyList<PlayerAttributes> squad, TeamSide opponent)
    {
        ArgumentNullException.ThrowIfNull(squad);
        var first = opponent == TeamSide.Home ? 0 : 4;
        return squad
            .Select((player, index) => index >= first && index < first + 4
                ? player with { Speed = (byte)Math.Clamp(player.Speed + OpponentSpeedChange, 0, 15) }
                : player)
            .ToArray();
    }

    /// <summary>Gives the opponent its tactic and, at the levels that have one, its coach.</summary>
    public void Brief(ComputerPlayerController computers, TeamSide opponent, float matchLengthSeconds)
    {
        ArgumentNullException.ThrowIfNull(computers);
        computers.SetTactic(opponent, OpponentTactic);
        if (OpponentCoach)
        {
            computers.SetCoach(opponent, new Coach(opponent, TacticByScore.ChaseAndHold(matchLengthSeconds)));
        }
    }
}
