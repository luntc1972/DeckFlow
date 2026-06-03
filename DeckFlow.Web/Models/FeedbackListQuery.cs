namespace DeckFlow.Web.Models;

/// <summary>Query model used to filter and page the admin feedback list.</summary>
public sealed class FeedbackListQuery
{
    /// <summary>Feedback status filter applied to the admin list.</summary>
    public FeedbackStatus? Status { get; set; } = FeedbackStatus.New;
    /// <summary>Feedback type filter applied to the admin list.</summary>
    public FeedbackType? Type { get; set; }
    /// <summary>One-based admin list page number.</summary>
    public int Page { get; set; } = 1;
    /// <summary>Number of feedback items requested per admin list page.</summary>
    public int PageSize { get; set; } = 50;
}
