namespace Swof.Engine;

/// <summary>
/// Identifies a stable outfield responsibility in the initial four-player futsal shape.
/// </summary>
public enum FutsalOutfieldRole
{
    /// <summary>No futsal outfield responsibility is assigned.</summary>
    None = 0,

    /// <summary>The deepest outfielder who balances and protects the team.</summary>
    Fixo = 1,

    /// <summary>The left-sided wide player.</summary>
    LeftAla = 2,

    /// <summary>The right-sided wide player.</summary>
    RightAla = 3,

    /// <summary>The highest outfielder who provides depth near the opposing goal.</summary>
    Pivo = 4
}
