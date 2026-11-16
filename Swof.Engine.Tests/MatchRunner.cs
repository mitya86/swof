using Swof.Engine;
using Swof.Engine.AI;

/// <summary>
/// Plays whole computer-versus-computer matches with nobody watching.
/// Usage: <c>dotnet run --project Swof.Engine.Tests -- matches [count] [firstSeed] [equal]</c>.
/// With <c>equal</c> every outfielder has the same attributes, so the scores show the game
/// rather than a mismatch.
/// </summary>
internal static class MatchRunner
{
    // Measured over 100 seeds: the same player (or nobody) never keeps the ball longer than
    // 5 s, the half-time pause included. Four times that is a dead match, not a slow one.
    private const int StallTicks = 20 * 60;
    private const int SoonAfterKickoffTicks = 10 * 60;

    internal sealed record MatchResult(
        int Seed,
        string? Fault,
        long Ticks,
        int HomeScore,
        int AwayScore,
        int Shots,
        int Passes,
        int ShotWindUps,
        int Slides,
        int Fouls,
        int Stoppages,
        float KeeperShare,
        long LongestSamePossessorTicks,
        string GoalTicks,
        int HomeShotsFirstHalf,
        int HomeShotsSecondHalf,
        int AwayShotsFirstHalf,
        int AwayShotsSecondHalf,
        int HomeGoalsFirstHalf,
        int AwayGoalsFirstHalf,
        TeamSide? FirstGoalBy,
        int ShotsSoonAfterKickoffByTaker,
        int ShotsSoonAfterKickoffByOpponent,
        int Possessions,
        float ForwardMetres,
        int LostInOwnThird,
        int FourSecondCalls,
        int SpotKicks,
        int CoachChanges = 0);

