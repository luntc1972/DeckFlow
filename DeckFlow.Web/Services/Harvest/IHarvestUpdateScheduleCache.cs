namespace DeckFlow.Web.Services.Harvest;

/// <summary>Cached update-harvest schedule contract.</summary>
public interface IHarvestUpdateScheduleCache
{
    /// <summary>Returns the current snapshot.</summary>
    HarvestUpdateScheduleSnapshot Snapshot();
    /// <summary>Forces the current in-memory snapshot to Paused after a committed auto-pause.</summary>
    void ForcePausedSnapshot();
    /// <summary>Refreshes the snapshot.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
