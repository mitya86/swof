using System.Numerics;

namespace Swof.Engine.AI;

/// <summary>Describes the goal-side point from which the designated defender pressures the carrier.</summary>
/// <param name="CarrierId">The opposing outfielder in possession.</param>
/// <param name="Target">The pressure point between the carrier and the defending goal.</param>
/// <param name="Distance">The defender's distance from the pressure point.</param>
public readonly record struct BallCarrierPressureOpportunity(
    int CarrierId,
    Vector2 Target,
    float Distance);

/// <summary>Describes the safety position occupied behind the designated presser.</summary>
/// <param name="CarrierId">The opposing outfielder in possession.</param>
/// <param name="PresserId">The teammate applying direct pressure.</param>
/// <param name="Target">The cover point between the carrier and the defending goal.</param>
/// <param name="Distance">The defender's distance from the cover point.</param>
public readonly record struct BallCarrierCoverOpportunity(
    int CarrierId,
    int PresserId,
    Vector2 Target,
    float Distance);

/// <summary>Finds the one defender responsible for direct pressure on an opposing outfield carrier.</summary>
public sealed class BallCarrierPressurePattern : IPlayerPattern<BallCarrierPressureOpportunity>
{
    private const float PressureDistance = 1.35f;

    /// <inheritdoc />
    public bool TryRecognize(PlayerDecisionContext context, out BallCarrierPressureOpportunity match)
    {
        ArgumentNullException.ThrowIfNull(context);
        match = default;

        if (!DefensiveAssignments.TryGetOpponentOutfieldCarrier(context, out var carrier))
        {
            return false;
        }

        var presser = DefensiveAssignments.FindPresser(context.Match, context.Player.Team, carrier);
        if (presser is null || presser.Id != context.Player.Id)
        {
            return false;
        }

        var target = DefensiveAssignments.GoalSidePoint(
            context.Match.Field,
            context.Player.Team,
            carrier.Position,
            PressureDistance,
            context.Player.Radius);
        match = new BallCarrierPressureOpportunity(
            carrier.Id,
            target,
            Vector2.Distance(context.Player.Position, target));
        return true;
    }
}

/// <summary>Finds the one defender responsible for covering the direct presser.</summary>
public sealed class BallCarrierCoverPattern : IPlayerPattern<BallCarrierCoverOpportunity>
{
    /// <inheritdoc />
    public bool TryRecognize(PlayerDecisionContext context, out BallCarrierCoverOpportunity match)
    {
        ArgumentNullException.ThrowIfNull(context);
        match = default;

        if (!DefensiveAssignments.TryGetOpponentOutfieldCarrier(context, out var carrier))
        {
            return false;
        }

        var presser = DefensiveAssignments.FindPresser(context.Match, context.Player.Team, carrier);
        if (presser is null)
        {
            return false;
        }

        var target = DefensiveAssignments.GoalSidePoint(
            context.Match.Field,
            context.Player.Team,
            carrier.Position,
            DefensiveAssignments.CoverDistance,
            context.Player.Radius);
        var cover = DefensiveAssignments.FindCover(
            context.Match,
            context.Player.Team,
            presser.Id,
            target);
        if (cover is null || cover.Id != context.Player.Id)
        {
            return false;
        }

        match = new BallCarrierCoverOpportunity(
            carrier.Id,
            presser.Id,
            target,
            Vector2.Distance(context.Player.Position, target));
        return true;
    }
}

/// <summary>Moves the designated defender toward the carrier without using a privileged tackle action.</summary>
public sealed class PressureBallCarrierInstruction : IPlayerInstruction
{
    private const float ArrivalDistance = 0.55f;
    private readonly IPlayerPattern<BallCarrierPressureOpportunity> _pattern;

    /// <summary>Creates the instruction with its independently replaceable pressure recognizer.</summary>
    /// <param name="pattern">The recognizer that assigns a presser and pressure point.</param>
    public PressureBallCarrierInstruction(IPlayerPattern<BallCarrierPressureOpportunity> pattern)
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

        return new PlayerInstructionCandidate(
            PlayerInstructionKind.PressureBallCarrier,
            PlayerInstructionPriorityBand.Urgent,
            1f / (1f + match.Distance),
            new PlayerInput(DirectionTo(context.Player.Position, match.Target, match.Distance), ActionDown: false));
    }

    private static Vector2 DirectionTo(Vector2 position, Vector2 target, float distance)
    {
        var offset = target - position;
        return distance <= ArrivalDistance || offset.LengthSquared() <= 0.0001f
            ? Vector2.Zero
            : Vector2.Normalize(offset);
    }
}

