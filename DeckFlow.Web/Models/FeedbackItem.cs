namespace DeckFlow.Web.Models;

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
