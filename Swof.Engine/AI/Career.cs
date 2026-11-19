using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Swof.Engine.AI;

/// <summary>
/// A squad of four outfielders and their coach, kept from match to match: after each match the squad develops from
/// how it played, the coach remembers how his tactic did, and the squad trains once. Saved to a file between launches.
/// </summary>
public sealed class Career
{
    /// <summary>The version a career file is written with; a file with another is refused, never guessed at.</summary>
    public const int FileVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { KeepConstructorValues } }
    };

    private static readonly string[] Names =
        ["Ari", "Bento", "Caio", "Dani", "Edu", "Fabio", "Gil", "Hugo", "Ivo", "Joao", "Kiko", "Leo", "Max", "Nuno", "Rui", "Teo"];

    private Career(int seed, int matchesPlayed, IReadOnlyList<SquadPlayer> squad, CoachModel coach)
    {
        Seed = seed;
        MatchesPlayed = matchesPlayed;
        Squad = squad;
        Coach = coach;
    }

    /// <summary>Gets the seed the career was rolled from; with the matches played it chooses each training session.</summary>
    public int Seed { get; }

    /// <summary>Gets how many matches the career has finished.</summary>
    public int MatchesPlayed { get; private set; }

    /// <summary>Gets the four outfielders in the slots (fixo, left ala, right ala, pivo) they last played.</summary>
    public IReadOnlyList<SquadPlayer> Squad { get; private set; }

    /// <summary>Gets the coach.</summary>
    public CoachModel Coach { get; }

    /// <summary>Rolls a new career: four outfielders' attributes and traits, and a coach. A seed always rolls the same one.</summary>
    public static Career New(int seed)
    {
        // A seeded Random is enough here: a career is rolled once and saved, so nothing replays the roll later.
        var random = new Random(seed);
        var names = Names.OrderBy(_ => random.Next()).Take(4).ToArray();
        // 4 to 15 is the range the match runner's squads use.
        byte Attribute() => (byte)random.Next(4, 16);
        var squad = names
            .Select(name => new SquadPlayer(
                name,
                new PlayerAttributes(Attribute(), Attribute(), Attribute(), Attribute(), Attribute(), Attribute(), Attribute()),
                PlayerHabits.Neutral,
                new PlayerTraits(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle())))
            .ToArray();
        return new Career(seed, 0, squad, new CoachModel(random.NextSingle(), random.NextSingle()));
    }

    /// <summary>The squad in the slots the coach puts them in for the next match.</summary>
    public IReadOnlyList<SquadPlayer> Lineup() => [.. Coach.Arrange([.. Squad.Select(player => player.Attributes)]).Select(index => Squad[index])];

    /// <summary>
    /// Finishes a match played by <see cref="Lineup"/>: the squad develops from its spells, the coach records the
    /// result of the tactic he started with, and the squad trains once.
    /// </summary>
    /// <param name="spells">Each lineup player's spells, in lineup order.</param>
    /// <param name="court">Every outfielder's spells in the match added together.</param>
    /// <param name="tactic">The tactic the match was started with.</param>
    /// <param name="goalDifference">The squad's goals minus the other team's.</param>
    public void AfterMatch(IReadOnlyList<PlayerSpells> spells, PlayerSpells court, TeamTactic tactic, int goalDifference)
    {
        var played = Development.After(Lineup(), spells, court, Coach);
        Coach.Record(tactic, goalDifference);
        Squad = Training.Session(played, Coach, unchecked(Seed * 7919 + MatchesPlayed));
        MatchesPlayed++;
    }

    /// <summary>Loads the career at <paramref name="path"/>, or rolls a new one from <paramref name="seed"/> when there is no file.</summary>
    /// <exception cref="InvalidDataException">The file is not a career of this version; it is left as it is.</exception>
    public static Career LoadOrNew(string path, int seed) => File.Exists(path) ? FromJson(File.ReadAllText(path)) : New(seed);

    /// <summary>Saves the career to <paramref name="path"/>, replacing the old file only once the new one is written.</summary>
    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var written = path + ".new";
        File.WriteAllText(written, ToJson());
        File.Move(written, path, overwrite: true);
    }

    /// <summary>The career as JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(
        new CareerFile(
            FileVersion,
            Seed,
            MatchesPlayed,
            [.. Squad],
            new CoachFile(
                Coach.Insight,
                Coach.Temperament,
                Coach.Asks,
                [.. CoachModel.Tactics.Select(tactic => new TacticFile(tactic.Name, Coach.ResultsWith(tactic).Matches, Coach.ResultsWith(tactic).GoalDifference))])),
        JsonOptions);

    /// <summary>Reads a career from JSON.</summary>
    /// <exception cref="InvalidDataException">The JSON is not a career of this version.</exception>
    public static Career FromJson(string json)
    {
        CareerFile? file;
        try
        {
            file = JsonSerializer.Deserialize<CareerFile>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("This is not a career file.", exception);
        }

        if (file?.Version != FileVersion || file.Squad is not { Length: 4 } || file.Coach?.Results is null || file.Coach.Asks is null
            || file.Squad.Any(player => player?.Name is null || player.Attributes is null || player.Habits is null || player.Traits is null)
            || file.Coach.Results.Any(result => result?.Tactic is null))
        {
            throw new InvalidDataException($"A career file must be version {FileVersion} with four players and a coach.");
        }

        var coach = new CoachModel(file.Coach.Insight, file.Coach.Temperament, file.Coach.Asks);
        foreach (var result in file.Coach.Results)
        {
            var tactic = CoachModel.Tactics.FirstOrDefault(tactic => tactic.Name == result.Tactic)
                ?? throw new InvalidDataException($"The career file names a tactic there is not: {result.Tactic}.");
            coach.Restore(tactic, result.Matches, result.GoalDifference);
        }

        return new Career(file.Seed, file.MatchesPlayed, file.Squad, coach);
    }

    /// <summary>A file keeps what rebuilds each object, not what is worked out from it (effective attributes and the like).</summary>
    private static void KeepConstructorValues(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        var parameters = info.Type.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var property in info.Properties.Where(property => !parameters.Contains(property.Name)).ToArray())
        {
            info.Properties.Remove(property);
        }
    }

    private sealed record CareerFile(int Version, int Seed, int MatchesPlayed, SquadPlayer[] Squad, CoachFile Coach);

    private sealed record CoachFile(float Insight, float Temperament, PlayerHabits Asks, TacticFile[] Results);

    private sealed record TacticFile(string Tactic, int Matches, int GoalDifference);
}
