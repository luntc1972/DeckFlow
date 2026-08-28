using DeckFlow.Web.Services.Scryfall;

namespace DeckFlow.CLI;

/// <summary>
/// Writes a one-line summary of <see cref="ScryfallCollectionCardCache"/> activity to the console,
/// so a CLI run can be compared across feature-flag arms.
/// </summary>
internal static class ScryfallCacheStatisticsReporter
{
    /// <summary>Writes a single invariant summary line for the supplied cache.</summary>
    /// <param name="cache">The collection cache whose counters are reported.</param>
    public static void Report(ScryfallCollectionCardCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);

        ScryfallCollectionCacheStatistics statistics = cache.GetStatistics();
        Console.WriteLine(FormattableString.Invariant(
            $"scryfall collection cache: enabled={statistics.Enabled}, hits={statistics.Hits}, misses={statistics.Misses}, stores={statistics.Stores}, bypasses={statistics.Bypasses}"));
    }
}
