namespace Swof.Engine.AI;

/// <summary>
/// Selects one instruction for an autonomous player from an ordered, extensible instruction set.
/// </summary>
public sealed class ComputerPlayerBrain
{
    private readonly IReadOnlyList<IPlayerInstruction> _instructions;

    /// <summary>
    /// Creates a computer-player brain with instructions in stable tie-breaking order.
    /// </summary>
    /// <param name="instructions">The instructions that this player may evaluate.</param>
    /// <exception cref="ArgumentException">The collection contains a null instruction.</exception>
    public ComputerPlayerBrain(IEnumerable<IPlayerInstruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);

        var copy = instructions.ToArray();
        if (copy.Any(instruction => instruction is null))
        {
            throw new ArgumentException("Instructions cannot contain null entries.", nameof(instructions));
        }

        _instructions = copy;
    }

    /// <summary>
    /// Creates the initial brain configuration. New behavior can be registered here one instruction at a time.
    /// </summary>
    /// <returns>A brain containing the currently supported concrete instructions.</returns>
    public static ComputerPlayerBrain CreateDefault()
    {
        return new ComputerPlayerBrain(new IPlayerInstruction[]
        {
            new RecoverLooseBallInstruction(new ClosestTeammateToLooseBallPattern()),
            new RetainPossessionInstruction(new SafePossessionPattern()),
            new PressureBallCarrierInstruction(new BallCarrierPressurePattern()),
            new CoverBallCarrierInstruction(new BallCarrierCoverPattern()),
            new OfferSafePassInstruction(new OpenPassingOptionPattern()),
            new MarkOpponentInstruction(new StructuredMarkingPattern()),
            new MaintainTeamShapeInstruction(new FutsalTeamShapePattern())
        });
    }

    /// <summary>
    /// Selects the highest-priority executable instruction for one player.
    /// </summary>
    /// <param name="context">The immutable player decision context.</param>
    /// <param name="priorities">The team's current tactical score modifiers.</param>
    /// <returns>The selected decision, or <see cref="PlayerDecision.None"/> when no instruction is available.</returns>
    /// <remarks>
    /// Priority bands are compared before scores. Exact ties preserve registration order so replay results remain stable.
    /// </remarks>
    public PlayerDecision Decide(PlayerDecisionContext context, TacticalPriorities? priorities = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        priorities ??= TacticalPriorities.Neutral;

        PlayerDecision? best = null;
        foreach (var instruction in _instructions)
        {
            var candidate = instruction.Evaluate(context);
            if (candidate is null)
            {
                continue;
            }

            Validate(candidate);
            var tacticalModifier = priorities.GetModifier(candidate.Kind);
            var effectiveScore = candidate.SituationalScore + tacticalModifier;
            if (!float.IsFinite(effectiveScore))
            {
                throw new InvalidOperationException($"Instruction {candidate.Kind} produced a non-finite effective score.");
            }

            if (best is not null
                && (candidate.PriorityBand < best.PriorityBand
                    || candidate.PriorityBand == best.PriorityBand && effectiveScore <= best.EffectiveScore))
            {
                continue;
            }

            best = new PlayerDecision(
                candidate.Kind,
                candidate.PriorityBand,
                candidate.SituationalScore,
                tacticalModifier,
                effectiveScore,
                candidate.Input);
        }

        return best ?? PlayerDecision.None;
    }

    private static void Validate(PlayerInstructionCandidate candidate)
    {
        if (candidate.Kind == PlayerInstructionKind.None)
        {
            throw new InvalidOperationException("An executable instruction must have a concrete kind.");
        }

        if (candidate.PriorityBand == PlayerInstructionPriorityBand.None)
        {
            throw new InvalidOperationException($"Instruction {candidate.Kind} must have a concrete priority band.");
        }

        if (!float.IsFinite(candidate.SituationalScore))
        {
            throw new InvalidOperationException($"Instruction {candidate.Kind} produced a non-finite situational score.");
        }
    }
}
