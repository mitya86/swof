namespace Swof.Engine;

public sealed record MatchSnapshot(
    FieldDimensions Field,
    IReadOnlyList<PlayerSnapshot> Players,
    IReadOnlyList<OfficialSnapshot> Officials,
    BallSnapshot Ball,
    int ActivePlayerId,
    int HomeScore,
    int AwayScore,
    long Tick,
    float ClockSeconds,
    bool IsBallOutOfPlay,
    int Period,
    bool IsMatchOver,
    MatchEvent LastEvent,
    IReadOnlyList<GameplayFeedbackEvent> FeedbackEvents)
{
    /// <summary>The match seed, for computer decisions that vary from match to match.</summary>
    public int Seed { get; init; }

    /// <summary>Fouls each team has committed in the current half.</summary>
    public int HomeFouls { get; init; }
    public int AwayFouls { get; init; }

    /// <summary>The foul rule for slides that the engine applies.</summary>
    public SlideFoulRule SlideFoul { get; init; } = SlideFoulRule.Off;

    /// <summary>The player standing over a kickoff, kick-in, corner or free kick that has not been kicked yet.</summary>
    public int? RestartTakerId { get; init; }

    /// <summary>Teams change ends at half-time, so the second half is seen from the other side.</summary>
    public bool EndsChanged => Period % 2 == 0;

    /// <summary>The match as the person sees it: turned around once the teams have changed ends.</summary>
    public MatchSnapshot AsSeen()
    {
        return EndsChanged ? TurnedAround() : this;
    }

    /// <summary>Turns a stick read on the screen into the simulation's directions, so up on the screen stays up.</summary>
    public PlayerInput FromScreen(PlayerInput input)
    {
        return EndsChanged ? input with { Move = -input.Move } : input;
    }

    public PlayerSnapshot ActivePlayer => Players.Single(player => player.Id == ActivePlayerId);

    public static MatchSnapshot FromState(MatchState state)
    {
        return new MatchSnapshot(
            state.Field,
            state.Players.Select(PlayerSnapshot.FromState).ToArray(),
            state.Officials.Select(OfficialSnapshot.FromState).ToArray(),
            BallSnapshot.FromState(state.Ball),
            state.ActivePlayerId,
            state.HomeScore,
            state.AwayScore,
            state.Tick,
            state.ClockSeconds,
            state.IsBallOutOfPlay,
            state.Period,
            state.IsMatchOver,
            state.LastEvent,
            state.FeedbackEvents.ToArray())
        {
            Seed = state.Seed,
            HomeFouls = state.HomeFouls,
            AwayFouls = state.AwayFouls,
            SlideFoul = state.SlideFoul,
            RestartTakerId = state.RestartTakerId
        };
    }

    public static MatchSnapshot Interpolate(MatchSnapshot from, MatchSnapshot to, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        if (from.Players.Count != to.Players.Count || from.Officials.Count != to.Officials.Count)
        {
            return to;
        }

        var players = new PlayerSnapshot[to.Players.Count];
        for (var i = 0; i < players.Length; i++)
        {
            if (from.Players[i].Id != to.Players[i].Id)
            {
                return to;
            }

            players[i] = PlayerSnapshot.Interpolate(from.Players[i], to.Players[i], alpha);
        }

        var officials = new OfficialSnapshot[to.Officials.Count];
        for (var i = 0; i < officials.Length; i++)
        {
            if (from.Officials[i].Id != to.Officials[i].Id)
            {
                return to;
            }

            officials[i] = OfficialSnapshot.Interpolate(from.Officials[i], to.Officials[i], alpha);
        }

        return to with
        {
            Players = players,
            Officials = officials,
            Ball = BallSnapshot.Interpolate(from.Ball, to.Ball, alpha),
            ClockSeconds = Lerp(from.ClockSeconds, to.ClockSeconds, alpha)
        };
    }

    /// <summary>
    /// The same moment seen from the other side of the court. The court is symmetric, so changing ends is a
    /// half turn of the view: the simulation keeps Home defending the west goal for the whole match.
    /// </summary>
    public MatchSnapshot TurnedAround()
    {
        return this with
        {
            Players = Players
                .Select(player => player with { Position = -player.Position, Velocity = -player.Velocity, Facing = Turned(player.Facing) })
                .ToArray(),
            Officials = Officials
                .Select(official => official with { Position = -official.Position, Velocity = -official.Velocity, Facing = Turned(official.Facing) })
                .ToArray(),
            Ball = Ball with { Position = -Ball.Position, Velocity = -Ball.Velocity },
            LastEvent = LastEvent with { Position = -LastEvent.Position },
            FeedbackEvents = FeedbackEvents
                .Select(feedback => feedback with { Position = -feedback.Position, Direction = -feedback.Direction })
                .ToArray()
        };
    }

    private static Azimuth Turned(Azimuth facing)
    {
        return new Azimuth((facing.NormalizedDegrees + 180f) % 360f);
    }

    private static float Lerp(float from, float to, float alpha)
    {
        return from + (to - from) * alpha;
    }
}
