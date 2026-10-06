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
}
