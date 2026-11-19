namespace Swof.Engine.AI;

/// <summary>
/// The human side of a computer player: how well he learns, how far he keeps to what he is asked, how he handles
/// pressure and what kind of person he is. Traits do not change; they decide how his habits develop and, in two
/// places, how he decides. Each runs from 0 to 1, and 0.5 changes nothing.
/// </summary>
public sealed record PlayerTraits
{
    /// <summary>
    /// The share of what a player learned that he gives up each match in a group he does not fit at all. A player
    /// whose fit is half or better gives up nothing; below that he gives up more the worse he fits.
    /// </summary>
    public const float FadePerMatch = 0.2f;

    /// <summary>The share of his usual patience a player with no composure loses when a close match is in its second half.</summary>
    public const float NervousRush = 0.4f;

    /// <summary>The share of slides through a carrier's back that a player with no discipline goes through with.</summary>
    public const float RecklessShare = 0.5f;

    /// <summary>Creates traits, refusing values that are not from 0 to 1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is not finite or not from 0 to 1.</exception>
    public PlayerTraits(float learning = 0.5f, float discipline = 0.5f, float composure = 0.5f, float temperament = 0.5f)
    {
        Learning = InRange(learning, nameof(learning));
        Discipline = InRange(discipline, nameof(discipline));
        Composure = InRange(composure, nameof(composure));
        Temperament = InRange(temperament, nameof(temperament));
    }

    /// <summary>How fast his habits follow what works for him: at 1 twice the usual pace, at 0 not at all.</summary>
    public float Learning { get; }

    /// <summary>How far he keeps to what he is asked. Below 0.5 he sometimes slides in where he would foul.</summary>
    public float Discipline { get; }

    /// <summary>How he handles pressure. Below 0.5 he gets rid of the ball sooner when a close match is in its second half.</summary>
    public float Composure { get; }

    /// <summary>What kind of person he is, from quiet (0) to fiery (1). Only the distance from his group's matters.</summary>
    public float Temperament { get; }

    /// <summary>Traits that change nothing.</summary>
    public static PlayerTraits Neutral { get; } = new();

    /// <summary>Loses his head when it matters.</summary>
    public static PlayerTraits Nervous { get; } = new(composure: 0f);

    /// <summary>Slides in whatever it costs.</summary>
    public static PlayerTraits Reckless { get; } = new(discipline: 0f);

    /// <summary>The presets by the name a launch option uses.</summary>
    public static IReadOnlyDictionary<string, PlayerTraits> ByName { get; } = new Dictionary<string, PlayerTraits>
    {
        ["neutral"] = Neutral,
        ["nervous"] = Nervous,
        ["reckless"] = Reckless
    };

    /// <summary>
    /// How well a player fits his group, from 0 to 1: 1 when his temperament is the group's average, 0 when it is
    /// half the scale or more away from it. A player with no group fits.
    /// </summary>
    /// <param name="temperament">His temperament.</param>
    /// <param name="group">The temperaments of the people he plays with, not his own.</param>
    public static float Fit(float temperament, IEnumerable<float> group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var others = group.ToArray();
        return others.Length == 0 ? 1f : Math.Clamp(1f - 2f * MathF.Abs(temperament - others.Average()), 0f, 1f);
    }

    /// <summary>
    /// The habits he comes out of a match with. He learns from his own spells at a pace set by his learning and his
    /// fit, and gives up part of what he had learned the worse he fits, so the same player flourishes in one group
    /// and fades in another. Neutral traits in a group he fits learn exactly as <see cref="PlayerHabits.After"/>.
    /// </summary>
    /// <param name="habits">His habits before the match.</param>
    /// <param name="own">His own spells in the match.</param>
    /// <param name="court">Every outfielder's spells in the match added together.</param>
    /// <param name="fit">How well he fits his group, from <see cref="Fit"/>.</param>
    public PlayerHabits Develop(PlayerHabits habits, PlayerSpells own, PlayerSpells court, float fit)
    {
        ArgumentNullException.ThrowIfNull(habits);
        if (!float.IsFinite(fit) || fit < 0f || fit > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(fit), fit, "Fit must be from 0 to 1.");
        }

        return habits.After(own, court, rate: 2f * Learning * fit).Toward(PlayerHabits.Neutral, FadePerMatch * MathF.Max(0f, 1f - 2f * fit));
    }

    /// <summary>
    /// What his patience is multiplied by now. A player with composure below 0.5 rushes when the score is within a
    /// goal in the second half.
    /// </summary>
    /// <remarks>ponytail: the whole second half counts as late, because a snapshot does not carry the match length.</remarks>
    public float PatienceNow(MatchSnapshot match)
    {
        ArgumentNullException.ThrowIfNull(match);
        var stressed = match.Period >= 2 && Math.Abs(match.HomeScore - match.AwayScore) <= 1;
        return stressed ? 1f - NervousRush * MathF.Max(0f, 1f - 2f * Composure) : 1f;
    }

    /// <summary>Gets the share of slides through a carrier's back he goes through with.</summary>
    public float RecklessChance => RecklessShare * MathF.Max(0f, 1f - 2f * Discipline);

    private static float InRange(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f || value > 1f)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be from 0 to 1.");
        }

        return value;
    }
}
