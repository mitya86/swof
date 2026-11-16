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

    /// <summary>
    /// Chooses the small differences in computer decisions. Zero is as valid as any other value;
    /// a replay or a networked match must carry the same seed.
    /// </summary>
    public int Seed { get; init; }

    /// <summary>The foul rule for slides, kept here so computer players read the same rule the engine applies.</summary>
    public SlideFoulRule SlideFoul { get; set; } = SlideFoulRule.Off;

    public FieldDimensions Field { get; }
    public IReadOnlyList<PlayerState> Players { get; }
    public IReadOnlyList<OfficialState> Officials { get; }
    public BallState Ball { get; }
    public int ActivePlayerId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    /// <summary>Fouls a team has committed in the current half. From the sixth, each one gives a 10 m kick.</summary>
    public int HomeFouls { get; set; }
    public int AwayFouls { get; set; }
    public long Tick { get; set; }
    public float ClockSeconds { get; set; }
    public bool IsBallOutOfPlay { get; set; }

    /// <summary>Gets or sets the current half, starting at 1.</summary>
    public int Period { get; set; } = 1;

    /// <summary>Gets or sets whether full time has been reached. A finished match no longer advances.</summary>
    public bool IsMatchOver { get; set; }

    /// <summary>Gets or sets the team that last played the ball; decides who is awarded a restart.</summary>
    public TeamSide? LastTouchTeam { get; set; }

    /// <summary>Gets or sets the ticks left before a stopped match resumes on its own.</summary>
    public int RestartTicksRemaining { get; set; }
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
