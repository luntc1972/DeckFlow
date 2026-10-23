using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Specifies ranking and selection of retention candidates.
/// </summary>
public sealed class RetentionRankingTests
{
    [Fact]
    public void SelectRemovals_CapExceededByOne_RemovesOnlyOldest()
    {
        var removals = RetentionRanking.SelectRemovals([new(1L, "deck-1", "Commander", Utc(1)), new(2L, "deck-2", "Commander", Utc(2)), new(3L, "deck-3", "Commander", Utc(3))], 2);

        Assert.Equal([1L], removals);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a date")]
    public void RankUtcFor_MissingOrBadUpdated_FallsBackToInserted(string? updatedUtc)
    {
        var insertedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        Assert.Equal(insertedUtc, RetentionRanking.RankUtcFor(updatedUtc, insertedUtc));
    }

    [Fact]
    public void SelectRemovals_EqualRankTimes_KeepsHigherQueueId()
    {
        var removals = RetentionRanking.SelectRemovals([new(1L, "deck-1", "Commander", Utc(1)), new(2L, "deck-2", "Commander", Utc(1)), new(3L, "deck-3", "Commander", Utc(1))], 2);

        Assert.Equal([1L], removals);
    }

    [Fact]
    public void SelectRemovals_Cutoff_RemovesOlderCandidatesUnderCap()
    {
        var removals = RetentionRanking.SelectRemovals([new(1L, "deck-1", "Commander", Utc(1)), new(2L, "deck-2", "Commander", Utc(3))], 100, Utc(2));

        Assert.Equal([1L], removals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SelectRemovals_CapBelowOne_Throws(int cap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetentionRanking.SelectRemovals([new(1L, "deck-1", "Commander", Utc(1))], cap));
    }

    [Fact]
    public void SelectRemovals_MultipleRemovals_ReturnedByQueueIdAscending()
    {
        var removals = RetentionRanking.SelectRemovals([new(9L, "deck-9", "Commander", Utc(1)), new(3L, "deck-3", "Commander", Utc(2)), new(7L, "deck-7", "Commander", Utc(3)), new(1L, "deck-1", "Commander", Utc(4)), new(5L, "deck-5", "Commander", Utc(5))], 2);

        Assert.Equal([3L, 7L, 9L], removals);
    }

    [Fact]
    public void SelectRemovals_CapAndCutoffCombined_RemovesBoth()
    {
        var removals = RetentionRanking.SelectRemovals([new(1L, "deck-1", "Commander", Utc(1)), new(2L, "deck-2", "Commander", Utc(2)), new(3L, "deck-3", "Commander", Utc(3)), new(4L, "deck-4", "Commander", Utc(4))], 3, Utc(3));

        Assert.Equal([1L, 2L], removals);
    }

    private static DateTimeOffset Utc(int day) => new(2026, 1, day, 0, 0, 0, TimeSpan.Zero);
}
