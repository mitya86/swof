using System.Numerics;

namespace Swof.Engine;

/// <summary>
/// Preserves action-button transitions until the fixed-step simulation consumes them.
/// </summary>
public sealed class PlayerInputBuffer
{
    private readonly Queue<bool> _pendingActionStates = new();
    private Vector2 _latestMove;
    private bool _capturedActionDown;
    private bool _tickActionDown;

    public int PendingActionTransitions => _pendingActionStates.Count;

    public void Capture(PlayerInput input)
    {
        _latestMove = input.NormalizedMove;
        if (input.ActionDown == _capturedActionDown)
        {
            return;
        }

        _capturedActionDown = input.ActionDown;
        _pendingActionStates.Enqueue(input.ActionDown);
    }

    public PlayerInput ConsumeForTick()
    {
        if (_pendingActionStates.TryDequeue(out var actionDown))
        {
            _tickActionDown = actionDown;
        }

        return new PlayerInput(_latestMove, _tickActionDown);
    }

    public void Reset(PlayerInput input = default)
    {
        _pendingActionStates.Clear();
        _latestMove = input.NormalizedMove;
        _capturedActionDown = input.ActionDown;
        _tickActionDown = input.ActionDown;
    }
}
