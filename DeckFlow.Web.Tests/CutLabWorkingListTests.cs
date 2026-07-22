using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabWorkingList"/> covering immutable working-list derivation.</summary>
public sealed class CutLabWorkingListTests
{
    [Fact]
    public void Derive_WithoutDecisions_ReturnsFullPoolUnchanged()
    {
        var pool = CreatePool();

        var workingList = CutLabWorkingList.Derive(pool, []);

        Assert.Equal(pool, workingList);
        Assert.Equal(["Arcane Signet", "Brainstorm", "Command Tower", "Aesi, Tyrant of Gyre Strait"], workingList.Select(card => card.Name).ToArray());
    }

    [Fact]
    public void Derive_LatestAcceptedDecision_ExcludesCardWithoutMutatingPool()
    {
        var pool = CreatePool();
        CutLabDecision[] decisions =
        [
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = CutLabDecisionKind.Deferred,
                Round = "round-1",
                Ordinal = 1,
            },
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = CutLabDecisionKind.Accepted,
                Round = "round-2",
                Ordinal = 2,
            },
        ];

        var workingList = CutLabWorkingList.Derive(pool, decisions);

        Assert.DoesNotContain(workingList, card => card.Name == "Arcane Signet");
        Assert.Contains(pool, card => card.Name == "Arcane Signet" && card.Quantity == 2 && card.TypeLine == "Artifact" && card.IsLocked && card.PackageId == "ramp");
    }

    [Fact]
    public void Derive_RemovingAcceptedDecision_RestoresOriginalCardMetadata()
    {
        var pool = CreatePool();
        CutLabDecision[] fullHistory =
        [
            new CutLabDecision
            {
                CardName = "Brainstorm",
                Kind = CutLabDecisionKind.Deferred,
                Round = "round-1",
                Ordinal = 1,
            },
            new CutLabDecision
            {
                CardName = "Brainstorm",
                Kind = CutLabDecisionKind.Rejected,
                Round = "round-2",
                Ordinal = 2,
            },
            new CutLabDecision
            {
                CardName = "Brainstorm",
                Kind = CutLabDecisionKind.Accepted,
                Round = "round-3",
                Ordinal = 3,
            },
        ];

        var excluded = CutLabWorkingList.Derive(pool, fullHistory);
        var restored = CutLabWorkingList.Derive(pool, fullHistory.Take(2).ToArray());
        var restoredWithoutHistory = CutLabWorkingList.Derive(pool, []);

        Assert.DoesNotContain(excluded, card => card.Name == "Brainstorm");
        Assert.Contains(restored, card => card.Name == "Brainstorm" && card.Quantity == 1 && card.TypeLine == "Instant" && !card.IsLocked && card.PackageId is null);
        Assert.Contains(restoredWithoutHistory, card => card.Name == "Brainstorm" && card.Quantity == 1 && card.TypeLine == "Instant" && !card.IsLocked && card.PackageId is null);
    }

    [Theory]
    [InlineData(CutLabDecisionKind.Rejected)]
    [InlineData(CutLabDecisionKind.Deferred)]
    public void Derive_NonAcceptedLatestDecision_KeepsCardAtFullPoolQuantity(CutLabDecisionKind kind)
    {
        var pool = CreatePool();
        CutLabDecision[] decisions =
        [
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = CutLabDecisionKind.Accepted,
                Round = "round-1",
                Ordinal = 1,
            },
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = kind,
                Round = "round-2",
                Ordinal = 2,
            },
        ];

        var workingList = CutLabWorkingList.Derive(pool, decisions);

        Assert.Contains(workingList, card => card.Name == "Arcane Signet" && card.Quantity == 2);
    }

    [Fact]
    public void Derive_LockedAndCommanderCardsRemainWhenTheyHaveNoAcceptedDecision()
    {
        var pool = CreatePool();
        CutLabDecision[] decisions =
        [
            new CutLabDecision
            {
                CardName = "Brainstorm",
                Kind = CutLabDecisionKind.Accepted,
                Round = "round-1",
                Ordinal = 1,
            },
        ];

        var workingList = CutLabWorkingList.Derive(pool, decisions);
        var acceptedCardNames = CutLabWorkingList.AcceptedCardNames(decisions);

        Assert.Contains(workingList, card => card.Name == "Aesi, Tyrant of Gyre Strait" && card.IsCommander);
        Assert.Contains(workingList, card => card.Name == "Arcane Signet" && card.IsLocked);
        Assert.Contains(workingList, card => card.Name == "Command Tower" && card.IsLocked);
        Assert.True(acceptedCardNames.Contains("brainstorm"));
        Assert.False(acceptedCardNames.Contains("Arcane Signet"));
    }

    [Fact]
    public void AcceptedCardNames_ReturnsSwapCandidatesFromCutPileOnly()
    {
        var pool =
            new[]
            {
                new CutLabPoolCard
                {
                    Name = "Commander",
                    Quantity = 1,
                    TypeLine = "Legendary Creature",
                    IsCommander = true,
                    IsLocked = true,
                },
                new CutLabPoolCard
                {
                    Name = "Working Card",
                    Quantity = 1,
                    TypeLine = "Spell",
                },
                new CutLabPoolCard
                {
                    Name = "Cut Card",
                    Quantity = 1,
                    TypeLine = "Spell",
                },
            };
        CutLabDecision[] decisions =
        [
            new CutLabDecision
            {
                CardName = "Cut Card",
                Kind = CutLabDecisionKind.Accepted,
                Round = CutLabCutRoundEngine.Round1Key,
                Ordinal = 1,
            },
        ];

        var workingList = CutLabWorkingList.Derive(pool, decisions);
        var swapCandidates = pool
            .Where(card => CutLabWorkingList.AcceptedCardNames(decisions).Contains(card.Name))
            .Select(card => card.Name)
            .ToArray();

        Assert.Equal(["Commander", "Working Card"], workingList.Select(card => card.Name).ToArray());
        Assert.Equal(["Cut Card"], swapCandidates);
        Assert.DoesNotContain("Working Card", swapCandidates);
    }

    [Fact]
    public void Derive_WithEmptyAdjustments_MatchesTwoArgumentOverload()
    {
        var pool = CreatePool();
        CutLabDecision[] decisions =
        [
            new CutLabDecision
            {
                CardName = "Brainstorm",
                Kind = CutLabDecisionKind.Rejected,
                Round = "round-1",
                Ordinal = 1,
            },
        ];

        IReadOnlyList<CutLabPoolCard> fromOldOverload = CutLabWorkingList.Derive(pool, decisions);
        IReadOnlyList<CutLabPoolCard> fromNewOverload = CutLabWorkingList.Derive(pool, decisions, []);

        Assert.Equal(fromOldOverload, fromNewOverload);
    }

    [Fact]
    public void Derive_AdjustmentOnExistingEntry_ClampsToLegalMax()
    {
        var pool = CreatePool();
        CutLabQuantityAdjustment[] adjustments =
        [
            new CutLabQuantityAdjustment
            {
                Name = "Arcane Signet",
                Delta = 4,
                IsAddedBasic = false,
            },
        ];

        IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(pool, [], adjustments);

        CutLabPoolCard adjusted = Assert.Single(workingList, card => card.Name == "Arcane Signet");
        Assert.Equal(1, adjusted.Quantity);
    }

    [Fact]
    public void Derive_AdjustmentToZero_DropsEntry()
    {
        var pool = CreatePool();
        CutLabQuantityAdjustment[] adjustments =
        [
            new CutLabQuantityAdjustment
            {
                Name = "Brainstorm",
                Delta = -1,
                IsAddedBasic = false,
            },
        ];

        IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(pool, [], adjustments);

        Assert.DoesNotContain(workingList, card => card.Name == "Brainstorm");
    }

    [Fact]
    public void Derive_AddedBasicWithoutMatchingEntry_MaterializesLandAtStableTailPosition()
    {
        var pool = CreatePool();
        CutLabQuantityAdjustment[] adjustments =
        [
            new CutLabQuantityAdjustment
            {
                Name = "Island",
                Delta = 2,
                IsAddedBasic = true,
            },
        ];

        IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(pool, [], adjustments);

        CutLabPoolCard island = Assert.Single(workingList, card => card.Name == "Island");
        Assert.Equal(2, island.Quantity);
        Assert.Equal("Basic Land — Island", island.TypeLine);
        Assert.False(island.IsCommander);
        Assert.False(island.IsLocked);
        Assert.Equal("Island", workingList[^1].Name);
        Assert.True(CutLabLockRules.IsLand(island.TypeLine));
    }

    [Fact]
    public void Derive_AppliesDecisionsBeforeAdjustments()
    {
        var pool = CreatePool();
        CutLabDecision[] decisions =
        [
            new CutLabDecision
            {
                CardName = "Brainstorm",
                Kind = CutLabDecisionKind.Accepted,
                Round = "round-1",
                Ordinal = 1,
            },
        ];
        CutLabQuantityAdjustment[] adjustments =
        [
            new CutLabQuantityAdjustment
            {
                Name = "Brainstorm",
                Delta = 1,
                IsAddedBasic = false,
            },
            new CutLabQuantityAdjustment
            {
                Name = "Island",
                Delta = 1,
                IsAddedBasic = true,
            },
        ];

        IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(pool, decisions, adjustments);

        Assert.DoesNotContain(workingList, card => card.Name == "Brainstorm");
        Assert.Equal("Island", workingList[^1].Name);
    }

    private static IReadOnlyList<CutLabPoolCard> CreatePool()
        =>
        [
            new CutLabPoolCard
            {
                Name = "Arcane Signet",
                Quantity = 2,
                TypeLine = "Artifact",
                IsLocked = true,
                PackageId = "ramp",
            },
            new CutLabPoolCard
            {
                Name = "Brainstorm",
                Quantity = 1,
                TypeLine = "Instant",
            },
            new CutLabPoolCard
            {
                Name = "Command Tower",
                Quantity = 1,
                TypeLine = "Land",
                IsLocked = true,
            },
            new CutLabPoolCard
            {
                Name = "Aesi, Tyrant of Gyre Strait",
                Quantity = 1,
                TypeLine = "Legendary Creature — Serpent",
                IsCommander = true,
                IsLocked = true,
            },
        ];
}
