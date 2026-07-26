using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for the pure round-sequencing engine used by Cut Lab phase 103.</summary>
public sealed class CutLabCutRoundEngineTests
{
    [Fact]
    public void BuildQueue_TwoDiscriminatingFindings_PlacesCardInRound1()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Round 1 Card", 4),
            Card("Round 2 Card", 2),
            Card("Round 3 Card", 1),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.CurveCongestion, "Round 1 Card"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Round 1 Card"),
                Finding(CutLabFindingKind.EnablerStarved, "Round 2 Card")),
            [],
            cardsToCutTarget: 3);

        CutLabRoundQueueItem round1 = Assert.Single(plan.Queue, item => item.CardName == "Round 1 Card");
        Assert.Equal(CutLabCutRoundEngine.Round1Key, round1.RoundKey);
        Assert.Equal(CutLabCutRoundEngine.Round1Label, round1.RoundLabel);
        Assert.Equal(2, round1.FindingCount);
        Assert.Equal(
            [CutLabFindingKind.CurveCongestion, CutLabFindingKind.StrandedSubtheme],
            round1.DiscriminatingFindingKinds);
    }

    [Fact]
    public void BuildQueue_WholeRoleFindingsDoNotInflateDiscriminatingTally()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Role Mate A", 3, roles: ["interaction"]),
            Card("Role Mate B", 4, roles: ["interaction"]),
            Card("Fallback", 5),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.WeakFloorCase, "Role Mate A", "Role Mate B"),
                Finding(CutLabFindingKind.RedundantFinishers, "Role Mate A", "Role Mate B")),
            [],
            cardsToCutTarget: 3);

        Assert.DoesNotContain(plan.Queue, item => item.RoundKey == CutLabCutRoundEngine.Round1Key);

        CutLabRoundQueueItem roleMate = Assert.Single(plan.Queue, item => item.CardName == "Role Mate A");
        Assert.Equal(CutLabCutRoundEngine.Round3Key, roleMate.RoundKey);
        Assert.Equal(0, roleMate.FindingCount);
        Assert.Empty(roleMate.DiscriminatingFindingKinds);
    }

    [Fact]
    public void BuildQueue_OneDiscriminatingFindingGoesToRound2_AndZeroFindingsGoToRound3()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Structural Choice", 3),
            Card("Preference Call", 1),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.CurveCongestion, "Structural Choice")),
            [],
            cardsToCutTarget: 2);

        CutLabRoundQueueItem round2 = Assert.Single(plan.Queue, item => item.CardName == "Structural Choice");
        CutLabRoundQueueItem round3 = Assert.Single(plan.Queue, item => item.CardName == "Preference Call");

        Assert.Equal(CutLabCutRoundEngine.Round2Key, round2.RoundKey);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, round3.RoundKey);
    }

    [Fact]
    public void BuildQueue_ExcludesLockedCommanderAndAcceptedCards()
    {
        IReadOnlyList<CutLabPoolCard> pool =
        [
            new CutLabPoolCard
            {
                Name = "Accepted Card",
                Quantity = 1,
                TypeLine = "Creature",
            },
            new CutLabPoolCard
            {
                Name = "Locked Card",
                Quantity = 1,
                TypeLine = "Artifact",
                IsLocked = true,
            },
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
                Name = "Eligible Card",
                Quantity = 1,
                TypeLine = "Sorcery",
            },
        ];

        CutLabDecision[] decisions =
        [
            new CutLabDecision
            {
                CardName = "Accepted Card",
                Kind = CutLabDecisionKind.Accepted,
                Round = CutLabCutRoundEngine.Round1Key,
                Ordinal = 1,
            },
        ];

        IReadOnlyList<CutLabRoundInputCard> workingList = pool
            .Select(card => Card(card.Name, 2, isLocked: card.IsLocked, isCommander: card.IsCommander))
            .ToArray();

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            decisions,
            cardsToCutTarget: 4);

        Assert.Equal(["Eligible Card"], plan.Queue.Select(item => item.CardName).ToArray());
    }

    [Fact]
    public void BuildQueue_CardQuantityExceedsRemainingTarget_ExcludesCardFromQueueAndNextProposal()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Overshoots Target", quantity: 35, manaValue: 1),
            Card("Fits Target", quantity: 2, manaValue: 2),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 2);

        Assert.DoesNotContain(plan.Queue, item => item.CardName == "Overshoots Target");
        Assert.NotNull(plan.NextProposal);
        Assert.Equal("Fits Target", plan.NextProposal!.CardName);
    }

    [Fact]
    public void BuildQueue_CardQuantityFitsRemainingTarget_IncludesCardInQueue()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Fits Target", quantity: 2, manaValue: 1),
            Card("Later Card", quantity: 1, manaValue: 2),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 2);

        Assert.Contains(plan.Queue, item => item.CardName == "Fits Target");
        Assert.Equal("Fits Target", plan.NextProposal?.CardName);
    }

    [Fact]
    public void BuildQueue_SingleCopyCardWithinRemainingTarget_StillIncludesCardInQueue()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Single Copy", quantity: 1, manaValue: 1),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 1);

        CutLabRoundQueueItem item = Assert.Single(plan.Queue);
        Assert.Equal("Single Copy", item.CardName);
        Assert.Equal("Single Copy", plan.NextProposal?.CardName);
    }

    [Fact]
    public void BuildQueue_Round3UsesDeltaOrderAndDeterministicFallback()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Alpha", 5),
            Card("Beta", 2),
            Card("Gamma", 2),
        ];

        CutLabRoundPlan deltaOrdered = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 3,
            round3DeltaMagnitudes: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Alpha"] = 0.9,
                ["Beta"] = 0.2,
                ["Gamma"] = 0.2,
            });

        Assert.Equal(["Beta", "Gamma", "Alpha"], deltaOrdered.Queue.Select(item => item.CardName).ToArray());

        CutLabRoundPlan firstFallback = CutLabCutRoundEngine.BuildQueue(workingList, Findings(), [], cardsToCutTarget: 3);
        CutLabRoundPlan secondFallback = CutLabCutRoundEngine.BuildQueue(workingList, Findings(), [], cardsToCutTarget: 3);

        Assert.Equal(["Beta", "Gamma", "Alpha"], firstFallback.Queue.Select(item => item.CardName).ToArray());
        Assert.Equal(firstFallback.Queue.Select(item => item.CardName), secondFallback.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void BuildQueue_AppendsDeferredThenRejectedCardsForLoopPass()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Structural Choice", 3),
            Card("Preference Call", 1),
            Card("Deferred Card", 4),
            Card("Rejected Card", 5),
        ];

        CutLabDecision[] decisions =
        [
            new CutLabDecision
            {
                CardName = "Deferred Card",
                Kind = CutLabDecisionKind.Deferred,
                Round = CutLabCutRoundEngine.Round2Key,
                Ordinal = 4,
            },
            new CutLabDecision
            {
                CardName = "Rejected Card",
                Kind = CutLabDecisionKind.Rejected,
                Round = CutLabCutRoundEngine.Round3Key,
                Ordinal = 6,
            },
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.CurveCongestion, "Structural Choice")),
            decisions,
            cardsToCutTarget: 4);

        Assert.Equal(
            [
                "Structural Choice",
                "Preference Call",
                "Deferred Card",
                "Rejected Card",
            ],
            plan.Queue.Select(item => item.CardName).ToArray());

        Assert.Equal(CutLabCutRoundEngine.Round2Label, plan.Queue[0].RoundLabel);
        Assert.Equal(CutLabCutRoundEngine.Round3Label, plan.Queue[1].RoundLabel);
        Assert.Equal(CutLabCutRoundEngine.SecondPassDeferredLabel, plan.Queue[2].RoundLabel);
        Assert.Equal(CutLabCutRoundEngine.SecondPassRejectedLabel, plan.Queue[3].RoundLabel);
    }

    [Fact]
    public void BuildQueue_ReturnsNextProposalAndCardsRemainingWithoutRoundGaps()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Immediate Proposal", 4),
            Card("Later Proposal", 2),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.CurveCongestion, "Immediate Proposal"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Immediate Proposal")),
            [],
            cardsToCutTarget: 2);

        Assert.NotNull(plan.NextProposal);
        Assert.Equal("Immediate Proposal", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round1Label, plan.NextProposal.RoundLabel);
        Assert.Equal(2, plan.CardsRemainingToTarget);
        Assert.Equal(plan.Queue[0], plan.NextProposal);
    }

    [Fact]
    public void BuildQueue_LockedOvershootRanksLeastCriticalRolesThenPrimaryTypes()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Payoff Creature", 1, isLocked: true, roles: ["payoffs"], typeLine: "Creature"),
            Card("Wincon Sorcery", 1, isLocked: true, roles: ["wincons"], typeLine: "Sorcery"),
            Card("Wincon Artifact", 1, isLocked: true, roles: ["wincons"], typeLine: "Artifact"),
            Card("Ramp Land", 1, quantity: 99, isLocked: true, isLand: true, roles: ["lands"]),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 2);

        Assert.Null(plan.NextProposal);
        CutLabLockedOvershootAdvisory advisory = Assert.IsType<CutLabLockedOvershootAdvisory>(plan.LockedOvershootAdvisory);
        Assert.Equal(2, advisory.CardsOverTarget);
        Assert.Equal(
            ["Creature", "Planeswalker", "Battle", "Instant", "Sorcery", "Artifact", "Enchantment", "Land", "Other"],
            CutLabViewModel.TypeGroupOrder);
        Assert.Collection(
            advisory.Groups,
            group =>
            {
                Assert.Equal("wincons", group.RoleKey);
                Assert.Equal(["Wincon Sorcery", "Wincon Artifact"], group.CardNames);
            },
            group =>
            {
                Assert.Equal("payoffs", group.RoleKey);
                Assert.Equal(["Payoff Creature"], group.CardNames);
            },
            group =>
            {
                Assert.Equal("lands", group.RoleKey);
                Assert.Equal(["Ramp Land"], group.CardNames);
            });
    }

    [Fact]
    public void BuildQueue_LockedOvershootAdvisoryAppearsBeforeQueueIsExhausted()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Locked Stack", 1, quantity: 105, isLocked: true, roles: ["payoffs"], typeLine: "Artifact"),
            Card("Immediate Proposal", 1, quantity: 1, typeLine: "Creature"),
            Card("Later Proposal", 2, quantity: 1, typeLine: "Sorcery"),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 7);

        Assert.NotEmpty(plan.Queue);
        Assert.NotNull(plan.NextProposal);
        Assert.Equal("Immediate Proposal", plan.NextProposal!.CardName);
        Assert.NotNull(plan.LockedOvershootAdvisory);
        Assert.Equal(5, plan.LockedOvershootAdvisory!.CardsOverTarget);
    }

    [Fact]
    public void BuildQueue_AtTargetDoesNotProduceLockedOvershootAdvisory()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Locked Card", 1, isLocked: true, roles: ["wincons"])],
            Findings(),
            [],
            cardsToCutTarget: 0);

        Assert.Equal(0, plan.CardsRemainingToTarget);
        Assert.Null(plan.LockedOvershootAdvisory);
    }

    [Theory]
    [InlineData(CutLabCutRoundEngine.Round1Key, CutLabCutRoundEngine.Round1Label, "Cards flagged by 2 or more structural findings from the section above.")]
    [InlineData(CutLabCutRoundEngine.Round2Key, CutLabCutRoundEngine.Round2Label, "Cards flagged by exactly one structural finding.")]
    [InlineData(CutLabCutRoundEngine.Round3Key, CutLabCutRoundEngine.Round3Label, "Everything else, ordered by smallest measurable tradeoff first.")]
    [InlineData(CutLabCutRoundEngine.SecondPassDeferredKey, CutLabCutRoundEngine.SecondPassDeferredLabel, "Still over 100 cards. These were deferred or kept earlier; take another look.")]
    [InlineData(CutLabCutRoundEngine.SecondPassRejectedKey, CutLabCutRoundEngine.SecondPassRejectedLabel, "Still over 100 cards. These were deferred or kept earlier; take another look.")]
    public void RoundHelpers_KnownRoundKeys_ReturnExpectedLabelAndBannerBody(string roundKey, string expectedLabel, string expectedBannerBody)
    {
        Assert.True(CutLabCutRoundEngine.IsKnownRoundKey(roundKey));
        Assert.Equal(expectedLabel, CutLabCutRoundEngine.LabelFor(roundKey));
        Assert.Equal(expectedBannerBody, CutLabCutRoundEngine.RoundBannerBodyFor(roundKey));
    }

    [Fact]
    public void RoundHelpers_UnknownRoundKey_FallsBackPredictably()
    {
        const string unknownRoundKey = "mystery-round";

        Assert.False(CutLabCutRoundEngine.IsKnownRoundKey(unknownRoundKey));
        Assert.False(CutLabCutRoundEngine.IsKnownRoundKey(null));
        Assert.Equal(unknownRoundKey, CutLabCutRoundEngine.LabelFor(unknownRoundKey));
        Assert.Equal(string.Empty, CutLabCutRoundEngine.RoundBannerBodyFor(unknownRoundKey));
    }

    private static CutLabRoundInputCard Card(
        string name,
        double manaValue,
        int quantity = 1,
        bool isLocked = false,
        bool isCommander = false,
        bool isLand = false,
        string? typeLine = null,
        IReadOnlyList<string>? roles = null,
        IReadOnlyList<string>? categories = null)
        => new(
            name,
            quantity,
            typeLine ?? (isLand ? "Land" : "Spell"),
            isCommander,
            isLocked,
            manaValue,
            isLand,
            roles ?? [],
            categories ?? []);

    private static CutLabFinding Finding(CutLabFindingKind kind, params string[] cardNames)
        => new(
            kind,
            kind.ToString(),
            kind.ToString(),
            cardNames.Select(cardName => new CutLabFindingEvidence(cardName, null)).ToArray());

    private static CutLabStructuralFindingsResult Findings(params CutLabFinding[] findings)
        => new(findings, true, true);
}
