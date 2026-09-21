namespace Swof.Engine.AI;

/// <summary>
/// Recognizes one reusable match pattern from an individual player's decision context.
/// </summary>
/// <typeparam name="TMatch">The immutable information produced when the pattern is recognized.</typeparam>
public interface IPlayerPattern<TMatch>
    where TMatch : struct
{
    /// <summary>
    /// Attempts to recognize the pattern for one player.
    /// </summary>
    /// <param name="context">The immutable decision context.</param>
    /// <param name="match">The recognized pattern data when successful.</param>
    /// <returns><see langword="true"/> when the pattern is present and available to this player.</returns>
    bool TryRecognize(PlayerDecisionContext context, out TMatch match);
}
