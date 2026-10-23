namespace DeckFlow.Web.Services.Harvest;

/// <summary>Specifies an operator-requested harvest maintenance operation.</summary>
public enum HarvestMaintenanceOperation
{
    /// <summary>Removes stored decks exceeding configured retention limits after rollout step 3 purges then drains.</summary>
    Drain,

    /// <summary>Rollout step 3 operation that purges stored decks before drain.</summary>
    Purge,

    /// <summary>Rollout step 4 operation that marks decks for refetch.</summary>
    Refetch
}
