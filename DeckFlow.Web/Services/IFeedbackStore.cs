using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

public interface IFeedbackStore
{
    Task<long> AddAsync(FeedbackSubmission submission, FeedbackRequestContext context, CancellationToken cancellationToken = default);
    Task<FeedbackItem?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken cancellationToken = default);
    Task<int> CountAsync(FeedbackStatus? status, FeedbackType? type, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<FeedbackStatus, int>> CountsByStatusAsync(CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(long id, FeedbackStatus status, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    string HashIp(string? ip);
}

public sealed record FeedbackRequestContext(
    string? Ip,
    string? UserAgent,
    string? PageUrl,
    string? AppVersion);
