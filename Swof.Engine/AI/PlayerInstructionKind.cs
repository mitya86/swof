namespace Swof.Engine.AI;

/// <summary>
/// Identifies an independently selectable instruction available to a computer-controlled player.
/// </summary>
public enum PlayerInstructionKind
{
    /// <summary>No instruction is currently executable.</summary>
    None = 0,

    /// <summary>Keep the player's current position.</summary>
    MaintainPosition = 1,

    /// <summary>Move toward a loose ball when this player is the team's closest eligible outfielder.</summary>
    RecoverLooseBall = 2,

    /// <summary>Move toward the player's futsal role target as the team shifts around play.</summary>
    MaintainTeamShape = 3,

    /// <summary>Move into open space where the teammate in possession can make a safe pass.</summary>
    OfferSafePass = 4,

    /// <summary>Track an assigned opponent from a compact, goal-side defensive position.</summary>
    MarkOpponent = 5,

    /// <summary>Carry into safer space and release a safe pass while this player possesses the ball.</summary>
    RetainPossession = 6,

    /// <summary>Close down the opposing ball carrier from a goal-side position.</summary>
    PressureBallCarrier = 7,

    /// <summary>Protect the space behind the player pressing the opposing ball carrier.</summary>
    CoverBallCarrier = 8,

    /// <summary>Shoot at goal with a held action.</summary>
    ShootAtGoal = 9
}
