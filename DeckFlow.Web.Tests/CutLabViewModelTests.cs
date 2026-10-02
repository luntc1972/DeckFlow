using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for direct Cut Lab floor-row view-model projection.</summary>
public sealed class CutLabViewModelTests
{
    [Fact]
    public void From_InfrastructureProposal_RendersInfrastructureRoundCopy()
    {
        var result = new CutLabProcessResult
        {
            HasResult = true,
            State = new CutLabState
            {
                Pool =
                [
                    new CutLabPoolCard { Name = "Sol Ring", Quantity = 1 },
                ],
            },
            RoleAssignmentsByCardName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            Findings = new CutLabStructuralFindingsResult([], true, true),
            RoundPlan = new CutLabRoundPlan
            {
                CardsRemainingToTarget = 1,
                Queue =
                [
                    new CutLabRoundQueueItem(
                        "Sol Ring",
                        CutLabCutRoundEngine.InfrastructureKey,
                        CutLabCutRoundEngine.InfrastructureLabel,
                        1,
                        []),
                ],
                NextProposal = new CutLabRoundQueueItem(
                    "Sol Ring",
                    CutLabCutRoundEngine.InfrastructureKey,
                    CutLabCutRoundEngine.InfrastructureLabel,
                    1,
                    []),
            },
        };

        CutLabViewModel model = CutLabViewModel.From(new CutLabRequest(), result);

        Assert.Equal(CutLabCutRoundEngine.InfrastructureKey, model.Proposal.RoundKey);
        Assert.Equal(CutLabCutRoundEngine.InfrastructureLabel, model.Proposal.RoundLabel);
        Assert.Equal("Mana base & staples — proposed only after everything else.", model.Proposal.RoundBannerBody);
    }

    [Fact]
    public void From_CutsMade_UsesStoredThemeAvailabilityOverCurrentPlan()
    {
        var result = new CutLabProcessResult
        {
            State = new CutLabState
            {
                Decisions =
                [
                    new CutLabDecision
                    {
                        CardName = "Arcane Signet",
                        Kind = CutLabDecisionKind.Accepted,
                        Round = CutLabCutRoundEngine.Round1Key,
                        CheckedCommanderThemesAvailable = true,
                        Ordinal = 1,
                    },
                ],
            },
            RoundPlan = new CutLabRoundPlan { Queue = [], CardsRemainingToTarget = 1, CheckedCommanderThemesAvailable = false },
        };

        CutLabViewModel model = CutLabViewModel.From(new CutLabRequest(), result);

        Assert.Equal(CutLabCutRoundEngine.Round1Label, Assert.Single(model.CutsMade).RoundLabel);
    }

    [Fact]
    public void BuildFloorRows_OutOfScopeRole_ShowsNotApplicable()
    {
        IReadOnlyList<CutLabFloorRowView> rows = BuildRows(
        [
            CreateResolvedFloor("lands", bracketValue: 36, commanderValue: 40, defaultValue: 36, floor: 36),
            CreateResolvedFloor("interaction-mass", bracketValue: 3, commanderValue: 9, defaultValue: 3, floor: 3),
            CreateResolvedFloor("protection", bracketValue: 4, commanderValue: 8, defaultValue: 4, floor: 4),
        ]);

        Assert.Collection(
            rows,
            row =>
            {
                Assert.Equal("n/a", row.CommanderDisplay);
                Assert.False(row.SupportsCommanderFloor);
            },
            row =>
            {
                Assert.Equal("n/a", row.CommanderDisplay);
                Assert.False(row.SupportsCommanderFloor);
            },
            row =>
            {
                Assert.Equal("n/a", row.CommanderDisplay);
                Assert.False(row.SupportsCommanderFloor);
            });
    }

    [Fact]
    public void BuildFloorRows_GoRoleWithNoCommanderMatch_ShowsEmptyMarker()
    {
        CutLabFloorRowView row = Assert.Single(BuildRows(
        [
            CreateResolvedFloor("engines", bracketValue: 6, commanderValue: null, defaultValue: 6, floor: 6),
        ]));

        Assert.True(row.SupportsCommanderFloor);
        Assert.Equal("—", row.CommanderDisplay);
        Assert.NotEqual("n/a", row.CommanderDisplay);
    }

