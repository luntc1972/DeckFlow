namespace DeckFlow.Web.Services.Harvest;

/// <summary>Specifies an operator-requested harvest maintenance operation.</summary>
public enum HarvestMaintenanceOperation
{
    /// <summary>Removes stored decks exceeding configured retention limits.</summary>
    Drain
}
