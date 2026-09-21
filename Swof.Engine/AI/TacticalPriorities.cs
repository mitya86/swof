using System.Collections.ObjectModel;

namespace Swof.Engine.AI;

/// <summary>
/// Supplies tactic-specific score modifiers without changing instruction logic or player physics.
/// </summary>
public sealed class TacticalPriorities
{
    private readonly IReadOnlyDictionary<PlayerInstructionKind, float> _modifiers;

    /// <summary>
    /// Creates a tactical-priority profile.
    /// </summary>
    /// <param name="modifiers">Score modifiers keyed by instruction kind.</param>
    /// <exception cref="ArgumentException">A modifier targets <c>None</c> or is not finite.</exception>
    public TacticalPriorities(IReadOnlyDictionary<PlayerInstructionKind, float> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        var copy = new Dictionary<PlayerInstructionKind, float>(modifiers.Count);
        foreach (var (kind, modifier) in modifiers)
        {
            if (kind == PlayerInstructionKind.None)
            {
                throw new ArgumentException("A tactical modifier must target a concrete instruction.", nameof(modifiers));
            }

            if (!float.IsFinite(modifier))
            {
                throw new ArgumentException("Tactical modifiers must be finite.", nameof(modifiers));
            }

            copy.Add(kind, modifier);
        }

        _modifiers = new ReadOnlyDictionary<PlayerInstructionKind, float>(copy);
    }

    /// <summary>Gets a profile that leaves every situational score unchanged.</summary>
    public static TacticalPriorities Neutral { get; } = new(
        new Dictionary<PlayerInstructionKind, float>());

    /// <summary>
    /// Gets the score modifier for one instruction.
    /// </summary>
    /// <param name="kind">The instruction being evaluated.</param>
    /// <returns>The configured modifier, or zero when no modifier is configured.</returns>
    public float GetModifier(PlayerInstructionKind kind)
    {
        return _modifiers.TryGetValue(kind, out var modifier) ? modifier : 0f;
    }
}
