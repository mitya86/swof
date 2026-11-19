using Swof.Engine.AI;

namespace Swof.Game;

internal sealed record SwofGameLaunchOptions(
    MatchFeedbackMode FeedbackMode,
    MatchDemoAction DemoAction,
    TeamTactic? OpponentTactic,
    TeamTactic TeamTactic,
    bool OpponentCoach,
    bool StartPaused = false,
    string? CapturePath = null)
{
    /// <summary>Seconds after launch at which the frame for <see cref="CapturePath"/> is written.</summary>
    public double CaptureAfterSeconds { get; init; } = 2d;

    /// <summary>Keys and touches played back as if a person made them, so a captured frame can show a state only input reaches.</summary>
    public IReadOnlyList<ScriptedInput> Script { get; init; } = [];

    /// <summary>Shows the start screen even with <see cref="CapturePath"/>, which otherwise opens in a match.</summary>
    public bool StartScreen { get; init; }

    /// <summary>A career file: Home plays as its squad, set out by its coach, and the career develops and is saved at each full time.</summary>
    public string? CareerPath { get; init; }

    public static SwofGameLaunchOptions Default { get; } = new(
        MatchFeedbackMode.Connected,
        MatchDemoAction.None,
        OpponentTactic: null,
        TeamTactic.HighPress,
        OpponentCoach: false);

    public static SwofGameLaunchOptions Parse(IEnumerable<string> arguments)
    {
        var feedbackMode = MatchFeedbackMode.Connected;
        var demoAction = MatchDemoAction.None;
        TeamTactic? opponentTactic = null;
        var startScreen = false;
        var teamTactic = TeamTactic.HighPress;
        string? careerPath = null;
        var opponentCoach = false;
        var startPaused = false;
        string? capturePath = null;
        var captureAfterSeconds = 2d;
        IReadOnlyList<ScriptedInput> script = [];
        foreach (var argument in arguments)
        {
            if (argument.StartsWith("--capture-after=", StringComparison.OrdinalIgnoreCase))
            {
                captureAfterSeconds = ParseSeconds(argument["--capture-after=".Length..]);
            }
            else if (argument.StartsWith("--script=", StringComparison.OrdinalIgnoreCase))
            {
                script = argument["--script=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(ScriptedInput.Parse).ToArray();
            }

            if (argument.StartsWith("--feedback=", StringComparison.OrdinalIgnoreCase))
            {
                var value = argument["--feedback=".Length..];
                if (!Enum.TryParse(value, ignoreCase: true, out feedbackMode)
                    || !Enum.IsDefined(feedbackMode))
                {
                    throw new ArgumentException($"Unknown feedback mode '{value}'.", nameof(arguments));
                }
            }
            else if (argument.StartsWith("--demo-action=", StringComparison.OrdinalIgnoreCase))
            {
                var value = argument["--demo-action=".Length..];
                if (!Enum.TryParse(value, ignoreCase: true, out demoAction)
                    || !Enum.IsDefined(demoAction))
                {
                    throw new ArgumentException($"Unknown demo action '{value}'.", nameof(arguments));
                }
            }
            else if (argument.StartsWith("--opponent-tactic=", StringComparison.OrdinalIgnoreCase))
            {
                opponentTactic = ParseTactic(argument["--opponent-tactic=".Length..]);
            }
            else if (argument.Equals("--opponent-coach", StringComparison.OrdinalIgnoreCase))
            {
                opponentCoach = true;
            }
            else if (argument.StartsWith("--capture=", StringComparison.OrdinalIgnoreCase))
            {
                capturePath = argument["--capture=".Length..];
            }
            else if (argument.Equals("--start-screen", StringComparison.OrdinalIgnoreCase))
            {
                startScreen = true;
            }
            else if (argument.Equals("--start-paused", StringComparison.OrdinalIgnoreCase))
            {
                startPaused = true;
            }
            else if (argument.StartsWith("--career=", StringComparison.OrdinalIgnoreCase))
            {
                careerPath = argument["--career=".Length..];
            }
            else if (argument.StartsWith("--team-tactic=", StringComparison.OrdinalIgnoreCase))
            {
                teamTactic = ParseTactic(argument["--team-tactic=".Length..]);
            }
        }

        return new SwofGameLaunchOptions(feedbackMode, demoAction, opponentTactic, teamTactic, opponentCoach, startPaused, capturePath)
        {
            CaptureAfterSeconds = captureAfterSeconds,
            Script = script,
            StartScreen = startScreen,
            CareerPath = careerPath
        };
    }

    internal static double ParseSeconds(string value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds >= 0d
            ? seconds
            : throw new ArgumentException($"'{value}' is not a time in seconds.", nameof(value));

    private static TeamTactic ParseTactic(string value) => value.ToLowerInvariant() switch
    {
        "high" => TeamTactic.HighPress,
        "half" => TeamTactic.HalfCourt,
        "up" => TeamTactic.PushUp,
        _ => throw new ArgumentException($"Unknown tactic '{value}' (high, half or up).", nameof(value))
    };
}
