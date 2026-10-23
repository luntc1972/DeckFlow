namespace DeckFlow.Web.Services.Harvest;

/// <summary>Summarizes the currently eligible maintenance work.</summary>
public sealed record HarvestMaintenancePreview(int RetentionExcessDecks, int PurgeCandidates, int RefetchCandidates);
