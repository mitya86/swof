namespace Swof.Engine.AI;

/// <summary>
/// Describes one executable instruction before tactical priority modifiers are applied.
/// </summary>
/// <param name="Kind">The stable instruction identity.</param>
/// <param name="PriorityBand">The instruction's urgency band.</param>
/// <param name="SituationalScore">The instruction's score for the current perceived situation.</param>
/// <param name="Input">The normal player input that executes the instruction.</param>
public sealed record PlayerInstructionCandidate(
    PlayerInstructionKind Kind,
    PlayerInstructionPriorityBand PriorityBand,
    float SituationalScore,
    PlayerInput Input);

/// <summary>
/// Records the instruction selected by a computer-controlled player and why it won.
/// </summary>
/// <param name="Kind">The selected instruction, or <see cref="PlayerInstructionKind.None"/>.</param>
/// <param name="PriorityBand">The selected urgency band.</param>
/// <param name="SituationalScore">The instruction's context-derived score.</param>
/// <param name="TacticalModifier">The modifier supplied by the team's current tactical priorities.</param>
/// <param name="EffectiveScore">The final score used for selection within the priority band.</param>
/// <param name="Input">The normal player input emitted by the decision.</param>
public sealed record PlayerDecision(
    PlayerInstructionKind Kind,
    PlayerInstructionPriorityBand PriorityBand,
    float SituationalScore,
    float TacticalModifier,
    float EffectiveScore,
    PlayerInput Input)
{
    /// <summary>Gets the decision returned when no registered instruction can execute.</summary>
    public static PlayerDecision None { get; } = new(
        PlayerInstructionKind.None,
        PlayerInstructionPriorityBand.None,
        0f,
        0f,
        0f,
        PlayerInput.Empty);
}
