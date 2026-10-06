namespace DeckFlow.Web.Services.Harvest;

/// <summary>Persists the single-row Archidekt throttle state.</summary>
public interface IHarvestThrottleStore
{
    /// <summary>Ensures the throttle table and its seed row exist.</summary>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the persisted throttle state.</summary>
    Task<HarvestThrottleSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Marks a trip and pauses both automated schedules atomically.</summary>
    Task MarkRateLimitedAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
