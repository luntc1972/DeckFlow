using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;

namespace DeckFlow.Web.Infrastructure;

/// <summary>
/// Converts cancellation caused by a disconnected client into an HTTP 499 response.
/// </summary>
public sealed class ClientAbortMiddleware
{
    /// <summary>
    /// Status code used when the client closes the request before it completes.
    /// </summary>
    public const int ClientClosedRequestStatusCode = 499;

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes the middleware.
    /// </summary>
    /// <param name="next">Next request delegate in the pipeline.</param>
    public ClientAbortMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>
    /// Invokes the next middleware and handles client disconnect cancellation.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <returns>A task representing the request.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        // Why: Serilog logs unhandled exceptions as 500, although a client disconnect is a 499.
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ClientClosedRequestStatusCode;
                // Why: status-code pages would re-execute the request and turn a client abort into an error page.
                var statusCodePages = context.Features.Get<IStatusCodePagesFeature>();
                if (statusCodePages is not null)
                {
                    statusCodePages.Enabled = false;
                }
            }
        }
    }
}
