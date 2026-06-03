using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

/// <summary>
/// Persists feedback submissions and admin review state over the configured relational database.
/// </summary>
public interface IFeedbackStore
{
    /// <summary>
    /// Stores a feedback submission with its request context.
    /// </summary>
    /// <param name="submission">Feedback payload submitted by the user.</param>
    /// <param name="context">Request metadata captured with the submission.</param>
    /// <param name="cancellationToken">Token used to cancel the insert.</param>
    /// <returns>The database id assigned to the new feedback row.</returns>
    Task<long> AddAsync(FeedbackSubmission submission, FeedbackRequestContext context, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns one feedback item by id, or null when no row exists.
    /// </summary>
    Task<FeedbackItem?> GetAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns a page of feedback items matching the supplied list query.
    /// </summary>
    Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the number of feedback rows matching optional status and type filters.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="type">Optional feedback type filter.</param>
    /// <param name="cancellationToken">Token used to cancel the count query.</param>
    /// <returns>The number of matching feedback submissions.</returns>
    Task<int> CountAsync(FeedbackStatus? status, FeedbackType? type, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns feedback counts grouped by review status.
    /// </summary>
    Task<IReadOnlyDictionary<FeedbackStatus, int>> CountsByStatusAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Updates the review status for a feedback row.
    /// </summary>
    /// <param name="id">Feedback row id to update.</param>
    /// <param name="status">New review status.</param>
    /// <param name="cancellationToken">Token used to cancel the update.</param>
    Task UpdateStatusAsync(long id, FeedbackStatus status, CancellationToken cancellationToken = default);
    /// <summary>
    /// Deletes a feedback row by id.
    /// </summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Produces the persisted representation for a client IP address.
    /// </summary>
    /// <returns>The salted hash of the IP address, or an empty string when no IP is supplied.</returns>
    string HashIp(string? ip);
}

/// <summary>
/// Captures request-side metadata stored with a feedback submission.
/// </summary>
/// <param name="Ip">Raw client IP address; the store salts and hashes it before persistence.</param>
/// <param name="UserAgent">Client User-Agent header, or null when absent.</param>
/// <param name="PageUrl">Page the feedback was submitted from, or null when unavailable.</param>
/// <param name="AppVersion">DeckFlow version string captured at submission time, or null.</param>
public sealed record FeedbackRequestContext(
    string? Ip,
    string? UserAgent,
    string? PageUrl,
    string? AppVersion);
