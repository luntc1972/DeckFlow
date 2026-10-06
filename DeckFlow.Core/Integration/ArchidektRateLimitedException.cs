using System.Net;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Indicates that Archidekt rate limiting requires the current operation to stop.
/// </summary>
/// <remarks>
/// Interactive imports share the limiter, and existing Web catch sites handle <c>HttpRequestException</c>,
/// so a trip degrades to the upstream-error message instead of a 500. Harvest paths rethrow it explicitly
/// in 06-03 and 06-11.
/// </remarks>
public sealed class ArchidektRateLimitedException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArchidektRateLimitedException"/> class.
    /// </summary>
    /// <param name="message">The upstream rate-limit failure message.</param>
    /// <param name="retryAfter">The upstream retry delay when available.</param>
    public ArchidektRateLimitedException(string message, TimeSpan? retryAfter)
        : base(message, inner: null, HttpStatusCode.TooManyRequests)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets the upstream retry delay when Archidekt provided one.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
