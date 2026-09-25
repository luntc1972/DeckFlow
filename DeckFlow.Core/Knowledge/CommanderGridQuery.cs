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

    private static readonly IReadOnlyDictionary<string, CommanderSortColumn> SortColumns = new Dictionary<string, CommanderSortColumn>(StringComparer.Ordinal)
    {
        ["name"] = CommanderSortColumn.Name,
        ["last_processed"] = CommanderSortColumn.LastProcessed,
        ["deck_count"] = CommanderSortColumn.DeckCount
    };

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
            SortBy = sortBy is not null && SortColumns.TryGetValue(sortBy, out var sortColumn) ? sortColumn : CommanderSortColumn.DeckCount,
            Descending = sortDir != "asc"
        };
    }

    /// <summary>Canonical sort-column token.</summary>
    public string SortByToken => SortColumns.FirstOrDefault(pair => pair.Value == SortBy).Key ?? "deck_count";

    /// <summary>Canonical sort-direction token.</summary>
    public string SortDirToken => Descending ? "desc" : "asc";

    /// <summary>Returns whether <paramref name="column"/> is the active sort column.</summary>
    /// <param name="column">Column whose active state is requested.</param>
    /// <returns><see langword="true"/> when the column is active.</returns>
    public bool IsActive(CommanderSortColumn column) => SortBy == column;

    /// <summary>Returns the direction token requested by clicking <paramref name="column"/>.</summary>
    /// <param name="column">Column whose header was clicked.</param>
    /// <returns>The reverse current direction for an active column; otherwise <c>asc</c> for Name and <c>desc</c> for Deck Count and Last Processed.</returns>
    public string NextDirectionToken(CommanderSortColumn column)
    {
        // Why: Count and timestamp columns open on their most useful end (most decks, newest); names open A to Z.
        return IsActive(column) ? (Descending ? "asc" : "desc") : column == CommanderSortColumn.Name ? "asc" : "desc";
    }

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
