using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for the pure round-sequencing engine used by Cut Lab phase 103.</summary>
public sealed class CutLabCutRoundEngineTests
{
    [Theory]
    [InlineData(true, "Round 1 \u00b7 Off-theme cuts", "Off-theme cuts \u2014 cards outside your deck's themes.", "On-theme cards in a stranded subtheme.")]
    [InlineData(false, "Round 1 \u00b7 Structural cuts", "Cards in a stranded subtheme that are also off your plan.", "Cards with one reason to cut: a stranded subtheme, or off your plan.")]
    public void RoundCopy_ReflectsThemeAvailability(bool themesAvailable, string round1Label, string round1Body, string round2Body)
    {
        Assert.Equal(round1Label, CutLabCutRoundEngine.LabelFor(CutLabCutRoundEngine.Round1Key, themesAvailable));
        Assert.Equal(round1Body, CutLabCutRoundEngine.RoundBannerBodyFor(CutLabCutRoundEngine.Round1Key, themesAvailable));
        Assert.Equal(round2Body, CutLabCutRoundEngine.RoundBannerBodyFor(CutLabCutRoundEngine.Round2Key, themesAvailable));
    }

    [Fact]
    public void BuildQueue_LockedFloorRoleWithRecursion_PreservesAdvisoryOrderAndGroups()
    {
        CutLabLockedOvershootAdvisory Advisory(string[] roles) => Assert.IsType<CutLabLockedOvershootAdvisory>(
            CutLabCutRoundEngine.BuildQueue(
                [Card("Draw", 1, quantity: 99, isLocked: true, roles: roles),
                 Card("Ramp", 1, quantity: 3, isLocked: true, roles: ["ramp"]),
                 Card("Floorless", 1, isLocked: true, roles: ["recursion"])],
                Findings(), [], cardsToCutTarget: 3,
                floorByRole: new Dictionary<string, int> { ["draw"] = 1, ["ramp"] = 1 },
                roleCounts: new Dictionary<string, int> { ["draw"] = 99, ["ramp"] = 3 }).LockedOvershootAdvisory);
        var baseline = Advisory(["draw"]);
        var expanded = Advisory(["draw", "recursion"]);
        Assert.Equal(baseline.Groups.Select(group => group.RoleKey), expanded.Groups.Select(group => group.RoleKey));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(baseline), System.Text.Json.JsonSerializer.Serialize(expanded));
        Assert.Contains(expanded.Groups, group => group.RoleKey == "other");
    }

    [Fact]
    public void BuildQueue_LockedFloorRoleWithStax_PreservesAdvisoryOrderAndGroups()
    {
        CutLabLockedOvershootAdvisory Advisory(string[] roles) => Assert.IsType<CutLabLockedOvershootAdvisory>(
            CutLabCutRoundEngine.BuildQueue(
                [Card("Draw", 1, quantity: 99, isLocked: true, roles: roles),
                 Card("Ramp", 1, quantity: 3, isLocked: true, roles: ["ramp"]),
                 Card("Floorless", 1, isLocked: true, roles: ["stax"])],
                Findings(), [], cardsToCutTarget: 3,
                floorByRole: new Dictionary<string, int> { ["draw"] = 1, ["ramp"] = 1 },
                roleCounts: new Dictionary<string, int> { ["draw"] = 99, ["ramp"] = 3 }).LockedOvershootAdvisory);
        var baseline = Advisory(["draw"]);
        var expanded = Advisory(["draw", "stax"]);
        Assert.Equal(baseline.Groups.Select(group => group.RoleKey), expanded.Groups.Select(group => group.RoleKey));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(baseline), System.Text.Json.JsonSerializer.Serialize(expanded));
        Assert.Contains(expanded.Groups, group => group.RoleKey == "other");
    }

    [Fact]
    public void RoundQueueItem_LegacyJson_DefaultsToUnchangedBanner()
    {
        const string json = """{"CardName":"Candidate","RoundKey":"round-2","RoundLabel":"","FindingCount":1,"DiscriminatingFindingKinds":[]}""";
        CutLabRoundQueueItem? item = System.Text.Json.JsonSerializer.Deserialize<CutLabRoundQueueItem>(json);

        Assert.NotNull(item);
        Assert.True(item.PlanSignalAvailable);
        Assert.Equal("Cards with one reason to cut: a stranded subtheme, or off your plan.",
            CutLabCutRoundEngine.RoundBannerBodyFor(CutLabCutRoundEngine.Round2Key, item.CheckedCommanderThemesAvailable, item.PlanSignalAvailable));
    }

    [Theory]
    [InlineData("neutral", "Cards in a stranded subtheme.")]
    [InlineData("null", "Cards in a stranded subtheme.")]
    [InlineData("available", "Cards with one reason to cut: a stranded subtheme, or off your plan.")]
    [InlineData("themes", "On-theme cards in a stranded subtheme.")]
    public void BuildQueue_Round2Banner_ReflectsPlanSignal(string mode, string expectedBody)
    {
        bool themesAvailable = mode == "themes";
        CutLabStructuralFindingsResult findings = mode == "available"
            ? Findings()
            : Findings(Finding(CutLabFindingKind.StrandedSubtheme, "Candidate"));
        IReadOnlyDictionary<string, CutLabPlanAffinity>? affinities = mode == "null"
            ? null
            : Affinities([("Candidate", themesAvailable ? 1 : 0), ("Anchor", mode == "available" || themesAvailable ? 1 : 0)], themesAvailable);
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Candidate", 4), Card("Anchor", 1)], findings, [],
            cardsToCutTarget: 1, planAffinities: affinities,
            checkedCommanderThemesAvailable: themesAvailable);

        Assert.Equal(CutLabCutRoundEngine.Round2Key, plan.NextProposal!.RoundKey);
        var result = new CutLabProcessResult
        {
            HasResult = true,
            State = new CutLabState
            {
                Pool = [new CutLabPoolCard { Name = "Candidate", Quantity = 1 }],
            },
            RoleAssignmentsByCardName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            Findings = findings,
            RoundPlan = plan,
        };
        CutLabViewModel model = CutLabViewModel.From(new CutLabRequest(), result);

        Assert.Equal(expectedBody, model.Proposal.RoundBannerBody);
        Assert.Equal(expectedBody, CutLabNextProposalBuilder.Build(plan, findings).RoundBannerBody);
    }

    [Fact]
    public void BuildQueue_CurveAndStrandedFindings_WithOffPlanSignal_PlacesCardInRound1()
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
            cardsToCutTarget: 3,
            planAffinities: Affinities([("Round 2 Card", 1), ("Round 3 Card", 1)]));

        CutLabRoundQueueItem round1 = Assert.Single(plan.Queue, item => item.CardName == "Round 1 Card");
        Assert.Equal(CutLabCutRoundEngine.Round1Key, round1.RoundKey);
        Assert.Equal(CutLabCutRoundEngine.Round1FallbackLabel, round1.RoundLabel);
        Assert.Equal(1, round1.FindingCount);
        Assert.Equal(
            [CutLabFindingKind.StrandedSubtheme],
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
            Findings(Finding(CutLabFindingKind.StrandedSubtheme, "Structural Choice")),
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
            Findings(Finding(CutLabFindingKind.StrandedSubtheme, "Structural Choice")),
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
            cardsToCutTarget: 2,
            planAffinities: Affinities([("Later Proposal", 1)]));

        Assert.NotNull(plan.NextProposal);
        Assert.Equal("Immediate Proposal", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round1FallbackLabel, plan.NextProposal.RoundLabel);
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
        Assert.Contains(advisory.Groups, group => group.RoleKey == "other");
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
    [InlineData(CutLabCutRoundEngine.Round1Key, CutLabCutRoundEngine.Round1FallbackLabel, "Cards in a stranded subtheme that are also off your plan.")]
    [InlineData(CutLabCutRoundEngine.Round2Key, CutLabCutRoundEngine.Round2Label, "Cards with one reason to cut: a stranded subtheme, or off your plan.")]
    [InlineData(CutLabCutRoundEngine.Round3Key, CutLabCutRoundEngine.Round3Label, "Everything else, ordered by smallest measurable tradeoff first.")]
    [InlineData(CutLabCutRoundEngine.InfrastructureKey, CutLabCutRoundEngine.InfrastructureLabel, "Mana base & staples — proposed only after everything else.")]
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
                Finding(CutLabFindingKind.StrandedSubtheme, "Combo Piece"),
                Finding(CutLabFindingKind.EnablerStarved, "Combo Piece")),
            [],
            cardsToCutTarget: 1);

        CutLabRoundQueueItem proposal = Assert.Single(plan.Queue);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, proposal.RoundKey);
        Assert.Equal(1, proposal.FindingCount);
        Assert.Equal([CutLabFindingKind.StrandedSubtheme], proposal.DiscriminatingFindingKinds);
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
                Finding(CutLabFindingKind.StrandedSubtheme, "Combo Piece", "Ordinary Card"),
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
            cardsToCutTarget: 2,
            planAffinities: Affinities([("Ashnod's Altar", 1)]));

        CutLabRoundQueueItem altar = Assert.Single(plan.Queue, item => item.CardName == "Ashnod's Altar");
        Assert.Equal(CutLabCutRoundEngine.Round3Key, altar.RoundKey);
        Assert.Equal(0, altar.FindingCount);

        // The genuinely-flagged card, not the combo piece, is the first proposal.
        Assert.Equal("Genuine Cut", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round1Key, plan.NextProposal.RoundKey);
    }

    [Fact]
    public void BuildQueue_ManySameKindStrandedFindings_DoNotOutrankOffPlanStrandedCard_StapleRegression()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Noisy Staple", 1),
            Card("Off Plan Card", 4),
            .. Enumerable.Range(0, 40).Select(index => Card($"Filler {index}", 3)),
        ];

        CutLabStructuralFindingsResult findings = Findings(
            [
                .. Enumerable.Range(0, 40).Select(index => Finding(CutLabFindingKind.StrandedSubtheme, "Noisy Staple", $"Filler {index}")),
                Finding(CutLabFindingKind.CurveCongestion, "Off Plan Card"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Off Plan Card"),
            ]);

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(workingList, findings, [], cardsToCutTarget: 5,
            planAffinities: Affinities([("Noisy Staple", 1)]));

        Assert.Equal("Off Plan Card", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round1Key, plan.NextProposal.RoundKey);
        Assert.Equal(1, plan.NextProposal.FindingCount);
        CutLabRoundQueueItem noisyStaple = Assert.Single(plan.Queue, item => item.CardName == "Noisy Staple");
        Assert.Equal(CutLabCutRoundEngine.Round2Key, noisyStaple.RoundKey);
        Assert.Equal(1, noisyStaple.FindingCount);
        Assert.Equal([CutLabFindingKind.StrandedSubtheme], noisyStaple.DiscriminatingFindingKinds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildQueue_MixedPool_PreservesRoundMembershipAndOrder(bool themesAvailable)
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("On Theme Six", 6),
            Card("Ramp Rock", 1, roles: ["ramp"]),
            Card("Off Theme Two", 2),
            Card("Combo Piece", 3),
            Card("Land", 0, isLand: true),
            Card("On Theme One", 1),
            Card("Off Theme Five", 5),
            Card("On Theme Four", 4),
        ];
        IReadOnlyDictionary<string, CutLabPlanAffinity> affinities = new Dictionary<string, CutLabPlanAffinity>(StringComparer.OrdinalIgnoreCase)
        {
            ["Off Theme Two"] = new([], 0, false),
            ["Off Theme Five"] = new([], 0, false),
            ["On Theme One"] = new([], 1, true),
            ["On Theme Four"] = new([], 1, true),
            ["On Theme Six"] = new([], 1, true),
            ["Combo Piece"] = new([], 1, true),
        };

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.StrandedSubtheme, "Off Theme Five", "On Theme Four", "Combo Piece"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Off Theme Five"),
                Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece")),
            [],
            cardsToCutTarget: workingList.Count,
            planAffinities: affinities,
            checkedCommanderThemesAvailable: themesAvailable);

        (string CardName, string RoundKey)[] expected = themesAvailable
            ?
            [
                ("Off Theme Five", CutLabCutRoundEngine.Round1Key),
                ("Off Theme Two", CutLabCutRoundEngine.Round1Key),
                ("On Theme Four", CutLabCutRoundEngine.Round2Key),
                ("Combo Piece", CutLabCutRoundEngine.Round2Key),
                ("On Theme One", CutLabCutRoundEngine.Round3Key),
                ("On Theme Six", CutLabCutRoundEngine.Round3Key),
                ("Ramp Rock", CutLabCutRoundEngine.InfrastructureKey),
                ("Land", CutLabCutRoundEngine.InfrastructureKey),
            ]
            :
            [
                ("Off Theme Five", CutLabCutRoundEngine.Round1Key),
                ("Off Theme Two", CutLabCutRoundEngine.Round2Key),
                ("On Theme Four", CutLabCutRoundEngine.Round2Key),
                ("Combo Piece", CutLabCutRoundEngine.Round2Key),
                ("On Theme One", CutLabCutRoundEngine.Round3Key),
                ("On Theme Six", CutLabCutRoundEngine.Round3Key),
                ("Ramp Rock", CutLabCutRoundEngine.InfrastructureKey),
                ("Land", CutLabCutRoundEngine.InfrastructureKey),
            ];

        Assert.Equal(expected, plan.Queue.Select(item => (item.CardName, item.RoundKey)));
    }

    [Fact]
    public void BuildQueue_InfrastructureCardsFollowAllNonInfrastructureCards()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Sol Ring", 1, roles: ["ramp"]),
            Card("Command Tower", 0, isLand: true),
            Card("Off Theme Card", 4),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.CurveCongestion, "Sol Ring"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Sol Ring"),
                Finding(CutLabFindingKind.CurveCongestion, "Command Tower"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Command Tower")),
            [],
            cardsToCutTarget: 3);

        Assert.Equal("Off Theme Card", plan.NextProposal!.CardName);
        Assert.All(plan.Queue.Where(item => item.CardName is "Sol Ring" or "Command Tower"), item =>
            Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, item.RoundKey));
    }

    [Fact]
    public void BuildQueue_NonRampNonLandStaple_UsesInfrastructureRound()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Esper Sentinel", 1),
            Card("Off Theme Card", 4),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.CurveCongestion, "Esper Sentinel")),
            [],
            cardsToCutTarget: 2,
            stapleCardNames: new HashSet<string>(["esper sentinel"], StringComparer.Ordinal));

        CutLabRoundQueueItem staple = Assert.Single(plan.Queue, item => item.CardName == "Esper Sentinel");
        Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, staple.RoundKey);
    }

    [Fact]
    public void BuildQueue_EmptyStapleSet_PreservesExistingRound()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList = [Card("Esper Sentinel", 1)];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(Finding(CutLabFindingKind.StrandedSubtheme, "Esper Sentinel")),
            [],
            cardsToCutTarget: 1,
            stapleCardNames: new HashSet<string>(StringComparer.Ordinal));

        CutLabRoundQueueItem card = Assert.Single(plan.Queue);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, card.RoundKey);
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
                Finding(CutLabFindingKind.StrandedSubtheme, "Agatha's Soul Cauldron", "Plain Filler"),
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
    public void BuildQueue_ComboProtectedCardSortsLastInRound1DespiteRepeatedFindingEvidence()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Combo Engine", 2),
            Card("Plain Filler", 4),
            Card("Plan Anchor", 1),
        ];

        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            workingList,
            Findings(
                Finding(CutLabFindingKind.CurveCongestion, "Combo Engine", "Plain Filler"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Combo Engine", "Plain Filler"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Combo Engine"),
                Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Engine")),
            [],
            cardsToCutTarget: 2,
            planAffinities: Affinities([("Plan Anchor", 1)]));

        Assert.Equal("Plain Filler", plan.NextProposal!.CardName);
        Assert.Equal(CutLabCutRoundEngine.Round1Key, plan.NextProposal.RoundKey);
        Assert.Equal(CutLabCutRoundEngine.Round1Key, Assert.Single(plan.Queue, item => item.CardName == "Combo Engine").RoundKey);
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
                Finding(CutLabFindingKind.StrandedSubtheme, "Needs Partner Piece", "Plain Filler"),
                Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.NeedsPartner, "Needs Partner Piece")),
            [],
            cardsToCutTarget: 2);

        // Both are round 2 (one discriminating finding each). A NeedsPartner-only combo finding
        // must not demote, so the higher-mana-value card leads on the normal tiebreak.
        Assert.Equal("Plain Filler", plan.NextProposal!.CardName);
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
    public void BuildQueue_FunctionalTwinsFinding_HasZeroTallyImpact()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Twin Card", 3), Card("Fallback", 1)],
            Findings(Finding(CutLabFindingKind.FunctionalTwins, "Twin Card")),
            [],
            cardsToCutTarget: 2);

        CutLabRoundQueueItem twinCard = Assert.Single(plan.Queue, item => item.CardName == "Twin Card");
        Assert.Equal(CutLabCutRoundEngine.Round3Key, twinCard.RoundKey);
        Assert.Equal(0, twinCard.FindingCount);
        Assert.Empty(twinCard.DiscriminatingFindingKinds);
    }

    [Fact]
    public void BuildQueue_FunctionalTwinsPlusOneOtherFinding_UsesOnlyOtherFinding()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Twin Card", 3), Card("Fallback", 1)],
            Findings(
                Finding(CutLabFindingKind.FunctionalTwins, "Twin Card"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Twin Card")),
            [],
            cardsToCutTarget: 2);

        CutLabRoundQueueItem twinCard = Assert.Single(plan.Queue, item => item.CardName == "Twin Card");
        Assert.Equal(CutLabCutRoundEngine.Round2Key, twinCard.RoundKey);
        Assert.Equal(1, twinCard.FindingCount);
        Assert.Equal([CutLabFindingKind.StrandedSubtheme], twinCard.DiscriminatingFindingKinds);
    }

    [Fact]
    public void BuildQueue_FunctionalTwinsDoesNotChangeNextProposal()
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
        Assert.Equal("Early Fallback", withTwins.NextProposal!.CardName);
        Assert.Equal(withoutTwins.NextProposal.CardName, withTwins.NextProposal.CardName);
    }

    [Fact]
    public void BuildQueue_FunctionalTwinsPreservesComboProtectedQueueComposition()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList = [Card("Combo Piece", 1), Card("Fallback", 2)];
        CutLabStructuralFindingsResult withoutTwins = Findings(
            Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece"));
        CutLabStructuralFindingsResult withTwins = Findings(
            Finding(CutLabFindingKind.ComboProtected, ComboBadgeState.CompletePiece, "Combo Piece"),
            Finding(CutLabFindingKind.FunctionalTwins, "Combo Piece"));

        CutLabRoundPlan baseline = CutLabCutRoundEngine.BuildQueue(workingList, withoutTwins, [], cardsToCutTarget: 2);
        CutLabRoundPlan flagged = CutLabCutRoundEngine.BuildQueue(workingList, withTwins, [], cardsToCutTarget: 2);

        Assert.Equal(baseline.NextProposal!.CardName, flagged.NextProposal!.CardName);
        Assert.Equal(baseline.Queue.Select(item => item.CardName), flagged.Queue.Select(item => item.CardName));
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
    public void BuildQueue_TwinsEvidenceMatchesNormalizedEquivalentWorkingListEntries_HasZeroTallyImpact()
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

        Assert.Equal(4, twinsPlan.Queue.Count);
        Assert.All(twinsPlan.Queue, item =>
        {
            Assert.Equal(0, item.FindingCount);
            Assert.Equal(CutLabCutRoundEngine.Round3Key, item.RoundKey);
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
    public void BuildQueue_TwinsDuplicateRawPoolEntries_HasZeroTallyImpact()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Twin Card", 1), Card("Twin Card", 1), Card("Card B", 1), Card("Card C", 1)],
            Findings(Finding(CutLabFindingKind.FunctionalTwins, "Twin Card", "Card B", "Card C")),
            [],
            cardsToCutTarget: 4);

        Assert.Equal(4, plan.Queue.Count);
        Assert.All(plan.Queue, item => Assert.Equal(0, item.FindingCount));
    }

    [Fact]
    public void BuildQueue_MultiRoleCardWithTwoTwinsFindings_HasZeroTallyImpact()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Multi-Role Twin", 3)],
            Findings(
                Finding(CutLabFindingKind.FunctionalTwins, "Multi-Role Twin"),
                Finding(CutLabFindingKind.FunctionalTwins, "Multi-Role Twin")),
            [],
            cardsToCutTarget: 1);

        CutLabRoundQueueItem twinCard = Assert.Single(plan.Queue);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, twinCard.RoundKey);
        Assert.Equal(0, twinCard.FindingCount);
    }

    [Fact]
    public void BuildQueue_CountedFindingKinds_ContainsOnlyStrandedSubtheme()
    {
        System.Reflection.FieldInfo? field = typeof(CutLabCutRoundEngine).GetField(
            "CountedFindingKinds",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(field);
        IReadOnlySet<CutLabFindingKind> countedKinds = Assert.IsAssignableFrom<IReadOnlySet<CutLabFindingKind>>(field!.GetValue(null));
        Assert.True(countedKinds.SetEquals([CutLabFindingKind.StrandedSubtheme]));
    }

    [Fact]
    public void BuildQueue_InfrastructureByLandTypeOrRamp_FollowsNonInfrastructure()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [
                Card("Spell", 2),
                Card("Sol Ring", 1, roles: ["ramp"]),
                Card("Command Tower", 0, isLand: false, typeLine: "Land — Tower"),
                Card("Ancient Tomb", 0, isLand: true),
            ],
            Findings(Finding(CutLabFindingKind.CurveCongestion, "Sol Ring", "Command Tower", "Ancient Tomb")),
            [],
            cardsToCutTarget: 4);

        Assert.Equal("Spell", plan.Queue[0].CardName);
        Assert.All(plan.Queue.Skip(1), item => Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, item.RoundKey));
    }

    [Fact]
    public void BuildQueue_CheckedThemesWithoutPlanAffinities_TreatsNonInfrastructureCardsAsOffTheme()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Spell", 2), Card("Sol Ring", 1, roles: ["ramp"])],
            Findings(),
            [],
            cardsToCutTarget: 2,
            planAffinities: null,
            checkedCommanderThemesAvailable: true);

        Assert.Equal(CutLabCutRoundEngine.Round1Key, Assert.Single(plan.Queue, item => item.CardName == "Spell").RoundKey);
        Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, Assert.Single(plan.Queue, item => item.CardName == "Sol Ring").RoundKey);
    }

    [Fact]
    public void BuildQueue_CheckedThemes_SeparatesOffThemeRound1FromFlaggedOnThemeRound2()
    {
        IReadOnlyDictionary<string, CutLabPlanAffinity> affinities = new Dictionary<string, CutLabPlanAffinity>(StringComparer.OrdinalIgnoreCase)
        {
            ["Off Theme"] = new([], 1, false),
            ["On Theme"] = new([], 0, true),
        };
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Off Theme", 2), Card("On Theme", 4)],
            Findings(Finding(CutLabFindingKind.StrandedSubtheme, "On Theme")),
            [],
            cardsToCutTarget: 2,
            planAffinities: affinities,
            checkedCommanderThemesAvailable: true);

        CutLabRoundQueueItem offTheme = Assert.Single(plan.Queue, item => item.CardName == "Off Theme");
        CutLabRoundQueueItem onTheme = Assert.Single(plan.Queue, item => item.CardName == "On Theme");
        Assert.True(plan.CheckedCommanderThemesAvailable);
        Assert.Equal(CutLabCutRoundEngine.Round1Key, offTheme.RoundKey);
        Assert.Equal(CutLabCutRoundEngine.Round1Label, offTheme.RoundLabel);
        Assert.True(offTheme.CheckedCommanderThemesAvailable);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, onTheme.RoundKey);
        Assert.True(onTheme.CheckedCommanderThemesAvailable);
    }

    [Fact]
    public void BuildQueue_HigherManaValueWinsTiesInRound1Round2AndInfrastructure()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [
                Card("Round One High", 6), Card("Round One Low", 2),
                Card("Round Two High", 5), Card("Round Two Low", 1),
                Card("Land High", 4, isLand: true), Card("Land Low", 0, isLand: true),
            ],
            Findings(
                Finding(CutLabFindingKind.StrandedSubtheme, "Round One High", "Round One Low", "Round Two High", "Round Two Low", "Land High", "Land Low"),
                Finding(CutLabFindingKind.StrandedSubtheme, "Round One High", "Round One Low")),
            [],
            cardsToCutTarget: 6,
            planAffinities: Affinities([("Round Two High", 1), ("Round Two Low", 1)]));

        Assert.Equal(["Round One High", "Round One Low"], plan.Queue.Where(item => item.RoundKey == CutLabCutRoundEngine.Round1Key).Select(item => item.CardName));
        Assert.Equal(["Round Two High", "Round Two Low"], plan.Queue.Where(item => item.RoundKey == CutLabCutRoundEngine.Round2Key).Select(item => item.CardName));
        Assert.Equal(["Land High", "Land Low"], plan.Queue.Where(item => item.RoundKey == CutLabCutRoundEngine.InfrastructureKey).Select(item => item.CardName));
    }

    [Fact]
    public void BuildQueue_RemainderUnsafeFirstPassCard_PreservesReachableCutAcrossAcceptedRebuild()
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Non-Infrastructure Singleton A", 1),
            Card("Non-Infrastructure Singleton B", 1),
            Card("Two-Copy Land", 0, quantity: 2, isLand: true),
        ];

        CutLabRoundPlan initialPlan = CutLabCutRoundEngine.BuildQueue(workingList, Findings(), [], cardsToCutTarget: 3);

        Assert.Equal("Non-Infrastructure Singleton A", initialPlan.NextProposal?.CardName);

        CutLabRoundPlan rebuiltPlan = CutLabCutRoundEngine.BuildQueue(
            [workingList[1], workingList[2]],
            Findings(),
            [new CutLabDecision { CardName = "Non-Infrastructure Singleton A", Kind = CutLabDecisionKind.Accepted, Round = initialPlan.NextProposal!.RoundKey, Ordinal = 1 }],
            cardsToCutTarget: 2);

        Assert.Equal(2, rebuiltPlan.CardsRemainingToTarget);
        Assert.NotEmpty(rebuiltPlan.Queue);
        Assert.Equal("Two-Copy Land", rebuiltPlan.NextProposal?.CardName);
        Assert.Equal(2, workingList.Single(card => card.Name == rebuiltPlan.NextProposal?.CardName).Quantity);
    }

    [Theory]
    [InlineData(CutLabDecisionKind.Deferred, CutLabCutRoundEngine.SecondPassDeferredKey)]
    [InlineData(CutLabDecisionKind.Rejected, CutLabCutRoundEngine.SecondPassRejectedKey)]
    public void BuildQueue_RemainderUnsafeSecondPassCard_PreservesReachableCutAcrossAcceptedRebuild(
        CutLabDecisionKind priorDecisionKind,
        string priorRound)
    {
        IReadOnlyList<CutLabRoundInputCard> workingList =
        [
            Card("Non-Infrastructure Singleton A", 1),
            Card("Non-Infrastructure Singleton B", 1),
            Card("Two-Copy Land", 0, quantity: 2, isLand: true),
        ];
        CutLabDecision[] priorDecisions =
        [
            new CutLabDecision { CardName = "Non-Infrastructure Singleton A", Kind = priorDecisionKind, Round = priorRound, Ordinal = 1 },
            new CutLabDecision { CardName = "Non-Infrastructure Singleton B", Kind = priorDecisionKind, Round = priorRound, Ordinal = 2 },
            new CutLabDecision { CardName = "Two-Copy Land", Kind = priorDecisionKind, Round = priorRound, Ordinal = 3 },
        ];

        CutLabRoundPlan initialPlan = CutLabCutRoundEngine.BuildQueue(workingList, Findings(), priorDecisions, cardsToCutTarget: 3);

        Assert.Equal("Non-Infrastructure Singleton A", initialPlan.NextProposal?.CardName);

        CutLabRoundPlan rebuiltPlan = CutLabCutRoundEngine.BuildQueue(
            [workingList[1], workingList[2]],
            Findings(),
            [.. priorDecisions, new CutLabDecision { CardName = "Non-Infrastructure Singleton A", Kind = CutLabDecisionKind.Accepted, Round = initialPlan.NextProposal!.RoundKey, Ordinal = 4 }],
            cardsToCutTarget: 2);

        Assert.Equal(2, rebuiltPlan.CardsRemainingToTarget);
        Assert.NotEmpty(rebuiltPlan.Queue);
        Assert.Equal("Two-Copy Land", rebuiltPlan.NextProposal?.CardName);
        Assert.Equal(2, workingList.Single(card => card.Name == rebuiltPlan.NextProposal?.CardName).Quantity);
    }

    [Theory]
    [InlineData(0, true, CutLabCutRoundEngine.Round1Key, 1)]
    [InlineData(1, true, CutLabCutRoundEngine.Round2Key, 1)]
    [InlineData(0, false, CutLabCutRoundEngine.Round2Key, 0)]
    [InlineData(1, false, CutLabCutRoundEngine.Round3Key, 0)]
    public void BuildQueue_StrandedFindingAndPlanScore_AssignsExpectedRound(int score, bool hasStrandedFinding, string expectedRoundKey, int expectedFindingCount)
    {
        CutLabRoundQueueItem card = PlanAffinityCard(score, hasStrandedFinding ? [CutLabFindingKind.StrandedSubtheme] : []);

        Assert.Equal(expectedRoundKey, card.RoundKey);
        Assert.Equal(expectedFindingCount, card.FindingCount);
    }

    [Fact]
    public void BuildQueue_NullPlanAffinities_TreatsOffPlanAsNoSignal()
    {
        AssertNoPlanSignal(null);
    }

    [Fact]
    public void BuildQueue_AllNeutralPlanAffinities_TreatsOffPlanAsNoSignal()
    {
        AssertNoPlanSignal(Affinities([("Stranded", 0), ("Plain", 0)]));
    }

    [Theory]
    [InlineData("Land")]
    [InlineData("Ramp")]
    [InlineData("Staple")]
    public void BuildQueue_OnlyInfrastructureCardOnPlan_TreatsOffPlanAsNoSignal(string infrastructureKind)
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Infrastructure", 0, isLand: infrastructureKind == "Land", roles: infrastructureKind == "Ramp" ? ["ramp"] : []), Card("Stranded", 2), Card("Plain", 2)],
            Findings(Finding(CutLabFindingKind.StrandedSubtheme, "Stranded")),
            [],
            cardsToCutTarget: 1,
            planAffinities: Affinities([("Infrastructure", 1), ("Stranded", 0), ("Plain", 0)]),
            stapleCardNames: infrastructureKind == "Staple" ? new HashSet<string>(["infrastructure"], StringComparer.Ordinal) : null);

        Assert.All(plan.Queue, card => Assert.False(card.PlanSignalAvailable));
        Assert.DoesNotContain(plan.Queue, card => card.RoundKey == CutLabCutRoundEngine.Round1Key);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, Assert.Single(plan.Queue, card => card.CardName == "Stranded").RoundKey);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, Assert.Single(plan.Queue, card => card.CardName == "Plain").RoundKey);
        Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, Assert.Single(plan.Queue, card => card.CardName == "Infrastructure").RoundKey);
    }

    [Fact]
    public void BuildQueue_CurveCongestionOnly_DoesNotCountTowardTally()
    {
        CutLabRoundQueueItem card = PlanAffinityCard(1, CutLabFindingKind.CurveCongestion);

        Assert.Equal(0, card.FindingCount);
        Assert.Empty(card.DiscriminatingFindingKinds);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, card.RoundKey);
    }

    [Fact]
    public void BuildQueue_CurveCongestionAndStrandedSubtheme_CountsOnlyStrandedSubtheme()
    {
        CutLabRoundQueueItem card = PlanAffinityCard(1, CutLabFindingKind.CurveCongestion, CutLabFindingKind.StrandedSubtheme);

        Assert.Equal(1, card.FindingCount);
        Assert.Equal([CutLabFindingKind.StrandedSubtheme], card.DiscriminatingFindingKinds);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, card.RoundKey);
    }

    [Fact]
    public void BuildQueue_CheckedThemesInThemeCurveCongestionOnly_AssignsRound3()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Candidate", 2)],
            Findings(Finding(CutLabFindingKind.CurveCongestion, "Candidate")),
            [],
            cardsToCutTarget: 1,
            planAffinities: Affinities([("Candidate", 1)], true),
            checkedCommanderThemesAvailable: true);

        Assert.Equal(CutLabCutRoundEngine.Round3Key, Assert.Single(plan.Queue).RoundKey);
    }

    [Theory]
    [InlineData("interaction-targeted", 4, 4, true, false, "round-3")]
    [InlineData("interaction-targeted", 4, 5, true, false, "round-1")]
    [InlineData("interaction-targeted", 4, 4, true, true, "round-2")]
    [InlineData("protection", 4, 3, false, true, "round-2")]
    [InlineData("protection", 4, 3, false, false, "round-3")]
    [InlineData("interaction-mass", 4, 4, true, false, "round-3")]
    [InlineData("protection", 4, 4, true, false, "round-3")]
    [InlineData("INTERACTION-TARGETED", 4, 4, true, false, "round-3")]
    [InlineData("draw", 4, 4, true, false, "round-1")]
    [InlineData("interaction-targeted", 0, 0, true, false, "round-1")]
    public void BuildQueue_InteractionFloor_RoutesByCurrentCount(
        string role, int floor, int count, bool checkedThemes, bool hasFinding, string expectedRound)
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Candidate", 4, roles: [role]), Card("Anchor", 1)],
            hasFinding ? Findings(Finding(CutLabFindingKind.StrandedSubtheme, "Candidate")) : Findings(),
            [], cardsToCutTarget: 1,
            floorByRole: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [role.ToLowerInvariant()] = floor },
            roleCounts: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [role.ToLowerInvariant()] = count },
            planAffinities: Affinities([("Candidate", 0), ("Anchor", 1)]),
            checkedCommanderThemesAvailable: checkedThemes);

        Assert.Equal(expectedRound, Assert.Single(plan.Queue, item => item.CardName == "Candidate").RoundKey);
    }

    [Theory]
    [InlineData("null-floors")]
    [InlineData("null-counts")]
    [InlineData("missing-floor")]
    [InlineData("missing-count")]
    public void BuildQueue_InteractionFloor_MissingInputsDoNotHold(string mode)
    {
        Dictionary<string, int> floors = mode == "missing-floor" ? [] : new() { ["protection"] = 3 };
        Dictionary<string, int> counts = mode == "missing-count" ? [] : new() { ["protection"] = 3 };
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Candidate", 4, roles: ["protection"])], Findings(), [], cardsToCutTarget: 1,
            floorByRole: mode == "null-floors" ? null : floors,
            roleCounts: mode == "null-counts" ? null : counts,
            checkedCommanderThemesAvailable: true);

        Assert.Equal(CutLabCutRoundEngine.Round1Key, Assert.Single(plan.Queue).RoundKey);
    }

    [Fact]
    public void BuildQueue_InteractionFloor_AnyInteractionRoleCanHold()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Candidate", 4, roles: ["interaction-mass", "protection"])], Findings(), [], cardsToCutTarget: 1,
            floorByRole: new Dictionary<string, int> { ["interaction-mass"] = 2, ["protection"] = 3 },
            roleCounts: new Dictionary<string, int> { ["interaction-mass"] = 3, ["protection"] = 3 },
            checkedCommanderThemesAvailable: true);

        Assert.Equal(CutLabCutRoundEngine.Round3Key, Assert.Single(plan.Queue).RoundKey);
    }

    [Fact]
    public void BuildQueue_InteractionFloor_InfrastructureStaysInfrastructure()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Candidate", 0, isLand: true, roles: ["protection"])], Findings(), [], cardsToCutTarget: 1,
            floorByRole: new Dictionary<string, int> { ["protection"] = 3 },
            roleCounts: new Dictionary<string, int> { ["protection"] = 3 },
            checkedCommanderThemesAvailable: true);

        Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, Assert.Single(plan.Queue).RoundKey);
    }

    [Fact]
    public void BuildQueue_InteractionFloor_HeldCardWithZeroAffinitySuppliesNoPlanSignal()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Held", 4, roles: ["protection"]), Card("Plain", 1)],
            Findings(Finding(CutLabFindingKind.StrandedSubtheme, "Held")), [], cardsToCutTarget: 1,
            floorByRole: new Dictionary<string, int> { ["protection"] = 3 },
            roleCounts: new Dictionary<string, int> { ["protection"] = 3 },
            planAffinities: Affinities([("Held", 0), ("Plain", 0)]));

        Assert.All(plan.Queue, item => Assert.False(item.PlanSignalAvailable));
        Assert.Equal(CutLabCutRoundEngine.Round2Key, Assert.Single(plan.Queue, item => item.CardName == "Held").RoundKey);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, Assert.Single(plan.Queue, item => item.CardName == "Plain").RoundKey);
    }

    [Fact]
    public void BuildQueue_InteractionFloor_HeldCardWithPositiveAffinitySuppliesPlanSignal()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Held", 4, roles: ["protection"]), Card("Plain", 1)],
            Findings(Finding(CutLabFindingKind.StrandedSubtheme, "Held")), [], cardsToCutTarget: 1,
            floorByRole: new Dictionary<string, int> { ["protection"] = 3 },
            roleCounts: new Dictionary<string, int> { ["protection"] = 3 },
            planAffinities: Affinities([("Held", 1), ("Plain", 0)]));

        Assert.All(plan.Queue, item => Assert.True(item.PlanSignalAvailable));
        Assert.Equal(CutLabCutRoundEngine.Round2Key, Assert.Single(plan.Queue, item => item.CardName == "Held").RoundKey);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, Assert.Single(plan.Queue, item => item.CardName == "Plain").RoundKey);
    }

    [Fact]
    public void BuildQueue_InteractionFloor_OrdinalDictionariesMatchRoleCaseInsensitively()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Held", 4, roles: ["interaction-targeted"])], Findings(), [], cardsToCutTarget: 1,
            floorByRole: new Dictionary<string, int>(StringComparer.Ordinal) { ["Interaction-Targeted"] = 3 },
            roleCounts: new Dictionary<string, int>(StringComparer.Ordinal) { ["Interaction-Targeted"] = 3 },
            checkedCommanderThemesAvailable: true);

        Assert.Equal(CutLabCutRoundEngine.Round3Key, Assert.Single(plan.Queue).RoundKey);
    }

    [Fact]
    public void BuildQueue_InteractionFloor_PlanRankOrdersPlainCardFirst()
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("A Held", 1, roles: ["protection"]), Card("Z Plain", 8)], Findings(), [], cardsToCutTarget: 1,
            floorByRole: new Dictionary<string, int> { ["protection"] = 3 },
            roleCounts: new Dictionary<string, int> { ["protection"] = 3 },
            planAffinities: Affinities([("A Held", 0), ("Z Plain", 0)]));

        Assert.All(plan.Queue, item => Assert.Equal(CutLabCutRoundEngine.Round3Key, item.RoundKey));
        Assert.Equal(new[] { "Z Plain", "A Held" }, plan.Queue.Select(item => item.CardName));
    }

    [Fact]
    public void BuildFindingsAndRoundPlan_InteractionFloor_UsesResolvedFloorAndWorkingCounts()
    {
        IReadOnlyDictionary<string, int> floors = CutLabFloorDefaults.ResolveDefaults(
            3, "", 4, [], null, null, null, []).ToDictionary(floor => floor.Role, floor => floor.DefaultValue);
        int floor = floors["interaction-targeted"];
        Assert.True(floor > 0);

        foreach (int count in new[] { floor + 1, floor })
        {
            CutLabPoolCard[] working =
            [
                new() { Name = "Candidate", Quantity = count, TypeLine = "Instant" },
                new() { Name = "Locked filler", Quantity = 100, TypeLine = "Land", IsLocked = true },
            ];
            CutLabAnalyzedCard[] analyzed = [new("Candidate", 1, false, ["interaction-targeted"], []) { Quantity = count }];
            CutLabAnalysisContext context = new(
                analyzed,
                new Dictionary<string, IReadOnlyList<string>> { ["Candidate"] = analyzed[0].Roles },
                new Dictionary<string, int> { ["interaction-targeted"] = analyzed.Sum(card => card.Quantity) },
                4, default,
                new CutLabClassificationContext([], true, true,
                    new Dictionary<string, IReadOnlyList<string>>(), new Dictionary<string, CutLabCardComboMembership>()),
                []);

            var result = CutLabCutRoundEngine.BuildFindingsAndRoundPlan(
                working, context, floors, [], twinsEnabled: false, checkedCommanderThemesAvailable: true);

            Assert.Equal(count > floor ? CutLabCutRoundEngine.Round1Key : CutLabCutRoundEngine.Round3Key,
                Assert.Single(result.RoundPlan.Queue).RoundKey);
        }
    }

    private static CutLabRoundQueueItem PlanAffinityCard(int score, params CutLabFindingKind[] kinds)
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [Card("Candidate", 2), Card("On Plan", 2)],
            Findings(kinds.Select(kind => Finding(kind, "Candidate")).ToArray()),
            [],
            cardsToCutTarget: 1,
            planAffinities: Affinities([("Candidate", score), ("On Plan", 1)]));

        return Assert.Single(plan.Queue, card => card.CardName == "Candidate");
    }

    private static CutLabRoundPlan AssertNoPlanSignal(IReadOnlyDictionary<string, CutLabPlanAffinity>? affinities, params CutLabRoundInputCard[] extraCards)
    {
        CutLabRoundPlan plan = CutLabCutRoundEngine.BuildQueue(
            [.. extraCards, Card("Stranded", 2), Card("Plain", 2)],
            Findings(Finding(CutLabFindingKind.StrandedSubtheme, "Stranded")),
            [],
            cardsToCutTarget: 1,
            planAffinities: affinities);

        Assert.DoesNotContain(plan.Queue, card => card.RoundKey == CutLabCutRoundEngine.Round1Key);
        Assert.Equal(CutLabCutRoundEngine.Round2Key, Assert.Single(plan.Queue, card => card.CardName == "Stranded").RoundKey);
        Assert.Equal(CutLabCutRoundEngine.Round3Key, Assert.Single(plan.Queue, card => card.CardName == "Plain").RoundKey);
        return plan;
    }

    private static IReadOnlyDictionary<string, CutLabPlanAffinity> Affinities((string Name, int Score)[] entries, bool isInCheckedCommanderTheme = false)
        => entries.ToDictionary(entry => CutLabCardNames.Normalize(entry.Name), entry => new CutLabPlanAffinity([], entry.Score, isInCheckedCommanderTheme), StringComparer.OrdinalIgnoreCase);

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
