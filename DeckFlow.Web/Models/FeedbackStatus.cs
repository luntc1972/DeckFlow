namespace DeckFlow.Web.Models;

/// <summary>Admin triage status for a feedback item.</summary>
public enum FeedbackStatus
{
    /// <summary>Feedback has not yet been reviewed.</summary>
    New = 0,
    /// <summary>Feedback has been reviewed by an admin.</summary>
    Read = 1,
    /// <summary>Feedback is hidden from the active admin queue.</summary>
    Archived = 2
}
