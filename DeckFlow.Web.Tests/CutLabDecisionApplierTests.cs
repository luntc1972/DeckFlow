using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabDecisionApplier"/> covering shared immutable decision application rules.</summary>
public sealed class CutLabDecisionApplierTests
{
    [Fact]
    public void Apply_Accept_AppendsAcceptedDecisionWithoutMutatingPool()
    {
        CutLabState state = BuildState();

        CutLabState updated = CutLabDecisionApplier.Apply(state, "Arcane Signet", CutLabDecideAction.Accept, "round-1");

        CutLabDecision decision = Assert.Single(updated.Decisions);
        Assert.Equal("Arcane Signet", decision.CardName);
        Assert.Equal(CutLabDecisionKind.Accepted, decision.Kind);
        Assert.Equal("round-1", decision.Round);
        Assert.Equal(1, decision.Ordinal);
        Assert.Same(state.Pool, updated.Pool);
        Assert.DoesNotContain(CutLabWorkingList.Derive(updated.Pool, updated.Decisions), card => card.Name == "Arcane Signet");
    }

    [Fact]
    public void Apply_Accept_OvershootingCard_ReturnsStateUnchanged()
    {
        CutLabState state = BuildStateWithQuantities(cardQuantity: 3, remainingCardsToCut: 1);

        CutLabState updated = CutLabDecisionApplier.Apply(state, "Arcane Signet", CutLabDecideAction.Accept, "round-1");

        Assert.Same(state, updated);
        Assert.Empty(updated.Decisions);
    }

    [Fact]
    public void Apply_Accept_FittingCard_AppendsAcceptedDecision()
    {
        CutLabState state = BuildStateWithQuantities(cardQuantity: 1, remainingCardsToCut: 1);

        CutLabState updated = CutLabDecisionApplier.Apply(state, "Arcane Signet", CutLabDecideAction.Accept, "round-1");

        CutLabDecision decision = Assert.Single(updated.Decisions);
        Assert.Equal("Arcane Signet", decision.CardName);
        Assert.Equal(CutLabDecisionKind.Accepted, decision.Kind);
    }

    [Theory]
    [InlineData(CutLabDecideAction.Reject, CutLabDecisionKind.Rejected)]
    [InlineData(CutLabDecideAction.Defer, CutLabDecisionKind.Deferred)]
    public void Apply_RejectOrDefer_AppendsDecisionAndLeavesCardInWorkingList(
        CutLabDecideAction action,
        CutLabDecisionKind expectedKind)
    {
        CutLabState state = BuildState();

        CutLabState updated = CutLabDecisionApplier.Apply(state, "Arcane Signet", action, "round-2");

        CutLabDecision decision = Assert.Single(updated.Decisions);
        Assert.Equal(expectedKind, decision.Kind);
        Assert.Same(state.Pool, updated.Pool);
        Assert.Contains(CutLabWorkingList.Derive(updated.Pool, updated.Decisions), card => card.Name == "Arcane Signet");
    }

