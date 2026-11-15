using System.Numerics;

namespace Swof.Engine.AI;

/// <summary>
/// Describes one opponent assigned to this defender and the structured goal-side position used to mark them.
/// </summary>
/// <param name="MarkedOpponentId">The uniquely assigned opposing outfielder.</param>
/// <param name="Target">The blended marking target toward which this defender should move.</param>
/// <param name="StructuralAnchor">The defender's compact 1-2-1 role anchor.</param>
/// <param name="GoalSidePoint">The close-marking point between the opponent and the defending goal.</param>
/// <param name="Distance">The defender's distance from the blended target.</param>
public readonly record struct MarkingOpportunity(
    int MarkedOpponentId,
    Vector2 Target,
    Vector2 StructuralAnchor,
    Vector2 GoalSidePoint,
    float Distance);

/// <summary>
/// Assigns distinct marks and blends man orientation with a compact futsal defensive structure.
/// </summary>
public sealed class StructuredMarkingPattern : IPlayerPattern<MarkingOpportunity>
{
    private const float MarkingDistance = 1.6f;
    private const float MarkingInfluence = 0.68f;
    private const float MinimumBoundaryClearance = 0.25f;

    /// <inheritdoc />
    public bool TryRecognize(PlayerDecisionContext context, out MarkingOpportunity match)
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

        var opponentPossessor = context.Match.Players.FirstOrDefault(player =>
            player.Id == possessorId && player.Team != context.Player.Team);
        if (opponentPossessor is null)
        {
            return false;
        }

        var assignments = BuildAssignments(
            context.Match,
            context.Player.Team,
            opponentPossessor,
            context.Tactic);
        if (!assignments.TryGetValue(context.Player.Id, out var markedOpponent))
        {
            return false;
        }

        var field = context.Match.Field;
        var structuralAnchor = GetStructuralAnchor(
            field,
            context.Player.Team,
            context.Player.FutsalRole);
        var ownGoal = new Vector2(
            context.Player.Team == TeamSide.Home ? field.Left : field.Right,
            0f);
        var fallbackGoalDirection = context.Player.Team == TeamSide.Home
            ? -Vector2.UnitX
            : Vector2.UnitX;
        var toOwnGoal = SafeNormalize(ownGoal - markedOpponent.Position, fallbackGoalDirection);
        var goalSidePoint = markedOpponent.Position + toOwnGoal * MarkingDistance;
        var target = Vector2.Lerp(structuralAnchor, goalSidePoint, MarkingInfluence);
        target = ClampToDefensiveStructure(
            target,
            field,
            context.Player.Team,
            context.Player.FutsalRole,
            context.Player.Radius);

