namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Sort columns available to the harvested-commanders grid.
/// </summary>
public enum CommanderSortColumn
{
    /// <summary>Sorts by processed deck count.</summary>
    DeckCount,
    /// <summary>Sorts by commander name.</summary>
    Name,
    /// <summary>Sorts by most recent processing time.</summary>
    LastProcessed
}

/// <summary>
/// Carries every commander-grid search and sort rule.
/// </summary>
public sealed record CommanderGridQuery
{
    /// <summary>Maximum number of search characters accepted from a request.</summary>
    public const int MaxSearchTermLength = 100;

    /// <summary>Character used to escape SQL LIKE metacharacters.</summary>
    public const char LikeEscapeCharacter = '\\';

    /// <summary>Optional operator-supplied search term.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Requested sort column.</summary>
    public CommanderSortColumn SortBy { get; init; } = CommanderSortColumn.DeckCount;

    /// <summary>Whether results sort descending.</summary>
    public bool Descending { get; init; } = true;

    /// <summary>Default grid query.</summary>
    public static CommanderGridQuery Default { get; } = new();

    /// <summary>Creates a safe query from request tokens.</summary>
    public static CommanderGridQuery FromRequest(string? search, string? sortBy, string? sortDir)
    {
        var trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (trimmedSearch?.Length > MaxSearchTermLength)
        {
            trimmedSearch = trimmedSearch[..MaxSearchTermLength];
        }

        return new CommanderGridQuery
        {
            SearchTerm = trimmedSearch,
            SortBy = sortBy switch
            {
                "name" => CommanderSortColumn.Name,
                "last_processed" => CommanderSortColumn.LastProcessed,
                "deck_count" => CommanderSortColumn.DeckCount,
                _ => CommanderSortColumn.DeckCount
            },
            Descending = sortDir != "asc"
        };
    }

    /// <summary>Canonical sort-column token.</summary>
    public string SortByToken => SortBy switch
    {
        CommanderSortColumn.Name => "name",
        CommanderSortColumn.LastProcessed => "last_processed",
        _ => "deck_count"
    };

    /// <summary>Canonical sort-direction token.</summary>
    public string SortDirToken => Descending ? "desc" : "asc";

    /// <summary>Escaped, normalized SQL LIKE prefix pattern.</summary>
    public string? SqlPrefixPattern
    {
        get
        {
            var key = CommanderSearchKey.Normalize(SearchTerm);
            if (key is null)
            {
                return null;
            }

            // Why: escaping metacharacters prevents widening a prefix search and protects the appended wildcard.
            return key.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal) + "%";
        }
    }

    /// <summary>Injective cache key for row-affecting query inputs.</summary>
    public string CacheToken => $"{SortByToken}|{SortDirToken}|{(SqlPrefixPattern is null ? "-" : $"+{SqlPrefixPattern}")}";
}
