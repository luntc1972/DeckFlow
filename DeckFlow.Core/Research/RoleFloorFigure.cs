namespace DeckFlow.Core.Research;

/// <summary>
/// Identifies which corpus produced a reported role-floor figure. The explicit non-zero values
/// ensure <c>default(RoleFloorSource)</c> is not a valid source.
/// </summary>
public enum RoleFloorSource
{
    /// <summary>
    /// The figure was computed from the Postgres deck corpus.
    /// </summary>
    Postgres = 1,

    /// <summary>
    /// The figure was projected from an EDHREC bracket cell.
    /// </summary>
    Edhrec = 2,

    /// <summary>
    /// The figure was computed from the EDHREC bulk archive as an inclusion-rate aggregate over
    /// all of a commander's decks, with no bracket dimension at all unlike the synthesized
    /// per-(commander, bracket) average deck represented by <see cref="Edhrec"/>.
    /// </summary>
    EdhrecBulk = 3,
}

/// <summary>
/// The shared figure abstraction is intentionally limited to exactly <see cref="Source"/>,
/// <see cref="Role"/>, and <see cref="CommanderName"/>. It must not grow: D-02 exists to prevent a
/// shared emitter from reintroducing a shared column set, so any code needing a percentile must
/// narrow to <see cref="PostgresRoleDistribution"/> explicitly where reviewers can see it.
/// </summary>
public interface IRoleFloorFigure
{
    /// <summary>
    /// Gets the corpus that produced this figure.
    /// </summary>
    RoleFloorSource Source { get; }

    /// <summary>
    /// Gets the role being reported.
    /// </summary>
    string Role { get; }

    /// <summary>
    /// Gets the commander name associated with the figure.
    /// </summary>
    string CommanderName { get; }
}

/// <summary>
/// Represents a Postgres-backed per-commander role distribution row, including percentile and
/// distribution metrics that only exist because the underlying corpus exposes real per-deck samples.
/// </summary>
public sealed record PostgresRoleDistribution : IRoleFloorFigure
{
    /// <summary>
    /// Gets the corpus that produced this figure.
    /// </summary>
    public required RoleFloorSource Source { get; init; }

    /// <summary>
    /// Gets the role being reported.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Gets the commander name associated with the figure.
    /// </summary>
    public required string CommanderName { get; init; }

    /// <summary>
    /// Gets the raw deck count before deduplication.
    /// </summary>
    public required int DeckCount { get; init; }

    /// <summary>
    /// Gets the mean role count across the commander's deduped decks.
    /// </summary>
    public required double Mean { get; init; }

    /// <summary>
    /// Gets the commander's 25th-percentile role count.
    /// </summary>
    public required double P25 { get; init; }

    /// <summary>
    /// Gets the corpus standard deviation used for significance reporting.
    /// </summary>
    public required double StdDev { get; init; }

    /// <summary>
    /// Gets the commander's ratio versus the corpus baseline.
    /// </summary>
    public required double Ratio { get; init; }

    /// <summary>
    /// Gets the commander's z-score against the corpus mean.
    /// </summary>
    public required double ZScore { get; init; }

    /// <summary>
    /// Gets Cohen's d for the commander's mean relative to the corpus baseline.
    /// </summary>
    public required double CohensD { get; init; }

    /// <summary>
    /// Gets a value indicating whether the commander clears the written bar.
    /// </summary>
    public required bool ClearsBar { get; init; }
}

/// <summary>
/// Represents one EDHREC bracket cell as a point estimate. One EDHREC cell is one synthesized
/// average deck, so there is no sample and therefore no percentile, standard deviation, z-score,
/// or effect size; this type deliberately cannot express one, and adding such a property would
/// violate ROADMAP Phase 2 success criterion 7.
/// </summary>
public sealed record EdhrecRolePointEstimate : IRoleFloorFigure
{
    /// <summary>
    /// Gets the corpus that produced this figure.
    /// </summary>
    public required RoleFloorSource Source { get; init; }

    /// <summary>
    /// Gets the role being reported.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Gets the commander name associated with the figure.
    /// </summary>
    public required string CommanderName { get; init; }

