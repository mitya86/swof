using System.Numerics;

namespace Swof.Engine.AI;

/// <summary>
/// Describes the role target an outfielder should occupy to preserve the current team shape.
/// </summary>
/// <param name="Target">The shifted role target on the court.</param>
/// <param name="Distance">The player's current distance from the target.</param>
public readonly record struct FutsalShapeOpportunity(Vector2 Target, float Distance);

/// <summary>
/// Recognizes a player's individual target in the initial four-outfielder futsal shape.
/// </summary>
public sealed class FutsalTeamShapePattern : IPlayerPattern<FutsalShapeOpportunity>
{
    private const float LongitudinalFollow = 0.55f;
    private const float LateralFollow = 0.45f;
    private const float BallLongitudinalFollow = 0.25f;
    private const float BallLateralFollow = 0.35f;
    private const float BoundaryClearance = 1.5f;

    /// <inheritdoc />
    public bool TryRecognize(PlayerDecisionContext context, out FutsalShapeOpportunity match)
    {
        ArgumentNullException.ThrowIfNull(context);
        match = default;

        if (context.Match.IsBallOutOfPlay
            || context.Player.Role != PlayerRole.Outfield
            || context.Player.FutsalRole == FutsalOutfieldRole.None)
        {
            return false;
        }

        var roleTarget = GetRoleAnchor(
            context.Match.Field,
            context.Player.Team,
            context.Player.FutsalRole);
        var activeTeammate = context.Match.Players.FirstOrDefault(player =>
            player.Id == context.Match.ActivePlayerId
            && player.Team == context.Player.Team
            && player.Role == PlayerRole.Outfield
            && player.FutsalRole != FutsalOutfieldRole.None);
        var shift = activeTeammate is null
            ? new Vector2(
                context.Match.Ball.Position.X * BallLongitudinalFollow,
                context.Match.Ball.Position.Y * BallLateralFollow)
            : GetActiveTeammateShift(context.Match.Field, activeTeammate);
        var unclampedTarget = roleTarget + shift;
        var target = new Vector2(
            Math.Clamp(
                unclampedTarget.X,
                context.Match.Field.Left + BoundaryClearance,
                context.Match.Field.Right - BoundaryClearance),
            Math.Clamp(
                unclampedTarget.Y,
                context.Match.Field.Top + BoundaryClearance,
                context.Match.Field.Bottom - BoundaryClearance));

        match = new FutsalShapeOpportunity(
            target,
            Vector2.Distance(context.Player.Position, target));
        return true;
    }

    private static Vector2 GetActiveTeammateShift(
        FieldDimensions field,
        PlayerSnapshot activeTeammate)
    {
        var activeAnchor = GetRoleAnchor(field, activeTeammate.Team, activeTeammate.FutsalRole);
        var displacement = activeTeammate.Position - activeAnchor;
        return new Vector2(
            displacement.X * LongitudinalFollow,
            displacement.Y * LateralFollow);
    }

    private static Vector2 GetRoleAnchor(
        FieldDimensions field,
        TeamSide team,
        FutsalOutfieldRole role)
    {
        var attackDirection = team == TeamSide.Home ? 1f : -1f;
        var longitudinal = role switch
        {
            FutsalOutfieldRole.Fixo => -field.Length * 0.3f,
            FutsalOutfieldRole.LeftAla or FutsalOutfieldRole.RightAla => -field.Length * 0.175f,
            FutsalOutfieldRole.Pivo => -field.Length * 0.0625f,
            _ => 0f
        };
        var lateral = role switch
        {
            FutsalOutfieldRole.LeftAla => -attackDirection * field.Width * 0.25f,
            FutsalOutfieldRole.RightAla => attackDirection * field.Width * 0.25f,
            _ => 0f
        };
        return new Vector2(longitudinal * attackDirection, lateral);
    }
}

/// <summary>
/// Emits normal movement input toward the player's current futsal role target.
/// </summary>
public sealed class MaintainTeamShapeInstruction : IPlayerInstruction
{
    private const float ArrivalDistance = 0.65f;
    private readonly IPlayerPattern<FutsalShapeOpportunity> _pattern;

    /// <summary>
    /// Creates the instruction with its independently replaceable shape recognizer.
    /// </summary>
    /// <param name="pattern">The recognizer that supplies this player's shifted role target.</param>
    public MaintainTeamShapeInstruction(IPlayerPattern<FutsalShapeOpportunity> pattern)
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
        var situationalScore = Math.Clamp(
            match.Distance / MathF.Max(1f, context.Match.Field.Length),
            0f,
            1f);

        return new PlayerInstructionCandidate(
            PlayerInstructionKind.MaintainTeamShape,
            PlayerInstructionPriorityBand.Shape,
            situationalScore,
            new PlayerInput(move, ActionDown: false));
    }
}
