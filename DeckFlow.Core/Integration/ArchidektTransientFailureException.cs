using System.Net;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Indicates that a transient Archidekt failure requires the current harvest run to stop.
/// </summary>
public sealed class ArchidektTransientFailureException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArchidektTransientFailureException"/> class.
    /// </summary>
    /// <param name="message">The upstream transient failure message.</param>
    /// <param name="statusCode">The upstream status code when available.</param>
    public ArchidektTransientFailureException(string message, HttpStatusCode? statusCode = null)
        : base(message, inner: null, statusCode)
    {
    }
}
