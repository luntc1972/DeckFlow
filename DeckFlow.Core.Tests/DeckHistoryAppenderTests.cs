using DeckFlow.Core.History;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

public sealed class DeckHistoryAppenderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-16T12:00:00Z");

    private static DeckEntry Entry(string name, int qty, string board = "mainboard") => new()
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = qty,
        Board = board,
    };

    [Fact]
    public void BuildSnapshot_SplitsCommanderFromMainboardAndDropsMaybeboard()
    {
        var entries = new[]
        {
            Entry("Tivit, Seller of Secrets", 1, "commander"),
            Entry("Sol Ring", 1),
            Entry("Rhystic Study", 1, "maybeboard"),
        };

        var snapshot = DeckHistoryAppender.BuildSnapshot(entries, "note", "label", Now);

        Assert.Equal("Tivit, Seller of Secrets", Assert.Single(snapshot.Commander));
        Assert.Equal("Sol Ring", Assert.Single(snapshot.Cards).Name);
        Assert.Equal("note", snapshot.Notes);
        Assert.Equal("label", snapshot.Label);
        Assert.Equal(Now, snapshot.Date);
    }

    [Fact]
    public void Append_ToNewFile_AssignsIdOneAndEmptyDelta()
    {
        var file = DeckHistoryAppender.CreateNew("My Deck", null);
        var snapshot = DeckHistoryAppender.BuildSnapshot([Entry("Sol Ring", 1)], null, null, Now);

        var result = DeckHistoryAppender.Append(file, snapshot);

        Assert.True(result.Appended);
        var appended = Assert.Single(result.File.Versions);
        Assert.Equal(1, appended.Id);
        Assert.NotNull(appended.Delta);
        Assert.Empty(appended.Delta!.Adds);
        Assert.Empty(appended.Delta.Cuts);
    }

    [Fact]
    public void Append_SecondVersion_GetsNextIdAndComputedDelta()
    {
        var file = DeckHistoryAppender.CreateNew("My Deck", null);
        file = DeckHistoryAppender.Append(
            file, DeckHistoryAppender.BuildSnapshot([Entry("Sol Ring", 1)], null, null, Now)).File;

        var second = DeckHistoryAppender.BuildSnapshot(
            [Entry("Sol Ring", 1), Entry("Mystic Remora", 1)], "added remora", null, Now.AddDays(1));
        var result = DeckHistoryAppender.Append(file, second);

        Assert.True(result.Appended);
        Assert.Equal(2, result.File.Versions.Count);
        Assert.Equal(2, result.File.Versions[1].Id);
        Assert.Equal("Mystic Remora", Assert.Single(result.File.Versions[1].Delta!.Adds).Name);
    }

    [Fact]
    public void Append_IdenticalDeck_DoesNotAppendAndWarns()
    {
        var file = DeckHistoryAppender.CreateNew("My Deck", null);
        file = DeckHistoryAppender.Append(
            file, DeckHistoryAppender.BuildSnapshot([Entry("Sol Ring", 1)], null, null, Now)).File;

        var duplicate = DeckHistoryAppender.BuildSnapshot([Entry("Sol Ring", 1)], "same", null, Now.AddDays(1));
        var result = DeckHistoryAppender.Append(file, duplicate);

        Assert.False(result.Appended);
        Assert.Single(result.File.Versions);
        Assert.Contains("identical", result.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecomputeDeltas_OverwritesHandEditedDeltas()
    {
        var tampered = new DeckHistoryFile
        {
            DeckName = "x",
            Versions =
            [
                new DeckSnapshot { Id = 1, Date = Now, Cards = [new SnapshotCard { Name = "Sol Ring", Qty = 1 }] },
                new DeckSnapshot
                {
                    Id = 2,
                    Date = Now.AddDays(1),
                    Cards = [new SnapshotCard { Name = "Mystic Remora", Qty = 1 }],
                    Delta = new SnapshotDelta { Adds = [new SnapshotCard { Name = "FAKE CARD", Qty = 9 }] },
                },
            ],
        };

        var recomputed = DeckHistoryAppender.RecomputeDeltas(tampered);

        Assert.Equal("Mystic Remora", Assert.Single(recomputed.Versions[1].Delta!.Adds).Name);
        Assert.Equal("Sol Ring", Assert.Single(recomputed.Versions[1].Delta!.Cuts).Name);
    }
}
