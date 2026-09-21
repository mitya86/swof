using System.Collections.ObjectModel;

namespace Swof.Engine;

/// <summary>
/// Carries the authoritative input selected for each controlled player during one simulation tick.
/// </summary>
public sealed class PlayerControlFrame
{
    private readonly IReadOnlyDictionary<int, PlayerInput> _inputs;
    private readonly IReadOnlyList<int> _playerIds;

    /// <summary>
    /// Creates a frame from player inputs. Entries are stored in player-id order for deterministic processing.
    /// </summary>
    /// <param name="inputs">The input assigned to each controlled player.</param>
    /// <exception cref="ArgumentException">An input is registered more than once for the same player.</exception>
    public PlayerControlFrame(IEnumerable<KeyValuePair<int, PlayerInput>> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var copy = new SortedDictionary<int, PlayerInput>();
        foreach (var (playerId, input) in inputs)
        {
            if (!copy.TryAdd(playerId, input))
            {
                throw new ArgumentException($"Player {playerId} has more than one input.", nameof(inputs));
            }
        }

        _inputs = new ReadOnlyDictionary<int, PlayerInput>(copy);
        _playerIds = copy.Keys.ToArray();
    }

    /// <summary>Gets a frame containing no controlled players.</summary>
    public static PlayerControlFrame Empty { get; } = new(
        Array.Empty<KeyValuePair<int, PlayerInput>>());

    /// <summary>Gets controlled player identifiers in stable processing order.</summary>
    public IReadOnlyList<int> PlayerIds => _playerIds;

    /// <summary>Gets the input assigned to each controlled player.</summary>
    public IReadOnlyDictionary<int, PlayerInput> Inputs => _inputs;

    /// <summary>
    /// Creates a frame containing one controlled player.
    /// </summary>
    /// <param name="playerId">The controlled player's identifier.</param>
    /// <param name="input">The player's input for the tick.</param>
    /// <returns>A single-player control frame.</returns>
    public static PlayerControlFrame ForPlayer(int playerId, PlayerInput input)
    {
        return new PlayerControlFrame(new[]
        {
            new KeyValuePair<int, PlayerInput>(playerId, input)
        });
    }

    /// <summary>
    /// Gets a player's input, or neutral input when the player is not assigned in this frame.
    /// </summary>
    /// <param name="playerId">The player identifier.</param>
    /// <returns>The assigned or neutral input.</returns>
    public PlayerInput GetInputOrEmpty(int playerId)
    {
        return _inputs.TryGetValue(playerId, out var input)
            ? input
            : PlayerInput.Empty;
    }
}