    [Fact]
    public void BuildFloorRows_GoRoleWithCommanderMatch_ShowsTheNumber()
    {
        CutLabFloorRowView row = Assert.Single(BuildRows(
        [
            CreateResolvedFloor("engines", bracketValue: 6, commanderValue: 9, defaultValue: 9, floor: 9),
        ]));

        Assert.Equal("9", row.CommanderDisplay);
    }

    [Fact]
    public void BuildFloorRows_CommanderBelowBracket_StillShowsTheCommanderNumber()
    {
        CutLabFloorRowView row = Assert.Single(BuildRows(
        [
            CreateResolvedFloor("payoffs", bracketValue: 6, commanderValue: 2, defaultValue: 6, floor: 6),
        ]));

        Assert.Equal("2", row.CommanderDisplay);
        Assert.Equal(6, row.Floor);
    }

    [Fact]
    public void BuildFloorRows_SourceLabel_NamesTheDrivingNumber()
    {
        IReadOnlyList<CutLabFloorRowView> rows = BuildRows(
        [
            CreateResolvedFloor("engines", bracketValue: 6, commanderValue: 9, defaultValue: 9, floor: 9),
            CreateResolvedFloor("payoffs", bracketValue: 6, commanderValue: 2, defaultValue: 6, floor: 6),
            CreateResolvedFloor("draw", bracketValue: 8, commanderValue: 8, defaultValue: 8, floor: 8),
            CreateResolvedFloor("wincons", bracketValue: 3, commanderValue: null, defaultValue: 3, floor: 3),
        ]);

        Assert.Equal("Commander", rows[0].SourceLabel);
        Assert.Equal("Bracket", rows[1].SourceLabel);
        Assert.Equal("Bracket", rows[2].SourceLabel);
        Assert.Equal("Bracket", rows[3].SourceLabel);
    }

    [Fact]
    public void BuildFloorRows_SourceDetail_ReportsTheEffectiveDefault()
    {
        IReadOnlyList<CutLabFloorRowView> rows = BuildRows(
        [
            CreateResolvedFloor("engines", bracketValue: 6, commanderValue: 9, defaultValue: 9, floor: 9, resolvedBracket: 4),
            CreateResolvedFloor("payoffs", bracketValue: 6, commanderValue: null, defaultValue: 6, floor: 6, bracketWasFallback: true),
        ]);

        Assert.Equal("Default for B4: 9", rows[0].SourceDetail);
        Assert.Equal("Default: 6 — based on Focused", rows[1].SourceDetail);
    }

    [Fact]
    public void BuildFloorRows_PreservesInPoolAndAtFloorBehavior()
    {
        IReadOnlyList<CutLabFloorRowView> rows = BuildRows(
            [
                CreateResolvedFloor("ramp", bracketValue: 10, commanderValue: 12, defaultValue: 12, floor: 4),
                CreateResolvedFloor("draw", bracketValue: 14, commanderValue: 15, defaultValue: 15, floor: 4),
            ],
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ramp"] = 5,
                ["draw"] = 6,
            });

