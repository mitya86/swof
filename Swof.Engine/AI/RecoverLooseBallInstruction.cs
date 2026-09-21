using System.Numerics;

namespace Swof.Engine.AI;

/// <summary>
/// Describes a loose ball that this player is the closest eligible teammate to recover.
/// </summary>
/// <param name="BallPosition">The current perceived ball position.</param>
/// <param name="Distance">The player's distance from the ball.</param>
public readonly record struct LooseBallRecoveryOpportunity(Vector2 BallPosition, float Distance);

/// <summary>
/// Recognizes when an outfielder is the closest teammate eligible to pursue a loose ball.
/// </summary>
public sealed class ClosestTeammateToLooseBallPattern : IPlayerPattern<LooseBallRecoveryOpportunity>
{
    /// <inheritdoc />
    public bool TryRecognize(PlayerDecisionContext context, out LooseBallRecoveryOpportunity match)
    {
        ArgumentNullException.ThrowIfNull(context);
        match = default;

        if (context.Match.IsBallOutOfPlay
            || context.Match.Ball.PossessorId is not null
            || context.Player.Role != PlayerRole.Outfield)
        {
            return false;
        }

        var closest = context.Match.Players
            .Where(player => player.Team == context.Player.Team && player.Role == PlayerRole.Outfield)
            .Select(player => new
            {
                Player = player,
                DistanceSquared = Vector2.DistanceSquared(player.Position, context.Match.Ball.Position)
            })
            .OrderBy(candidate => candidate.DistanceSquared)
            .ThenBy(candidate => candidate.Player.Id)
            .FirstOrDefault();

        if (closest is null || closest.Player.Id != context.Player.Id)
        {
            return false;
        }

        match = new LooseBallRecoveryOpportunity(
            context.Match.Ball.Position,
            MathF.Sqrt(closest.DistanceSquared));
        return true;
    }
}

/// <summary>
/// Emits movement toward a loose ball when the recovery pattern is available.
/// </summary>
public sealed class RecoverLooseBallInstruction : IPlayerInstruction
{
    private readonly IPlayerPattern<LooseBallRecoveryOpportunity> _pattern;

    /// <summary>
    /// Creates the instruction with its independently replaceable pattern recognizer.
    /// </summary>
    /// <param name="pattern">The recognizer that decides whether this player should recover the ball.</param>
    public RecoverLooseBallInstruction(IPlayerPattern<LooseBallRecoveryOpportunity> pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        _pattern = pattern;
    }

    /// <inheritdoc />
    public PlayerInstructionCandidate? Evaluate(PlayerDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_pattern.TryRecognize(context, out var match))
        {
            return null;
        }

        var offset = match.BallPosition - context.Player.Position;
        var move = offset.LengthSquared() <= 0.0001f
            ? Vector2.Zero
            : Vector2.Normalize(offset);
        var situationalScore = 1f / (1f + match.Distance);

        return new PlayerInstructionCandidate(
            PlayerInstructionKind.RecoverLooseBall,
            PlayerInstructionPriorityBand.Urgent,
            situationalScore,
            new PlayerInput(move, ActionDown: false));
    }
}
