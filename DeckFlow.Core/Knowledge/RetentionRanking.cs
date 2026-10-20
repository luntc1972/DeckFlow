using System.Globalization;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Ranks retained decks and selects candidates that exceed retention limits.
/// </summary>
internal static class RetentionRanking
{
    /// <summary>
    /// Gets the timestamp used to rank a stored deck.
    /// </summary>
    /// <param name="updatedUtc">Optional Archidekt update timestamp.</param>
    /// <param name="insertedUtc">Queue insertion timestamp.</param>
    /// <returns>The parsed update timestamp, or the insertion timestamp when parsing fails.</returns>
    internal static DateTimeOffset RankUtcFor(string? updatedUtc, DateTimeOffset insertedUtc)
        => DateTimeOffset.TryParse(updatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : insertedUtc;

    /// <summary>
    /// Selects queue rows to remove after applying a per-commander cap and optional age cutoff.
    /// </summary>
    /// <param name="candidates">Eligible stored decks.</param>
    /// <param name="cap">Maximum retained decks.</param>
    /// <param name="cutoffUtc">Optional age cutoff.</param>
    /// <returns>Queue identifiers selected for removal in ascending order.</returns>
    internal static IReadOnlyList<long> SelectRemovals(IReadOnlyList<RetentionCandidate> candidates, int cap, DateTimeOffset? cutoffUtc = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfLessThan(cap, 1);

        var overCap = candidates
            .OrderByDescending(candidate => candidate.RankUtc)
            .ThenByDescending(candidate => candidate.DeckQueueId)
            .Skip(cap)
            .Select(candidate => candidate.DeckQueueId);
        var olderThanCutoff = cutoffUtc.HasValue
            ? candidates.Where(candidate => candidate.RankUtc < cutoffUtc.Value).Select(candidate => candidate.DeckQueueId)
            : Enumerable.Empty<long>();

        return overCap.Concat(olderThanCutoff).Distinct().Order().ToArray();
    }
}
