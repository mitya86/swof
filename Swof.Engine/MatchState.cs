namespace Swof.Engine;

public sealed class MatchState
{
    public MatchState(FieldDimensions field, IReadOnlyList<PlayerState> players, BallState ball, int activePlayerId)
    {
        Field = field;
        Players = players;
        Ball = ball;
        ActivePlayerId = activePlayerId;
    }

    public FieldDimensions Field { get; }
    public IReadOnlyList<PlayerState> Players { get; }
    public BallState Ball { get; }
    public int ActivePlayerId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public long Tick { get; set; }
    public float ClockSeconds { get; set; }
    public bool IsBallOutOfPlay { get; set; }
    public MatchEvent LastEvent { get; set; } = MatchEvent.None;

    public PlayerState ActivePlayer => Players.Single(player => player.Id == ActivePlayerId);
}
