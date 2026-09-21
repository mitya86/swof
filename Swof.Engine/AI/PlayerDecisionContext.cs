namespace Swof.Engine.AI;

/// <summary>
/// Provides the immutable match view from which one computer-controlled player makes a decision.
/// </summary>
public sealed class PlayerDecisionContext
{
    /// <summary>
    /// Creates a decision context for one player in a match snapshot.
    /// </summary>
    /// <param name="match">The immutable authoritative snapshot visible to the decision layer.</param>
    /// <param name="playerId">The player whose independent instructions will be evaluated.</param>
    /// <exception cref="InvalidOperationException">The player is not present in the snapshot.</exception>
    public PlayerDecisionContext(MatchSnapshot match, int playerId)
    {
        ArgumentNullException.ThrowIfNull(match);

        Match = match;
        Player = match.Players.FirstOrDefault(player => player.Id == playerId)
            ?? throw new InvalidOperationException($"Player {playerId} is not present in the match snapshot.");
    }

    /// <summary>Gets the immutable match snapshot used for this decision.</summary>
    public MatchSnapshot Match { get; }

    /// <summary>Gets the player making this decision.</summary>
    public PlayerSnapshot Player { get; }
}
