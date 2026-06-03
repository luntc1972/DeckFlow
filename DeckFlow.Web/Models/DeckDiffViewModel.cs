using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;

namespace DeckFlow.Web.Models;

/// <summary>View model that carries the deck sync workflow state and generated prompt artifacts.</summary>
public sealed class DeckDiffViewModel
{
    /// <summary>Deck workflow tab that should render as active.</summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.Sync;

    /// <summary>Deck diff form input echoed back to the Razor view.</summary>
    public DeckDiffRequest Request { get; init; } = new();

    /// <summary>Category suggestion form input echoed back to the Razor view.</summary>
    public CategorySuggestionRequest SuggestionRequest { get; init; } = new();

    /// <summary>Computed deck diff, if a sync request completed successfully.</summary>
    public DeckDiff? Diff { get; init; }

    /// <summary>Text export containing only the deck delta.</summary>
    public string? DeltaText { get; init; }

    /// <summary>Text export containing the full import payload.</summary>
    public string? FullImportText { get; init; }

    /// <summary>Human-readable deck comparison report.</summary>
    public string? ReportText { get; init; }

    /// <summary>Checklist text for resolving deck swaps.</summary>
    public string? SwapChecklistText { get; init; }

    /// <summary>Instructions text for using the generated deck sync artifacts.</summary>
    public string? InstructionsText { get; init; }

    /// <summary>Error message shown for deck sync failures.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Error message shown for category suggestion failures.</summary>
    public string? SuggestionErrorMessage { get; init; }

    /// <summary>Exact categories found from the optional reference deck.</summary>
    public string? ExactSuggestedCategoriesText { get; init; }

    /// <summary>Prompt context explaining exact category suggestions.</summary>
    public string? ExactSuggestionContextText { get; init; }

    /// <summary>Categories inferred from harvested local deck data.</summary>
    public string? InferredCategoriesText { get; init; }

    /// <summary>Prompt context explaining inferred category suggestions.</summary>
    public string? InferredSuggestionContextText { get; init; }

    /// <summary>Fallback category hints derived from EDHREC themes.</summary>
    public string? EdhrecCategoriesText { get; init; }

    /// <summary>Prompt context explaining EDHREC-derived category hints.</summary>
    public string? EdhrecSuggestionContextText { get; init; }

    /// <summary>Category hints returned by Scryfall Tagger.</summary>
    public string? TaggerCategoriesText { get; init; }

    /// <summary>Prompt context explaining Scryfall Tagger category hints.</summary>
    public string? TaggerSuggestionContextText { get; init; }

    /// <summary>Whether no category suggestion source produced a useful result.</summary>
    public bool NoSuggestionsFound { get; init; }

    /// <summary>User-facing message shown when no suggestions are available.</summary>
    public string? NoSuggestionsMessage { get; init; }

    /// <summary>Human-readable summary of the suggestion sources that contributed results.</summary>
    public string? SuggestionSourceSummary { get; init; }
    /// <summary>Deck-count totals describing harvested appearances for the suggested card.</summary>
    public CardDeckTotals CardDeckTotals { get; init; } = CardDeckTotals.Empty;
}