    [Fact]
    public void Apply_Restore_RemovesAllDecisionRecordsForCardAndReturnsOriginalPoolEntry()
    {
        CutLabState state = BuildState(
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = CutLabDecisionKind.Deferred,
                Round = "round-2",
                Ordinal = 1,
            },
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = CutLabDecisionKind.Rejected,
                Round = "round-3",
                Ordinal = 2,
            },
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = CutLabDecisionKind.Accepted,
                Round = "round-1",
                Ordinal = 3,
            },
            new CutLabDecision
            {
                CardName = "Other Card",
                Kind = CutLabDecisionKind.Accepted,
                Round = "round-1",
                Ordinal = 4,
            });

        CutLabState updated = CutLabDecisionApplier.Apply(state, "Arcane Signet", CutLabDecideAction.Restore, "round-3");

        Assert.DoesNotContain(updated.Decisions, decision => string.Equals(decision.CardName, "Arcane Signet", StringComparison.OrdinalIgnoreCase));
        Assert.Single(updated.Decisions);
        CutLabPoolCard restored = Assert.Single(CutLabWorkingList.Derive(updated.Pool, updated.Decisions), card => card.Name == "Arcane Signet");
        Assert.Equal("Artifact", restored.TypeLine);
        Assert.Equal("pkg-1", restored.PackageId);
    }

    [Fact]
    public void Apply_AcceptLockedCommander_DoesNotCutCommanderAndPreservesCommanderLock()
    {
        CutLabState state = BuildState();

        CutLabState updated = CutLabDecisionApplier.Apply(state, "Commander", CutLabDecideAction.Accept, "round-1");

        Assert.Empty(updated.Decisions);
        CutLabPoolCard commander = Assert.Single(updated.Pool, card => card.Name == "Commander");
        Assert.True(commander.IsCommander);
        Assert.True(commander.IsLocked);
        Assert.Contains(CutLabWorkingList.Derive(updated.Pool, updated.Decisions), card => card.Name == "Commander");
    }

    [Fact]
    public void Apply_MultipleDecisions_AssignsStrictlyIncreasingOrdinals()
    {
        CutLabState state = BuildState();

        CutLabState deferred = CutLabDecisionApplier.Apply(state, "Arcane Signet", CutLabDecideAction.Defer, "round-2");
        CutLabState rejected = CutLabDecisionApplier.Apply(deferred, "Counterspell", CutLabDecideAction.Reject, "round-3");
        CutLabState accepted = CutLabDecisionApplier.Apply(rejected, "Lightning Greaves", CutLabDecideAction.Accept, "round-1");

        Assert.Equal([1, 2, 3], accepted.Decisions.Select(decision => decision.Ordinal).ToArray());
    }

    [Fact]
    public void LatestRoundForCard_UsesHighestOrdinalRoundAndFallsBackToRound1()
    {
        CutLabState state = BuildState(
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = CutLabDecisionKind.Deferred,
                Round = CutLabCutRoundEngine.Round2Key,
                Ordinal = 1,
            },
            new CutLabDecision
            {
                CardName = "Arcane Signet",
                Kind = CutLabDecisionKind.Rejected,
                Round = CutLabCutRoundEngine.Round3Key,
                Ordinal = 4,
            },
            new CutLabDecision
            {
                CardName = "Counterspell",
                Kind = CutLabDecisionKind.Accepted,
                Round = CutLabCutRoundEngine.Round1Key,
                Ordinal = 3,
            });

        Assert.Equal(CutLabCutRoundEngine.Round3Key, CutLabDecisionApplier.LatestRoundForCard(state, "Arcane Signet"));
        Assert.Equal(CutLabCutRoundEngine.Round1Key, CutLabDecisionApplier.LatestRoundForCard(state, "Lightning Greaves"));
    }

    private static CutLabState BuildState(params CutLabDecision[] decisions)
        => new()
        {
            Commander = "Commander",
            Pool =
            [
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
                    Name = "Arcane Signet",
                    Quantity = 1,
                    TypeLine = "Artifact",
                    PackageId = "pkg-1",
                },
                new CutLabPoolCard
                {
                    Name = "Counterspell",
                    Quantity = 99,
                    TypeLine = "Instant",
                },
                new CutLabPoolCard
                {
                    Name = "Lightning Greaves",
                    Quantity = 1,
                    TypeLine = "Artifact",
                },
            ],
            Decisions = decisions,
        };

    private static CutLabState BuildStateWithQuantities(int cardQuantity, int remainingCardsToCut)
    {
        int fillerQuantity = 99 + remainingCardsToCut - cardQuantity;
        CutLabPoolCard[] pool =
        [
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
                Name = "Arcane Signet",
                Quantity = cardQuantity,
                TypeLine = "Artifact",
                PackageId = "pkg-1",
            },
            new CutLabPoolCard
            {
                Name = "Filler Card",
                Quantity = fillerQuantity,
                TypeLine = "Instant",
            },
        ];

        return new CutLabState
        {
            Commander = "Commander",
            Pool = pool,
            Decisions = [],
        };
    }
}
