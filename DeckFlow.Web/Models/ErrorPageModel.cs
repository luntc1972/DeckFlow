namespace DeckFlow.Web.Models;

/// <summary>
/// View data for the branded error page, covering both unhandled exceptions and re-executed
/// status codes (404, 403, and friends).
/// </summary>
public sealed record ErrorPageModel
{
    /// <summary>
    /// Gets the originating HTTP status code, or <see langword="null"/> when the page was reached
    /// through the exception handler rather than <c>UseStatusCodePagesWithReExecute</c>.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>Gets a value indicating whether this render is a "page not found" case.</summary>
    public bool IsNotFound => StatusCode == 404;

    /// <summary>Gets a value indicating whether this render is an access-denied case.</summary>
    public bool IsForbidden => StatusCode is 401 or 403;
}