    /// <summary>
    /// The seed chooses the eight outfielders' attributes (unless the squads are equal) and the
    /// small differences in computer decisions. Goalkeepers stay fixed so that shot numbers
    /// compare across seeds.
    /// </summary>
    internal static MatchResult Play(
        int seed,
        float halfLengthSeconds = 0f,
        bool equalSquads = false,
        TeamSide? userControlledTeam = null,
        TeamTactic? homeTactic = null,
        TeamTactic? awayTactic = null,
        Func<float, TacticByScore>? homeRule = null,
        Func<float, TacticByScore>? awayRule = null,
        bool homeCoach = false,
        bool awayCoach = false)
    {
        var random = new Random(seed);
        // 4 to 15 is the range the default squads already use.
        byte Roll() => (byte)random.Next(4, 16);
        var squad = Enumerable.Range(0, 8)
            .Select(_ => new PlayerAttributes(Roll(), Roll(), Roll(), Roll(), Roll(), Roll(), Roll()))
            .ToArray();
        var tuning = halfLengthSeconds > 0f
            ? PhysicsTuning.Default with { HalfLengthSeconds = halfLengthSeconds }
            : PhysicsTuning.Default;
        if (equalSquads)
        {
            Array.Fill(squad, PlayerAttributes.DefaultOutfield);
        }

        var engine = MatchEngine.CreateDefault(tuning, outfieldAttributes: squad, seed: seed, userControlledTeam: userControlledTeam, startWithKickoff: true);
        var computers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
        computers.SetTactic(TeamSide.Home, homeTactic ?? TeamTactic.HighPress);
        computers.SetTactic(TeamSide.Away, awayTactic ?? TeamTactic.HighPress);
        if (homeRule is not null)
        {
            computers.SetTacticByScore(TeamSide.Home, homeRule(tuning.HalfLengthSeconds * 2f));
        }

        if (awayRule is not null)
        {
            computers.SetTacticByScore(TeamSide.Away, awayRule(tuning.HalfLengthSeconds * 2f));
        }

        if (homeCoach)
        {
            computers.SetCoach(TeamSide.Home, new Coach(TeamSide.Home, TacticByScore.ChaseAndHold(tuning.HalfLengthSeconds * 2f)));
        }

        if (awayCoach)
        {
            computers.SetCoach(TeamSide.Away, new Coach(TeamSide.Away, TacticByScore.ChaseAndHold(tuning.HalfLengthSeconds * 2f)));
        }

        var state = engine.State;
        var nobody = new Dictionary<int, PlayerInput>();
        // Stoppages add to the clock; three times the playing time is far beyond any real match.
        var tickCap = (long)(tuning.HalfLengthSeconds * 2f * 3f / tuning.TickSeconds);

        int shots = 0, passes = 0, windUps = 0, slides = 0, fouls = 0, stoppages = 0, lastScore = 0;
        long lastFeedback = 0, keeperTicks = 0, sameFor = 0, longestSame = 0;
        int? lastPossessor = null;
        var shooting = new HashSet<int>();
        var sliding = new HashSet<int>();
        var wasOut = false;
        var goalTicks = new List<long>();
        // Indexed [team, half]: the two teams are meant to be mirror images, so a lasting
        // difference between these says where one of them is favoured.
        var shotsBy = new int[2, 2];
        int homeGoalsFirstHalf = 0, awayGoalsFirstHalf = 0, lastHome = 0;
        TeamSide? firstGoalBy = null;
        // Taking a kickoff after conceding should not be a handicap: if the team that just
        // scored gets most of the next shots, one goal leads to another.
        TeamSide? kickoffTaker = null;
        long kickoffTick = 0;
        int shotsByTaker = 0, shotsByOpponent = 0;
        int fourSecondCalls = 0, spotKicks = 0;
        // A possession runs from a team gaining the ball to the other team gaining it. How far
        // toward the opposing goal the ball got in that time says whether teams attack or retreat.
        TeamSide? holders = null;
        float gainedAtX = 0f, lastHeldX = 0f, forwardMetres = 0f;
        int possessions = 0, lostInOwnThird = 0;
        string? fault = null;

        while (!state.IsMatchOver && fault is null)
        {
            engine.StepTick(computers.CreateControlFrame(engine.CurrentSnapshot, nobody));

            // Feedback events are the engine's own record of a struck ball.
            foreach (var feedback in state.FeedbackEvents.Where(e => e.Sequence > lastFeedback))
            {
                lastFeedback = feedback.Sequence;
                shots += feedback.Type == GameplayFeedbackType.ShotContact ? 1 : 0;
                if (feedback.Type == GameplayFeedbackType.ShotContact && feedback.Team is { } team)
                {
                    if (kickoffTaker is not null && state.Tick - kickoffTick < SoonAfterKickoffTicks)
                    {
                        shotsByTaker += team == kickoffTaker ? 1 : 0;
                        shotsByOpponent += team == kickoffTaker ? 0 : 1;
                    }

                    shotsBy[team == TeamSide.Home ? 0 : 1, Math.Min(state.Period, 2) - 1]++;
                }

                passes += feedback.Type == GameplayFeedbackType.PassContact ? 1 : 0;
            }

            // A computer player that starts winding up a shot; far more of these than
            // shots means the decision is flickering and the wind-up comes out as a pass.
            foreach (var (id, decision) in computers.LastDecisions)
            {
                if (decision.Kind != PlayerInstructionKind.ShootAtGoal || !decision.Input.ActionDown)
                {
                    shooting.Remove(id);
                }
                else if (shooting.Add(id))
                {
                    windUps++;
                }
            }

            foreach (var player in state.Players)
            {
                if (!player.IsSliding)
                {
                    sliding.Remove(player.Id);
                }
                else if (sliding.Add(player.Id))
                {
                    slides++;
                }

                if (!InBounds(player.Position, state.Field))
                {
                    fault = $"player {player.Id} left the hall at tick {state.Tick}";
                }
            }

            if (!InBounds(state.Ball.Position, state.Field))
            {
                fault = $"ball left the hall at tick {state.Tick}";
            }

            if (state.IsBallOutOfPlay && !wasOut)
            {
                stoppages++;
                if (state.LastEvent.Type == MatchEventType.Foul)
                {
                    fouls++;
                }

                fourSecondCalls += state.LastEvent.Type == MatchEventType.FourSeconds ? 1 : 0;
                spotKicks += state.LastEvent.Restart is RestartKind.Penalty or RestartKind.TenMetreKick ? 1 : 0;
            }

            wasOut = state.IsBallOutOfPlay;

            var possessor = state.Players.FirstOrDefault(player => player.HasPossession);
            if (possessor?.IsGoalkeeper == true)
            {
                keeperTicks++;
            }

            if (possessor is not null)
            {
                if (possessor.Team != holders)
                {
                    if (holders is { } losers)
                    {
                        var attack = losers == TeamSide.Home ? 1f : -1f;
                        possessions++;
                        forwardMetres += (lastHeldX - gainedAtX) * attack;
                        lostInOwnThird += lastHeldX * attack < -state.Field.Length / 6f ? 1 : 0;
                    }

                    holders = possessor.Team;
                    gainedAtX = state.Ball.Position.X;
                }

                lastHeldX = state.Ball.Position.X;
            }

            sameFor = possessor?.Id == lastPossessor ? sameFor + 1 : 0;
            lastPossessor = possessor?.Id;
            longestSame = Math.Max(longestSame, sameFor);

            if (state.HomeScore + state.AwayScore != lastScore)
            {
                lastScore = state.HomeScore + state.AwayScore;
                goalTicks.Add(state.Tick);
                var scorer = state.HomeScore > lastHome ? TeamSide.Home : TeamSide.Away;
                firstGoalBy ??= scorer;
                kickoffTaker = scorer == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                kickoffTick = state.Tick;
                lastHome = state.HomeScore;
                if (state.Period == 1)
                {
                    homeGoalsFirstHalf = state.HomeScore;
                    awayGoalsFirstHalf = state.AwayScore;
                }
            }

            if (sameFor > StallTicks)
            {
                fault = $"nothing changed hands for {StallTicks / 60} s before tick {state.Tick}";
            }

            if (state.Tick > tickCap)
            {
                fault = $"no full time after {tickCap} ticks";
            }
        }

        return new MatchResult(
            seed,
            fault,
            state.Tick,
            state.HomeScore,
            state.AwayScore,
            shots,
            passes,
            windUps,
            slides,
            fouls,
            stoppages,
            keeperTicks / (float)Math.Max(1, state.Tick),
            longestSame,
            string.Join(',', goalTicks),
            shotsBy[0, 0],
            shotsBy[0, 1],
            shotsBy[1, 0],
            shotsBy[1, 1],
            homeGoalsFirstHalf,
            awayGoalsFirstHalf,
            firstGoalBy,
            shotsByTaker,
            shotsByOpponent,
            possessions,
            forwardMetres,
            lostInOwnThird,
            fourSecondCalls,
            spotKicks,
            computers.CoachChanges);
    }

