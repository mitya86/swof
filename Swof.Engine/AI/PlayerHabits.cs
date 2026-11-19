namespace Swof.Engine.AI;

/// <summary>
/// How one computer player tends to play: the small per-player model that a career will later
/// grow from what works for him. Neutral habits leave every decision exactly as it was.
/// </summary>
/// <remarks>
/// A habit is kept only when there is an outcome of the player's own that can later be counted
/// for it (a carry that kept the ball, a pass that arrived), because that count is what will teach it.
/// </remarks>
public sealed record PlayerHabits
{
    public const float MinimumCarryPatience = 0.5f;
    public const float MaximumCarryPatience = 2f;
    public const float MinimumForwardRisk = -0.2f;
    public const float MaximumForwardRisk = 0.4f;

    /// <summary>Creates habits, refusing values outside the bounds a career may move them within.</summary>
    /// <param name="carryPatience">See <see cref="CarryPatience"/>.</param>
    /// <param name="forwardRisk">See <see cref="ForwardRisk"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is not finite or is out of its bounds.</exception>
    public PlayerHabits(float carryPatience = 1f, float forwardRisk = 0f)
    {
        CarryPatience = InRange(carryPatience, MinimumCarryPatience, MaximumCarryPatience, nameof(carryPatience));
        ForwardRisk = InRange(forwardRisk, MinimumForwardRisk, MaximumForwardRisk, nameof(forwardRisk));
    }

    /// <summary>
    /// How long the player keeps the ball before passing, as a multiple of the usual wait.
    /// A dribbler is above 1, a passer below. The quick release of a pressed carrier in its own
    /// third is a fix for losing the ball there, not a style, and is never scaled.
    /// </summary>
    public float CarryPatience { get; }

    /// <summary>How much more the player values carrying the ball toward the opposing goal than safe space.</summary>
    public float ForwardRisk { get; }

    /// <summary>Habits that change nothing.</summary>
    public static PlayerHabits Neutral { get; } = new();

    /// <summary>Keeps the ball and takes it forward himself.</summary>
    public static PlayerHabits Runner { get; } = new(carryPatience: 1.75f, forwardRisk: 0.3f);

    /// <summary>Moves the ball on early.</summary>
    public static PlayerHabits Passer { get; } = new(carryPatience: 0.6f);

    /// <summary>The presets by the name a launch option uses.</summary>
    public static IReadOnlyDictionary<string, PlayerHabits> ByName { get; } = new Dictionary<string, PlayerHabits>
    {
        ["neutral"] = Neutral,
        ["runner"] = Runner,
        ["passer"] = Passer
    };

    private static float InRange(float value, float minimum, float maximum, string name)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be from {minimum} to {maximum}.");
        }

        return value;
    }
}
