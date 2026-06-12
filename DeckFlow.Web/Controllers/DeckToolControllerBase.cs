using Microsoft.AspNetCore.Mvc;
using System.Threading;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Provides shared timeout helpers for deck tool controllers extracted from <see cref="DeckController" />.
/// </summary>
public abstract class DeckToolControllerBase : Controller
{
    /// <summary>
    /// Gets the soft per-request timeout budget for lookup-family actions.
    /// </summary>
    protected static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets the soft per-request timeout budget for suggestion-family actions.
    /// </summary>
    protected static readonly TimeSpan SuggestionTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Creates a linked timeout scope for the current HTTP request.
    /// </summary>
    /// <param name="timeout">The soft timeout budget to apply.</param>
    /// <returns>A linked cancellation-token source that callers own and must dispose.</returns>
    protected CancellationTokenSource CreateTimeoutScope(TimeSpan timeout)
    {
        var timeoutScope = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
        timeoutScope.CancelAfter(timeout);
        // Why: upstream error translation already lives in static helpers; the base only keeps real cross-cutting timeout behavior.
        return timeoutScope;
    }
}
