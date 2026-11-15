using Swof.Engine.AI;

namespace Swof.Game;

internal sealed record SwofGameLaunchOptions(
    MatchFeedbackMode FeedbackMode,
    MatchDemoAction DemoAction,
    TeamTactic OpponentTactic)
{
    public static SwofGameLaunchOptions Default { get; } = new(
        MatchFeedbackMode.Connected,
        MatchDemoAction.None,
        TeamTactic.HighPress);

    public static SwofGameLaunchOptions Parse(IEnumerable<string> arguments)
    {
        var feedbackMode = MatchFeedbackMode.Connected;
        var demoAction = MatchDemoAction.None;
        var opponentTactic = TeamTactic.HighPress;
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
                var value = argument["--opponent-tactic=".Length..];
                opponentTactic = value.ToLowerInvariant() switch
                {
                    "high" => TeamTactic.HighPress,
                    "half" => TeamTactic.HalfCourt,
                    _ => throw new ArgumentException($"Unknown opponent tactic '{value}' (high or half).", nameof(arguments))
                };
            }
        }

        return new SwofGameLaunchOptions(feedbackMode, demoAction, opponentTactic);
    }
}
