using System.Collections.ObjectModel;

namespace Swof.Engine.AI;

/// <summary>
/// Resolves independent computer-player decisions into a per-tick control frame.
/// </summary>
public sealed class ComputerPlayerController
{
    private readonly IReadOnlyDictionary<int, ComputerPlayerBrain> _brains;
    private readonly Dictionary<TeamSide, TacticalPriorities> _teamPriorities = new();
    private IReadOnlyDictionary<int, PlayerDecision> _lastDecisions =
        new ReadOnlyDictionary<int, PlayerDecision>(new Dictionary<int, PlayerDecision>());

    /// <summary>
    /// Creates a controller from one independently owned brain per computer-capable player.
    /// </summary>
    /// <param name="brains">Brains keyed by their player identifiers.</param>
    /// <exception cref="ArgumentException">A player identifier is duplicated or a brain is null.</exception>
    public ComputerPlayerController(IEnumerable<KeyValuePair<int, ComputerPlayerBrain>> brains)
    {
        ArgumentNullException.ThrowIfNull(brains);

        var copy = new SortedDictionary<int, ComputerPlayerBrain>();
        foreach (var (playerId, brain) in brains)
        {
            if (brain is null)
            {
                throw new ArgumentException($"Player {playerId} has a null brain.", nameof(brains));
            }

            if (!copy.TryAdd(playerId, brain))
            {
                throw new ArgumentException($"Player {playerId} has more than one brain.", nameof(brains));
            }
        }

        _brains = new ReadOnlyDictionary<int, ComputerPlayerBrain>(copy);
    }

    /// <summary>Gets the most recent independently selected decisions, keyed by computer player.</summary>
    public IReadOnlyDictionary<int, PlayerDecision> LastDecisions => _lastDecisions;

    /// <summary>
    /// Creates the default outfield-player roster for a match snapshot.
    /// </summary>
    /// <param name="match">The snapshot whose outfield players receive individual brains.</param>
    /// <returns>A controller ready to fill any outfield slots not assigned to humans.</returns>
    public static ComputerPlayerController CreateDefault(MatchSnapshot match)
    {
        ArgumentNullException.ThrowIfNull(match);

        return Create(match, _ => true);
    }

    /// <summary>
    /// Creates individual brains only for the outfield players on one team.
    /// </summary>
    /// <param name="match">The snapshot whose selected outfield players receive individual brains.</param>
    /// <param name="team">The only team whose outfield players should be controlled.</param>
    /// <returns>A controller that leaves every other team's slots unassigned.</returns>
    public static ComputerPlayerController CreateForTeam(MatchSnapshot match, TeamSide team)
    {
        ArgumentNullException.ThrowIfNull(match);

        return Create(match, player => player.Team == team);
    }

    private static ComputerPlayerController Create(
        MatchSnapshot match,
        Func<PlayerSnapshot, bool> isEligible)
    {
        var brains = match.Players
            .Where(player => player.Role == PlayerRole.Outfield && isEligible(player))
            .OrderBy(player => player.Id)
            .Select(player => new KeyValuePair<int, ComputerPlayerBrain>(
                player.Id,
                ComputerPlayerBrain.CreateDefault()));
        return new ComputerPlayerController(brains);
    }

    /// <summary>
    /// Changes the tactical priorities used by every computer player on one team.
    /// </summary>
    /// <param name="team">The affected team.</param>
    /// <param name="priorities">The priorities supplied by that team's coach.</param>
    public void SetTacticalPriorities(TeamSide team, TacticalPriorities priorities)
    {
        ArgumentNullException.ThrowIfNull(priorities);
        _teamPriorities[team] = priorities;
    }

    /// <summary>
    /// Preserves assigned inputs and fills every remaining registered outfield slot with an independent AI decision.
    /// </summary>
    /// <param name="match">The common immutable pre-tick snapshot.</param>
    /// <param name="assignedInputs">Inputs already assigned to human or remote controllers.</param>
    /// <returns>A complete deterministic control frame for registered outfield players and assigned controllers.</returns>
    public PlayerControlFrame CreateControlFrame(
        MatchSnapshot match,
        IReadOnlyDictionary<int, PlayerInput> assignedInputs)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(assignedInputs);

        var playersById = match.Players.ToDictionary(player => player.Id);
        var inputs = new Dictionary<int, PlayerInput>(assignedInputs);
        var decisions = new SortedDictionary<int, PlayerDecision>();

        foreach (var (playerId, brain) in _brains)
        {
            if (inputs.ContainsKey(playerId))
            {
                continue;
            }

            if (!playersById.TryGetValue(playerId, out var player)
                || player.Role != PlayerRole.Outfield)
            {
                continue;
            }

            var priorities = _teamPriorities.TryGetValue(player.Team, out var configuredPriorities)
                ? configuredPriorities
                : TacticalPriorities.Neutral;
            var decision = brain.Decide(new PlayerDecisionContext(match, playerId), priorities);
            decisions.Add(playerId, decision);
            inputs.Add(playerId, decision.Input);
        }

        _lastDecisions = new ReadOnlyDictionary<int, PlayerDecision>(decisions);
        return new PlayerControlFrame(inputs);
    }
}