    /// <summary>Plays <paramref name="count"/> matches and returns how many broke.</summary>
    internal static int Run(
        int count,
        int firstSeed,
        bool equalSquads,
        TeamTactic? homeTactic = null,
        TeamTactic? awayTactic = null,
        Func<float, TacticByScore>? homeRule = null,
        Func<float, TacticByScore>? awayRule = null,
        bool homeCoach = false,
        bool awayCoach = false)
    {
        var results = Enumerable.Range(firstSeed, count)
            .Select(seed => Play(seed, equalSquads: equalSquads, homeTactic: homeTactic, awayTactic: awayTactic, homeRule: homeRule, awayRule: awayRule, homeCoach: homeCoach, awayCoach: awayCoach))
            .ToArray();
        foreach (var r in results)
        {
            Console.WriteLine(
                $"{(r.Fault is null ? "OK  " : "FAIL")} seed {r.Seed,4}  {r.HomeScore}-{r.AwayScore}  shots {r.Shots,3} (wind-ups {r.ShotWindUps,4})  passes {r.Passes,3}  slides {r.Slides,3}  fouls {r.Fouls,2}  " +
                $"stoppages {r.Stoppages,3}  keeper {r.KeeperShare:P0}  longest same possessor {r.LongestSamePossessorTicks / 60f:F1}s" +
                (r.Fault is null ? "" : $"  {r.Fault}"));
        }

        void Spread(string name, Func<MatchResult, float> pick) =>
            Console.WriteLine($"{name,-22} mean {results.Average(pick):F1}  min {results.Min(pick):F1}  max {results.Max(pick):F1}");

        Console.WriteLine();
        Spread("goals", r => r.HomeScore + r.AwayScore);
        Spread("shots", r => r.Shots);
        Spread("shot wind-ups", r => r.ShotWindUps);
        Spread("passes", r => r.Passes);
        Spread("slides", r => r.Slides);
        Spread("fouls", r => r.Fouls);
        Spread("four-second calls", r => r.FourSecondCalls);
        Spread("penalties and 10 m kicks", r => r.SpotKicks);
        if (homeCoach || awayCoach)
        {
            Spread("coach tactic changes", r => r.CoachChanges);
        }

        Spread("stoppages", r => r.Stoppages);
        Spread("possessions", r => r.Possessions);
        Spread("forward m / possession", r => r.ForwardMetres / Math.Max(1, r.Possessions));
        Spread("lost in own third", r => r.LostInOwnThird);
        Spread("keeper share %", r => r.KeeperShare * 100f);
        Spread("longest possessor s", r => r.LongestSamePossessorTicks / 60f);
        var distinct = results.Select(r => $"{r.HomeScore}-{r.AwayScore}:{r.GoalTicks}:{r.Shots}:{r.Passes}:{r.Slides}").Distinct().Count();
        var home = results.Count(r => r.HomeScore > r.AwayScore);
        var away = results.Count(r => r.AwayScore > r.HomeScore);
        Console.WriteLine($"{distinct} different matches of {count}; home wins {home}, draws {count - home - away}, away wins {away}");

        Console.WriteLine(
            $"first half   home {results.Sum(r => r.HomeGoalsFirstHalf)} goals from {results.Sum(r => r.HomeShotsFirstHalf)} shots, " +
            $"away {results.Sum(r => r.AwayGoalsFirstHalf)} from {results.Sum(r => r.AwayShotsFirstHalf)}");
        Console.WriteLine(
            $"second half  home {results.Sum(r => r.HomeScore - r.HomeGoalsFirstHalf)} goals from {results.Sum(r => r.HomeShotsSecondHalf)} shots, " +
            $"away {results.Sum(r => r.AwayScore - r.AwayGoalsFirstHalf)} from {results.Sum(r => r.AwayShotsSecondHalf)}");
        Console.WriteLine(
            $"first goal   home {results.Count(r => r.FirstGoalBy == TeamSide.Home)}, away {results.Count(r => r.FirstGoalBy == TeamSide.Away)}");

        Console.WriteLine(
            $"10 s after a goal  kickoff taker {results.Sum(r => r.ShotsSoonAfterKickoffByTaker)} shots, team that scored {results.Sum(r => r.ShotsSoonAfterKickoffByOpponent)}");

        // Every real match has Home as the person's team, which switches the active player and
        // makes Home's goalkeeper wait. Those paths must still finish a match, so a few seeds
        // are replayed that way; they lean toward Away and stay out of the figures above.
        var withUserTeam = Enumerable.Range(firstSeed, Math.Min(count, 10))
            .Select(seed => Play(seed, equalSquads: equalSquads, userControlledTeam: TeamSide.Home))
            .ToArray();
        foreach (var r in withUserTeam.Where(r => r.Fault is not null))
        {
            Console.WriteLine($"FAIL seed {r.Seed,4} with Home as the user team  {r.Fault}");
        }

        var broken = results.Concat(withUserTeam).Count(r => r.Fault is not null);
        Console.WriteLine(broken == 0 ? $"All {count} matches reached full time, and {withUserTeam.Length} more with Home as the user team." : $"{broken} of {count + withUserTeam.Length} matches broke.");
        return broken + FailedMarks(results, equalSquads);
    }

