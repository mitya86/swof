using System.Numerics;

namespace Swof.Engine;

/// <summary>A rectangle on the screen, in pixels.</summary>
public readonly record struct ScreenRect(Vector2 Min, Vector2 Max)
{
    public bool Contains(Vector2 point) =>
        point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;
}

/// <summary>
/// A pause button in the top right corner and, once it is touched, a column of items to choose from.
/// A touch that starts on the button, or that is on the screen while the menu is open, belongs to the menu
/// until it is released, so it never also moves the stick or presses the action button.
/// </summary>
public sealed class PauseMenu
{
    /// <summary>Side of the pause button, as a fraction of the screen's short side.</summary>
    public const float ButtonSizeFraction = 0.1f;

    /// <summary>Gap between the pause button and the screen's edges, as a fraction of the short side.</summary>
    public const float ButtonMarginFraction = 0.02f;

    /// <summary>Height of one item, as a fraction of the short side.</summary>
    public const float ItemHeightFraction = 0.12f;

    /// <summary>Width of one item, as a fraction of the short side.</summary>
    public const float ItemWidthFraction = 0.7f;

    /// <summary>Gap between items, as a fraction of the short side.</summary>
    public const float ItemGapFraction = 0.03f;

    private readonly HashSet<int> _owned = [];
    private HashSet<int> _held = [];

    public PauseMenu(IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        Labels = labels;
    }

    /// <summary>The items, top to bottom. They can be swapped while touches are held; a held touch stays the menu's.</summary>
    public IReadOnlyList<string> Labels { get; set; }

    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public static ScreenRect ButtonBounds(Vector2 screenSize)
    {
        var shortSide = MathF.Min(screenSize.X, screenSize.Y);
        var max = new Vector2(screenSize.X - shortSide * ButtonMarginFraction, shortSide * (ButtonMarginFraction + ButtonSizeFraction));
        return new ScreenRect(max - new Vector2(shortSide * ButtonSizeFraction), max);
    }

    /// <summary>Where an item is drawn and touched: a column centred on the screen.</summary>
    public ScreenRect ItemBounds(int index, Vector2 screenSize)
    {
        var shortSide = MathF.Min(screenSize.X, screenSize.Y);
        var size = new Vector2(shortSide * ItemWidthFraction, shortSide * ItemHeightFraction);
        var step = size.Y + shortSide * ItemGapFraction;
        var columnHeight = step * Labels.Count - shortSide * ItemGapFraction;
        var min = new Vector2((screenSize.X - size.X) / 2f, (screenSize.Y - columnHeight) / 2f + step * index);
        return new ScreenRect(min, min + size);
    }

    /// <summary>
    /// Reads this frame's touches. Call once a frame with every touch still held, also when there are none,
    /// because a touch is new only when it was absent from the read before.
    /// </summary>
    /// <param name="touches">Every touch held this frame.</param>
    /// <param name="screenSize">The screen in pixels.</param>
    /// <param name="remaining">The touches left for the stick and the action button.</param>
    /// <returns>The item a new touch landed on while the menu was open, or null.</returns>
    public int? Read(IReadOnlyList<TouchPoint> touches, Vector2 screenSize, out IReadOnlyList<TouchPoint> remaining)
    {
        ArgumentNullException.ThrowIfNull(touches);
        _owned.RemoveWhere(id => !touches.Any(touch => touch.Id == id));

        int? chosen = null;
        foreach (var touch in touches)
        {
            var isNew = !_held.Contains(touch.Id);
            if (IsOpen)
            {
                _owned.Add(touch.Id);
                if (isNew && chosen is null)
                {
                    var index = Enumerable.Range(0, Labels.Count).FirstOrDefault(i => ItemBounds(i, screenSize).Contains(touch.Position), -1);
                    chosen = index < 0 ? null : index;
                }
            }
            else if (isNew && ButtonBounds(screenSize).Contains(touch.Position))
            {
                _owned.Add(touch.Id);
                IsOpen = true;
            }
        }

        _held = touches.Select(touch => touch.Id).ToHashSet();
        remaining = touches.Where(touch => !_owned.Contains(touch.Id)).ToArray();
        return chosen;
    }
}
