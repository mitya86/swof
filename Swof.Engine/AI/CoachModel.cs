namespace Swof.Engine.AI;

/// <summary>
/// A coach as a small model of his own, between matches: what kind of person he is, how well he reads his players,
/// the style he asks of them, and which tactic has worked for him. In a match the team still plays through
/// <see cref="Coach"/> and the tactic this model chose.
/// </summary>
public sealed class CoachModel
{
    /// <summary>The tactics a coach chooses between, in the order he first tries them.</summary>
    public static IReadOnlyList<TeamTactic> Tactics { get; } = [TeamTactic.HighPress, TeamTactic.HalfCourt, TeamTactic.PushUp];

    // A slot's merit from a player's speed, ball control and tackling: the only attributes the engine reads.
    private static readonly (int Speed, int BallControl, int Tackling)[] SlotWeights =
    [
        (0, 1, 2), // fixo: wins the ball and keeps it at the back
        (1, 1, 0), // left ala
        (1, 1, 0), // right ala
        (2, 1, 0)  // pivo: gets in behind
    ];

    private readonly Dictionary<TeamTactic, (int Matches, int GoalDifference)> _results = new();

    /// <summary>Creates a coach.</summary>
    /// <param name="insight">See <see cref="Insight"/>.</param>
    /// <param name="temperament">See <see cref="Temperament"/>.</param>
    /// <param name="asks">See <see cref="Asks"/>; neutral when omitted.</param>
    /// <exception cref="ArgumentOutOfRangeException">A trait is not from 0 to 1.</exception>
    public CoachModel(float insight = 0.5f, float temperament = 0.5f, PlayerHabits? asks = null)
    {
        Insight = InRange(insight, nameof(insight));
        Temperament = InRange(temperament, nameof(temperament));
        Asks = asks ?? PlayerHabits.Neutral;
    }

    /// <summary>
    /// How well he reads his players, from 0 to 1. At a half or less he keeps them where they are; above it he
    /// moves them toward the positions their attributes suit, all the way at 1.
    /// </summary>
    public float Insight { get; }

    /// <summary>What kind of person he is; it counts in his players' fit like a team-mate's.</summary>
    public float Temperament { get; }

    /// <summary>The habits he asks of his players. A disciplined player who fits takes them up.</summary>
    public PlayerHabits Asks { get; }

    /// <summary>How many matches he gives each tactic before he judges them, because one match is mostly luck.</summary>
    public const int TrialsPerTactic = 3;

    /// <summary>
    /// The tactic he starts the next match with: one he has not given its trials yet, in the order of
    /// <see cref="Tactics"/>, and after that the one with the best goal difference per match so far.
    /// </summary>
    /// <remarks>
    /// ponytail: after the trials he never tries a tactic that is behind again, so a tactic unlucky in its trials
    /// stays out; give a losing coach a fresh trial when careers show that happening.
    /// </remarks>
    public TeamTactic UsualTactic =>
        Tactics.FirstOrDefault(tactic => _results.GetValueOrDefault(tactic).Matches < TrialsPerTactic)
        ?? Tactics.MaxBy(tactic => _results[tactic].GoalDifference / (float)_results[tactic].Matches)!;

    /// <summary>How many matches he has started with a tactic, and his team's goal difference over them.</summary>
    public (int Matches, int GoalDifference) ResultsWith(TeamTactic tactic) => _results.GetValueOrDefault(tactic);

    /// <summary>Remembers how a match went with the tactic he started it with.</summary>
    /// <param name="tactic">The tactic.</param>
    /// <param name="goalDifference">His team's goals minus the other team's.</param>
    public void Record(TeamTactic tactic, int goalDifference)
    {
        ArgumentNullException.ThrowIfNull(tactic);
        var (matches, difference) = _results.GetValueOrDefault(tactic);
        _results[tactic] = (matches + 1, difference + goalDifference);
    }

    /// <summary>
    /// Where he puts four outfielders: for each slot (fixo, left ala, right ala, pivo) the index of the player he
    /// puts there. He weighs how well each player's attributes suit a slot against leaving players where they are,
    /// by his insight.
    /// </summary>
    /// <param name="players">The four outfielders, in the slots they hold now.</param>
    public IReadOnlyList<int> Arrange(IReadOnlyList<PlayerAttributes> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (players.Count != SlotWeights.Length)
        {
            throw new ArgumentException($"A coach arranges {SlotWeights.Length} outfielders.", nameof(players));
        }

        var meritWeight = MathF.Max(0f, 2f * Insight - 1f);
        int[] best = [0, 1, 2, 3];
        var bestScore = float.NegativeInfinity;
        foreach (var order in Orders([0, 1, 2, 3]))
        {
            var merit = 0f;
            var kept = 0f;
            for (var slot = 0; slot < order.Length; slot++)
            {
                var (speed, ballControl, tackling) = SlotWeights[slot];
                var player = players[order[slot]];
                merit += (speed * player.Speed + ballControl * player.BallControl + tackling * player.Tackling) / (15f * (speed + ballControl + tackling));
                kept += order[slot] == slot ? 1f : 0f;
            }

            var score = meritWeight * merit + (1f - meritWeight) * kept;
            if (score > bestScore + 0.0001f)
            {
                bestScore = score;
                best = order;
            }
        }

        return best;
    }

    private static IEnumerable<int[]> Orders(int[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (var first = 0; first < items.Length; first++)
        {
            foreach (var rest in Orders([.. items.Where((_, index) => index != first)]))
            {
                yield return [items[first], .. rest];
            }
        }
    }

    private static float InRange(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f || value > 1f)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be from 0 to 1.");
        }

        return value;
    }
}
