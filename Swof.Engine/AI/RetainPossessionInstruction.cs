using System.Numerics;

namespace Swof.Engine.AI;

/// <summary>
/// Describes the safest carry direction and an optional safe passing outlet for the player in possession.
/// </summary>
/// <param name="CarryDirection">The normalized direction toward the best nearby space.</param>
/// <param name="CarryScore">The deterministic safety score for the carry direction.</param>
/// <param name="PassTargetId">The selected receiving outfielder, when a safe lane exists.</param>
/// <param name="PassDirection">The normalized direction toward the selected receiver.</param>
/// <param name="PassScore">The deterministic safety score for the selected pass.</param>
/// <param name="NearestOpponentDistance">The distance from the possessor to the nearest opponent.</param>
public readonly record struct PossessionOpportunity(
    Vector2 CarryDirection,
    float CarryScore,
    int? PassTargetId,
    Vector2 PassDirection,
    float PassScore,
    float NearestOpponentDistance);

/// <summary>
/// Recognizes safe nearby space and passing lanes from the ball carrier's own match view.
/// </summary>
public sealed class SafePossessionPattern : IPlayerPattern<PossessionOpportunity>
{
    private const float CarryLookAhead = 3f;
    private const float CarryIndecision = 0.08f;
    private const float BoundaryClearanceScale = 3f;
    private const float UsefulOpponentClearance = 6f;
    private const float UsefulTeammateSpacing = 4f;
    private const float MinimumPassDistance = 3f;
    private const float DesiredPassDistance = 8f;
    private const float MaximumPassDistance = 18f;
    private const float MinimumSafeLaneClearance = 3f;
    private const float MinimumSafeReceiverClearance = 3f;
    // An opponent takes a pass that comes within 1.97 m of it (body, ball and
    // PhysicsTuning.FastBallCollectionReach) and steps toward it while the ball leaves the passer's feet...
    private const float LaneBodyReach = 2.5f;
    // ...and it covers this much more ground for every metre the ball has to travel to pass it.
    private const float LaneReachPerMetre = 0.25f;

    /// <inheritdoc />
    public bool TryRecognize(PlayerDecisionContext context, out PossessionOpportunity match)
    {
        ArgumentNullException.ThrowIfNull(context);
        match = default;

        if (context.Match.IsBallOutOfPlay
            || context.Player.Role != PlayerRole.Outfield
            || !context.Player.HasPossession
            || context.Match.Ball.PossessorId != context.Player.Id)
        {
            return false;
        }

        var teammates = context.Match.Players
            .Where(player => player.Team == context.Player.Team
                && player.Role == PlayerRole.Outfield
                && player.Id != context.Player.Id)
            .OrderBy(player => player.Id)
            .ToArray();
        var opponents = context.Match.Players
            .Where(player => player.Team != context.Player.Team)
            .OrderBy(player => player.Id)
            .ToArray();
        var nearestOpponentDistance = opponents.Length == 0
            ? context.Match.Field.Length
            : opponents.Min(player => Vector2.Distance(context.Player.Position, player.Position));
        var attackDirection = context.Player.Team == TeamSide.Home ? 1f : -1f;
        var carry = FindBestCarryDirection(context, teammates, opponents, attackDirection);
        var pass = FindBestPass(context.Player, teammates, opponents, attackDirection);

        match = new PossessionOpportunity(
            carry.Direction,
            carry.Score,
            pass?.TargetId,
            pass?.Direction ?? Vector2.Zero,
            pass?.Score ?? 0f,
            nearestOpponentDistance);
        return true;
    }

