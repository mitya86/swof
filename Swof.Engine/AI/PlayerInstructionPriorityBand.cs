namespace Swof.Engine.AI;

/// <summary>
/// Separates instructions by urgency before their contextual scores are compared.
/// </summary>
public enum PlayerInstructionPriorityBand
{
    /// <summary>No instruction priority.</summary>
    None = 0,

    /// <summary>Shape maintenance and other low-urgency positioning.</summary>
    Shape = 1,

    /// <summary>A tactical opportunity that may improve the current phase of play.</summary>
    TacticalOpportunity = 2,

    /// <summary>An immediate situation that should normally interrupt positional behavior.</summary>
    Urgent = 3,

    /// <summary>An emergency such as preventing an imminent goal.</summary>
    Emergency = 4
}