    /// <summary>
    /// Pass marks for the game as a whole, over at least 100 matches. They are wide: they catch a
    /// change that breaks the shape of a match or the balance between the two teams, not one that
    /// moves a figure a little. Balance is only judged with identical squads.
    /// </summary>
    private static int FailedMarks(MatchResult[] results, bool equalSquads)
    {
        if (results.Length < 100)
        {
            return 0;
        }

        var goals = results.Average(r => r.HomeScore + r.AwayScore);
        var shots = results.Average(r => r.Shots);
        var possessions = results.Average(r => r.Possessions);
        var forward = results.Average(r => r.ForwardMetres / Math.Max(1, r.Possessions));
        var winGap = Math.Abs(results.Count(r => r.HomeScore > r.AwayScore) - results.Count(r => r.AwayScore > r.HomeScore)) * 100 / results.Length;
        var byTaker = results.Sum(r => r.ShotsSoonAfterKickoffByTaker);
        var byScorer = results.Sum(r => r.ShotsSoonAfterKickoffByOpponent);
        List<(string Mark, string Was, bool Met)> marks =
        [
            ("3 to 9 goals a match", $"{goals:0.0}", goals is >= 3 and <= 9),
            ("15 to 35 shots a match", $"{shots:0.0}", shots is >= 15 and <= 35),
            ("at most 200 possessions a match", $"{possessions:0.0}", possessions <= 200),
            ("the ball goes forward at least 3.5 m a possession", $"{forward:0.0}", forward >= 3.5f),
        ];
        if (equalSquads)
        {
            marks.Add(("neither team wins 25 more matches in 100 than the other", $"{winGap}", winGap <= 25));
            marks.Add(("after a goal the team that scored does not take twice the shots of the team kicking off", $"{byScorer} to {byTaker}", byScorer <= byTaker * 2));
        }

        foreach (var (mark, was, _) in marks.Where(m => !m.Met))
        {
            Console.WriteLine($"MARK MISSED: {mark} (was {was})");
        }

        Console.WriteLine($"{marks.Count(m => m.Met)} of {marks.Count} pass marks met.");
        return marks.Count(m => !m.Met);
    }

    private static bool InBounds(System.Numerics.Vector2 position, FieldDimensions field) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y)
        && MathF.Abs(position.X) < field.Length && MathF.Abs(position.Y) < field.Width;
}
