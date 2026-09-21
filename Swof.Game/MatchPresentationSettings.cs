namespace Swof.Game;

internal readonly record struct MatchPresentationSettings(
    MatchFeedbackMode FeedbackMode,
    bool CameraEffectsEnabled,
    bool ShowDebugHud)
{
    public float FeedbackIntensity => FeedbackMode == MatchFeedbackMode.Dry ? 0f : 1f;

    public string FeedbackLabel => FeedbackMode.ToString().ToUpperInvariant();
}
