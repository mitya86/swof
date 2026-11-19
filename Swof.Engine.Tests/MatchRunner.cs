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
        int CoachChanges = 0,
        int AwayPasses = 0,
        PlayerOutcomes? Outcomes = null);

    /// <summary>Every outfielder's outcomes, equal when the same players ended the same way, so a replayed match compares equal.</summary>
    internal sealed record PlayerOutcomes(IReadOnlyList<PlayerOutcome> Players)
    {
        public bool Equals(PlayerOutcomes? other) => other is not null && Players.SequenceEqual(other.Players);

        public override int GetHashCode() => Players.Count;
    }

    /// <summary>
    /// How one outfielder's spells on the ball ended: kept when his own team had it next or he scored, lost when
    /// the other team had it next. This is the kind of count a player's habits will later learn from.
    /// </summary>
    internal sealed record PlayerOutcome(PlayerAttributes Attributes, int Kept, int Lost, float ForwardMetres);

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
        bool awayCoach = false,
        byte awaySpeed = 0,
        byte awayTechnique = 0,
        Difficulty? awayDifficulty = null,
        int? randomPersonSeed = null,
        PlayerHabits? homeHabits = null,
        PlayerHabits? awayHabits = null)
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

        if (awaySpeed > 0 || awayTechnique > 0)
        {
            // Every Away outfielder gets this speed, and this ball control and tackling, to measure a weaker or
            // stronger opponent. The engine reads no other attribute yet.
            var speed = awaySpeed > 0 ? awaySpeed : PlayerAttributes.DefaultOutfield.Speed;
            var technique = awayTechnique > 0 ? awayTechnique : PlayerAttributes.DefaultOutfield.BallControl;
            Array.Fill(squad, PlayerAttributes.DefaultOutfield with { Speed = speed, BallControl = technique, Tackling = technique }, 4, 4);
        }

        if (awayDifficulty is not null)
        {
            squad = [.. awayDifficulty.Squad(squad, TeamSide.Away)];
        }

        var engine = MatchEngine.CreateDefault(tuning, outfieldAttributes: squad, seed: seed, userControlledTeam: userControlledTeam, startWithKickoff: true);
        var computers = ComputerPlayerController.CreateDefault(engine.CurrentSnapshot);
        computers.SetTactic(TeamSide.Home, homeTactic ?? TeamTactic.HighPress);
        computers.SetTactic(TeamSide.Away, awayTactic ?? TeamTactic.HighPress);
        // The level's own tactic and coach, unless a tactic was asked for by name.
        awayDifficulty?.Brief(computers, TeamSide.Away, tuning.HalfLengthSeconds * 2f);
        if (awayDifficulty is not null && awayTactic is not null)
        {
            computers.SetTactic(TeamSide.Away, awayTactic);
        }

        // Every outfielder of a side gets the same habits, so the side's results show what the habit does.
        foreach (var player in engine.CurrentSnapshot.Players.Where(player => player.Role == PlayerRole.Outfield))
        {
            if ((player.Team == TeamSide.Home ? homeHabits : awayHabits) is { } habits)
            {
                computers.SetHabits(player.Id, habits);
            }
        }

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
        // A person working the controls at random: the stick held one way, or let go, for a
        // while; the button tapped, held or left alone. It steers whichever player the engine
        // gives the user team, so switching, kickoffs and restarts all meet a real input.
        var person = randomPersonSeed is { } personSeed ? new Random(personSeed) : null;
        var stick = System.Numerics.Vector2.Zero;
        var button = false;
        int stickFor = 0, buttonFor = 0;
        // Stoppages add to the clock; three times the playing time is far beyond any real match.
        var tickCap = (long)(tuning.HalfLengthSeconds * 2f * 3f / tuning.TickSeconds);

        int shots = 0, passes = 0, awayPasses = 0, windUps = 0, slides = 0, fouls = 0, stoppages = 0, lastScore = 0;
        long lastFeedback = 0, keeperTicks = 0, sameFor = 0, longestSame = 0;
        int? lastPossessor = null;
        // The outfielder whose spell on the ball is still open; it survives the ball being loose or in flight.
        PlayerState? holder = null;
        var kept = new Dictionary<int, int>();
        var lost = new Dictionary<int, int>();
        // How far toward the opposing goal each player took the ball while it was his, summed over his spells.
        var carried = new Dictionary<int, float>();
        float holderStartX = 0f, holderLastX = 0f;
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
            var before = (state.Tick, state.HomeScore, state.AwayScore, state.Period);
            var inputs = nobody;
            if (person is not null)
            {
                if (--stickFor <= 0)
                {
                    stickFor = person.Next(5, 60);
                    var angle = person.NextSingle() * MathF.Tau;
                    stick = person.Next(5) == 0 ? System.Numerics.Vector2.Zero : new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle));
                }

                if (--buttonFor <= 0)
                {
                    button = !button;
                    buttonFor = !button ? person.Next(1, 90) : person.Next(2) == 0 ? person.Next(1, 5) : person.Next(5, 70);
                }

                inputs = new Dictionary<int, PlayerInput> { [state.ActivePlayerId] = new PlayerInput(stick, button) };
            }

            engine.StepTick(computers.CreateControlFrame(engine.CurrentSnapshot, inputs));

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
                awayPasses += feedback.Type == GameplayFeedbackType.PassContact && feedback.Team == TeamSide.Away ? 1 : 0;
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

            if (state.HomeScore + state.AwayScore != lastScore && holder is not null)
            {
                // The kickoff that follows a goal goes to the other team; the spell still ended well.
                var scored = (holder.Team == TeamSide.Home ? state.HomeScore : state.AwayScore)
                    > (holder.Team == TeamSide.Home ? before.HomeScore : before.AwayScore);
                (scored ? kept : lost)[holder.Id] = (scored ? kept : lost).GetValueOrDefault(holder.Id) + 1;
                Carried(holder);
                holder = null;
            }
            else if (possessor is not null && possessor.Id != holder?.Id)
            {
                if (holder is not null)
                {
                    var ends = possessor.Team == holder.Team ? kept : lost;
                    ends[holder.Id] = ends.GetValueOrDefault(holder.Id) + 1;
                    Carried(holder);
                }

                holder = possessor.IsGoalkeeper ? null : possessor;
                holderStartX = state.Ball.Position.X;
            }

            if (possessor is not null && possessor.Id == holder?.Id)
            {
                holderLastX = state.Ball.Position.X;
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

            fault ??= BrokenRule(state, before);
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
            computers.CoachChanges,
            awayPasses,
            new PlayerOutcomes(state.Players.Where(player => !player.IsGoalkeeper)
                .Select(player => new PlayerOutcome(player.Attributes, kept.GetValueOrDefault(player.Id), lost.GetValueOrDefault(player.Id), carried.GetValueOrDefault(player.Id)))
                .ToArray()));

        void Carried(PlayerState spellHolder)
        {
            var attack = spellHolder.Team == TeamSide.Home ? 1f : -1f;
            carried[spellHolder.Id] = carried.GetValueOrDefault(spellHolder.Id) + (holderLastX - holderStartX) * attack;
        }
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
        bool awayCoach = false,
        byte awaySpeed = 0,
        byte awayTechnique = 0,
        Difficulty? awayDifficulty = null,
        PlayerHabits? homeHabits = null,
        PlayerHabits? awayHabits = null,
        bool outcomes = false)
    {
        var results = Enumerable.Range(firstSeed, count)
            .Select(seed => Play(seed, equalSquads: equalSquads, homeTactic: homeTactic, awayTactic: awayTactic, homeRule: homeRule, awayRule: awayRule, homeCoach: homeCoach, awayCoach: awayCoach, awaySpeed: awaySpeed, awayTechnique: awayTechnique, awayDifficulty: awayDifficulty, homeHabits: homeHabits, awayHabits: awayHabits))
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

        if (homeHabits is not null || awayHabits is not null)
        {
            Spread("home passes", r => r.Passes - r.AwayPasses);
            Spread("away passes", r => r.AwayPasses);
            Spread("home goals", r => r.HomeScore);
            Spread("away goals", r => r.AwayScore);
        }

        if (outcomes)
        {
            // Whether a player's own spells end better with an attribute: the signal habits would learn from.
            var players = results.SelectMany(r => r.Outcomes?.Players ?? []).ToArray();
            foreach (var (name, pick) in new (string, Func<PlayerAttributes, byte>)[]
            {
                ("speed", a => a.Speed), ("ball control", a => a.BallControl), ("tackling", a => a.Tackling)
            })
            {
                var line = string.Join("  ", new[] { (4, 7), (8, 11), (12, 15) }.Select(range =>
                {
                    var group = players.Where(p => pick(p.Attributes) >= range.Item1 && pick(p.Attributes) <= range.Item2).ToArray();
                    var spells = group.Sum(p => p.Kept + p.Lost);
                    return $"{range.Item1}-{range.Item2}: kept {group.Sum(p => p.Kept) * 100f / Math.Max(1, spells):F1}% of {spells / (float)Math.Max(1, group.Length):F1} spells, {group.Sum(p => p.ForwardMetres) / Math.Max(1, spells):F2} m forward each";
                }));
                Console.WriteLine($"{name,-13} {line}");
            }
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
        // are replayed that way, with the controls worked at random; they stay out of the
        // figures above.
        var withUserTeam = Enumerable.Range(firstSeed, Math.Min(count, 10))
            .Select(seed => Play(seed, equalSquads: equalSquads, userControlledTeam: TeamSide.Home, randomPersonSeed: seed))
            .ToArray();
        foreach (var r in withUserTeam.Where(r => r.Fault is not null))
        {
            Console.WriteLine($"FAIL seed {r.Seed,4} with Home worked at random  {r.Fault}");
        }

        var broken = results.Concat(withUserTeam).Count(r => r.Fault is not null);
        Console.WriteLine(broken == 0 ? $"All {count} matches reached full time, and {withUserTeam.Length} more with Home's controls worked at random." : $"{broken} of {count + withUserTeam.Length} matches broke.");
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
            // The floor was 3 when players ran at 14 m/s. At 11 m/s the pace alone would give about 2.6; equal squads
            // score 2.5 and squads of unequal players 2.2 (their shots from 6 to 10 m score less; why is not known).
            ("2 to 9 goals a match", $"{goals:0.0}", goals is >= 2 and <= 9),
            // The floor was 15 while a computer's taker ran a restart in: over 100 matches half of all shots were his,
            // taken before anyone else touched the ball. Kicked as the rules say, equal squads shoot 15.8 times and
            // mixed squads 13.0.
            ("10 to 35 shots a match", $"{shots:0.0}", shots is >= 10 and <= 35),
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

    /// <summary>
    /// Rules every tick of every match must keep, whatever the players do. A broken one is a
    /// fault even when the match still reaches full time.
    /// </summary>
    internal static string? BrokenRule(MatchState state, (long Tick, int Home, int Away, int Period) before)
    {
        if (state.Tick != before.Tick + 1)
        {
            return $"the clock went from tick {before.Tick} to {state.Tick}";
        }

        if (state.HomeScore < before.Home || state.AwayScore < before.Away || state.HomeScore + state.AwayScore > before.Home + before.Away + 1)
        {
            return $"the score went from {before.Home}-{before.Away} to {state.HomeScore}-{state.AwayScore} at tick {state.Tick}";
        }

        if (state.Period < before.Period)
        {
            return $"the period went back from {before.Period} to {state.Period} at tick {state.Tick}";
        }

        if (state.Players.Count(player => player.HasPossession) > 1)
        {
            return $"two players had the ball at tick {state.Tick}";
        }

        if (state.Players.Any(player => !Finite(player.Velocity)) || !Finite(state.Ball.Velocity) || !float.IsFinite(state.Ball.Height) || !float.IsFinite(state.Ball.VerticalVelocity))
        {
            return $"a speed or height stopped being a number at tick {state.Tick}";
        }

        return null;
    }

    private static bool Finite(System.Numerics.Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

    private static bool InBounds(System.Numerics.Vector2 position, FieldDimensions field) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y)
        && MathF.Abs(position.X) < field.Length && MathF.Abs(position.Y) < field.Width;
}
