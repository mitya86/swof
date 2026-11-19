namespace Swof.Engine.AI;

/// <summary>One outfielder of a squad as a career keeps him.</summary>
/// <param name="Name">His name.</param>
/// <param name="Attributes">What his body and feet can do; these do not change.</param>
/// <param name="Habits">How he tends to play; these develop.</param>
/// <param name="Traits">What kind of person he is; these do not change.</param>
public sealed record SquadPlayer(string Name, PlayerAttributes Attributes, PlayerHabits Habits, PlayerTraits Traits);

/// <summary>How a squad develops from play, in a match or in training.</summary>
public static class Development
{
    /// <summary>
    /// The squad after some play: each player develops from his own spells by his traits, his fit with his
    /// team-mates and coach, and what his coach asks.
    /// </summary>
    /// <param name="squad">The squad, in the slots they played.</param>
    /// <param name="spells">Each player's spells, in the same order.</param>
    /// <param name="court">Every outfielder's spells added together, the other team's included.</param>
    /// <param name="coach">Their coach, if they have one.</param>
    public static IReadOnlyList<SquadPlayer> After(IReadOnlyList<SquadPlayer> squad, IReadOnlyList<PlayerSpells> spells, PlayerSpells court, CoachModel? coach = null)
    {
        ArgumentNullException.ThrowIfNull(squad);
        ArgumentNullException.ThrowIfNull(spells);
        if (spells.Count != squad.Count)
        {
            throw new ArgumentException("Every player needs his spells.", nameof(spells));
        }

        return [.. squad.Select((player, index) =>
        {
            var group = squad.Where((_, other) => other != index).Select(mate => mate.Traits.Temperament);
            var fit = PlayerTraits.Fit(player.Traits.Temperament, coach is null ? group : group.Append(coach.Temperament));
            return player with { Habits = player.Traits.Develop(player.Habits, spells[index], court, fit, coach?.Asks) };
        })];
    }
}

/// <summary>
/// Training between matches: short computer-only drills of the squad's four outfielders against four ordinary
/// sparring players, from which the squad develops as it would from a match. A seed replays the same session.
/// </summary>
public static class Training
{
    /// <summary>Seconds in each half of a drill.</summary>
    public const float DrillHalfSeconds = 60f;

    /// <summary>Drills in a session: together as long as a match, so a session teaches about what a match does.</summary>
    public const int DrillsPerSession = 3;

    /// <summary>
    /// Plays one session and returns the squad after it. The spells of all its drills are added up before the squad
    /// develops, because one short drill holds too few spells to compare.
    /// </summary>
    /// <param name="squad">The four outfielders, in the slots (fixo, left ala, right ala, pivo) they train in.</param>
    /// <param name="coach">Their coach, if they have one; the squad trains in his usual tactic.</param>
    /// <param name="seed">Chooses the small differences in computer decisions.</param>
    public static IReadOnlyList<SquadPlayer> Session(IReadOnlyList<SquadPlayer> squad, CoachModel? coach, int seed)
    {
        ArgumentNullException.ThrowIfNull(squad);
        if (squad.Count != 4)
        {
            throw new ArgumentException("A squad trains with four outfielders.", nameof(squad));
        }

        var spells = new PlayerSpells[squad.Count];
        var court = default(PlayerSpells);
        for (var drill = 0; drill < DrillsPerSession; drill++)
        {
            var (own, all) = Drill(squad, coach, unchecked(seed * DrillsPerSession + drill));
            for (var index = 0; index < spells.Length; index++)
            {
                spells[index] += own[index];
            }

            court += all;
        }

        return Development.After(squad, spells, court, coach);
    }

    /// <summary>Plays one drill and returns each squad player's spells and every outfielder's together.</summary>
    public static (PlayerSpells[] Own, PlayerSpells Court) Drill(IReadOnlyList<SquadPlayer> squad, CoachModel? coach, int seed)
    {
        ArgumentNullException.ThrowIfNull(squad);
        var tuning = PhysicsTuning.Default with { HalfLengthSeconds = DrillHalfSeconds };
        var attributes = squad.Select(player => player.Attributes).Concat(Enumerable.Repeat(PlayerAttributes.DefaultOutfield, 4)).ToArray();
        var engine = MatchEngine.CreateDefault(tuning, outfieldAttributes: attributes, seed: seed, userControlledTeam: null, startWithKickoff: true);
        var computers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
        // Home's outfielders are listed in the slots the squad was given.
        var ids = engine.State.Players.Where(player => player.Team == TeamSide.Home && !player.IsGoalkeeper).Select(player => player.Id).ToArray();
        for (var index = 0; index < squad.Count; index++)
        {
            computers.SetHabits(ids[index], squad[index].Habits);
            computers.SetTraits(ids[index], squad[index].Traits);
        }

        if (coach is not null)
        {
            computers.SetTactic(TeamSide.Home, coach.UsualTactic);
        }

        var ledger = new SpellLedger();
        var nobody = new Dictionary<int, PlayerInput>();
        // Full time comes after two halves and their stoppages; this is far beyond any drill and only stops a hang.
        var lastTick = (long)(2f * DrillHalfSeconds / tuning.TickSeconds) * 10;
        while (!engine.State.IsMatchOver)
        {
            if (engine.State.Tick > lastTick)
            {
                throw new InvalidOperationException($"A drill with seed {seed} did not reach full time.");
            }

            engine.StepTick(computers.CreateControlFrame(engine.CurrentSnapshot, nobody));
            ledger.Watch(engine.CurrentSnapshot);
        }

        var court = ledger.Counts.Values.Aggregate(default(PlayerSpells), (sum, spells) => sum + spells);
        return ([.. ids.Select(ledger.Of)], court);
    }
}
