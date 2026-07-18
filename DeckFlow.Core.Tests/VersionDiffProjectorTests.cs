using DeckFlow.Core.History;

namespace DeckFlow.Core.Tests;

public sealed class VersionDiffProjectorTests
{
    private static DeckSnapshot Snapshot(string[] commander, params (string Name, int Qty)[] cards) => new()
    {
        Id = 1,
        Date = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
        Commander = commander,
        Cards = cards.Select(c => new SnapshotCard { Name = c.Name, Qty = c.Qty }).ToList(),
    };

    [Fact]
    public void Project_AddedAndCutCards_LandInAddsAndCuts()
    {
        var older = Snapshot(["Tivit, Seller of Secrets"], ("Sol Ring", 1), ("Dockside Extortionist", 1));
        var newer = Snapshot(["Tivit, Seller of Secrets"], ("Sol Ring", 1), ("Mystic Remora", 1));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Equal("Mystic Remora", Assert.Single(diff.Adds).Name);
        Assert.Equal("Dockside Extortionist", Assert.Single(diff.Cuts).Name);
        Assert.Empty(diff.QuantityChanges);
    }

    [Fact]
    public void Project_QuantityShift_LandsInQuantityChanges()
    {
        var older = Snapshot([], ("Island", 8));
        var newer = Snapshot([], ("Island", 7));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Empty(diff.Adds);
        Assert.Empty(diff.Cuts);
        var change = Assert.Single(diff.QuantityChanges);
        Assert.Equal("Island", change.Name);
        Assert.Equal(8, change.From);
        Assert.Equal(7, change.To);
    }

    [Fact]
    public void Project_CommanderSwap_AppearsAsAddAndCut()
    {
        var older = Snapshot(["Tivit, Seller of Secrets"], ("Sol Ring", 1));
        var newer = Snapshot(["Kraum, Ludevic's Opus"], ("Sol Ring", 1));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Equal("Kraum, Ludevic's Opus", Assert.Single(diff.Adds).Name);
        Assert.Equal("Tivit, Seller of Secrets", Assert.Single(diff.Cuts).Name);
    }

    [Fact]
    public void Project_NameMatchingIsNormalized_NotCaseSensitive()
    {
        var older = Snapshot([], ("Sol Ring", 1));
        var newer = Snapshot([], ("sol ring", 1));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Empty(diff.Adds);
        Assert.Empty(diff.Cuts);
        Assert.Empty(diff.QuantityChanges);
    }

    [Fact]
    public void Project_ResultsAreAlphabetical()
    {
        var older = Snapshot([], ("Zealous Conscripts", 1), ("Arcane Signet", 1));
        var newer = Snapshot([], ("Brainstorm", 1), ("Abrade", 1));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Equal(["Abrade", "Brainstorm"], diff.Adds.Select(a => a.Name).ToArray());
        Assert.Equal(["Arcane Signet", "Zealous Conscripts"], diff.Cuts.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void Project_IdenticalSnapshots_ReturnEmptyDiff()
    {
        var snapshot = Snapshot(["Tivit, Seller of Secrets"], ("Sol Ring", 1), ("Island", 8));

        var diff = VersionDiffProjector.Project(snapshot, snapshot);

        Assert.Empty(diff.Adds);
        Assert.Empty(diff.Cuts);
        Assert.Empty(diff.QuantityChanges);
    }
}
