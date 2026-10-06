namespace DeckFlow.Web.Services.Harvest;

/// <summary>Cached update-harvest schedule contract.</summary>
public interface IHarvestUpdateScheduleCache
{
    /// <summary>Returns the current snapshot.</summary>
    HarvestUpdateScheduleSnapshot Snapshot();
    /// <summary>Refreshes the snapshot.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
