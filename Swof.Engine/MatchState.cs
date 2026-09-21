using System.Numerics;

namespace Swof.Engine;

public sealed class MatchState
{
    private const int FeedbackHistoryCapacity = 128;
    private readonly List<GameplayFeedbackEvent> _feedbackEvents = new();
    private long _nextFeedbackSequence = 1;

    public MatchState(
        FieldDimensions field,
        IReadOnlyList<PlayerState> players,
        BallState ball,
        int activePlayerId,
        IReadOnlyList<OfficialState>? officials = null)
    {
        Field = field;
        Players = players;
        Ball = ball;
        ActivePlayerId = activePlayerId;
        Officials = officials ?? Array.Empty<OfficialState>();
    }

    public FieldDimensions Field { get; }
    public IReadOnlyList<PlayerState> Players { get; }
    public IReadOnlyList<OfficialState> Officials { get; }
    public BallState Ball { get; }
    public int ActivePlayerId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public long Tick { get; set; }
    public float ClockSeconds { get; set; }
    public bool IsBallOutOfPlay { get; set; }
    public MatchEvent LastEvent { get; set; } = MatchEvent.None;
    public IReadOnlyList<GameplayFeedbackEvent> FeedbackEvents => _feedbackEvents;

    public PlayerState ActivePlayer => Players.Single(player => player.Id == ActivePlayerId);

    internal void AddFeedback(
        GameplayFeedbackType type,
        PlayerState? player,
        Vector2 position,
        Vector2 direction,
        float intensity)
    {
        _feedbackEvents.Add(new GameplayFeedbackEvent(
            _nextFeedbackSequence++,
            Tick,
            type,
            player?.Id,
            player?.Team,
            position,
            direction,
            Math.Clamp(intensity, 0f, 1f)));

        if (_feedbackEvents.Count > FeedbackHistoryCapacity)
        {
            _feedbackEvents.RemoveRange(0, _feedbackEvents.Count - FeedbackHistoryCapacity);
        }
    }
}
