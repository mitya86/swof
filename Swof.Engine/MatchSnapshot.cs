namespace Swof.Engine;

public sealed record MatchSnapshot(
    FieldDimensions Field,
    IReadOnlyList<PlayerSnapshot> Players,
    BallSnapshot Ball,
    int ActivePlayerId,
    int HomeScore,
    int AwayScore,
    long Tick,
    float ClockSeconds,
    bool IsBallOutOfPlay,
    MatchEvent LastEvent)
{
    public PlayerSnapshot ActivePlayer => Players.Single(player => player.Id == ActivePlayerId);

    public static MatchSnapshot FromState(MatchState state)
    {
        return new MatchSnapshot(
            state.Field,
            state.Players.Select(PlayerSnapshot.FromState).ToArray(),
            BallSnapshot.FromState(state.Ball),
            state.ActivePlayerId,
            state.HomeScore,
            state.AwayScore,
            state.Tick,
            state.ClockSeconds,
            state.IsBallOutOfPlay,
            state.LastEvent);
    }

    public static MatchSnapshot Interpolate(MatchSnapshot from, MatchSnapshot to, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        if (from.Players.Count != to.Players.Count)
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

        return to with
        {
            Players = players,
            Ball = BallSnapshot.Interpolate(from.Ball, to.Ball, alpha),
            ClockSeconds = Lerp(from.ClockSeconds, to.ClockSeconds, alpha)
        };
    }

    private static float Lerp(float from, float to, float alpha)
    {
        return from + (to - from) * alpha;
    }
}
