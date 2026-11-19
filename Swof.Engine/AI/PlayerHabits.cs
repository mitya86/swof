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

    /// <summary>Fewer spells than this on either side of a comparison teach nothing: the share kept would be mostly chance.</summary>
    public const int MinimumSpellsToCompare = 5;

    /// <summary>Patience gained for each unit by which long holds beat short ones for this player, more than for the court.</summary>
    public const float PatiencePerKeptShare = 0.1f;

    /// <summary>The share of a habit's distance from neutral it gives up each match, so habits settle rather than run to a bound.</summary>
    public const float Settling = 0.05f;

    /// <summary>
    /// The habits a player comes out of a match with. He compares his own spells with each other: if his long holds
    /// kept the ball better than his quick releases, by more than they did for everyone on the court, he becomes more
    /// patient. Comparing a player with himself cancels his position on the court, and comparing that with the court
    /// cancels what is true of every player (a long hold is always riskier), leaving what his attributes make of it.
    /// Each match also gives up a little of every habit's distance from neutral, so habits settle where the evidence
    /// and that pull balance.
    /// </summary>
    /// <param name="own">The player's own spells in the match.</param>
    /// <param name="court">Every outfielder's spells in the match added together, his own included.</param>
    /// <param name="rate">How fast he learns: 1 is the usual pace, 0 changes nothing.</param>
    /// <returns>The new habits, kept within their bounds.</returns>
    /// <remarks>
    /// Nothing here is random, so a career replays from its seeds.
    /// ponytail: forward risk only settles and does not learn. Judged by the share kept alone, a forward carry always
    /// looks worse than a safe pass, and teams that learned it that way lost goals; it learns once a spell's worth
    /// counts the ground gained.
    /// </remarks>
    public PlayerHabits After(PlayerSpells own, PlayerSpells court, float rate = 1f)
    {
        if (!float.IsFinite(rate) || rate < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "The learning rate must be finite and not negative.");
        }

        var patience = CarryPatience + rate * (PatiencePerKeptShare * Edge(own.Long, own.Short, court.Long, court.Short) - Settling * (CarryPatience - 1f));
        var risk = ForwardRisk - rate * Settling * ForwardRisk;
        return new PlayerHabits(
            Math.Clamp(patience, MinimumCarryPatience, MaximumCarryPatience),
            Math.Clamp(risk, MinimumForwardRisk, MaximumForwardRisk));
    }

    /// <summary>How much better spells of one kind kept the ball than the rest for the player, beyond the same for the court.</summary>
    private static float Edge(SpellEnds own, SpellEnds ownRest, SpellEnds court, SpellEnds courtRest)
    {
        var counted = own.Spells >= MinimumSpellsToCompare && ownRest.Spells >= MinimumSpellsToCompare;
        return counted ? own.KeptShare - ownRest.KeptShare - (court.KeptShare - courtRest.KeptShare) : 0f;
    }

    private static float InRange(float value, float minimum, float maximum, string name)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be from {minimum} to {maximum}.");
        }

        return value;
    }
}
