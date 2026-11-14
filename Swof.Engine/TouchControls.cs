using System.Numerics;

namespace Swof.Engine;

/// <summary>One finger, or the mouse, held on the screen.</summary>
/// <param name="Id">Stays the same from touch-down to release.</param>
/// <param name="Position">Screen position in pixels.</param>
public readonly record struct TouchPoint(int Id, Vector2 Position);

/// <summary>
/// Turns touches into the same stick-and-one-button <see cref="PlayerInput"/> a keyboard or gamepad gives.
/// A touch that starts on the left half of the screen is the stick, centred where it started; a touch that
/// starts on the right half holds the action button. A touch keeps its job until it is released.
/// </summary>
public sealed class TouchControls
{
    /// <summary>Drag distance for a full push of the stick, as a fraction of the screen's short side.</summary>
    public const float StickRadiusFraction = 0.12f;

    /// <summary>Drag distance below which the stick is at rest, as a fraction of the screen's short side.</summary>
    public const float DeadZoneFraction = 0.02f;

    private readonly HashSet<int> _actionIds = [];
    private int? _stickId;

    /// <summary>Where the stick touch started, or null when the stick is not held.</summary>
    public Vector2? StickOrigin { get; private set; }

    /// <summary>Where to draw the stick's knob: the stick touch, kept within the stick radius.</summary>
    public Vector2 StickKnob { get; private set; }

    /// <summary>Stick radius in pixels at the last read.</summary>
    public float StickRadius { get; private set; }

    /// <summary>Where the action button is being held, or null.</summary>
    public Vector2? ActionPoint { get; private set; }

    /// <summary>Reads this frame's touches. Call once a frame with every touch still held.</summary>
    public PlayerInput Read(IReadOnlyList<TouchPoint> touches, Vector2 screenSize)
    {
        ArgumentNullException.ThrowIfNull(touches);
        var shortSide = MathF.Min(screenSize.X, screenSize.Y);
        StickRadius = shortSide * StickRadiusFraction;

        if (_stickId is int heldStick && !touches.Any(touch => touch.Id == heldStick))
        {
            _stickId = null;
            StickOrigin = null;
        }

        _actionIds.RemoveWhere(id => !touches.Any(touch => touch.Id == id));

        var move = Vector2.Zero;
        ActionPoint = null;
        foreach (var touch in touches)
        {
            if (_actionIds.Contains(touch.Id))
            {
                ActionPoint = touch.Position;
            }
            else if (touch.Id == _stickId)
            {
                var drag = touch.Position - StickOrigin!.Value;
                var length = drag.Length();
                StickKnob = length <= StickRadius ? touch.Position : StickOrigin.Value + drag / length * StickRadius;
                move = length <= shortSide * DeadZoneFraction ? Vector2.Zero : drag / length;
            }
            else if (touch.Position.X >= screenSize.X / 2f)
            {
                _actionIds.Add(touch.Id);
                ActionPoint = touch.Position;
            }
            else if (_stickId is null)
            {
                _stickId = touch.Id;
                StickOrigin = touch.Position;
                StickKnob = touch.Position;
            }

            // A second finger on the stick side does nothing while the stick is held.
        }

        return new PlayerInput(move, _actionIds.Count > 0);
    }

    /// <summary>Forgets every held touch, for when the window loses focus.</summary>
    public void Reset()
    {
        _actionIds.Clear();
        _stickId = null;
        StickOrigin = null;
        ActionPoint = null;
    }
}
