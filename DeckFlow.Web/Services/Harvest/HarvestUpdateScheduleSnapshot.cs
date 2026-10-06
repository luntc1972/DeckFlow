namespace DeckFlow.Web.Services.Harvest;

/// <summary>Update-harvest schedule state kept separate because the bulk row is CHECK-pinned.</summary>
public sealed record HarvestUpdateScheduleSnapshot(int? IntervalMinutes, bool Paused, DateTimeOffset UpdatedUtc);
