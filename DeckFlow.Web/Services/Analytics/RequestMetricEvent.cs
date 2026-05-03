namespace DeckFlow.Web.Services.Analytics;

/// <summary>
/// Immutable event record representing a single observed HTTP request for analytics
/// aggregation. Produced by the analytics middleware (Wave 3) and buffered for batch
/// flushing into <c>request_metrics</c> and <c>request_metric_ip_seen</c> by the
/// flusher (Wave 2).
/// </summary>
/// <param name="RouteKey">Normalized route identifier, e.g. <c>"Deck/Index"</c>.</param>
/// <param name="DayUtc">The UTC calendar date the request was observed.</param>
/// <param name="StatusClass">HTTP status class as a short: 200, 301, 400, 500, etc. (first digit × 100).</param>
/// <param name="IsError">
/// <c>true</c> when the response status is 4xx or 5xx; used to increment
/// <c>error_count</c> in <c>request_metrics</c>.
/// </param>
/// <param name="IpHash">
/// SHA-256 hex hash of the client IP (via <see cref="DeckFlow.Web.Security.IpHasher"/>),
/// or <c>null</c> when the IP could not be resolved. Stored in
/// <c>request_metric_ip_seen</c> for unique-IP counting without retaining PII (SC #3).
/// </param>
public sealed record RequestMetricEvent(
    string RouteKey,
    DateOnly DayUtc,
    short StatusClass,
    bool IsError,
    string? IpHash);
