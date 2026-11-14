using System.Numerics;

namespace Swof.Engine.AI;

/// <summary>Describes a shot the ball carrier can take now.</summary>
/// <param name="AimDirection">The normalized direction from the carrier to the aim point in the goal.</param>
/// <param name="Distance">The distance from the carrier to the goal centre.</param>
public readonly record struct ShootingChance(Vector2 AimDirection, float Distance);

/// <summary>
/// Recognizes a carrier inside shooting range with an open line to the side of the goal the goalkeeper has left.
/// </summary>
public sealed class ShootingChancePattern : IPlayerPattern<ShootingChance>
{
    internal const float MaximumShotDistance = 12f;
    // Squarer than 60 degrees off straight the goal mouth is too narrow to aim at.
    private const float MinimumGoalFacingCosine = 0.5f;
    private const float AimOffsetFromGoalCentre = 0.9f;
    private const float BlockedLaneClearance = 0.9f;

    /// <inheritdoc />
    public bool TryRecognize(PlayerDecisionContext context, out ShootingChance match)
    {
        ArgumentNullException.ThrowIfNull(context);
        match = default;

        var player = context.Player;
        if (context.Match.IsBallOutOfPlay
            || player.Role != PlayerRole.Outfield
            || !player.HasPossession)
        {
            return false;
        }

        var field = context.Match.Field;
        var goalX = player.Team == TeamSide.Home ? field.Right : field.Left;
        var distance = Vector2.Distance(player.Position, new Vector2(goalX, 0f));
        if (distance > MaximumShotDistance
            || MathF.Abs(goalX - player.Position.X) < distance * MinimumGoalFacingCosine)
        {
            return false;
        }

        var goalkeeper = context.Match.Players.FirstOrDefault(candidate =>
            candidate.Team != player.Team && candidate.Role == PlayerRole.Goalkeeper);
        var aimY = goalkeeper is null
            ? 0f
            : goalkeeper.Position.Y > 0f ? -AimOffsetFromGoalCentre : AimOffsetFromGoalCentre;
        var aimPoint = new Vector2(goalX, aimY);
        var isBlocked = context.Match.Players.Any(candidate =>
            candidate.Team != player.Team
            && candidate.Role == PlayerRole.Outfield
            && SafePossessionPattern.DistanceToSegment(candidate.Position, player.Position, aimPoint)
                < BlockedLaneClearance);
        if (isBlocked)
        {
            return false;
        }

        match = new ShootingChance(Vector2.Normalize(aimPoint - player.Position), distance);
        return true;
    }
}

/// <summary>
/// Shoots with the same held button a human uses: face the goal, hold until the engine releases the shot,
/// then keep the stick neutral so the follow-up movement does not bend the ball.
/// </summary>
public sealed class ShootAtGoalInstruction : IPlayerInstruction
{
    // ponytail: the stick stays neutral for the whole aftertouch window, so computer players never curve
    // or lift a shot. Emit a deliberate aftertouch here when difficulty levels need better finishing.
    public const int FollowThroughTicks = 12;

    private readonly IPlayerPattern<ShootingChance> _pattern;
    private Vector2 _aimDirection;
    private bool _holding;
    private bool _holdConfirmed;
    private int _followThroughTicksRemaining;

    /// <summary>Creates the instruction with its independently replaceable shooting recognizer.</summary>
    /// <param name="pattern">The recognizer that supplies the aim direction.</param>
    public ShootAtGoalInstruction(IPlayerPattern<ShootingChance> pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        _pattern = pattern;
    }

    /// <inheritdoc />
    public PlayerInstructionCandidate? Evaluate(PlayerDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var player = context.Player;
        if (context.Match.IsBallOutOfPlay)
        {
            _holding = false;
            _holdConfirmed = false;
            _followThroughTicksRemaining = 0;
            return null;
        }

        if (!player.HasPossession)
        {
            // A kick animation tells a released shot apart from being robbed during the wind-up.
            // Only a hold this instruction actually carried counts: a proposed shot can lose to a pass.
            if (_holdConfirmed && player.KickAnimation01 > 0f)
            {
                _followThroughTicksRemaining = FollowThroughTicks;
            }

            _holding = false;
            _holdConfirmed = false;
            if (context.Match.Ball.PossessorId is not null)
            {
                _followThroughTicksRemaining = 0;
            }

            if (_followThroughTicksRemaining <= 0)
            {
                return null;
            }

            _followThroughTicksRemaining--;
            return Committed(Vector2.Zero, actionDown: false);
        }

        _followThroughTicksRemaining = 0;

        // The charge shows the button really is down for this player. A hold released early would be a
        // tap pass, so an in-progress hold outranks every other possession choice until the shot leaves.
        if (_holding && player.ActionCharge01 > 0f)
        {
            _holdConfirmed = true;
            return Committed(_aimDirection, actionDown: true);
        }

        _holding = false;
        _holdConfirmed = false;
        if (!_pattern.TryRecognize(context, out var chance))
        {
            return null;
        }

        _aimDirection = chance.AimDirection;
        _holding = true;
        return new PlayerInstructionCandidate(
            PlayerInstructionKind.ShootAtGoal,
            PlayerInstructionPriorityBand.TacticalOpportunity,
            // An unpressured carry scores about 1, so an open carrier shoots from roughly 9 m and a
            // pressured one from the edge of range.
            0.9f + 0.4f * (1f - chance.Distance / ShootingChancePattern.MaximumShotDistance),
            new PlayerInput(_aimDirection, ActionDown: true));
    }

    private static PlayerInstructionCandidate Committed(Vector2 move, bool actionDown)
    {
        return new PlayerInstructionCandidate(
            PlayerInstructionKind.ShootAtGoal,
            PlayerInstructionPriorityBand.Urgent,
            1f,
            new PlayerInput(move, actionDown));
    }
}
