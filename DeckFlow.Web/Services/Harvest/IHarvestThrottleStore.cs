namespace DeckFlow.Web.Services.Harvest;

/// <summary>Persists the single-row Archidekt throttle state.</summary>
public interface IHarvestThrottleStore
{
    /// <summary>Ensures the throttle table and its seed row exist.</summary>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the persisted throttle state.</summary>
    Task<HarvestThrottleSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the operator rate and applies it to the process-wide limiter.</summary>
    Task SaveRateAsync(int ratePerMinute, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Marks a trip and pauses both automated schedules atomically.</summary>
    Task MarkRateLimitedAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Clears a trip and resumes both schedules, or returns false and writes nothing when no marker is set; callers reload both schedule caches afterwards.</summary>
    Task<bool> ResumeAfterRateLimitAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
