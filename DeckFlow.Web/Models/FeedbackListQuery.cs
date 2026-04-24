namespace DeckFlow.Web.Models;

public sealed class FeedbackListQuery
{
    public FeedbackStatus? Status { get; set; } = FeedbackStatus.New;
    public FeedbackType? Type { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
