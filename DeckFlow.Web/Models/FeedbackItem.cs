namespace DeckFlow.Web.Models;

/// <summary>Persisted feedback row displayed in the admin feedback console.</summary>
/// <param name="Id">Database identifier for the feedback item.</param>
/// <param name="CreatedUtc">UTC timestamp when the feedback was submitted.</param>
/// <param name="Type">Kind of feedback submitted by the user.</param>
/// <param name="Message">Feedback body supplied by the user.</param>
/// <param name="Email">Optional contact email supplied by the user.</param>
/// <param name="PageUrl">Page URL where the feedback was submitted, when available.</param>
/// <param name="UserAgent">Browser user agent captured with the submission, when available.</param>
/// <param name="IpHash">Salted hash of the submitter IP, when captured.</param>
/// <param name="AppVersion">Application version captured with the submission, when available.</param>
/// <param name="Status">Current moderation or triage status for the feedback item.</param>
public sealed record FeedbackItem(
    long Id,
    DateTime CreatedUtc,
    FeedbackType Type,
    string Message,
    string? Email,
    string? PageUrl,
    string? UserAgent,
    string? IpHash,
    string? AppVersion,
    FeedbackStatus Status);
