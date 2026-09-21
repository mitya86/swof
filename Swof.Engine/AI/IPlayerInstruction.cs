namespace Swof.Engine.AI;

/// <summary>
/// Evaluates one independently registered instruction for a computer-controlled player.
/// </summary>
public interface IPlayerInstruction
{
    /// <summary>
    /// Returns an executable candidate when this instruction's required patterns and constraints are satisfied.
    /// </summary>
    /// <param name="context">The immutable decision context for one player.</param>
    /// <returns>An executable candidate, or <see langword="null"/> when the instruction is unavailable.</returns>
    PlayerInstructionCandidate? Evaluate(PlayerDecisionContext context);
}
