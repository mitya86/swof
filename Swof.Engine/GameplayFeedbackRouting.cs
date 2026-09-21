namespace Swof.Engine;

public static class GameplayFeedbackRouting
{
    /// <summary>
    /// Returns whether a feedback event is relevant to the local team.
    /// Teamless events describe match-wide state and remain audible locally.
    /// </summary>
    public static bool IsRelevantToTeam(GameplayFeedbackEvent feedbackEvent, TeamSide localTeam)
    {
        ArgumentNullException.ThrowIfNull(feedbackEvent);
        return feedbackEvent.Team is null || feedbackEvent.Team == localTeam;
    }

    /// <summary>
    /// Converts world-space event direction into the bounded stereo pan expected by the audio API.
    /// </summary>
    public static float GetStereoPan(GameplayFeedbackEvent feedbackEvent)
    {
        ArgumentNullException.ThrowIfNull(feedbackEvent);
        return Math.Clamp(feedbackEvent.Direction.X, -1f, 1f);
    }
}
