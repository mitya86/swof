using Swof.Engine.AI;

namespace Swof.Game;

internal sealed record SwofGameLaunchOptions(
    MatchFeedbackMode FeedbackMode,
    MatchDemoAction DemoAction,
    TeamTactic OpponentTactic,
    TeamTactic TeamTactic,
    bool OpponentFollowsScore,
    bool StartPaused = false,
    string? CapturePath = null)
{
    public static SwofGameLaunchOptions Default { get; } = new(
        MatchFeedbackMode.Connected,
        MatchDemoAction.None,
        TeamTactic.HighPress,
        TeamTactic.HighPress,
        OpponentFollowsScore: false);

    public static SwofGameLaunchOptions Parse(IEnumerable<string> arguments)
    {
        var feedbackMode = MatchFeedbackMode.Connected;
        var demoAction = MatchDemoAction.None;
        var opponentTactic = TeamTactic.HighPress;
        var teamTactic = TeamTactic.HighPress;
        var opponentFollowsScore = false;
        var startPaused = false;
        string? capturePath = null;
        foreach (var argument in arguments)
        {
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
            else if (argument.Equals("--opponent-follows-score", StringComparison.OrdinalIgnoreCase))
            {
                opponentFollowsScore = true;
            }
            else if (argument.StartsWith("--capture=", StringComparison.OrdinalIgnoreCase))
            {
                capturePath = argument["--capture=".Length..];
            }
            else if (argument.Equals("--start-paused", StringComparison.OrdinalIgnoreCase))
            {
                startPaused = true;
            }
            else if (argument.StartsWith("--team-tactic=", StringComparison.OrdinalIgnoreCase))
            {
                teamTactic = ParseTactic(argument["--team-tactic=".Length..]);
            }
        }

        return new SwofGameLaunchOptions(feedbackMode, demoAction, opponentTactic, teamTactic, opponentFollowsScore, startPaused, capturePath);
    }

    private static TeamTactic ParseTactic(string value) => value.ToLowerInvariant() switch
    {
        "high" => TeamTactic.HighPress,
        "half" => TeamTactic.HalfCourt,
        "up" => TeamTactic.PushUp,
        _ => throw new ArgumentException($"Unknown tactic '{value}' (high, half or up).", nameof(value))
    };
}
