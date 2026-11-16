namespace Swof.Engine.AI;

/// <summary>
/// A computer team's coach. It makes up its mind only every few seconds, and the team plays what it asked for a
/// few seconds after that. It reads nothing but match snapshots and holds no
/// randomness, so a seed still replays the same match.
/// </summary>
/// <param name="team">The team it coaches.</param>
/// <param name="plan">What it wants by the score and the time left.</param>
public sealed class Coach(TeamSide team, TacticByScore plan)
{
    private long _lastTick = -1;
    private float _nextLookSeconds;
    private TeamTactic? _told;
    private TeamTactic? _asked;
    private float _askedTakesEffectSeconds;

    /// <summary>Seconds of play between two decisions.</summary>
    public float LookEverySeconds { get; init; } = 10f;

    /// <summary>Seconds of play between a decision and the team playing it.</summary>
    public float DelaySeconds { get; init; } = 5f;

    /// <summary>How many times the team has been given a different tactic.</summary>
    public int Changes { get; private set; }

    /// <summary>The tactic the team plays now.</summary>
    public TeamTactic Tactic(TeamTactic usual) => _told ?? usual;

    /// <summary>Watches one tick. Call once per tick, before asking for the tactic.</summary>
    public void Watch(MatchSnapshot match, TeamTactic usual)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(usual);
        if (match.Tick == _lastTick)
        {
            return;
        }

        if (match.Tick < _lastTick)
        {
            // A new match.
            _nextLookSeconds = 0f;
            _told = null;
            _asked = null;
            Changes = 0;
        }

        _lastTick = match.Tick;
        var clock = match.ClockSeconds;
        if (_asked is not null && clock >= _askedTakesEffectSeconds)
        {
            if (_asked != Tactic(usual))
            {
                Changes++;
            }

            _told = _asked;
            _asked = null;
        }

        if (clock < _nextLookSeconds)
        {
            return;
        }

        _nextLookSeconds = clock + LookEverySeconds;
        var wanted = plan.Choose(match, team, usual);
        if (wanted != (_asked ?? Tactic(usual)))
        {
            _asked = wanted;
            _askedTakesEffectSeconds = clock + DelaySeconds;
        }
    }
}