        match = new MarkingOpportunity(
            markedOpponent.Id,
            target,
            structuralAnchor,
            goalSidePoint,
            Vector2.Distance(context.Player.Position, target));
        return true;
    }

    private static IReadOnlyDictionary<int, PlayerSnapshot> BuildAssignments(
        MatchSnapshot match,
        TeamSide defendingTeam,
        PlayerSnapshot possessor,
        TeamTactic tactic)
    {
        var presser = possessor.Role == PlayerRole.Outfield
            ? DefensiveAssignments.FindPresser(match, defendingTeam, possessor)
            : null;
        var coverTarget = DefensiveAssignments.GoalSidePoint(
            match.Field,
            defendingTeam,
            DefensiveAssignments.PressPoint(match.Field, defendingTeam, possessor.Position, tactic),
            DefensiveAssignments.CoverDistance,
            playerRadius: 0f);
        var cover = presser is null
            ? null
            : DefensiveAssignments.FindCover(match, defendingTeam, presser.Id, coverTarget);
        var defenders = match.Players
            .Where(player => player.Team == defendingTeam
                && player.Role == PlayerRole.Outfield
                && player.Id != presser?.Id
                && player.Id != cover?.Id)
            .OrderBy(player => RoleOrder(player.FutsalRole))
            .ThenBy(player => player.Id)
            .ToArray();
        var availableAttackers = match.Players
            .Where(player => player.Team != defendingTeam
                && player.Role == PlayerRole.Outfield
                && (possessor.Role != PlayerRole.Outfield || player.Id != possessor.Id))
            .OrderBy(player => RoleOrder(player.FutsalRole))
            .ThenBy(player => player.Id)
            .ToList();
        var assignments = new Dictionary<int, PlayerSnapshot>(defenders.Length);

        foreach (var defender in defenders)
        {
            var preferredRole = PreferredOpponentRole(defender.FutsalRole);
            var markedOpponent = availableAttackers
                .Where(player => player.FutsalRole == preferredRole)
                .OrderBy(player => Vector2.DistanceSquared(player.Position, defender.Position))
                .ThenBy(player => player.Id)
                .FirstOrDefault()
                ?? availableAttackers
                    .OrderBy(player => Vector2.DistanceSquared(player.Position, defender.Position))
                    .ThenBy(player => player.Id)
                    .FirstOrDefault();
            if (markedOpponent is null)
            {
                continue;
            }

            assignments.Add(defender.Id, markedOpponent);
            availableAttackers.Remove(markedOpponent);
        }

        return assignments;
    }

    private static Vector2 GetStructuralAnchor(
        FieldDimensions field,
        TeamSide team,
        FutsalOutfieldRole role)
    {
        var attackDirection = team == TeamSide.Home ? 1f : -1f;
        var ownGoalX = team == TeamSide.Home ? field.Left : field.Right;
        var depth = role switch
        {
            FutsalOutfieldRole.Fixo => 5f,
            FutsalOutfieldRole.LeftAla or FutsalOutfieldRole.RightAla => 8f,
            FutsalOutfieldRole.Pivo => 11f,
            _ => 7f
        };
        var lateral = role switch
        {
            FutsalOutfieldRole.LeftAla => -attackDirection * 4f,
            FutsalOutfieldRole.RightAla => attackDirection * 4f,
            _ => 0f
        };
        return new Vector2(ownGoalX + attackDirection * depth, lateral);
    }

    private static Vector2 ClampToDefensiveStructure(
        Vector2 target,
        FieldDimensions field,
        TeamSide team,
        FutsalOutfieldRole role,
        float playerRadius)
    {
        var maximumDepth = role switch
        {
            FutsalOutfieldRole.Fixo => 9f,
            FutsalOutfieldRole.LeftAla or FutsalOutfieldRole.RightAla => 12f,
            FutsalOutfieldRole.Pivo => 15f,
            _ => 10f
        };
        var boundaryClearance = Math.Max(MinimumBoundaryClearance, playerRadius);
        var minX = field.Left + boundaryClearance;
        var maxX = field.Right - boundaryClearance;
        if (team == TeamSide.Home)
        {
            maxX = Math.Min(maxX, field.Left + maximumDepth);
        }
        else
        {
            minX = Math.Max(minX, field.Right - maximumDepth);
        }

        return new Vector2(
            Math.Clamp(target.X, minX, maxX),
            Math.Clamp(target.Y, field.Top + boundaryClearance, field.Bottom - boundaryClearance));
    }

    private static FutsalOutfieldRole PreferredOpponentRole(FutsalOutfieldRole defenderRole)
    {
        return defenderRole switch
        {
            FutsalOutfieldRole.Fixo => FutsalOutfieldRole.Pivo,
            FutsalOutfieldRole.LeftAla => FutsalOutfieldRole.RightAla,
            FutsalOutfieldRole.RightAla => FutsalOutfieldRole.LeftAla,
            FutsalOutfieldRole.Pivo => FutsalOutfieldRole.Fixo,
            _ => FutsalOutfieldRole.None
        };
    }

    private static int RoleOrder(FutsalOutfieldRole role)
    {
        return role switch
        {
            FutsalOutfieldRole.Fixo => 0,
            FutsalOutfieldRole.LeftAla => 1,
            FutsalOutfieldRole.RightAla => 2,
            FutsalOutfieldRole.Pivo => 3,
            _ => 4
        };
    }

    private static Vector2 SafeNormalize(Vector2 value, Vector2 fallback)
    {
        return value.LengthSquared() <= 0.0001f
            ? fallback
            : Vector2.Normalize(value);
    }
}

/// <summary>
/// Emits movement toward a defender's structured marking position.
/// </summary>
public sealed class MarkOpponentInstruction : IPlayerInstruction
{
    private const float ArrivalDistance = 0.6f;
    private readonly IPlayerPattern<MarkingOpportunity> _pattern;

    /// <summary>
    /// Creates the instruction with its independently replaceable marking recognizer.
    /// </summary>
    /// <param name="pattern">The recognizer that supplies this defender's mark and target.</param>
    public MarkOpponentInstruction(IPlayerPattern<MarkingOpportunity> pattern)
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
        var situationalScore = 1f / (1f + match.Distance);

        return new PlayerInstructionCandidate(
            PlayerInstructionKind.MarkOpponent,
            PlayerInstructionPriorityBand.TacticalOpportunity,
            situationalScore,
            new PlayerInput(move, ActionDown: false));
    }
}