    private static (Vector2 Direction, float Score) FindBestCarryDirection(
        PlayerDecisionContext context,
        IReadOnlyList<PlayerSnapshot> teammates,
        IReadOnlyList<PlayerSnapshot> opponents,
        float attackDirection)
    {
        var directions = CreateCarryDirections(attackDirection);
        var field = context.Match.Field;
        var bestDirection = directions[0];
        var bestScore = float.NegativeInfinity;

        for (var index = 0; index < directions.Length; index++)
        {
            var direction = directions[index];
            var unclampedTarget = context.Player.Position + direction * CarryLookAhead;
            var target = new Vector2(
                Math.Clamp(unclampedTarget.X, field.Left + context.Player.Radius, field.Right - context.Player.Radius),
                Math.Clamp(unclampedTarget.Y, field.Top + context.Player.Radius, field.Bottom - context.Player.Radius));
            var boundaryClearance = MathF.Min(
                MathF.Min(target.X - field.Left, field.Right - target.X),
                MathF.Min(target.Y - field.Top, field.Bottom - target.Y));
            // A direction that leads off the court is not somewhere to carry the ball: clamped,
            // it looked like free space next to the line and the carrier dribbled out of play.
            if (unclampedTarget.X < field.Left || unclampedTarget.X > field.Right
                || unclampedTarget.Y < field.Top || unclampedTarget.Y > field.Bottom)
            {
                continue;
            }

            // In its own half a carrier does not turn toward its own goal. Running from a presser
            // that way took whole teams back to their goal line, where they lost the ball.
            // ponytail: a flat rule; a pressed carrier with no way forward needs a clearance instead.
            if (direction.X * attackDirection < 0f && context.Player.Position.X * attackDirection < 0f)
            {
                continue;
            }

            // A carrier turns a quarter at a time. Reversing at speed swings the ball right round
            // the body, which the dribble model answers with a mistouch about six ticks later.
            // Only when nowhere else is left does it turn right round.
            var reversal = Vector2.Dot(direction, context.Player.Facing.ToUnitVector()) < -0.01f ? 1f : 0f;
            var opponentClearance = MinimumPointClearance(target, opponents, UsefulOpponentClearance);
            var teammateSpacing = MinimumPointClearance(target, teammates, UsefulTeammateSpacing);
            var forwardProgress = Vector2.Dot(direction, new Vector2(attackDirection, 0f));
            var score = Math.Clamp(opponentClearance / UsefulOpponentClearance, 0f, 1f) * 0.52f
                + Math.Clamp(boundaryClearance / BoundaryClearanceScale, 0f, 1f) * 0.25f
                + Math.Clamp(teammateSpacing / UsefulTeammateSpacing, 0f, 1f) * 0.13f
                + (forwardProgress + 1f) * 0.05f
                - reversal
                // A small leaning that changes twice a second, so a player in the same spot
                // does not always turn the same way and a match does not repeat itself.
                + (DecisionNoise.Roll(context.Match, context.Player.Id, purpose: 100 + index, holdTicks: 30) - 0.5f)
                    * CarryIndecision;

            if (score > bestScore)
            {
                bestDirection = direction;
                bestScore = score;
            }
        }

        return (bestDirection, bestScore);
    }

    private static PassOption? FindBestPass(
        PlayerSnapshot possessor,
        IReadOnlyList<PlayerSnapshot> teammates,
        IReadOnlyList<PlayerSnapshot> opponents,
        float attackDirection)
    {
        PassOption? best = null;
        foreach (var teammate in teammates)
        {
            var offset = teammate.Position - possessor.Position;
            var distance = offset.Length();
            if (distance < MinimumPassDistance || distance > MaximumPassDistance)
            {
                continue;
            }

            var laneClearance = MinimumLaneClearance(possessor.Position, teammate.Position, opponents);
            var receiverClearance = MinimumPointClearance(
                teammate.Position,
                opponents,
                UsefulOpponentClearance);
            if (laneClearance < MinimumSafeLaneClearance
                || receiverClearance < MinimumSafeReceiverClearance)
            {
                continue;
            }

            var direction = offset / distance;
            var laneScore = Math.Clamp(laneClearance / UsefulOpponentClearance, 0f, 1f);
            var receiverScore = Math.Clamp(receiverClearance / UsefulOpponentClearance, 0f, 1f);
            var distanceScore = 1f - Math.Clamp(
                MathF.Abs(distance - DesiredPassDistance) / DesiredPassDistance,
                0f,
                1f);
            var forwardProgress = Math.Clamp(direction.X * attackDirection, -1f, 1f);
            var score = laneScore * 0.42f
                + receiverScore * 0.34f
                + distanceScore * 0.16f
                + (forwardProgress + 1f) * 0.2f;
            var candidate = new PassOption(teammate.Id, direction, score);
            if (best is null || candidate.Score > best.Value.Score)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static Vector2[] CreateCarryDirections(float attackDirection)
    {
        return
        [
            new Vector2(attackDirection, 0f),
            Vector2.Normalize(new Vector2(attackDirection, -1f)),
            Vector2.Normalize(new Vector2(attackDirection, 1f)),
            new Vector2(0f, -1f),
            new Vector2(0f, 1f),
            Vector2.Normalize(new Vector2(-attackDirection, -1f)),
            Vector2.Normalize(new Vector2(-attackDirection, 1f)),
            new Vector2(-attackDirection, 0f)
        ];
    }

    private static float MinimumLaneClearance(
        Vector2 start,
        Vector2 end,
        IReadOnlyList<PlayerSnapshot> players)
    {
        if (players.Count == 0)
        {
            return UsefulOpponentClearance;
        }

        // An opponent beside or behind the passer is not in the way of a pass going away from it,
        // however close it stands; measured by plain distance, a pressed carrier never had a pass.
        // The result keeps the old scale: MinimumSafeLaneClearance means "just out of reach".
        var lane = end - start;
        var length = lane.Length();
        if (length <= 0.0001f)
        {
            return players.Min(player => Vector2.Distance(player.Position, start));
        }

        var direction = lane / length;
        var clearance = UsefulOpponentClearance;
        foreach (var player in players)
        {
            var along = Vector2.Dot(player.Position - start, direction);
            if (along <= 0f)
            {
                continue;
            }

            var reach = MathF.Min(MinimumSafeLaneClearance, LaneBodyReach + LaneReachPerMetre * MathF.Min(along, length));
            var distance = DistanceToSegment(player.Position, start, end);
            clearance = MathF.Min(clearance, distance - reach + MinimumSafeLaneClearance);
        }

        return clearance;
    }

    private static float MinimumPointClearance(
        Vector2 point,
        IReadOnlyList<PlayerSnapshot> players,
        float emptyValue)
    {
        if (players.Count == 0)
        {
            return emptyValue;
        }

        return players.Min(player => Vector2.Distance(point, player.Position));
    }

    internal static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return Vector2.Distance(point, start);
        }

        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, start + segment * t);
    }

    private readonly record struct PassOption(int TargetId, Vector2 Direction, float Score);
}

