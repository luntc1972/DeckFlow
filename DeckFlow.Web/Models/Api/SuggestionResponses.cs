using System;
using System.Collections.Generic;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;

namespace DeckFlow.Web.Models.Api;

/// <summary>
/// Response payload returned from the card suggestion API.
/// </summary>
public sealed class CategorySuggestionApiResponse
{
    /// <summary>
    /// Card name that was queried.
    /// </summary>
    public string CardName { get; init; } = string.Empty;
    /// <summary>
    /// Exact category text from the optional reference deck.
    /// </summary>
    public string ExactCategoriesText { get; init; } = string.Empty;
    /// <summary>Prompt context explaining the exact reference-deck categories.</summary>
    public string ExactSuggestionContextText { get; init; } = string.Empty;
    /// <summary>
    /// Categories inferred from the local cache.
    /// </summary>
    public string InferredCategoriesText { get; init; } = string.Empty;
    /// <summary>Prompt context explaining locally inferred categories.</summary>
    public string InferredSuggestionContextText { get; init; } = string.Empty;
    /// <summary>
    /// Fallback themes inferred from EDHREC data.
    /// </summary>
    public string EdhrecCategoriesText { get; init; } = string.Empty;
    /// <summary>Prompt context explaining EDHREC-derived category hints.</summary>
    public string EdhrecSuggestionContextText { get; init; } = string.Empty;
    /// <summary>Whether the response includes exact reference-deck categories.</summary>
    public bool HasExactCategories { get; init; }
    /// <summary>Whether the response includes locally inferred categories.</summary>
    public bool HasInferredCategories { get; init; }
    /// <summary>Whether the response includes EDHREC-derived category hints.</summary>
    public bool HasEdhrecCategories { get; init; }
    /// <summary>
    /// Oracle/functional tags from Scryfall Tagger.
    /// </summary>
    public string TaggerCategoriesText { get; init; } = string.Empty;
    /// <summary>Prompt context explaining Scryfall Tagger category hints.</summary>
    public string TaggerSuggestionContextText { get; init; } = string.Empty;
    /// <summary>Whether the response includes Scryfall Tagger category hints.</summary>
    public bool HasTaggerCategories { get; init; }
    /// <summary>Human-readable summary of the sources that contributed suggestions.</summary>
    public string? SuggestionSourceSummary { get; init; }
    /// <summary>Whether every suggestion source returned no useful category data.</summary>
    public bool NoSuggestionsFound { get; init; }
    /// <summary>User-facing message shown when no suggestions are available.</summary>
    public string? NoSuggestionsMessage { get; init; }
    /// <summary>Deck-count totals describing how often the card appears in harvested data.</summary>
    public CardDeckTotals CardDeckTotals { get; init; } = CardDeckTotals.Empty;
}

/// <summary>
/// Response payload returned from the commander category API.
/// </summary>
public sealed class CommanderCategoryApiResponse
{
    /// <summary>
    /// Commander name that was queried.
    /// </summary>
    public string CommanderName { get; init; } = string.Empty;
    /// <summary>Number of card rows matched for the commander.</summary>
    public int CardRowCount { get; init; }
    /// <summary>Number of distinct categories represented in the response.</summary>
    public int CategoryCount { get; init; }
    /// <summary>Number of harvested decks contributing commander data.</summary>
    public int HarvestedDeckCount { get; init; }
    /// <summary>Deck-count totals for the cards included in the commander category response.</summary>
    public CardDeckTotals CardDeckTotals { get; init; } = CardDeckTotals.Empty;
    /// <summary>Category summaries sorted for display by the commander category UI.</summary>
    public IReadOnlyList<CommanderCategorySummaryDto> Summaries { get; init; } = Array.Empty<CommanderCategorySummaryDto>();
    /// <summary>User-facing message shown when the commander has no harvested results.</summary>
    public string? NoResultsMessage { get; init; }
}

/// <summary>
/// Simple DTO describing a commander category summary.
/// </summary>
public sealed class CommanderCategorySummaryDto
{
    /// <summary>Category label assigned to harvested commander cards.</summary>
    public string Category { get; init; } = string.Empty;
    /// <summary>Total card rows assigned to the category.</summary>
    public int Count { get; init; }
    /// <summary>Total harvested decks represented by the category.</summary>
    public int DeckCount { get; init; }
}

/// <summary>
/// Response payload returned from the mechanic rules lookup API.
/// </summary>
public sealed class MechanicLookupApiResponse
{
    /// <summary>
    /// Mechanic name that was queried.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// Whether a matching mechanic entry was found.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Matched mechanic or rules term.
    /// </summary>
    public string? MechanicName { get; init; }

    /// <summary>
    /// Primary rule reference for the match.
    /// </summary>
    public string? RuleReference { get; init; }

    /// <summary>
    /// Explains how the mechanic was matched.
    /// </summary>
    public string? MatchType { get; init; }

    /// <summary>
    /// Official rules text returned from Wizards.
    /// </summary>
    public string? RulesText { get; init; }

    /// <summary>
    /// Optional summary text when available.
    /// </summary>
    public string? SummaryText { get; init; }

    /// <summary>
    /// Official Wizards rules page URL.
    /// </summary>
    public string RulesPageUrl { get; init; } = string.Empty;

    /// <summary>
    /// Direct URL to the current Comprehensive Rules text file.
    /// </summary>
    public string? RulesTextUrl { get; init; }

    /// <summary>
    /// User-facing not found message.
    /// </summary>
    public string? NotFoundMessage { get; init; }
}

/// <summary>Request payload used to start a bounded Archidekt cache harvest job.</summary>
public sealed class ArchidektCacheJobStartRequest
{
    /// <summary>Maximum number of seconds the harvest job should run.</summary>
    public int DurationSeconds { get; init; }
}

/// <summary>Status payload describing the current or most recent Archidekt cache harvest job.</summary>
public class ArchidektCacheJobStatusResponse
{
    /// <summary>Stable identifier for the harvest job.</summary>
    public Guid JobId { get; init; }
    /// <summary>Current lifecycle state of the harvest job.</summary>
    public string State { get; init; } = string.Empty;
    /// <summary>Requested maximum runtime for the harvest job.</summary>
    public int DurationSeconds { get; init; }
    /// <summary>UTC timestamp when the job was requested.</summary>
    public DateTimeOffset RequestedUtc { get; init; }
    /// <summary>UTC timestamp when processing started, if it has started.</summary>
    public DateTimeOffset? StartedUtc { get; init; }
    /// <summary>UTC timestamp when processing completed, if it has completed.</summary>
    public DateTimeOffset? CompletedUtc { get; init; }
    /// <summary>Number of decks processed by the harvest job.</summary>
    public int DecksProcessed { get; init; }
    /// <summary>Number of additional decks discovered while processing.</summary>
    public int AdditionalDecksFound { get; init; }
    /// <summary>Error message captured from the job, if it failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>Response payload returned after requesting an Archidekt cache harvest job.</summary>
public sealed class ArchidektCacheJobEnqueueResponse : ArchidektCacheJobStatusResponse
{
    /// <summary>Whether the request started a new job instead of reusing an active one.</summary>
    public bool StartedNewJob { get; init; }
    /// <summary>URL clients can poll for job status.</summary>
    public string StatusUrl { get; init; } = string.Empty;
}
