namespace DeckFlow.Web.Services.Harvest;

/// <summary>Contains the operator text and presentation state for a maintenance run.</summary>
public sealed record HarvestMaintenanceStatusView(string State, string Tone, string Headline, IReadOnlyList<string> Details, bool IsActive);
