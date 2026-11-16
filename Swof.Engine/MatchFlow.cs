using System.Numerics;
using Swof.Engine.AI;

namespace Swof.Engine;

/// <summary>The product's name, written once.</summary>
public static class Product
{
    public const string Name = "SWOF";
}

public enum MatchScreen
{
    Start,
    Playing,
    Paused,
    FullTime
}

/// <summary>What the host must do after a read of the flow.</summary>
public enum FlowEvent
{
    None,

    /// <summary>Start a new match with <see cref="MatchFlow.Difficulty"/> and <see cref="MatchFlow.Tactic"/>.</summary>
    StartMatch,

    /// <summary>The match was left for the start screen.</summary>
    LeaveMatch,

    /// <summary><see cref="MatchFlow.Tactic"/> changed during a match.</summary>
    TacticChanged
}

/// <summary>
/// Which screen the game is on and what its menu holds: the start screen, the match, the pause menu and the
/// result at full time. A host draws <see cref="Heading"/> and <see cref="Menu"/> and acts on the events; it decides
/// nothing itself. Menus are worked with the same stick and button as the match, or by touching an item.
/// </summary>
public sealed class MatchFlow
{
    /// <summary>A touch or press meant for a last shot must not start the next match before the result is seen.</summary>
    public const float ResultHoldSeconds = 1f;

    public static IReadOnlyList<TeamTactic> Tactics { get; } = [TeamTactic.HighPress, TeamTactic.HalfCourt, TeamTactic.PushUp];

    private int _stick;
    private bool _buttonDown;
    private bool _buttonIsMenus;
    private float _shownAtSeconds;
    private string _result = string.Empty;

    /// <param name="startScreen">False opens straight into a match, for demos and captured frames.</param>
    /// <param name="tactic">The person's team's tactic at the start.</param>
    public MatchFlow(bool startScreen = true, TeamTactic? tactic = null)
    {
        Tactic = tactic ?? TeamTactic.HighPress;
        Show(startScreen ? MatchScreen.Start : MatchScreen.Playing, 0f);
    }

    public PauseMenu Menu { get; } = new([]);

    public MatchScreen Screen { get; private set; }

    public Difficulty Difficulty { get; private set; } = Difficulty.Normal;

    /// <summary>The person's team's tactic.</summary>
    public TeamTactic Tactic { get; private set; }

    /// <summary>The item the stick is on.</summary>
    public int Cursor { get; private set; }

    public bool IsMenuOpen => Screen != MatchScreen.Playing;

    /// <summary>The line above the menu.</summary>
    public string Heading => Screen switch
    {
        MatchScreen.Start => Product.Name,
        MatchScreen.Paused => "Paused",
        MatchScreen.FullTime => _result,
        _ => string.Empty
    };

    /// <summary>
    /// The input the match may see. A button that was down while a menu was open stays the menu's until it is
    /// let go, so the press that chose Play or Resume is never also a pass.
    /// </summary>
    public PlayerInput ForMatch(PlayerInput input)
    {
        return _buttonIsMenus ? input with { ActionDown = false } : input;
    }

    /// <summary>Opens the pause menu, if a match is being played.</summary>
    public void Pause(float seconds = 0f)
    {
        if (Screen == MatchScreen.Playing)
        {
            Show(MatchScreen.Paused, seconds);
        }
    }

    public void SetTactic(TeamTactic tactic)
    {
        Tactic = tactic;
        SetLabels();
    }

    /// <summary>
    /// Reads one frame. Call it every frame, also during play, because a press or a touch is new only when it was
    /// absent from the read before.
    /// </summary>
    /// <param name="match">The match as it stands.</param>
    /// <param name="input">The stick and button as read on the screen, before any turn for changed ends.</param>
    /// <param name="pausePressed">Whether the pause key was pressed this frame.</param>
    /// <param name="touches">Every touch held this frame.</param>
    /// <param name="screenSize">The screen in pixels.</param>
    /// <param name="seconds">A clock that keeps running while paused.</param>
    /// <param name="remaining">The touches left for the stick and the action button.</param>
    public FlowEvent Update(
        MatchSnapshot match,
        PlayerInput input,
        bool pausePressed,
        IReadOnlyList<TouchPoint> touches,
        Vector2 screenSize,
        float seconds,
        out IReadOnlyList<TouchPoint> remaining)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (match.IsMatchOver && Screen is MatchScreen.Playing or MatchScreen.Paused)
        {
            _result = $"Full time   Home {match.HomeScore} - {match.AwayScore} Away";
            Show(MatchScreen.FullTime, seconds);
        }
        else if (pausePressed && Screen is MatchScreen.Playing or MatchScreen.Paused)
        {
            Show(Screen == MatchScreen.Playing ? MatchScreen.Paused : MatchScreen.Playing, seconds);
        }

        var chosen = Menu.Read(touches, screenSize, out remaining);
        if (Screen == MatchScreen.Playing && Menu.IsOpen)
        {
            // The pause button in the corner was touched.
            Show(MatchScreen.Paused, seconds);
        }

        // A held stick moves the cursor once, and a held button chooses once.
        var stick = input.Move.Y > 0.5f ? 1 : input.Move.Y < -0.5f ? -1 : 0;
        var moved = stick != 0 && stick != _stick;
        _stick = stick;
        var pressed = input.ActionDown && !_buttonDown;
        _buttonDown = input.ActionDown;
        _buttonIsMenus = input.ActionDown && (_buttonIsMenus || IsMenuOpen);
        if (!IsMenuOpen)
        {
            return FlowEvent.None;
        }

        if (moved)
        {
            Cursor = Math.Clamp(Cursor + stick, 0, Menu.Labels.Count - 1);
        }

        chosen ??= pressed ? Cursor : null;
        if (chosen is not int item || (Screen == MatchScreen.FullTime && seconds - _shownAtSeconds < ResultHoldSeconds))
        {
            return FlowEvent.None;
        }

        Cursor = item;
        return Choose(item, seconds);
    }

    private FlowEvent Choose(int item, float seconds)
    {
        switch (Screen)
        {
            case MatchScreen.Start when item == 1:
                Difficulty = Difficulty.All[(Difficulty.All.ToList().IndexOf(Difficulty) + 1) % Difficulty.All.Count];
                SetLabels();
                return FlowEvent.None;
            case MatchScreen.Paused when item >= 1 && item <= Tactics.Count:
                SetTactic(Tactics[item - 1]);
                return FlowEvent.TacticChanged;
            case MatchScreen.Paused when item == 0:
                Show(MatchScreen.Playing, seconds);
                return FlowEvent.None;
            case MatchScreen.Start or MatchScreen.FullTime when item == 0:
                Show(MatchScreen.Playing, seconds);
                return FlowEvent.StartMatch;
            default:
                Show(MatchScreen.Start, seconds);
                return FlowEvent.LeaveMatch;
        }
    }

    private void Show(MatchScreen screen, float seconds)
    {
        Screen = screen;
        Cursor = 0;
        _shownAtSeconds = seconds;
        SetLabels();
        if (IsMenuOpen)
        {
            Menu.Open();
        }
        else
        {
            Menu.Close();
        }
    }

    private void SetLabels()
    {
        Menu.Labels = Screen switch
        {
            MatchScreen.Start => ["Play", $"Difficulty: {Difficulty.Name}"],
            MatchScreen.FullTime => ["New match", "Leave match"],
            _ => ["Resume", .. Tactics.Select(tactic => tactic == Tactic ? $"Tactic: {tactic.Name} (on)" : $"Tactic: {tactic.Name}"), "Leave match"]
        };
    }
}
