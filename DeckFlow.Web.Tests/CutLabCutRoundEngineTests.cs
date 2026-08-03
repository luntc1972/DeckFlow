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
            Card("Role Mate A", 3, roles: ["interaction-targeted"]),
            Card("Role Mate B", 4, roles: ["interaction-targeted"]),
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
    public void BuildQueue_LockedOvershootWithNoFloorData_KeepsTheFixedRoleOrder()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Mass Wipe", 1, isLocked: true, roles: ["interaction-mass"], typeLine: "Sorcery"),
            Card("Targeted Answer", 1, isLocked: true, roles: ["interaction-targeted"], typeLine: "Instant"),
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
        Assert.Equal(4, advisory.CardsOverTarget);
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
                Assert.Equal("interaction-mass", group.RoleKey);
                Assert.Equal(["Mass Wipe"], group.CardNames);
            },
            group =>
            {
                Assert.Equal("interaction-targeted", group.RoleKey);
                Assert.Equal(["Targeted Answer"], group.CardNames);
            },
            group =>
            {
                Assert.Equal("lands", group.RoleKey);
                Assert.Equal(["Ramp Land"], group.CardNames);
            });
    }

    [Fact]
    public void BuildQueue_LockedOvershootRanksByHeadroomDescending()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Mass Wipe", 1, isLocked: true, roles: ["interaction-mass"], typeLine: "Sorcery"),
            Card("Targeted Answer", 1, isLocked: true, roles: ["interaction-targeted"], typeLine: "Instant"),
            Card("Payoff Creature", 1, isLocked: true, roles: ["payoffs"], typeLine: "Creature"),
            Card("Wincon Sorcery", 1, isLocked: true, roles: ["wincons"], typeLine: "Sorcery"),
            Card("Wincon Artifact", 1, isLocked: true, roles: ["wincons"], typeLine: "Artifact"),
            Card("Draw Engine", 1, quantity: 99, isLocked: true, roles: ["draw"], typeLine: "Enchantment"),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 2,
            floorByRole: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["DRAW"] = 10,
                ["payoffs"] = 0,
                ["interaction-mass"] = 0,
                ["interaction-targeted"] = 0,
                ["WINCONS"] = 2,
            },
            roleCounts: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["draw"] = 99,
                ["PAYOFFS"] = 1,
                ["interaction-mass"] = 1,
                ["interaction-targeted"] = 1,
                ["wincons"] = 2,
            });

        CutLabLockedOvershootAdvisory advisory = Assert.IsType<CutLabLockedOvershootAdvisory>(plan.LockedOvershootAdvisory);
        // Why: the fixed array put wincons first as least-structural, but wincons usually has the least slack
        // against its floor. Headroom ranking should surface the roomiest role first and the tightest last.
        Assert.Equal(
            ["draw", "payoffs", "interaction-mass", "interaction-targeted", "wincons"],
            advisory.Groups.Select(group => group.RoleKey).ToArray());
    }

    [Fact]
    public void BuildQueue_LockedOvershootHeadroomTies_FallBackToTheFixedRoleOrder()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Payoff Creature", 1, isLocked: true, roles: ["payoffs"], typeLine: "Creature"),
            Card("Engine Artifact", 1, isLocked: true, roles: ["engines"], typeLine: "Artifact"),
            Card("Locked Lands", 1, quantity: 99, isLocked: true, isLand: true, roles: ["lands"]),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 2,
            floorByRole: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["payoffs"] = 1,
                ["engines"] = 1,
                ["lands"] = 0,
            },
            roleCounts: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["payoffs"] = 2,
                ["engines"] = 2,
                ["lands"] = 99,
            });

        CutLabLockedOvershootAdvisory advisory = Assert.IsType<CutLabLockedOvershootAdvisory>(plan.LockedOvershootAdvisory);
        Assert.Equal(["lands", "payoffs", "engines"], advisory.Groups.Select(group => group.RoleKey).ToArray());
    }

    [Fact]
    public void BuildQueue_LockedOvershootMultiRoleCard_IsAttributedToItsTightestRole()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Split Role Card", 1, isLocked: true, roles: ["wincons", "lands"], typeLine: "Enchantment"),
            Card("Wincon Filler", 1, quantity: 100, isLocked: true, roles: ["wincons"], typeLine: "Sorcery"),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 2,
            floorByRole: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["wincons"] = 0,
                ["lands"] = 1,
            },
            roleCounts: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["wincons"] = 100,
                ["lands"] = 1,
            });

        CutLabLockedOvershootAdvisory advisory = Assert.IsType<CutLabLockedOvershootAdvisory>(plan.LockedOvershootAdvisory);
        CutLabLockedOvershootGroup landsGroup = Assert.Single(advisory.Groups, group => group.RoleKey == "lands");
        Assert.Equal(["Split Role Card"], landsGroup.CardNames);
    }

    [Fact]
    public void BuildQueue_LockedOvershootRoleMissingFromFloorMap_DoesNotThrow()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Unknown Role Card", 1, isLocked: true, roles: ["mystery-role"], typeLine: "Artifact"),
            Card("Known Role Card", 1, quantity: 100, isLocked: true, roles: ["payoffs"], typeLine: "Creature"),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 2,
            floorByRole: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["payoffs"] = 0,
            },
            roleCounts: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["payoffs"] = 100,
            });

        CutLabLockedOvershootAdvisory advisory = Assert.IsType<CutLabLockedOvershootAdvisory>(plan.LockedOvershootAdvisory);
        Assert.Contains(advisory.Groups, group => group.RoleKey == "mystery-role");
    }

    [Fact]
    public void BuildQueue_LockedOvershootOtherRole_StaysLastDespiteHighCount()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Payoff Creature", 1, isLocked: true, roles: ["payoffs"], typeLine: "Creature"),
            Card("No Role Card", 1, isLocked: true, roles: [], typeLine: "Artifact"),
            Card("Locked Lands", 1, quantity: 99, isLocked: true, isLand: true, roles: ["lands"]),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [],
            cardsToCutTarget: 2,
            floorByRole: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["payoffs"] = 0,
                ["lands"] = 99,
            },
            roleCounts: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["payoffs"] = 1,
                ["lands"] = 99,
                ["other"] = 500,
            });

        CutLabLockedOvershootAdvisory advisory = Assert.IsType<CutLabLockedOvershootAdvisory>(plan.LockedOvershootAdvisory);
        Assert.Equal("other", advisory.Groups[^1].RoleKey);
        Assert.Equal(["No Role Card"], advisory.Groups[^1].CardNames);
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

    [Fact]
    public void BuildQueue_EnablerStarvedDoesNotCountTowardDiscriminatingTally()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList = [Card("Combo Piece", 3)];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.CurveCongestion, "Combo Piece"),
                Finding(CutLabFindingKind.EnablerStarved, "Combo Piece")),
            [],
            cardsToCutTarget: 1);

        CutLabRoundQueueItem proposal = Assert.Single(plan.Queue);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, proposal.RoundKey);
        Assert.Equal(1, proposal.FindingCount);
        Assert.Equal([CutLabFindingKind.CurveCongestion], proposal.DiscriminatingFindingKinds);
    }

    [Fact]
    public void BuildQueue_OnlyEnablerStarvedFindings_LandInRound3()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList = [Card("Orphaned Piece", 2)];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.EnablerStarved, "Orphaned Piece"),
                Finding(CutLabFindingKind.EnablerStarved, "Orphaned Piece")),
            [],
            cardsToCutTarget: 1);

        CutLabRoundQueueItem proposal = Assert.Single(plan.Queue);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, proposal.RoundKey);
        Assert.Equal(0, proposal.FindingCount);
        Assert.Empty(proposal.DiscriminatingFindingKinds);
    }

    [Fact]
    public void BuildQueue_ComboProtectedPlusEnablerStarved_IsNotPromotedAboveRound3()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList = [Card("Live Combo Piece", 3)];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Live Combo Piece"),
                Finding(CutLabFindingKind.EnablerStarved, "Live Combo Piece")),
            [],
            cardsToCutTarget: 1);

        CutLabRoundQueueItem proposal = Assert.Single(plan.Queue);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, proposal.RoundKey);
        Assert.Equal(0, proposal.FindingCount);
        Assert.Empty(proposal.DiscriminatingFindingKinds);
    }

    [Fact]
    public void BuildQueue_ComboProtectedFunctionalTwin_DoesNotPromoteButRemainsInItsLegitimateRound()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Combo Piece", 2), Card("Ordinary Card", 3)],
            Findings(
                Finding(CutLabFindingKind.FunctionalTwins, "Combo Piece"),
                Finding(CutLabFindingKind.CurveCongestion, "Combo Piece", "Ordinary Card"),
                Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece")),
            [],
            cardsToCutTarget: 2);

        Assert.Equal("Ordinary Card", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, plan.NextProposal.RoundKey);
        CutLabRoundQueueItem comboPiece = Assert.Single(plan.Queue, item => item.CardName == "Combo Piece");
        Assert.Equal(CutLabCutRoundEngine.Round2Key, comboPiece.RoundKey);
        Assert.Equal(1, comboPiece.FindingCount);
    }

    // Why: pins the July 2026 report where Ashnod's Altar was proposed first under
    // "Obvious cuts" on a live Celes, Rune Knight deck. Combo findings are advisory,
    // so no number of them may promote a card into round 1 on their own.
    [Fact]
    public void BuildQueue_ComboDenseCardNeverReachesRound1_AshnodsAltarRegression()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Ashnod's Altar", 3),
            Card("Genuine Cut", 5),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.EnablerStarved, "Ashnod's Altar"),
                Finding(CutLabFindingKind.EnablerStarved, "Ashnod's Altar"),
                Finding(CutLabFindingKind.EnablerStarved, "Ashnod's Altar"),
                Finding(CutLabFindingKind.EnablerStarved, "Ashnod's Altar"),
                Finding(CutLabFindingKind.EnablerStarved, "Ashnod's Altar"),
                Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Ashnod's Altar"),
                Finding(CutLabFindingKind.CurveCongestion, "Genuine Cut"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Genuine Cut")),
            [],
            cardsToCutTarget: 2);

        CutLabRoundQueueItem altar = Assert.Single(plan.Queue, item => item.CardName == "Ashnod's Altar");
        Assert.Equal(CutLabCutRoundEngine.Round3Key, altar.RoundKey);
        Assert.Equal(0, altar.FindingCount);

        // The genuinely-flagged card, not the combo piece, is the first proposal.
        Assert.Equal("Genuine Cut", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round1Key, plan.NextProposal.RoundKey);
    }

    // Why: pins the July 2026 report where Agatha's Soul Cauldron led round 2 on a single
    // curve-congestion finding while sitting in two complete combos. Combo membership must
    // break the tie so a combo piece is proposed after an equally-flagged non-combo card.
    [Fact]
    public void BuildQueue_ComboProtectedCardSortsAfterEquallyFlaggedNonComboCard_AgathaRegression()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Agatha's Soul Cauldron", 2),
            Card("Plain Filler", 5),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.CurveCongestion, "Agatha's Soul Cauldron", "Plain Filler"),
                Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Agatha's Soul Cauldron")),
            [],
            cardsToCutTarget: 2);

        // Both are round 2 (one discriminating finding each); the combo piece must not lead.
        Assert.Equal("Plain Filler", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, plan.NextProposal.RoundKey);

        CutLabRoundQueueItem cauldron = Assert.Single(plan.Queue, item => item.CardName == "Agatha's Soul Cauldron");
        Assert.Equal(CutLabCutRoundEngine.Round2Key, cauldron.RoundKey);
        Assert.True(
            plan.Queue.ToList().IndexOf(cauldron) > 0,
            "combo-protected card must not be the first proposal in its round");
    }

    [Fact]
    public void BuildQueue_ComboProtectedCardSortsLastInRound1DespiteHigherTally()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Combo Engine", 2),
            Card("Plain Filler", 4),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.CurveCongestion, "Combo Engine", "Plain Filler"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Combo Engine", "Plain Filler"),
                Finding(CutLabFindingKind.CurveCongestion, "Combo Engine"),
                Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Engine")),
            [],
            cardsToCutTarget: 2);

        Assert.Equal("Plain Filler", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round1Key, plan.NextProposal.RoundKey);
    }

    /// <summary>
    /// Why: deferred cards are revisited after the first-pass rounds, so complete
    /// combo pieces must remain cuttable without becoming the lead proposal there.
    /// </summary>
    [Fact]
    public void BuildQueue_ComboProtectedCardSortsLastInSecondPassDespiteEarlierOrdinal()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Combo Piece", 1),
            Card("Plain Deferred Card", 6),
        ];
        IReadOnlyList<CutLabDecision> decisions =
        [
            new CutLabDecision { CardName = "Combo Piece", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.Round3Key, Ordinal = 1 },
            new CutLabDecision { CardName = "Plain Deferred Card", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.Round3Key, Ordinal = 2 },
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece")),
            decisions,
            cardsToCutTarget: 2);

        Assert.Equal("Plain Deferred Card", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.SecondPassDeferredKey, plan.NextProposal.RoundKey);
        CutLabRoundQueueItem comboPiece = Assert.Single(plan.Queue, item => item.CardName == "Combo Piece");
        Assert.True(plan.Queue.ToList().IndexOf(comboPiece) > 0, "combo-protected card must not be the first second-pass proposal");

        CutLabRoundPlan revisitedPlan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece")),
            [
                new CutLabDecision { CardName = "Combo Piece", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.Round3Key, Ordinal = 1 },
                new CutLabDecision { CardName = "Plain Deferred Card", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.SecondPassDeferredKey, Ordinal = 5 },
            ],
            cardsToCutTarget: 2);

        Assert.Equal("Combo Piece", revisitedPlan.NextProposal!.CardName);
    }

    /// <summary>
    /// Why: rejected cards have their own second-pass queue, where complete combo
    /// pieces must likewise stay available without leading the retry sequence.
    /// </summary>
    [Fact]
    public void BuildQueue_ComboProtectedCardSortsLastInRejectedSecondPassDespiteEarlierOrdinal()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Combo Piece", 1),
            Card("Plain Rejected Card", 6),
        ];
        IReadOnlyList<CutLabDecision> decisions =
        [
            new CutLabDecision { CardName = "Combo Piece", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.Round3Key, Ordinal = 1 },
            new CutLabDecision { CardName = "Plain Rejected Card", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.Round3Key, Ordinal = 2 },
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece")),
            decisions,
            cardsToCutTarget: 2);

        Assert.Equal("Plain Rejected Card", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.SecondPassRejectedKey, plan.NextProposal.RoundKey);
        CutLabRoundQueueItem comboPiece = Assert.Single(plan.Queue, item => item.CardName == "Combo Piece");
        Assert.True(plan.Queue.ToList().IndexOf(comboPiece) > 0, "combo-protected card must not be the first rejected second-pass proposal");

        CutLabRoundPlan revisitedPlan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece")),
            [
                new CutLabDecision { CardName = "Combo Piece", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.Round3Key, Ordinal = 1 },
                new CutLabDecision { CardName = "Plain Rejected Card", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.SecondPassRejectedKey, Ordinal = 5 },
            ],
            cardsToCutTarget: 2);

        Assert.Equal("Combo Piece", revisitedPlan.NextProposal!.CardName);
    }

    [Fact]
    public void BuildQueue_SecondPassComboPieceSurfacesAfterOrdinaryCardIsRevisited()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Plain Deferred Card", 6),
            Card("Combo Piece", 1),
        ];
        CutLabStructuralFindingsResult findings = Findings(
            Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece"));

        CutLabRoundPlan firstAppearancePlan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            findings,
            [
                new CutLabDecision { CardName = "Plain Deferred Card", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.Round2Key, Ordinal = 2 },
                new CutLabDecision { CardName = "Combo Piece", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.Round2Key, Ordinal = 1 },
            ],
            cardsToCutTarget: 2);

        Assert.Equal("Plain Deferred Card", firstAppearancePlan.NextProposal!.CardName);

        CutLabRoundPlan revisitedPlan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            findings,
            [
                new CutLabDecision { CardName = "Plain Deferred Card", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.SecondPassDeferredKey, Ordinal = 5 },
                new CutLabDecision { CardName = "Combo Piece", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.Round2Key, Ordinal = 2 },
            ],
            cardsToCutTarget: 2);

        Assert.Equal("Combo Piece", revisitedPlan.NextProposal!.CardName);
    }

    [Fact]
    public void BuildQueue_SecondPassRejectedComboPieceSurfacesAfterOrdinaryCardIsRevisited()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Plain Rejected Card", 6),
            Card("Combo Piece", 1),
        ];
        CutLabStructuralFindingsResult findings = Findings(
            Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece"));

        CutLabRoundPlan firstAppearancePlan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            findings,
            [
                new CutLabDecision { CardName = "Plain Rejected Card", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.Round2Key, Ordinal = 2 },
                new CutLabDecision { CardName = "Combo Piece", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.Round2Key, Ordinal = 1 },
            ],
            cardsToCutTarget: 2);

        Assert.Equal("Plain Rejected Card", firstAppearancePlan.NextProposal!.CardName);

        CutLabRoundPlan revisitedPlan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            findings,
            [
                new CutLabDecision { CardName = "Plain Rejected Card", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.SecondPassRejectedKey, Ordinal = 5 },
                new CutLabDecision { CardName = "Combo Piece", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.Round2Key, Ordinal = 2 },
            ],
            cardsToCutTarget: 2);

        Assert.Equal("Combo Piece", revisitedPlan.NextProposal!.CardName);
    }

    [Fact]
    public void BuildQueue_SecondPassRevisitedCardsRotateByOrdinal()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Earlier Revisited Card", 6),
            Card("Zeroth Revisited Card", 1),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(),
            [
                new CutLabDecision { CardName = "Earlier Revisited Card", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.SecondPassDeferredKey, Ordinal = 6 },
                new CutLabDecision { CardName = "Zeroth Revisited Card", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.SecondPassDeferredKey, Ordinal = 5 },
            ],
            cardsToCutTarget: 2);

        Assert.Equal("Zeroth Revisited Card", plan.NextProposal!.CardName);
    }

    [Fact]
    public void BuildQueue_SecondPassRevisitedComboPieceRotatesByOrdinal()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("A Plain Deferred Card", 6),
            Card("Z Combo Piece", 1),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Z Combo Piece")),
            [
                new CutLabDecision { CardName = "A Plain Deferred Card", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.SecondPassDeferredKey, Ordinal = 12 },
                new CutLabDecision { CardName = "Z Combo Piece", Kind = CutLabDecisionKind.Deferred, Round = CutLabCutRoundEngine.SecondPassDeferredKey, Ordinal = 10 },
            ],
            cardsToCutTarget: 2);

        Assert.Equal("Z Combo Piece", plan.NextProposal!.CardName);
    }

    [Fact]
    public void BuildQueue_SecondPassRevisitedRejectedComboPieceRotatesByOrdinal()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("A Plain Rejected Card", 6),
            Card("Z Combo Piece", 1),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Z Combo Piece")),
            [
                new CutLabDecision { CardName = "A Plain Rejected Card", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.SecondPassRejectedKey, Ordinal = 12 },
                new CutLabDecision { CardName = "Z Combo Piece", Kind = CutLabDecisionKind.Rejected, Round = CutLabCutRoundEngine.SecondPassRejectedKey, Ordinal = 10 },
            ],
            cardsToCutTarget: 2);

        Assert.Equal("Z Combo Piece", plan.NextProposal!.CardName);
    }

    [Fact]
    public void BuildQueue_ComboProtectedCardSortsLastInRound3DespiteLowerManaValue()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Cheap Combo Piece", 1),
            Card("Expensive Filler", 6),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Cheap Combo Piece")),
            [],
            cardsToCutTarget: 2);

        Assert.Equal("Expensive Filler", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, plan.NextProposal.RoundKey);
    }

    // Why: a card that is only missing its combo partner (NeedsPartner) is a dead piece and a
    // prime cut candidate — the same card EnablerStarved already flags. Demoting it a second
    // time for the same reason is backwards; only a genuinely CompletePiece should be demoted.
    [Fact]
    public void BuildQueue_ComboProtectedNeedsPartnerOnly_DoesNotDemoteCard()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Needs Partner Piece", 2),
            Card("Plain Filler", 5),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.CurveCongestion, "Needs Partner Piece", "Plain Filler"),
                Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.NeedsPartner, "Needs Partner Piece")),
            [],
            cardsToCutTarget: 2);

        // Both are round 2 (one discriminating finding each). A NeedsPartner-only combo finding
        // must not demote, so the lower-mana-value card leads on the normal tiebreak.
        Assert.Equal("Needs Partner Piece", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, plan.NextProposal.RoundKey);
    }

    // Why: the combo-protected set's names come from Commander Spellbook (front-face only);
    // the rank lookup key comes from the deck's full DFC name. Without normalizing both sides
    // through CutLabCardNames, a DFC combo piece silently escapes demotion.
    [Fact]
    public void BuildQueue_ComboProtectedDfcCardNormalizesAcrossFrontBackName_MalakirRebirthRegression()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Malakir Rebirth // Malakir Mire", 1),
            Card("Plain Filler", 6),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Malakir Rebirth")),
            [],
            cardsToCutTarget: 2);

        // Both land in round 3 (no discriminating findings). The DFC combo piece must still be
        // recognized and demoted behind the non-combo filler despite its lower mana value.
        Assert.Equal("Plain Filler", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, plan.NextProposal.RoundKey);
    }

    // Why: DFC truncation is not the only way the two name sources diverge. CardNormalizer also
    // strips punctuation, so a deck spelling the apostrophe as U+2019 and Commander Spellbook
    // spelling it as U+0027 are the same card only after normalization. Pinned separately from the
    // DFC case: a refactor could keep a DFC-safe path while dropping normalization, and without
    // this test the suite would stay green while every apostrophe card escaped demotion again.
    [Fact]
    public void BuildQueue_ComboProtectedCardNormalizesDivergentApostrophes_LimDulsVaultRegression()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Lim-Dul’s Vault", 2),
            Card("Plain Filler", 6),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Lim-Dul's Vault")),
            [],
            cardsToCutTarget: 2);

        // Both land in round 3 (no discriminating findings). The combo piece must be demoted behind
        // the non-combo filler despite its lower mana value, even though the two sources spell the
        // apostrophe differently.
        Assert.Equal("Plain Filler", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, plan.NextProposal.RoundKey);
    }

    [Fact]
    public void BuildQueue_FunctionalTwinsFindingIsDiscriminating_PlacesCardInRound2()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Twin Card", 3), Card("Fallback", 1)],
            Findings(Finding(CutLabFindingKind.FunctionalTwins, "Twin Card")),
            [],
            cardsToCutTarget: 2);

        CutLabRoundQueueItem twinCard = Assert.Single(plan.Queue, item => item.CardName == "Twin Card");
        Assert.Equal(CutLabCutRoundEngine.Round2Key, twinCard.RoundKey);
        Assert.Equal(1, twinCard.FindingCount);
        Assert.Equal([CutLabFindingKind.FunctionalTwins], twinCard.DiscriminatingFindingKinds);
    }

    [Fact]
    public void BuildQueue_FunctionalTwinsPlusOneOtherFinding_PlacesCardInRound1()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Twin Card", 3), Card("Fallback", 1)],
            Findings(
                Finding(CutLabFindingKind.FunctionalTwins, "Twin Card"),
                Finding(CutLabFindingKind.CurveCongestion, "Twin Card")),
            [],
            cardsToCutTarget: 2);

        CutLabRoundQueueItem twinCard = Assert.Single(plan.Queue, item => item.CardName == "Twin Card");
        Assert.Equal(CutLabCutRoundEngine.Round1Key, twinCard.RoundKey);
        Assert.Equal(2, twinCard.FindingCount);
        Assert.Equal(
            [CutLabFindingKind.CurveCongestion, CutLabFindingKind.FunctionalTwins],
            twinCard.DiscriminatingFindingKinds);
    }

    // Why: a finding-only assertion would pass if FunctionalTwins were excluded from the tally;
    // the proposal swap proves its promotion changes the outcome this phase exists to change.
    [Fact]
    public void BuildQueue_FunctionalTwinsChangesNextProposal()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Early Fallback", 1),
            Card("Twin Card", 5),
        ];

        CutLabRoundPlan withoutTwins = CutLabCutRoundEngine.BuildQueue(workingList, Findings(), [], cardsToCutTarget: 2);
        CutLabRoundPlan withTwins = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.FunctionalTwins, "Twin Card")),
            [],
            cardsToCutTarget: 2);

        Assert.NotNull(withoutTwins.NextProposal);
        Assert.NotNull(withTwins.NextProposal);
        Assert.Equal("Early Fallback", withoutTwins.NextProposal!.CardName);
        Assert.Equal("Twin Card", withTwins.NextProposal!.CardName);
        Assert.NotEqual(withoutTwins.NextProposal.CardName, withTwins.NextProposal.CardName);
    }

    [Fact]
    public void BuildQueue_FunctionalTwinsOnALockedOrCommanderCard_DoesNotPropose()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [
                Card("Locked Twin", 1, isLocked: true),
                Card("Commander Twin", 1, isCommander: true),
                Card("Eligible Card", 5),
            ],
            Findings(Finding(CutLabFindingKind.FunctionalTwins, "Locked Twin", "Commander Twin")),
            [],
            cardsToCutTarget: 3);

        Assert.Equal(["Eligible Card"], plan.Queue.Select(item => item.CardName).ToArray());
    }

    [Fact]
    public void BuildQueue_TwinsEvidenceMatchesNormalizedEquivalentWorkingListEntries()
    {
        Assert.Equal(
            CutLabCardNames.Normalize("Malakir Rebirth"),
            CutLabCardNames.Normalize("Malakir Rebirth // Malakir Mire"));
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Malakir Rebirth // Malakir Mire", 1),
            Card("Malakir Rebirth", 1),
            Card("Card B", 1),
            Card("Card C", 1),
        ];

        CutLabRoundPlan twinsPlan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.FunctionalTwins, "Malakir Rebirth", "Card B", "Card C")),
            [],
            cardsToCutTarget: 4);

        // Why: Assert.All passes vacuously on an empty queue, so pin the count first. All four raw
        // entries must be present -- that is what proves the long form inherited the twins tally
        // through the D-23 normalized join rather than being dropped by raw lookup.
        Assert.Equal(4, twinsPlan.Queue.Count);
        Assert.All(twinsPlan.Queue, item =>
        {
            Assert.Equal(1, item.FindingCount);
            Assert.Equal(CutLabCutRoundEngine.Round2Key, item.RoundKey);
        });

        CutLabRoundPlan nonTwinsPlan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.CurveCongestion, "Malakir Rebirth", "Card B", "Card C")),
            [],
            cardsToCutTarget: 4);

        CutLabRoundQueueItem longForm = Assert.Single(
            nonTwinsPlan.Queue,
            item => item.CardName == "Malakir Rebirth // Malakir Mire");
        Assert.Equal(0, longForm.FindingCount);
    }

    [Fact]
    public void BuildQueue_TwinsDuplicateRawPoolEntries_CountsEachRawKeyOnce()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Twin Card", 1), Card("Twin Card", 1), Card("Card B", 1), Card("Card C", 1)],
            Findings(Finding(CutLabFindingKind.FunctionalTwins, "Twin Card", "Card B", "Card C")),
            [],
            cardsToCutTarget: 4);

        // Why: Assert.All passes vacuously on an empty queue, so pin the count first. Both identical
        // raw entries plus the two other evidence cards must be queued; the case-insensitive hash
        // set supplies the once-per-raw-key tally behavior.
        Assert.Equal(4, plan.Queue.Count);
        Assert.All(plan.Queue, item => Assert.Equal(1, item.FindingCount));
    }

    // Why: D-16 deliberately counts each role-specific twins finding: a card filling two saturated
    // slots at the same cost and type is twice as redundant. Change this test if D-16 is overridden.
    [Fact]
    public void BuildQueue_MultiRoleCardWithTwoTwinsFindings_ReachesRound1()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Multi-Role Twin", 3)],
            Findings(
                Finding(CutLabFindingKind.FunctionalTwins, "Multi-Role Twin"),
                Finding(CutLabFindingKind.FunctionalTwins, "Multi-Role Twin")),
            [],
            cardsToCutTarget: 1);

        CutLabRoundQueueItem twinCard = Assert.Single(plan.Queue);
        Assert.Equal(CutLabCutRoundEngine.Round1Key, twinCard.RoundKey);
        Assert.Equal(2, twinCard.FindingCount);
    }

    // Why: behavioral tests prove the effect; this names the cause when a future change excludes
    // FunctionalTwins from the tally without widening the private field just for test access.
    [Fact]
    public void BuildQueue_FunctionalTwinsAbsentFromExclusionSet_IsAssertedStructurally()
    {
        System.Reflection.FieldInfo? field = typeof(CutLabCutRoundEngine).GetField(
            "ExcludedFindingKindsFromTally",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(field);
        IReadOnlySet<CutLabFindingKind> exclusions = Assert.IsAssignableFrom<IReadOnlySet<CutLabFindingKind>>(field!.GetValue(null));
        Assert.DoesNotContain(CutLabFindingKind.FunctionalTwins, exclusions);
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

    private static CutLabFinding Finding(CutLabFindingKind kind, ComboBadgeState badgeState, params string[] cardNames)
        => new(
            kind,
            kind.ToString(),
            kind.ToString(),
            cardNames.Select(cardName => new CutLabFindingEvidence(cardName, null, badgeState)).ToArray());

    private static CutLabStructuralFindingsResult Findings(params CutLabFinding[] findings)
        => new(findings, true, true);
}