/// <summary>
/// Carries into space, then executes a safe pass as a real one-tick press and release.
/// </summary>
/// <remarks>
/// Each player owns a separate instruction instance, so its short commitments are individual and deterministic.
/// </remarks>
public sealed class RetainPossessionInstruction : IPlayerInstruction
{
    public const int MinimumPassDelayTicks = 30;

    /// <summary>
    /// Ticks a pressed carrier in its own third keeps the ball before passing. Waiting the usual half second there,
    /// the last outfielder had the ball taken off it where it stood about 18 times a match.
    /// </summary>
    public const int OwnThirdPassDelayTicks = 10;
    internal const int NormalPassDelayTicks = 48;
    internal const int CarryCommitmentTicks = 10;
    internal const float PressureDistance = 3.25f;

    private readonly IPlayerPattern<PossessionOpportunity> _pattern;
    private int _possessionTicks;
    private int _carryTicksRemaining;
    private Vector2 _committedCarryDirection;
    private Vector2 _committedPassDirection;
    private bool _releasePending;

    /// <summary>Creates the instruction with its independently replaceable possession recognizer.</summary>
    /// <param name="pattern">The recognizer that supplies safe carry and pass options.</param>
    public RetainPossessionInstruction(IPlayerPattern<PossessionOpportunity> pattern)
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
            Reset();
            return null;
        }

        if (_releasePending)
        {
            _releasePending = false;
            _possessionTicks = 0;
            return CreateCandidate(match.PassScore, _committedPassDirection, actionDown: false);
        }

        _possessionTicks++;
        var attackDirection = context.Player.Team == TeamSide.Home ? 1f : -1f;
        var isInOwnThird = context.Player.Position.X * attackDirection < -context.Match.Field.Length / 6f;
        var mayPass = match.PassTargetId is not null
            && (_possessionTicks >= NormalPassDelayTicks
                || _possessionTicks >= (isInOwnThird ? OwnThirdPassDelayTicks : MinimumPassDelayTicks)
                    && match.NearestOpponentDistance <= PressureDistance);
        if (mayPass)
        {
            _committedPassDirection = match.PassDirection;
            _releasePending = true;
            return CreateCandidate(match.PassScore, _committedPassDirection, actionDown: true);
        }

        if (_carryTicksRemaining <= 0 || _committedCarryDirection.LengthSquared() <= 0.0001f)
        {
            _committedCarryDirection = match.CarryDirection;
            _carryTicksRemaining = CarryCommitmentTicks;
        }

        _carryTicksRemaining--;
        return CreateCandidate(match.CarryScore, _committedCarryDirection, actionDown: false);
    }

    private static PlayerInstructionCandidate CreateCandidate(
        float score,
        Vector2 direction,
        bool actionDown)
    {
        return new PlayerInstructionCandidate(
            PlayerInstructionKind.RetainPossession,
            PlayerInstructionPriorityBand.TacticalOpportunity,
            score,
            new PlayerInput(direction, actionDown));
    }

    private void Reset()
    {
        _possessionTicks = 0;
        _carryTicksRemaining = 0;
        _committedCarryDirection = Vector2.Zero;
        _committedPassDirection = Vector2.Zero;
        _releasePending = false;
    }
}
