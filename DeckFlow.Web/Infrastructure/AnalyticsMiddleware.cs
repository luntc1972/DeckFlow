using DeckFlow.Web.Extensions;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services.Analytics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Infrastructure;

/// <summary>
/// IMiddleware implementation that captures per-request analytics metrics and enqueues
/// them into <see cref="RequestMetricsBuffer"/> for asynchronous persistence (D-05, D-11,
/// D-12, D-13, ANLY-01, ANLY-03, ANLY-06).
/// </summary>
/// <remarks>
/// Placement: registered AFTER <c>app.UseRouting()</c> so <c>HttpContext.GetEndpoint()</c>
/// returns the resolved route; registered BEFORE <c>app.MapControllers()</c> per D-12.
/// Static assets (<c>/css/</c>, <c>/js/</c>, <c>/lib/</c>, <c>/extensions/</c>,
/// <c>/favicon.ico</c>, <c>/_health</c>) are filtered by path-prefix check BEFORE endpoint
/// resolution to avoid cardinality blow-up (D-11, ANLY-06).
/// All post-pipeline capture work is wrapped in a try/catch so analytics exceptions never
/// propagate into the request pipeline (T-08-14).
/// </remarks>
public sealed class AnalyticsMiddleware : IMiddleware
{
    private readonly RequestMetricsBuffer _buffer;
    private readonly AnalyticsSaltAccessor _saltAccessor;
    private readonly ILogger<AnalyticsMiddleware> _logger;

    /// <summary>
    /// Initialises the analytics middleware with its buffer, salt accessor, and logger.
    /// </summary>
    /// <param name="buffer">Singleton write-behind buffer for metric events.</param>
    /// <param name="saltAccessor">Holds the IP-hash salt resolved once at startup (D-13).</param>
    /// <param name="logger">Structured logger for capture failures.</param>
    public AnalyticsMiddleware(
        RequestMetricsBuffer buffer,
        AnalyticsSaltAccessor saltAccessor,
        ILogger<AnalyticsMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(saltAccessor);
        ArgumentNullException.ThrowIfNull(logger);
        _buffer = buffer;
        _saltAccessor = saltAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Why: status-code re-execution runs this pipeline a second time to render the error
        // page, so recording it would duplicate the original request's analytics event.
        if (context.Features.Get<IStatusCodeReExecuteFeature>() is not null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // (D-11 / ANLY-06) Filter static assets by path prefix BEFORE endpoint resolution.
        // Avoids high-cardinality versioned asset paths entering the metrics table.
        var path = context.Request.Path.Value;
        if (path is not null && (
            path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/extensions/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/_health", StringComparison.OrdinalIgnoreCase)))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Run the full request pipeline before capturing the response status.
        await next(context).ConfigureAwait(false);

        // Capture metrics on the way out. Swallow all exceptions so analytics never
        // propagates into the request pipeline (T-08-14).
        try
        {
            // (D-05 / D-06) Route key from resolved endpoint — never from Request.Path.Value,
            // which would cause cardinality blow-up on parameterised routes (T-08-12).
            var endpoint = context.GetEndpoint();
            var routeKey = endpoint?.DisplayName ?? "__unmatched__";

            // (D-04) Skip 1xx informational responses — not meaningful for analytics.
            var status = context.Response.StatusCode;
            if (status < 200 || status >= 600)
            {
                return;
            }

            // (D-04) Map to coarse status class: 2xx+3xx -> 2, 4xx -> 4, 5xx -> 5.
            short statusClass = status switch
            {
                >= 200 and < 400 => 2,
                >= 400 and < 500 => 4,
                _ => 5,
            };

            // (D-07) Operator-chosen is_error definition: errors are 4xx (except 404)
            // and 5xx. 404 is excluded because "not found" is not an operator-actionable
            // error; it is normal traffic exploring invalid paths.
            var isError = status >= 400 && status != 404 && status < 600;

            // (D-13 / ANLY-03) Hash the client IP using the salt resolved at startup.
            // Returns null when the salt is not yet populated (first-request race on cold start).
            var salt = _saltAccessor.Salt;
            var ipHash = salt is null ? null : IpHasher.HashRequestIp(context, salt);

            var dayUtc = DateOnly.FromDateTime(DateTime.UtcNow);

            _buffer.Enqueue(new RequestMetricEvent(routeKey, dayUtc, statusClass, isError, ipHash));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Analytics.Middleware.CaptureFailed analytics capture failed but request succeeded.");
        }
    }
}
