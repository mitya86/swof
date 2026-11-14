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
        TeamSide? FirstGoalBy);

    /// <summary>
    /// The seed chooses the eight outfielders' attributes (unless the squads are equal) and the
    /// small differences in computer decisions. Goalkeepers stay fixed so that shot numbers
    /// compare across seeds.
    /// </summary>
    internal static MatchResult Play(int seed, float halfLengthSeconds = 0f, bool equalSquads = false)
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

        var engine = MatchEngine.CreateDefault(tuning, outfieldAttributes: squad, seed: seed, userControlledTeam: null);
        var computers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
        var state = engine.State;
        var nobody = new Dictionary<int, PlayerInput>();
        // Stoppages add to the clock; three times the playing time is far beyond any real match.
        var tickCap = (long)(tuning.HalfLengthSeconds * 2f * 3f / tuning.TickSeconds);

        int shots = 0, passes = 0, windUps = 0, slides = 0, stoppages = 0, lastScore = 0;
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
            }

            wasOut = state.IsBallOutOfPlay;

            var possessor = state.Players.FirstOrDefault(player => player.HasPossession);
            if (possessor?.IsGoalkeeper == true)
            {
                keeperTicks++;
            }

            sameFor = possessor?.Id == lastPossessor ? sameFor + 1 : 0;
            lastPossessor = possessor?.Id;
            longestSame = Math.Max(longestSame, sameFor);

            if (state.HomeScore + state.AwayScore != lastScore)
            {
                lastScore = state.HomeScore + state.AwayScore;
                goalTicks.Add(state.Tick);
                firstGoalBy ??= state.HomeScore > lastHome ? TeamSide.Home : TeamSide.Away;
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
            firstGoalBy);
    }

    /// <summary>Plays <paramref name="count"/> matches and returns how many broke.</summary>
    internal static int Run(int count, int firstSeed, bool equalSquads)
    {
        var results = Enumerable.Range(firstSeed, count).Select(seed => Play(seed, equalSquads: equalSquads)).ToArray();
        foreach (var r in results)
        {
            Console.WriteLine(
                $"{(r.Fault is null ? "OK  " : "FAIL")} seed {r.Seed,4}  {r.HomeScore}-{r.AwayScore}  shots {r.Shots,3} (wind-ups {r.ShotWindUps,4})  passes {r.Passes,3}  slides {r.Slides,3}  " +
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
        Spread("stoppages", r => r.Stoppages);
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

        var broken = results.Count(r => r.Fault is not null);
        Console.WriteLine(broken == 0 ? $"All {count} matches reached full time." : $"{broken} of {count} matches broke.");
        return broken;
    }

    private static bool InBounds(System.Numerics.Vector2 position, FieldDimensions field) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y)
        && MathF.Abs(position.X) < field.Length && MathF.Abs(position.Y) < field.Width;
}