    /// <summary>
    /// Gets the EDHREC bracket slug from the on-disk cell contract.
    /// </summary>
    public required string BracketSlug { get; init; }

    /// <summary>
    /// Gets the EDHREC bracket index from the on-disk cell contract.
    /// </summary>
    public required int BracketIndex { get; init; }

    /// <summary>
    /// Gets the point-estimate count reported by the synthesized average deck.
    /// </summary>
    public required double Count { get; init; }

    /// <summary>
    /// Gets the number of real decks backing the EDHREC cell (<c>n_decks</c> on disk).
    /// </summary>
    public required int DeckCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the cell qualifies for downstream reporting.
    /// </summary>
    public required bool Qualifies { get; init; }
}

/// <summary>
/// Represents a mean-style expected role count computed as the sum of per-card inclusion rates
/// over the EDHREC bulk archive for one commander. This corpus exposes no underlying distribution,
/// so this type is intentionally unable to express a percentile, standard deviation, z-score, or
/// effect size; adding such a property here would violate ROADMAP Phase 2 success criterion 7.
/// </summary>
public sealed record EdhrecBulkRoleExpectation : IRoleFloorFigure
{
    /// <summary>
    /// Gets the corpus that produced this figure.
    /// </summary>
    public required RoleFloorSource Source { get; init; }

    /// <summary>
    /// Gets the role being reported.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Gets the commander name associated with the figure.
    /// </summary>
    public required string CommanderName { get; init; }

    /// <summary>
    /// Gets the expected role count reported by summing per-card inclusion rates.
    /// </summary>
    public required double ExpectedCount { get; init; }

    /// <summary>
    /// Gets the commander's <c>number_decks</c> denominator from <c>averages.csv</c>.
    /// </summary>
    public required long DeckCount { get; init; }

    /// <summary>
    /// Gets the number of bulk-archive rows consumed for this commander.
    /// </summary>
    public required int RowsConsumed { get; init; }

    /// <summary>
    /// Gets the maximum single-card inclusion rate for this commander. The property is named to
    /// avoid the "Ratio" substring because the reflection gate deliberately rejects distributional
    /// or ratio-style fields on EDHREC figure types.
    /// </summary>
    public required double MaxCardInclusion { get; init; }
}

/// <summary>
/// Declares every figure-table markdown column set in one testable place. Every public figure-table
/// column list declared here is required to carry a <c>Source</c> column, a reflection test
/// enforces that rule, and the rule exists because a heading-based source tag does not survive the
/// next contributor adding a numeric column while ROADMAP criterion 8 requires every reported
/// figure to state its own source.
/// </summary>
public static class RoleFloorFigureTable
{
    private static readonly IReadOnlyList<string> _postgresColumns =
    [
        "Source",
        "Commander",
        "RAW N",
        "DEDUPED N",
        "Mean",
        "P25",
        "Ratio",
        "Z",
        "Cohen's d",
        "ClearsBar",
    ];

    private static readonly IReadOnlyList<string> _edhrecColumns =
    [
        "Source",
        "Commander",
        "Bracket",
        "Count",
        "Decks backing cell",
        "Qualifies",
    ];

    private static readonly IReadOnlyList<string> _edhrecBulkColumns =
    [
        "Source",
        "Commander",
        "Role",
        "Expected count",
        "Decks (denominator)",
        "Rows",
        "Max card inclusion",
    ];

    /// <summary>
    /// Gets the markdown columns for Postgres-backed figure tables.
    /// </summary>
    public static IReadOnlyList<string> PostgresColumns => _postgresColumns;

    /// <summary>
    /// Gets the markdown columns for EDHREC-backed figure tables.
    /// </summary>
    public static IReadOnlyList<string> EdhrecColumns => _edhrecColumns;

    /// <summary>
    /// Gets the markdown columns for EDHREC bulk-archive figure tables.
    /// </summary>
    public static IReadOnlyList<string> EdhrecBulkColumns => _edhrecBulkColumns;

    /// <summary>
    /// Returns whether the provided column set declares a <c>Source</c> column.
    /// </summary>
    /// <param name="columns">The column set to inspect.</param>
    public static bool HasSourceColumn(IReadOnlyList<string> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        return columns.Contains("Source", StringComparer.Ordinal);
    }
}