        Assert.Equal(5, rows[0].InPoolCount);
        Assert.True(rows[0].AtFloor);
        Assert.Equal(6, rows[1].InPoolCount);
        Assert.False(rows[1].AtFloor);
    }

    [Fact]
    public void BuildFloorRows_EmitsOneRowPerResolvedFloorInOrder()
    {
        IReadOnlyList<CutLabFloorRowView> rows = BuildRows(
        [
            CreateResolvedFloor("wincons", bracketValue: 3, commanderValue: null, defaultValue: 3, floor: 3),
            CreateResolvedFloor("ramp", bracketValue: 10, commanderValue: 12, defaultValue: 12, floor: 12),
            CreateResolvedFloor("lands", bracketValue: 36, commanderValue: null, defaultValue: 36, floor: 36),
        ]);

        Assert.Equal(["wincons", "ramp", "lands"], rows.Select(row => row.RoleKey).ToArray());
    }

    [Fact]
    public void BuildFloorFeasibilityMessage_NamesBothNumbersAndTheRelaxCandidates()
    {
        string message = CutLabViewModel.BuildFloorFeasibilityMessage(new CutLabFloorFeasibilityResult
        {
            RequiredNonlandSlots = 68,
            AvailableNonlandSlots = 63,
            LandsFloor = 36,
            RelaxCandidates =
            [
                new CutLabFloorRelaxCandidate { RoleKey = "payoffs", Floor = 15, CommanderRaise = 5 },
                new CutLabFloorRelaxCandidate { RoleKey = "ramp", Floor = 12, CommanderRaise = 3 },
            ],
        });

        Assert.Contains("68", message);
        Assert.Contains("63", message);
        Assert.Contains("36", message);
        Assert.Contains("Payoffs", message);
        Assert.Contains("Ramp", message);
    }

    [Fact]
    public void BuildFloorFeasibilityMessage_IncludesEachCandidatesRaiseAmount()
    {
        // Why: the counts-and-labels test above would still pass if every raise amount vanished.
        // Task 2 requires the amount to render, so this test must fail when the clause is removed.
        string message = CutLabViewModel.BuildFloorFeasibilityMessage(new CutLabFloorFeasibilityResult
        {
            RequiredNonlandSlots = 68,
            AvailableNonlandSlots = 63,
            LandsFloor = 36,
            RelaxCandidates =
            [
                new CutLabFloorRelaxCandidate { RoleKey = "payoffs", Floor = 15, CommanderRaise = 4 },
                new CutLabFloorRelaxCandidate { RoleKey = "engines", Floor = 12, CommanderRaise = 3 },
            ],
        });

        Assert.Contains("Payoffs (raised by 4)", message);
        Assert.Contains("Engines (raised by 3)", message);
    }

    [Fact]
    public void BuildFloorFeasibilityMessage_CandidateWithNoRaise_OmitsTheRaiseSuffix()
    {
        string message = CutLabViewModel.BuildFloorFeasibilityMessage(new CutLabFloorFeasibilityResult
        {
            RequiredNonlandSlots = 68,
            AvailableNonlandSlots = 63,
            LandsFloor = 36,
            RelaxCandidates =
            [
                new CutLabFloorRelaxCandidate { RoleKey = "engines", Floor = 12, CommanderRaise = null },
            ],
        });

        Assert.Contains("Engines", message);
        Assert.DoesNotContain("Engines (raised by", message);
        Assert.DoesNotContain("raised by 0", message);
    }

    [Fact]
    public void BuildFloorFeasibilityMessage_StatesTheEstimateIsConservative()
    {
        // Why: D-06a requires the copy to admit the estimate is conservative rather than precise.
        string message = CutLabViewModel.BuildFloorFeasibilityMessage(new CutLabFloorFeasibilityResult
        {
            RequiredNonlandSlots = 68,
            AvailableNonlandSlots = 63,
            LandsFloor = 36,
            RelaxCandidates =
            [
                new CutLabFloorRelaxCandidate { RoleKey = "payoffs", Floor = 15, CommanderRaise = 4 },
            ],
        });

        Assert.Contains("This is a conservative estimate", message);
        Assert.Contains("every engine is also a draw spell", message);
        Assert.Contains("may be larger", message);
    }

    [Fact]
    public void BuildFloorFeasibilityMessage_NoRelaxCandidates_OmitsTheActionSentence()
    {
        string message = CutLabViewModel.BuildFloorFeasibilityMessage(new CutLabFloorFeasibilityResult
        {
            RequiredNonlandSlots = 68,
            AvailableNonlandSlots = 63,
            LandsFloor = 36,
            RelaxCandidates = [],
        });

        Assert.DoesNotContain("Relax ", message);
        Assert.DoesNotContain("first.", message);
    }

    private static IReadOnlyList<CutLabFloorRowView> BuildRows(
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors,
        IReadOnlyDictionary<string, int>? countsByRole = null)
        => CutLabViewModel.BuildFloorRows(
            resolvedFloors,
            countsByRole ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            "Focused");

    [Fact]
    public void BuildPlanPanel_Archetype_RowsIncludeNoneFirstThenSevenInCatalogOrder()
    {
        CutLabPlanPanelView panel = CutLabViewModel.BuildPlanPanel(null, [], false);
        Assert.Equal(string.Empty, panel.ArchetypeRows[0].Slug);
        Assert.Equal("No archetype", panel.ArchetypeRows[0].DisplayName);
        Assert.Equal("Cut Lab behaves exactly as it does today.", panel.ArchetypeRows[0].Definition);
        Assert.Equal("No strategies added, goals unchanged.", panel.ArchetypeRows[0].Detail);
        Assert.Equal("Adds: no strategies · Goals T3 / T3 / T6", panel.ArchetypeRows.Single(row => row.Slug == "value-engine").Detail);
        Assert.Equal(CutLabArchetypeCatalog.Entries.Select(entry => entry.Slug), panel.ArchetypeRows.Skip(1).Select(row => row.Slug));
    }

    [Fact]
    public void BuildPlanPanel_Archetype_SuggestedRowFlaggedWithReason_ChosenRowChecked()
    {
        CutLabPlanPanelView panel = CutLabViewModel.BuildPlanPanel(new CutLabPlanProfile { Archetype = "stax" }, [], false, new("turbo-combo", CutLabArchetypeConfidence.High, "Fast mana."));
        Assert.True(panel.ArchetypeRows.Single(row => row.Slug == "stax").IsChecked);
        Assert.True(panel.ArchetypeRows.Single(row => row.Slug == "turbo-combo").IsSuggested);
    }

    [Fact]
    public void BuildPlanPanel_Archetype_SuggestedBadgeHiddenWhenDifferentArchetypeChecked()
    {
        CutLabPlanPanelView panel = CutLabViewModel.BuildPlanPanel(new CutLabPlanProfile { Archetype = "stax" }, [], false, new("turbo-combo", CutLabArchetypeConfidence.High, "Fast mana."));

        CutLabPlanArchetypeRowView suggested = panel.ArchetypeRows.Single(row => row.Slug == "turbo-combo");
        Assert.True(suggested.IsSuggested);
        Assert.False(suggested.IsChecked);

        CutLabPlanPanelView chosenPanel = CutLabViewModel.BuildPlanPanel(new CutLabPlanProfile { Archetype = "turbo-combo" }, [], false, new("turbo-combo", CutLabArchetypeConfidence.High, "Fast mana."));
        CutLabPlanArchetypeRowView chosenSuggested = chosenPanel.ArchetypeRows.Single(row => row.Slug == "turbo-combo");
        Assert.True(chosenSuggested.IsSuggested);
        Assert.True(chosenSuggested.IsChecked);
    }

    [Fact]
    public void BuildPlanPanel_Archetype_PresetStrategyRowImpliedNotChecked()
    {
        CutLabPlanPanelView panel = CutLabViewModel.BuildPlanPanel(new CutLabPlanProfile { Archetype = "stax" }, [], false);
        CutLabPlanStrategyRowView row = panel.StrategyRows.Single(row => row.Slug == "stax");
        Assert.False(row.IsChecked);
        Assert.True(row.IsImpliedByArchetype);
    }

    [Fact]
    public void BuildPlanPanel_Archetype_PresetStrategyRowCheckedIsNotImplied()
    {
        CutLabPlanPanelView panel = CutLabViewModel.BuildPlanPanel(new CutLabPlanProfile { Archetype = "stax", GenericStrategies = ["stax"] }, [], false);
        CutLabPlanStrategyRowView row = panel.StrategyRows.Single(row => row.Slug == "stax");
        Assert.True(row.IsChecked);
        Assert.False(row.IsImpliedByArchetype);
    }

    [Fact]
    public void BuildPlanPanel_Archetype_NullSuggestion_NoBadgeNoReason()
    {
        CutLabPlanPanelView panel = CutLabViewModel.BuildPlanPanel(null, [], false);
        Assert.All(panel.ArchetypeRows, row => Assert.False(row.IsSuggested));
    }

    [Fact]
    public void BuildPlanPanel_Archetype_ArchetypeOnly_HidesZeroSelectionNotice()
    {
        CutLabPlanPanelView panel = CutLabViewModel.BuildPlanPanel(new CutLabPlanProfile { Archetype = "stax" }, [], false);
        Assert.False(panel.ZeroSelectionNotice);
    }

    private static CutLabResolvedFloor CreateResolvedFloor(
        string role,
        int bracketValue,
        int? commanderValue,
        int defaultValue,
        int floor,
        int resolvedBracket = 4,
        bool bracketWasFallback = false)
        => new()
        {
            Role = role,
            Floor = floor,
            IsUserSet = false,
            DefaultValue = defaultValue,
            BracketValue = bracketValue,
            CommanderValue = commanderValue,
            ResolvedBracket = resolvedBracket,
            BracketWasFallback = bracketWasFallback,
        };
}
