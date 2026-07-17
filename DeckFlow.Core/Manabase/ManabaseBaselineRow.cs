namespace DeckFlow.Core.Manabase;

/// <summary>
/// Canonical string values for the <c>source</c> column of the manabase baseline table.
/// Stored verbatim (lowercase) so the column reads the same across dialects and future EDHREC rows.
/// </summary>
public static class ManabaseBaselineSources
{
    /// <summary>Rows aggregated from DeckFlow's own classified crawl corpus.</summary>
    public const string Corpus = "corpus";

    /// <summary>Rows backfilled from EDHREC (optional, permission-gated — not written in this milestone).</summary>
    public const string Edhrec = "edhrec";

    /// <summary>The commander_slug sentinel identifying the global-per-bracket fallback row.</summary>
    public const string GlobalCommanderSlug = "*";
}

/// <summary>
/// One persisted baseline cell: the average lands/ramp/draw a set of decks ran for a given
/// (commander, bracket, source), with the sample size behind the average. A row where
/// <see cref="CommanderSlug"/> equals <see cref="ManabaseBaselineSources.GlobalCommanderSlug"/>
/// is the global-per-bracket fallback. Averages are always present (computed over the sample).
/// </summary>
public sealed record ManabaseBaselineRow
{
    /// <summary>Canonical commander key, or <c>*</c> for the global-per-bracket fallback row.</summary>
    public required string CommanderSlug { get; init; }

    /// <summary>Power bracket 1-5 (Exhibition..cEDH).</summary>
    public required int Bracket { get; init; }

    /// <summary>Data source: <see cref="ManabaseBaselineSources.Corpus"/> or <see cref="ManabaseBaselineSources.Edhrec"/>.</summary>
    public required string Source { get; init; }

    /// <summary>Average land count across the sample.</summary>
    public required double AvgLands { get; init; }

    /// <summary>Average ramp count across the sample (classified as the analyzer's ramp budget).</summary>
    public required double AvgRamp { get; init; }

    /// <summary>Average card-draw count across the sample (classified as the analyzer's draw budget).</summary>
    public required double AvgDraw { get; init; }

    /// <summary>Number of decks behind the averages (weighting + display).</summary>
    public required int DeckCount { get; init; }

    /// <summary>UTC time the cell was computed.</summary>
    public required DateTime ComputedUtc { get; init; }
}
