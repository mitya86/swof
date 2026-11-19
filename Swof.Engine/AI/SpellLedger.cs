namespace Swof.Engine.AI;

/// <summary>How a group of spells on the ball ended.</summary>
/// <param name="Kept">Spells after which the player's own team had the ball next, or that ended in his team's goal.</param>
/// <param name="Lost">Spells after which the other team had the ball next, or that ended in its goal.</param>
public readonly record struct SpellEnds(int Kept, int Lost)
{
    /// <summary>Gets how many spells ended.</summary>
    public int Spells => Kept + Lost;

    /// <summary>Gets the share of spells kept, or zero with no spells.</summary>
    public float KeptShare => Spells == 0 ? 0f : Kept / (float)Spells;

    /// <summary>Adds two counts.</summary>
    public static SpellEnds operator +(SpellEnds left, SpellEnds right) => new(left.Kept + right.Kept, left.Lost + right.Lost);

    /// <summary>Counts one more spell.</summary>
    public SpellEnds With(bool kept) => kept ? this with { Kept = Kept + 1 } : this with { Lost = Lost + 1 };
}

/// <summary>How one outfielder's spells on the ball ended over a match, split the ways his habits learn from.</summary>
/// <param name="Long">Spells he held for at least <see cref="SpellLedger.LongSpellTicks"/>.</param>
/// <param name="Short">Spells he held for less.</param>
/// <param name="Forward">Spells in which he took the ball at least <see cref="SpellLedger.ForwardSpellMetres"/> toward the opposing goal.</param>
/// <param name="Other">Spells in which he did not.</param>
/// <param name="ForwardMetres">How far toward the opposing goal he took the ball, summed over his spells.</param>
public readonly record struct PlayerSpells(SpellEnds Long, SpellEnds Short, SpellEnds Forward, SpellEnds Other, float ForwardMetres)
{
    /// <summary>Gets every spell, however it is split.</summary>
    public SpellEnds All => Long + Short;

    /// <summary>Gets how many spells ended.</summary>
    public int Spells => All.Spells;

    /// <summary>Gets the metres carried forward per spell, or zero with no spells.</summary>
    public float MetresPerSpell => Spells == 0 ? 0f : ForwardMetres / Spells;

    /// <summary>Adds two players' counts, for the figures of a group.</summary>
    public static PlayerSpells operator +(PlayerSpells left, PlayerSpells right) =>
        new(left.Long + right.Long, left.Short + right.Short, left.Forward + right.Forward, left.Other + right.Other, left.ForwardMetres + right.ForwardMetres);
}

/// <summary>
/// Counts, from snapshots alone, how each outfielder's spells on the ball end. A spell runs from an outfielder
/// gaining the ball to the next player gaining it (or a goal); it survives the ball being loose or in flight,
/// so a pass that arrives is kept and one that is cut out is lost. These counts are what habits learn from.
/// </summary>
public sealed class SpellLedger
{
    /// <summary>A spell held this long is a long one: the usual wait before a computer player passes.</summary>
    public const int LongSpellTicks = 48;

    /// <summary>A spell that took the ball this far toward the opposing goal is a forward one.</summary>
    public const float ForwardSpellMetres = 2f;

    private readonly Dictionary<int, PlayerSpells> _counts = new();
    private int? _holderId;
    private TeamSide _holderTeam;
    private float _startX;
    private float _lastX;
    private long _startTick;
    private long _lastTick;
    private int _homeScore;
    private int _awayScore;

    /// <summary>Gets the counts so far, keyed by player.</summary>
    public IReadOnlyDictionary<int, PlayerSpells> Counts => _counts;

    /// <summary>Gets one player's counts so far.</summary>
    public PlayerSpells Of(int playerId) => _counts.GetValueOrDefault(playerId);

    /// <summary>Watches one tick. Call once per tick with the snapshot after it.</summary>
    /// <param name="match">The snapshot.</param>
    public void Watch(MatchSnapshot match)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (match.HomeScore != _homeScore || match.AwayScore != _awayScore)
        {
            if (_holderId is { } scorer)
            {
                // The kickoff that follows a goal goes to the other team; a spell that ended in a goal still ended well.
                var scored = _holderTeam == TeamSide.Home ? match.HomeScore > _homeScore : match.AwayScore > _awayScore;
                End(scorer, scored);
            }

            _holderId = null;
            _homeScore = match.HomeScore;
            _awayScore = match.AwayScore;
            return;
        }

        var possessor = match.Players.FirstOrDefault(player => player.HasPossession);
        if (possessor is not null && possessor.Id != _holderId)
        {
            if (_holderId is { } holderId)
            {
                End(holderId, possessor.Team == _holderTeam);
            }

            _holderId = possessor.Role == PlayerRole.Goalkeeper ? null : possessor.Id;
            _holderTeam = possessor.Team;
            _startX = match.Ball.Position.X;
            _lastX = _startX;
            _startTick = match.Tick;
            _lastTick = match.Tick;
        }
        else if (possessor is not null)
        {
            _lastX = match.Ball.Position.X;
            _lastTick = match.Tick;
        }
    }

    private void End(int playerId, bool kept)
    {
        var metres = (_lastX - _startX) * (_holderTeam == TeamSide.Home ? 1f : -1f);
        var counts = _counts.GetValueOrDefault(playerId);
        var isLong = _lastTick - _startTick >= LongSpellTicks;
        var isForward = metres >= ForwardSpellMetres;
        _counts[playerId] = new PlayerSpells(
            isLong ? counts.Long.With(kept) : counts.Long,
            isLong ? counts.Short : counts.Short.With(kept),
            isForward ? counts.Forward.With(kept) : counts.Forward,
            isForward ? counts.Other : counts.Other.With(kept),
            counts.ForwardMetres + metres);
        _holderId = null;
    }
}
