namespace DeckFlow.Web.Services.Harvest;

/// <summary>Persistence contract for the separate update-harvest schedule.</summary>
public interface IHarvestUpdateScheduleStore
{
    /// <summary>Ensures the parallel schedule table and its seed row exist.</summary>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets the schedule snapshot.</summary>
    Task<HarvestUpdateScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default);
    /// <summary>Saves interval and pause state.</summary>
    Task SaveAsync(int? intervalMinutes, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Saves the interval without changing the existing pause state.</summary>
    /// <param name="intervalMinutes">Interval in minutes, or null to disable scheduled runs.</param>
    /// <param name="now">Wall-clock time stamped into <c>updated_utc</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task SaveIntervalAsync(int? intervalMinutes, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Sets the pause state without changing the existing interval.</summary>
    /// <param name="paused">Whether scheduler ticks should short-circuit.</param>
    /// <param name="now">Wall-clock time stamped into <c>updated_utc</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task SetPausedAsync(bool paused, DateTimeOffset now, CancellationToken cancellationToken = default);
}
