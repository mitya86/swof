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
            DefensiveAssignments.PressPoint(context.Match.Field, context.Player.Team, carrier.Position, context.Tactic),
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
            DefensiveAssignments.PressPoint(context.Match.Field, context.Player.Team, carrier.Position, context.Tactic),
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
    // The presser holds station about 1.9 m goal-side of the carrier; a slide covers about 3.3 m.
    public const float TackleRange = 2.2f;
    private const float TackleFacingCosine = 0.9f;

    // Longer than a slide and its recovery: a press during either would not start a new slide.
    internal const int TackleCooldownTicks = 90;

    /// <summary>Ticks the presser shadows a carrier within reach before sliding, so the tackle can be read and beaten.</summary>
    public const int JockeyTicksBeforeTackle = 20;

    private readonly IPlayerPattern<BallCarrierPressureOpportunity> _pattern;
    private int _tackleCooldownTicksRemaining;
    private int _ticksWithinReach;

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
        if (_tackleCooldownTicksRemaining > 0)
        {
            _tackleCooldownTicksRemaining--;
        }

        if (!_pattern.TryRecognize(context, out var match))
        {
            _ticksWithinReach = 0;
            return null;
        }

        var input = new PlayerInput(
            DirectionTo(context.Player.Position, match.Target, match.Distance),
            ActionDown: false);
        var carrier = context.Match.Players.First(player => player.Id == match.CarrierId);
        var toCarrier = carrier.Position - context.Player.Position;
        var carrierDistance = toCarrier.Length();
        _ticksWithinReach = carrierDistance <= TackleRange ? _ticksWithinReach + 1 : 0;
        if (_tackleCooldownTicksRemaining == 0
            && _ticksWithinReach > JockeyTicksBeforeTackle
            && carrierDistance > 0.0001f)
        {
            // Turn onto the carrier first, then press once: the engine starts the slide on the press.
            var direction = toCarrier / carrierDistance;
            var isFacingCarrier = Vector2.Dot(context.Player.Facing.ToUnitVector(), direction) >= TackleFacingCosine;
            if (isFacingCarrier && WouldFoul(context, carrier) && !GoesThroughWithIt(context))
            {
                // A slide from here would go through the carrier's back: step round to the ball instead.
                var toBall = context.Match.Ball.Position - context.Player.Position;
                input = new PlayerInput(DirectionTo(context.Player.Position, context.Match.Ball.Position, toBall.Length()), ActionDown: false);
            }
            else
            {
                input = new PlayerInput(direction, ActionDown: isFacingCarrier);
                if (isFacingCarrier)
                {
                    _tackleCooldownTicksRemaining = TackleCooldownTicks;
                }
            }
        }

        return new PlayerInstructionCandidate(
            PlayerInstructionKind.PressureBallCarrier,
            PlayerInstructionPriorityBand.Urgent,
            1f / (1f + match.Distance),
            input);
    }

    // ponytail: the carrier and ball are taken to stay where they are while the slide travels, so a
    // carrier who turns away during it can still be fouled. Predict their movement if computers foul too often.
    private static bool WouldFoul(PlayerDecisionContext context, PlayerSnapshot carrier)
    {
        var rule = context.Match.SlideFoul;
        var slider = context.Player;
        var ball = context.Match.Ball;
        var slideDirection = slider.Facing.ToUnitVector();
        var toCarrier = carrier.Position - slider.Position;
        var reach = slider.Radius + ball.Radius + rule.HitDistance;
        var along = Vector2.Dot(toCarrier, slideDirection);
        var besideSquared = toCarrier.LengthSquared() - along * along;
        var travel = MathF.Max(0f, along - MathF.Sqrt(MathF.Max(0f, reach * reach - besideSquared)));
        return rule.IsFoul(
            slideDirection,
            slider.Position + slideDirection * travel,
            slider.Radius,
            carrier.Facing.ToUnitVector(),
            ball.Position,
            ball.Radius);
    }

    // An undisciplined player sometimes slides in anyway; the roll is held a second so he does not change his mind every tick.
    private static bool GoesThroughWithIt(PlayerDecisionContext context) =>
        context.Traits.RecklessChance > 0f
        && DecisionNoise.Roll(context.Match, context.Player.Id, purpose: 200, holdTicks: 60) < context.Traits.RecklessChance;

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

    /// <summary>Where the team meets a carrier: the carrier itself, or the nearest point on the press line.</summary>
    public static Vector2 PressPoint(
        FieldDimensions field,
        TeamSide defendingTeam,
        Vector2 carrierPosition,
        TeamTactic tactic)
    {
        var x = defendingTeam == TeamSide.Home
            ? MathF.Min(carrierPosition.X, field.Left + tactic.PressLineMetres)
            : MathF.Max(carrierPosition.X, field.Right - tactic.PressLineMetres);
        return new Vector2(x, carrierPosition.Y);
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
