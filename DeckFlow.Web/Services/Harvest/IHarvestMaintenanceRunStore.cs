namespace DeckFlow.Web.Services.Harvest;

/// <summary>Persists maintenance runs across process restarts.</summary>
public interface IHarvestMaintenanceRunStore
{
    /// <summary>Creates the schema and interrupts runs from the previous process.</summary>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
    /// <summary>Records an accepted start.</summary>
    Task InsertAsync(HarvestMaintenanceStatus status, CancellationToken cancellationToken = default);
    /// <summary>Updates progress while preserving terminal states.</summary>
    Task SaveAsync(HarvestMaintenanceStatus status, CancellationToken cancellationToken = default);
    /// <summary>Returns the most recently started run.</summary>
    Task<HarvestMaintenanceStatus?> GetLatestAsync(CancellationToken cancellationToken = default);
}
