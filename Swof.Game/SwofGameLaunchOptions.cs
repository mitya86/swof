namespace Swof.Game;

internal sealed record SwofGameLaunchOptions(
    MatchFeedbackMode FeedbackMode,
    MatchDemoAction DemoAction)
{
    public static SwofGameLaunchOptions Default { get; } = new(
        MatchFeedbackMode.Connected,
        MatchDemoAction.None);

    public static SwofGameLaunchOptions Parse(IEnumerable<string> arguments)
    {
        var feedbackMode = MatchFeedbackMode.Connected;
        var demoAction = MatchDemoAction.None;
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
        }

        return new SwofGameLaunchOptions(feedbackMode, demoAction);
    }
}
