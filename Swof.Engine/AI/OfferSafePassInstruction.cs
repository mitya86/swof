using System.Numerics;

namespace Swof.Engine.AI;

/// <summary>
/// Describes an open off-ball position from which this player can receive a safe pass.
/// </summary>
/// <param name="Target">The selected support position.</param>
/// <param name="Distance">The player's distance from that position.</param>
/// <param name="Score">The deterministic openness score used to select it.</param>
/// <param name="LaneClearance">The nearest opponent's distance from the passing lane.</param>
/// <param name="OpponentClearance">The nearest opponent's distance from the target.</param>
public readonly record struct SafePassOpportunity(
    Vector2 Target,
    float Distance,
    float Score,
    float LaneClearance,
    float OpponentClearance);

/// <summary>
/// Finds a role-appropriate open position for one non-possessing outfielder.
/// </summary>
public sealed class OpenPassingOptionPattern : IPlayerPattern<SafePassOpportunity>
{
    private const float BoundaryClearance = 1.25f;
    private const float DesiredPassDistance = 8f;
    private const float UsefulLaneClearance = 3f;
    private const float UsefulOpponentClearance = 4f;
    private const float UsefulTeammateSpacing = 4f;
    private const float CandidateSpacing = 2.5f;

    private static readonly Vector2[] CandidateVariations =
    [
        Vector2.Zero,
        new(CandidateSpacing, 0f),
        new(-CandidateSpacing, 0f),
        new(0f, CandidateSpacing),
        new(0f, -CandidateSpacing),
        new(CandidateSpacing * 0.7f, CandidateSpacing * 0.7f),
        new(CandidateSpacing * 0.7f, -CandidateSpacing * 0.7f),
        new(-CandidateSpacing * 0.7f, CandidateSpacing * 0.7f),
        new(-CandidateSpacing * 0.7f, -CandidateSpacing * 0.7f)
    ];

    /// <inheritdoc />
    public bool TryRecognize(PlayerDecisionContext context, out SafePassOpportunity match)
    {
        ArgumentNullException.ThrowIfNull(context);
        match = default;

        if (context.Match.IsBallOutOfPlay
            || context.Player.Role != PlayerRole.Outfield
            || context.Player.HasPossession
            || context.Match.Ball.PossessorId is not int possessorId)
        {
            return false;
        }

        var possessor = context.Match.Players.FirstOrDefault(player =>
            player.Id == possessorId && player.Team == context.Player.Team);
        if (possessor is null)
        {
            return false;
        }

        var field = context.Match.Field;
        var attackDirection = context.Player.Team == TeamSide.Home ? 1f : -1f;
        var roleOffset = GetRoleOffset(context.Player.FutsalRole);
        var opponents = context.Match.Players
            .Where(player => player.Team != context.Player.Team)
            .ToArray();
        var otherTeammates = context.Match.Players
            .Where(player => player.Team == context.Player.Team
                && player.Id != context.Player.Id
                && player.Id != possessor.Id)
            .ToArray();

        SafePassOpportunity? best = null;
        foreach (var variation in CandidateVariations)
        {
            var localOffset = roleOffset + variation;
            var unclamped = possessor.Position + new Vector2(
                localOffset.X * attackDirection,
                localOffset.Y * attackDirection);
            var target = new Vector2(
                Math.Clamp(unclamped.X, field.Left + BoundaryClearance, field.Right - BoundaryClearance),
                Math.Clamp(unclamped.Y, field.Top + BoundaryClearance, field.Bottom - BoundaryClearance));
            var passDistance = Vector2.Distance(possessor.Position, target);
            var travelDistance = Vector2.Distance(context.Player.Position, target);
            var laneClearance = MinimumLaneClearance(possessor.Position, target, opponents);
            var opponentClearance = MinimumPointClearance(target, opponents);
            var teammateSpacing = MinimumPointClearance(target, otherTeammates);
            var score = ScoreCandidate(
                passDistance,
                travelDistance,
                laneClearance,
                opponentClearance,
                teammateSpacing,
                field.Length);
            var candidate = new SafePassOpportunity(
                target,
                travelDistance,
                score,
                laneClearance,
                opponentClearance);

            if (best is null || candidate.Score > best.Value.Score)
            {
                best = candidate;
            }
        }

        if (best is null)
        {
            return false;
        }

        match = best.Value;
        return true;
    }

    private static Vector2 GetRoleOffset(FutsalOutfieldRole role)
    {
        return role switch
        {
            FutsalOutfieldRole.Fixo => new Vector2(-5f, 0f),
            FutsalOutfieldRole.LeftAla => new Vector2(1f, -5f),
            FutsalOutfieldRole.RightAla => new Vector2(1f, 5f),
            FutsalOutfieldRole.Pivo => new Vector2(6f, 0f),
            _ => new Vector2(-3f, 0f)
        };
    }

    private static float ScoreCandidate(
        float passDistance,
        float travelDistance,
        float laneClearance,
        float opponentClearance,
        float teammateSpacing,
        float fieldLength)
    {
        var laneScore = Math.Clamp(laneClearance / UsefulLaneClearance, 0f, 1f);
        var opennessScore = Math.Clamp(opponentClearance / UsefulOpponentClearance, 0f, 1f);
        var spacingScore = Math.Clamp(teammateSpacing / UsefulTeammateSpacing, 0f, 1f);
        var distanceScore = 1f - Math.Clamp(
            MathF.Abs(passDistance - DesiredPassDistance) / DesiredPassDistance,
            0f,
            1f);
        var travelCost = Math.Clamp(travelDistance / MathF.Max(1f, fieldLength * 0.3f), 0f, 1f);

        return laneScore * 0.38f
            + opennessScore * 0.3f
            + spacingScore * 0.18f
            + distanceScore * 0.14f
            - travelCost * 0.08f;
    }

    private static float MinimumLaneClearance(
        Vector2 start,
        Vector2 end,
        IReadOnlyList<PlayerSnapshot> players)
    {
        if (players.Count == 0)
        {
            return UsefulLaneClearance;
        }

        return players.Min(player => DistanceToSegment(player.Position, start, end));
    }

    private static float MinimumPointClearance(
        Vector2 point,
        IReadOnlyList<PlayerSnapshot> players)
    {
        if (players.Count == 0)
        {
            return Math.Max(UsefulOpponentClearance, UsefulTeammateSpacing);
        }

        return players.Min(player => Vector2.Distance(point, player.Position));
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
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
}

/// <summary>
/// Emits normal movement input toward this player's best open passing position.
/// </summary>
public sealed class OfferSafePassInstruction : IPlayerInstruction
{
    private const float ArrivalDistance = 0.6f;
    private readonly IPlayerPattern<SafePassOpportunity> _pattern;

    /// <summary>
    /// Creates the instruction with its independently replaceable passing-option recognizer.
    /// </summary>
    /// <param name="pattern">The recognizer that supplies an open support target.</param>
    public OfferSafePassInstruction(IPlayerPattern<SafePassOpportunity> pattern)
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

        var offset = match.Target - context.Player.Position;
        var move = match.Distance <= ArrivalDistance || offset.LengthSquared() <= 0.0001f
            ? Vector2.Zero
            : Vector2.Normalize(offset);

        return new PlayerInstructionCandidate(
            PlayerInstructionKind.OfferSafePass,
            PlayerInstructionPriorityBand.TacticalOpportunity,
            match.Score,
            new PlayerInput(move, ActionDown: false));
    }
}
