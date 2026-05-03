namespace DeckFlow.Web.Services.Analytics;

/// <summary>
/// Postgres-only persistence contract for request-level analytics aggregates.
/// Implements D-01 (per-route/day/status_class hit + error counters) and D-03
/// (ip_seen side table for unique-IP counting without storing raw IPs).
/// </summary>
/// <remarks>
/// This interface is intentionally Postgres-only: the analytics feature is a
/// paid-tier addition and has no SQLite branch. Callers on SQLite (local-dev)
/// should not register an implementation; the store no-ops gracefully on
/// <see cref="EnsureSchemaAsync"/> when the underlying connection is not Postgres.
/// </remarks>
public interface IRequestMetricsStore
{
    /// <summary>
    /// Ensures the <c>request_metrics</c> and <c>request_metric_ip_seen</c> tables
    /// exist in the Postgres database. Idempotent: safe to call on every startup.
    /// No-ops gracefully when the underlying connection is not Postgres.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a batch of <see cref="RequestMetricEvent"/> records to Postgres in a
    /// single transaction. Uses <c>unnest(@arrays)</c> bulk UPSERT for efficiency:
    /// <list type="bullet">
    /// <item><description><c>request_metrics</c>: ON CONFLICT (route_key, day_utc, status_class) DO UPDATE — increments hit_count and error_count.</description></item>
    /// <item><description><c>request_metric_ip_seen</c>: ON CONFLICT (route_key, day_utc, ip_hash) DO NOTHING — deduplicates IPs per route per day.</description></item>
    /// </list>
    /// Returns immediately when <paramref name="events"/> is empty.
    /// </summary>
    /// <param name="events">Batch of events to persist. May be empty.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertBatchAsync(IReadOnlyList<RequestMetricEvent> events, CancellationToken cancellationToken = default);
}