/// <summary>Moves the assigned cover defender into the space behind the presser.</summary>
public sealed class CoverBallCarrierInstruction : IPlayerInstruction
{
    private const float ArrivalDistance = 0.65f;
    private readonly IPlayerPattern<BallCarrierCoverOpportunity> _pattern;

    /// <summary>Creates the instruction with its independently replaceable cover recognizer.</summary>
    /// <param name="pattern">The recognizer that assigns a cover defender and target.</param>
    public CoverBallCarrierInstruction(IPlayerPattern<BallCarrierCoverOpportunity> pattern)
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
            PlayerInstructionKind.CoverBallCarrier,
            PlayerInstructionPriorityBand.TacticalOpportunity,
            1f + 1f / (1f + match.Distance),
            new PlayerInput(move, ActionDown: false));
    }
}

/// <summary>Provides stable pressure and cover assignments shared by individual defensive patterns.</summary>
internal static class DefensiveAssignments
{
    public const float CoverDistance = 4.25f;
    private const float WrongSidePenalty = 0.75f;

    public static bool TryGetOpponentOutfieldCarrier(
        PlayerDecisionContext context,
        out PlayerSnapshot carrier)
    {
        carrier = null!;
        if (context.Match.IsBallOutOfPlay
            || context.Player.Role != PlayerRole.Outfield
            || context.Player.HasPossession
            || context.Match.Ball.PossessorId is not int possessorId)
        {
            return false;
        }

        var recognized = context.Match.Players.FirstOrDefault(player =>
            player.Id == possessorId
            && player.Team != context.Player.Team
            && player.Role == PlayerRole.Outfield);
        if (recognized is null)
        {
            return false;
        }

        carrier = recognized;
        return true;
    }

    public static PlayerSnapshot? FindPresser(
        MatchSnapshot match,
        TeamSide defendingTeam,
        PlayerSnapshot carrier)
    {
        var defenders = match.Players
            .Where(player => player.Team == defendingTeam && player.Role == PlayerRole.Outfield)
            .ToArray();
        var activeDefender = defenders.FirstOrDefault(player => player.Id == match.ActivePlayerId);
        if (activeDefender is not null)
        {
            return activeDefender;
        }

        return defenders
            .OrderBy(player => PressureScore(player, carrier.Position, match.Field, defendingTeam))
            .ThenBy(player => player.Id)
            .FirstOrDefault();
    }

    public static PlayerSnapshot? FindCover(
        MatchSnapshot match,
        TeamSide defendingTeam,
        int presserId,
        Vector2 coverTarget)
    {
        return match.Players
            .Where(player => player.Team == defendingTeam
                && player.Role == PlayerRole.Outfield
                && player.Id != presserId)
            .OrderBy(player => Vector2.DistanceSquared(player.Position, coverTarget))
            .ThenBy(player => player.Id)
            .FirstOrDefault();
    }

    public static Vector2 GoalSidePoint(
        FieldDimensions field,
        TeamSide defendingTeam,
        Vector2 carrierPosition,
        float distance,
        float playerRadius)
    {
        var ownGoal = new Vector2(
            defendingTeam == TeamSide.Home ? field.Left : field.Right,
            0f);
        var fallback = defendingTeam == TeamSide.Home ? -Vector2.UnitX : Vector2.UnitX;
        var toGoal = ownGoal - carrierPosition;
        var direction = toGoal.LengthSquared() <= 0.0001f
            ? fallback
            : Vector2.Normalize(toGoal);
        var target = carrierPosition + direction * distance;
        return new Vector2(
            Math.Clamp(target.X, field.Left + playerRadius, field.Right - playerRadius),
            Math.Clamp(target.Y, field.Top + playerRadius, field.Bottom - playerRadius));
    }

    private static float PressureScore(
        PlayerSnapshot player,
        Vector2 threatPosition,
        FieldDimensions field,
        TeamSide defendingTeam)
    {
        var wrongSideDistance = defendingTeam == TeamSide.Home
            ? MathF.Max(0f, player.Position.X - threatPosition.X)
            : MathF.Max(0f, threatPosition.X - player.Position.X);
        return Vector2.Distance(player.Position, threatPosition)
            + wrongSideDistance * WrongSidePenalty;
    }
}
