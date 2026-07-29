using DeckFlow.Core.Manabase;
using DeckFlow.Web.Extensions;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabUiPatchBuilder"/> covering server-authored patch projection and no-JS parity.</summary>
public sealed class CutLabUiPatchBuilderTests
{
    [Fact]
    public async Task BuildAsync_ReturnsServerAuthoredCountsAndExportEligibility()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Basic Filler", quantity: 99, isLocked: true),
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList));
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService());
        IReadOnlyList<CutLabDecideFloorWarningDto> suppliedWarnings =
        [
            new CutLabDecideFloorWarningDto
            {
                Role = "ramp",
                NewCount = 0,
                Floor = 1,
                Message = "Use supplied warnings.",
            },
        ];

        CutLabUiPatchDto patch = await builder.BuildAsync(
            state,
            state.Intent.PlayExperience,
            ["Commander"],
            floorWarnings: suppliedWarnings);

        Assert.Equal(CutLabStateSerializer.Serialize(state), patch.CutLabStateJson);
        Assert.Equal(100, patch.CurrentCount);
        Assert.Equal(0, patch.CardsRemaining);
        Assert.True(patch.CanBuildExport);
        Assert.True(patch.NextProposal.IsTerminal);
        Assert.True(patch.NextProposal.IsAtTarget);
        Assert.False(patch.NextProposal.IsNothingToCut);
        Assert.Null(patch.ProposalDeltas);
        Assert.Same(suppliedWarnings, patch.FloorWarnings);
    }

    [Fact]
    public async Task BuildAsync_ReturnsCutsMadeAndWhatifOptionsFromServerState()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Arcane Signet", quantity: 1),
                Card("Counterspell", quantity: 1),
                Card("Cut Card", quantity: 1),
                Card("Basic Filler", quantity: 98, isLocked: true),
            ],
            decisions:
            [
                new CutLabDecision
                {
                    CardName = "Cut Card",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round2Key,
                    Ordinal = 2,
                },
                new CutLabDecision
                {
                    CardName = "Counterspell",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ]);
        CutLabUiPatchBuilder builder = new(new FakeAnalysisContextBuilder(workingList => CreateAnalysisContext(workingList)), new FakeSimulationService());

        CutLabUiPatchDto patch = await builder.BuildAsync(
            state,
            state.Intent.PlayExperience,
            ["Commander"]);

        Assert.Collection(
            patch.CutsMade,
            cut =>
            {
                Assert.Equal("Cut Card", cut.CardName);
                Assert.Equal(CutLabCutRoundEngine.Round2Label, cut.RoundLabel);
                Assert.Equal(2, cut.Ordinal);
            },
            cut =>
            {
                Assert.Equal("Counterspell", cut.CardName);
                Assert.Equal(CutLabCutRoundEngine.Round1Label, cut.RoundLabel);
                Assert.Equal(1, cut.Ordinal);
            });
        Assert.Equal(["Arcane Signet"], patch.WhatifCardOutOptions);
        Assert.Equal(["Counterspell", "Cut Card"], patch.WhatifCardInOptions);
    }

    [Fact]
    public async Task BuildAsync_ReturnsGroupedStructuralFindings()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Arcane Signet", quantity: 1),
                Card("Counterspell", quantity: 1),
                Card("Basic Filler", quantity: 99, isLocked: true),
            ],
            roleFloors:
            [
                new CutLabRoleFloor
                {
                    Role = "ramp",
                    Floor = 1,
                    IsUserSet = true,
                },
                new CutLabRoleFloor
                {
                    Role = "interaction-targeted",
                    Floor = 2,
                    IsUserSet = true,
                },
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(
            workingList,
            comboDataAvailable: false,
            categoryDataAvailable: false));
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService());

        CutLabUiPatchDto patch = await builder.BuildAsync(
            state,
            state.Intent.PlayExperience,
            ["Commander"]);

        Assert.False(patch.ComboDataAvailable);
        Assert.False(patch.CategoryDataAvailable);
        CutLabDecideFindingGroupDto weakFloorGroup = Assert.Single(
            patch.StructuralFindings,
            group => group.Kind == CutLabFindingKind.WeakFloorCase);
        Assert.Equal("Weak floor cases", weakFloorGroup.Heading);
        Assert.Collection(
            weakFloorGroup.Items,
            item =>
            {
                Assert.Equal(CutLabFindingKind.WeakFloorCase, item.Kind);
                Assert.Equal("Ramp is at 1 against a floor of 1 — every card in this role is effectively protected already.", item.Lead);
                Assert.Equal(["Arcane Signet"], item.Evidence);
            },
            item =>
            {
                Assert.Equal(CutLabFindingKind.WeakFloorCase, item.Kind);
                Assert.Equal("Targeted removal is at 1 against a floor of 2 — every card in this role is effectively protected already.", item.Lead);
                Assert.Equal(["Counterspell"], item.Evidence);
            });
    }

    [Fact]
    public async Task BuildAsync_RoundTripsCompleteComboBadgesAndComboProtectedFindings()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Heliod, Sun-Crowned", quantity: 1),
                Card("Walking Ballista", quantity: 1),
                Card("Basic Filler", quantity: 97, isLocked: true),
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList));
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService());

        CutLabUiPatchDto patch = await builder.BuildAsync(
            state,
            state.Intent.PlayExperience,
            ["Commander"]);

        CutLabDecideFindingGroupDto comboGroup = Assert.Single(
            patch.StructuralFindings,
            group => group.Kind == CutLabFindingKind.ComboProtected);
        Assert.Equal("Combo-protected cards", comboGroup.Heading);
        CutLabDecideFindingDto comboFinding = Assert.Single(comboGroup.Items);
        Assert.Equal(["Heliod, Sun-Crowned · MV 3", "Walking Ballista · MV 4"], comboFinding.Evidence);
        Assert.Equal(ComboBadgeState.CompletePiece, patch.ComboBadgeByCardName[CutLabCardNames.Normalize("Heliod, Sun-Crowned")].BadgeState);
        Assert.Equal("Infinite damage", patch.ComboBadgeByCardName[CutLabCardNames.Normalize("Heliod, Sun-Crowned")].Context);
        Assert.Equal(ComboBadgeState.CompletePiece, patch.ComboBadgeByCardName[CutLabCardNames.Normalize("Walking Ballista")].BadgeState);
    }

    /// <summary>
    /// Why: proposal names come from the deck while combo evidence comes from
    /// Spellbook, which can use a different apostrophe character for one card.
    /// </summary>
    [Fact]
    public void BuildNextProposal_CurlyApostropheCardName_StillCarriesItsFindingChips()
    {
        CutLabDecideNextProposalDto proposal = BuildNextProposalForTest("Lim-Dul’s Vault", "Lim-Dul's Vault");

        Assert.Contains("Combo-protected cards", proposal.FindingChips);
        Assert.DoesNotContain("Unrelated finding", proposal.FindingChips);
    }

    /// <summary>
    /// Why: a deck records double-faced cards by both faces while Spellbook
    /// supplies the front face, so the proposal chip needs the shared name form.
    /// </summary>
    [Fact]
    public void BuildNextProposal_DoubleFacedCardName_StillCarriesItsFindingChips()
    {
        CutLabDecideNextProposalDto proposal = BuildNextProposalForTest("Malakir Rebirth // Malakir Mire", "Malakir Rebirth");

        Assert.Contains("Combo-protected cards", proposal.FindingChips);
        Assert.DoesNotContain("Unrelated finding", proposal.FindingChips);
    }

    [Fact]
    public async Task BuildAsync_RoundTripsNearComboNeedsPartnerBadge()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Demonic Consultation", quantity: 1),
                Card("Tainted Pact", quantity: 1),
                Card("Basic Filler", quantity: 97, isLocked: true),
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList));
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService());

        CutLabUiPatchDto patch = await builder.BuildAsync(
            state,
            state.Intent.PlayExperience,
            ["Commander"]);

        Assert.Equal(ComboBadgeState.NeedsPartner, patch.ComboBadgeByCardName[CutLabCardNames.Normalize("Demonic Consultation")].BadgeState);
        Assert.Equal("Needs Thassa's Oracle", patch.ComboBadgeByCardName[CutLabCardNames.Normalize("Demonic Consultation")].Context);
        Assert.Equal(ComboBadgeState.NeedsPartner, patch.ComboBadgeByCardName[CutLabCardNames.Normalize("Tainted Pact")].BadgeState);
    }

    [Fact]
    public async Task BuildAsync_LockedOvershootSetsNothingToCutAndReturnsAdvisory()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Wincon Sorcery", quantity: 1, isLocked: true, typeLine: "Sorcery"),
                Card("Payoff Creature", quantity: 1, isLocked: true, typeLine: "Creature"),
                Card("Basic Filler", quantity: 100, isLocked: true, typeLine: "Artifact"),
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList));
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService());

        CutLabUiPatchDto patch = await builder.BuildAsync(
            state,
            state.Intent.PlayExperience,
            ["Commander"]);

        Assert.True(patch.NextProposal.IsTerminal);
        Assert.False(patch.NextProposal.IsAtTarget);
        Assert.True(patch.NextProposal.IsNothingToCut);
        Assert.NotNull(patch.LockedOvershootAdvisory);
        Assert.NotEmpty(patch.LockedOvershootAdvisory!.Groups);
    }

    [Fact]
    public async Task BuildAsync_ReturnsLockedOvershootAdvisoryAlongsideNextProposal()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Locked Stack", quantity: 105, isLocked: true, typeLine: "Artifact"),
                Card("Immediate Proposal", quantity: 1, typeLine: "Creature"),
                Card("Later Proposal", quantity: 1, typeLine: "Sorcery"),
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList));
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService());

        CutLabUiPatchDto patch = await builder.BuildAsync(
            state,
            state.Intent.PlayExperience,
            ["Commander"]);

        Assert.False(patch.NextProposal.IsTerminal);
        Assert.Equal("Immediate Proposal", patch.NextProposal.CardName);
        Assert.NotNull(patch.LockedOvershootAdvisory);
        // Why: TargetDeckSize (100) counts the commander as one of the 100 slots, so a locked
        // commander plus 105 other locked cards is 106 locked total, 6 over target - not 5.
        Assert.Equal(6, patch.LockedOvershootAdvisory!.CardsOverTarget);
    }

    [Fact]
    public async Task BuildAsync_LockedOvershootAdvisory_GroupsFollowHeadroomOrder()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Persistent Petitioners", quantity: 99, isLocked: true, typeLine: "Creature"),
                Card("Deferred Card", quantity: 1, isLocked: true, typeLine: "Creature"),
                Card("Wincon Sorcery", quantity: 1, isLocked: true, typeLine: "Sorcery"),
            ],
            roleFloors:
            [
                new CutLabRoleFloor
                {
                    Role = "draw",
                    Floor = 10,
                    IsUserSet = true,
                },
                new CutLabRoleFloor
                {
                    Role = "payoffs",
                    Floor = 0,
                    IsUserSet = true,
                },
                new CutLabRoleFloor
                {
                    Role = "wincons",
                    Floor = 1,
                    IsUserSet = true,
                },
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList));
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService());

        CutLabUiPatchDto patch = await builder.BuildAsync(
            state,
            state.Intent.PlayExperience,
            ["Commander"]);

        CutLabLockedOvershootAdvisoryDto advisory = Assert.IsType<CutLabLockedOvershootAdvisoryDto>(patch.LockedOvershootAdvisory);
        Assert.Collection(
            advisory.Groups,
            group => Assert.Equal(["Persistent Petitioners"], group.CardNames),
            group => Assert.Equal(["Deferred Card"], group.CardNames),
            group => Assert.Equal(["Wincon Sorcery"], group.CardNames));
    }

    [Fact]
    public async Task BuildAsync_MatchesCutLabViewModelForNoJsParityFields()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Arcane Signet", quantity: 1, typeLine: "Artifact"),
                Card("Persistent Petitioners", quantity: 1, typeLine: "Creature"),
                Card("Basic Filler", quantity: 98, isLocked: true, typeLine: "Artifact"),
            ],
            roleFloors:
            [
                new CutLabRoleFloor
                {
                    Role = "ramp",
                    Floor = 1,
                    IsUserSet = true,
                },
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList));
        FakeSimulationService simulationService = new();
        CutLabUiPatchBuilder builder = new(contextBuilder, simulationService);
        IReadOnlyDictionary<string, int> floorByRole = BuildFloorMap(state.RoleFloors);

        CutLabUiPatchDto patch = await builder.BuildAsync(
            state,
            state.Intent.PlayExperience,
            ["Commander"]);
        CutLabViewModel viewModel = BuildViewModel(state, floorByRole, contextBuilder, simulationService);

        Assert.Equal(viewModel.CurrentCount, patch.CurrentCount);
        Assert.Equal(viewModel.CurrentCount == 100, patch.CanBuildExport);
        Assert.Equal(viewModel.Proposal.FloorWarnings, patch.FloorWarnings.Select(warning => warning.Message).ToArray());
        Assert.Equal(viewModel.CutsMade.Count, patch.CutsMade.Count);
        Assert.Equal(viewModel.WhatifCardOutOptions, patch.WhatifCardOutOptions);
        Assert.Equal(viewModel.WhatifCardInOptions, patch.WhatifCardInOptions);
        Assert.Equal(viewModel.AddableBasics, patch.AddableBasics);

        Assert.Collection(
            patch.QuantityTuners,
            row =>
            {
                CutLabTunableRowView viewRow = Assert.Single(viewModel.WorkingListRows, candidate => candidate.Name == row.CardName);
                Assert.Equal("Commander", row.CardName);
                Assert.Equal(viewRow.RoleLabel, row.RoleLabel);
                Assert.Equal(viewRow.CurrentQuantity, row.CurrentQuantity);
                Assert.Equal(viewRow.LegalMax, row.LegalMax);
                Assert.Equal(viewRow.IsLegalMultiple, row.IsLegalMultiple);
                Assert.Equal(viewRow.IsAddedBasic, row.IsAddedBasic);
                Assert.True(row.IsLockedOrCommander);
                Assert.True(row.AddDisabled);
                Assert.True(row.RemoveDisabled);
            },
            row =>
            {
                CutLabTunableRowView viewRow = Assert.Single(viewModel.WorkingListRows, candidate => candidate.Name == row.CardName);
                Assert.Equal("Arcane Signet", row.CardName);
                Assert.Equal(viewRow.RoleLabel, row.RoleLabel);
                Assert.Equal(viewRow.CurrentQuantity, row.CurrentQuantity);
                Assert.Equal(viewRow.LegalMax, row.LegalMax);
                Assert.Equal(viewRow.IsLegalMultiple, row.IsLegalMultiple);
                Assert.False(row.IsLockedOrCommander);
                Assert.True(row.AddDisabled);
                Assert.False(row.RemoveDisabled);
            },
            row =>
            {
                CutLabTunableRowView viewRow = Assert.Single(viewModel.WorkingListRows, candidate => candidate.Name == row.CardName);
                Assert.Equal("Persistent Petitioners", row.CardName);
                Assert.Equal(viewRow.RoleLabel, row.RoleLabel);
                Assert.Equal(viewRow.CurrentQuantity, row.CurrentQuantity);
                Assert.Equal(viewRow.LegalMax, row.LegalMax);
                Assert.Equal(viewRow.IsLegalMultiple, row.IsLegalMultiple);
                Assert.False(row.IsLockedOrCommander);
                Assert.False(row.AddDisabled);
                Assert.False(row.RemoveDisabled);
            },
            row =>
            {
                CutLabTunableRowView viewRow = Assert.Single(viewModel.WorkingListRows, candidate => candidate.Name == row.CardName);
                Assert.Equal("Basic Filler", row.CardName);
                Assert.Equal(viewRow.RoleLabel, row.RoleLabel);
                Assert.Equal(viewRow.CurrentQuantity, row.CurrentQuantity);
                Assert.Equal(viewRow.LegalMax, row.LegalMax);
                Assert.Equal(viewRow.IsLegalMultiple, row.IsLegalMultiple);
                Assert.True(row.IsLockedOrCommander);
                Assert.True(row.AddDisabled);
                Assert.True(row.RemoveDisabled);
            });
    }

    [Fact]
    public async Task BuildAsync_ReconcilesTunerRowsAndAddableBasics_WhenBasicAddedOrRemoved()
    {
        CutLabState addedState = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Arcane Signet", quantity: 1, typeLine: "Artifact"),
                Card("Basic Filler", quantity: 98, isLocked: true, typeLine: "Artifact"),
            ],
            quantityAdjustments:
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Forest",
                    Delta = 1,
                    IsAddedBasic = true,
                },
            ]);
        CutLabState removedState = addedState with
        {
            QuantityAdjustments =
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Forest",
                    Delta = 1,
                    IsAddedBasic = true,
                },
                new CutLabQuantityAdjustment
                {
                    Name = "Forest",
                    Delta = -1,
                    IsAddedBasic = true,
                },
            ],
        };
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList));
        FakeSimulationService simulationService = new();
        CutLabUiPatchBuilder builder = new(contextBuilder, simulationService);

        CutLabUiPatchDto addedPatch = await builder.BuildAsync(
            addedState,
            addedState.Intent.PlayExperience,
            ["Commander"]);
        CutLabViewModel addedViewModel = BuildViewModel(addedState, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), contextBuilder, simulationService);

        Assert.Contains(addedPatch.QuantityTuners, row => row.CardName == "Forest" && row.IsAddedBasic);
        Assert.DoesNotContain("Forest", addedPatch.AddableBasics);
        Assert.Equal(addedViewModel.AddableBasics, addedPatch.AddableBasics);
        Assert.Contains(addedViewModel.WorkingListRows, row => row.Name == "Forest" && row.IsAddedBasic);

        CutLabUiPatchDto removedPatch = await builder.BuildAsync(
            removedState,
            removedState.Intent.PlayExperience,
            ["Commander"]);
        CutLabViewModel removedViewModel = BuildViewModel(removedState, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), contextBuilder, simulationService);

        Assert.DoesNotContain(removedPatch.QuantityTuners, row => row.CardName == "Forest");
        Assert.Contains("Forest", removedPatch.AddableBasics);
        Assert.Equal(removedViewModel.AddableBasics, removedPatch.AddableBasics);
        Assert.DoesNotContain(removedViewModel.WorkingListRows, row => row.Name == "Forest");
    }

    [Fact]
    public void BuildAdjustPatch_ReturnsLightAdjustProjectionWithoutAnalysisOrSimulation()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Arcane Signet", quantity: 1, typeLine: "Artifact"),
                Card("Cut Card", quantity: 1, typeLine: "Artifact"),
                Card("Basic Filler", quantity: 99, isLocked: true, typeLine: "Artifact"),
            ],
            decisions:
            [
                new CutLabDecision
                {
                    CardName = "Cut Card",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = CutLabCutRoundEngine.Round1Key,
                    Ordinal = 1,
                },
            ],
            quantityAdjustments:
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Arcane Signet",
                    Delta = 1,
                },
                new CutLabQuantityAdjustment
                {
                    Name = "Forest",
                    Delta = 1,
                    IsAddedBasic = true,
                },
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList))
        {
            ThrowOnBuild = true,
        };
        FakeSimulationService simulationService = new()
        {
            ThrowOnComputeProposalDeltas = true,
        };
        CutLabUiPatchBuilder builder = new(contextBuilder, simulationService);

        CutLabUiPatchDto patch = builder.BuildAdjustPatch(state, ["Commander"]);

        Assert.Equal(CutLabStateSerializer.Serialize(state), patch.CutLabStateJson);
        Assert.Equal(102, patch.CurrentCount);
        Assert.Equal(2, patch.CardsRemaining);
        Assert.False(patch.CanBuildExport);
        Assert.Null(patch.NextProposal);
        Assert.Null(patch.ProposalDeltas);
        Assert.Empty(patch.StructuralFindings);
        Assert.Empty(patch.FloorWarnings);
        Assert.Collection(
            patch.CutsMade,
            cut =>
            {
                Assert.Equal("Cut Card", cut.CardName);
                Assert.Equal(CutLabCutRoundEngine.Round1Key, cut.RoundKey);
                Assert.Equal(CutLabCutRoundEngine.Round1Label, cut.RoundLabel);
                Assert.Equal(1, cut.Ordinal);
            });
        Assert.False(patch.ComboDataAvailable);
        Assert.False(patch.CategoryDataAvailable);
        Assert.Equal(["Arcane Signet", "Forest"], patch.WhatifCardOutOptions);
        Assert.Equal(["Cut Card"], patch.WhatifCardInOptions);
        Assert.Equal(
            CutLabBasicLands.Names.Where(name => !string.Equals(name, "Forest", StringComparison.OrdinalIgnoreCase)).ToArray(),
            patch.AddableBasics);
        Assert.Contains(patch.QuantityTuners, row => row.CardName == "Arcane Signet" && row.CurrentQuantity == 1 && row.AddDisabled);
        Assert.Contains(patch.QuantityTuners, row => row.CardName == "Forest" && row.CurrentQuantity == 1 && row.IsAddedBasic && row.RoleLabel == "Lands");
        Assert.Equal(0, contextBuilder.BuildCalls);
        Assert.Equal(0, simulationService.ComputeProposalDeltasCalls);
    }

    [Fact]
    public void BuildAdjustPatch_SerializesPopupCardTextPatchWithoutExplicitNullRichFields()
    {
        CutLabCardTextView view = new()
        {
            Cmc = 4,
            CastPercent = 92.1,
        };

        string json = JsonSerializer.Serialize(view, CreateMvcJsonSerializerOptions());
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.False(root.TryGetProperty("typeLine", out _));
        Assert.False(root.TryGetProperty("manaCost", out _));
        Assert.False(root.TryGetProperty("setCode", out _));
        Assert.False(root.TryGetProperty("collectorNumber", out _));
        Assert.False(root.TryGetProperty("oracleText", out _));
        Assert.False(root.TryGetProperty("power", out _));
        Assert.False(root.TryGetProperty("toughness", out _));
        Assert.Equal(4, root.GetProperty("cmc").GetInt32());
        Assert.Equal(92.1, root.GetProperty("castPercent").GetDouble());
    }

    [Fact]
    public void BuildAdjustPatch_LeavesComboBadgeMapEmpty()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true),
                Card("Heliod, Sun-Crowned", quantity: 1),
                Card("Walking Ballista", quantity: 1),
                Card("Basic Filler", quantity: 97, isLocked: true),
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList))
        {
            ThrowOnBuild = true,
        };
        CutLabUiPatchBuilder builder = new(contextBuilder, new FakeSimulationService());

        CutLabUiPatchDto patch = builder.BuildAdjustPatch(state, ["Commander"]);

        Assert.Empty(patch.StructuralFindings);
        Assert.Empty(patch.ComboBadgeByCardName);
        Assert.False(patch.ComboDataAvailable);
    }

    [Fact]
    public void BuildAdjustPatch_ReturnsTerminalAtTargetProposalWhenAdjustedWorkingListHitsOneHundred()
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Forest", quantity: 1, typeLine: "Basic Land — Forest"),
                Card("Basic Filler", quantity: 97, isLocked: true, typeLine: "Artifact"),
            ],
            quantityAdjustments:
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Forest",
                    Delta = 1,
                },
            ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList))
        {
            ThrowOnBuild = true,
        };
        FakeSimulationService simulationService = new()
        {
            ThrowOnComputeProposalDeltas = true,
        };
        CutLabUiPatchBuilder builder = new(contextBuilder, simulationService);

        CutLabUiPatchDto patch = builder.BuildAdjustPatch(state, ["Commander"]);

        Assert.Equal(100, patch.CurrentCount);
        Assert.Equal(0, patch.CardsRemaining);
        Assert.True(patch.CanBuildExport);
        Assert.NotNull(patch.NextProposal);
        Assert.True(patch.NextProposal.IsTerminal);
        Assert.True(patch.NextProposal.IsAtTarget);
        Assert.False(patch.NextProposal.IsNothingToCut);
        Assert.Equal(0, contextBuilder.BuildCalls);
        Assert.Equal(0, simulationService.ComputeProposalDeltasCalls);
    }

    [Theory]
    [InlineData(97, 0, 99, false)]
    [InlineData(98, 1, 101, false)]
    public void BuildAdjustPatch_DoesNotReturnTerminalProposalWhenAdjustedWorkingListIsNotAtTarget(
        int fillerQuantity,
        int signetDelta,
        int expectedCount,
        bool expectedCanBuildExport)
    {
        CutLabState state = CreateState(
            pool:
            [
                Card("Commander", quantity: 1, isCommander: true, isLocked: true, typeLine: "Legendary Creature"),
                Card("Forest", quantity: 1, typeLine: "Basic Land — Forest"),
                Card("Basic Filler", quantity: fillerQuantity, isLocked: true, typeLine: "Artifact"),
            ],
            quantityAdjustments: signetDelta == 0
                ? []
                :
                [
                    new CutLabQuantityAdjustment
                    {
                        Name = "Forest",
                        Delta = signetDelta,
                    },
                ]);
        FakeAnalysisContextBuilder contextBuilder = new(workingList => CreateAnalysisContext(workingList))
        {
            ThrowOnBuild = true,
        };
        FakeSimulationService simulationService = new()
        {
            ThrowOnComputeProposalDeltas = true,
        };
        CutLabUiPatchBuilder builder = new(contextBuilder, simulationService);

        CutLabUiPatchDto patch = builder.BuildAdjustPatch(state, ["Commander"]);

        Assert.Equal(expectedCount, patch.CurrentCount);
        Assert.Equal(expectedCanBuildExport, patch.CanBuildExport);
        Assert.Null(patch.NextProposal);
        Assert.Equal(0, contextBuilder.BuildCalls);
        Assert.Equal(0, simulationService.ComputeProposalDeltasCalls);
    }

    [Fact]
    public void AddDeckFlowCutLabServices_RegistersCutLabUiPatchBuilder()
    {
        ServiceCollection services = new();

        services.AddDeckFlowCutLabServices();

        ServiceDescriptor descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(ICutLabUiPatchBuilder));
        Assert.Equal(typeof(CutLabUiPatchBuilder), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    private static CutLabViewModel BuildViewModel(
        CutLabState state,
        IReadOnlyDictionary<string, int> floorByRole,
        FakeAnalysisContextBuilder contextBuilder,
        FakeSimulationService simulationService)
    {
        IReadOnlyList<CutLabPoolCard> workingList = CutLabWorkingList.Derive(state.Pool, state.Decisions, state.QuantityAdjustments);
        CutLabAnalysisContext context = contextBuilder.BuildAsync(
            workingList,
            state.Intent.PlayExperience,
            ["Commander"]).GetAwaiter().GetResult();
        (CutLabStructuralFindingsResult findings, CutLabRoundPlan roundPlan) = CutLabCutRoundEngine.BuildFindingsAndRoundPlan(
            workingList,
            context,
            floorByRole,
            state.Decisions);
        CutLabProposalDeltas? deltas = roundPlan.NextProposal is null
            ? null
            : simulationService.ComputeProposalDeltas(
                workingList,
                roundPlan.NextProposal.CardName,
                state.Intent.PlayExperience,
                goals: state.Goals).GetAwaiter().GetResult();
        CutLabProcessResult result = new()
        {
            State = state,
            SerializedStateJson = CutLabStateSerializer.Serialize(state),
            HasResult = true,
            RoleAssignmentsByCardName = context.RolesByCardName,
            ResolvedFloors = BuildResolvedFloors(floorByRole),
            Findings = findings,
            RoundPlan = roundPlan,
            InitialProposalDeltas = deltas,
        };
        CutLabRequest request = new()
        {
            CutLabStateJson = result.SerializedStateJson,
            PlayExperience = state.Intent.PlayExperience,
        };

        return CutLabViewModel.From(request, result);
    }

    private static IReadOnlyList<CutLabResolvedFloor> BuildResolvedFloors(IReadOnlyDictionary<string, int> floorByRole)
        => CutLabFloorRules.RoleKeys
            .Select(role => new CutLabResolvedFloor
            {
                Role = role,
                Floor = floorByRole.TryGetValue(role, out int floor) ? floor : 0,
                DefaultValue = floorByRole.TryGetValue(role, out floor) ? floor : 0,
                IsUserSet = floorByRole.ContainsKey(role),
                ResolvedBracket = 3,
                BracketWasFallback = false,
            })
            .ToArray();

    private static IReadOnlyDictionary<string, int> BuildFloorMap(IReadOnlyList<CutLabRoleFloor> roleFloors)
    {
        Dictionary<string, int> floors = new(StringComparer.OrdinalIgnoreCase);
        foreach (CutLabRoleFloor floor in roleFloors)
        {
            if (!string.IsNullOrWhiteSpace(floor.Role))
            {
                floors[floor.Role] = floor.Floor;
            }
        }

        return floors;
    }

    private static JsonSerializerOptions CreateMvcJsonSerializerOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static CutLabDecideNextProposalDto BuildNextProposalForTest(string proposalCardName, string evidenceCardName)
    {
        MethodInfo method = typeof(CutLabUiPatchBuilder).GetMethod("BuildNextProposal", BindingFlags.NonPublic | BindingFlags.Static)!;
        CutLabRoundQueueItem nextProposal = new(proposalCardName, CutLabCutRoundEngine.Round3Key, CutLabCutRoundEngine.Round3Label, 0, []);
        CutLabRoundPlan roundPlan = new() { Queue = [nextProposal], NextProposal = nextProposal, CardsRemainingToTarget = 1 };
        CutLabStructuralFindingsResult findings = new(
            [
                new CutLabFinding(CutLabFindingKind.ComboProtected, "Combo-protected cards", "matched", [new CutLabFindingEvidence(evidenceCardName, null)]),
                new CutLabFinding(CutLabFindingKind.CurveCongestion, "Unrelated finding", "unmatched", [new CutLabFindingEvidence("Genuinely Different Card", null)]),
            ],
            true,
            true);

        return Assert.IsType<CutLabDecideNextProposalDto>(method.Invoke(null, [roundPlan, findings]));
    }

    private static CutLabState CreateState(
        IReadOnlyList<CutLabPoolCard>? pool = null,
        IReadOnlyList<CutLabDecision>? decisions = null,
        IReadOnlyList<CutLabRoleFloor>? roleFloors = null,
        IReadOnlyList<CutLabQuantityAdjustment>? quantityAdjustments = null)
        => new()
        {
            Commander = "Commander",
            Pool = pool ?? [Card("Commander", quantity: 1, isCommander: true, isLocked: true), Card("Arcane Signet", quantity: 1), Card("Counterspell", quantity: 1), Card("Basic Filler", quantity: 99, isLocked: true)],
            Decisions = decisions ?? [],
            RoleFloors = roleFloors ?? [],
            QuantityAdjustments = quantityAdjustments ?? [],
            Intent = new CutLabIntent
            {
                PlayExperience = "Focused",
                Bracket = 3,
            },
        };

    private static CutLabPoolCard Card(
        string name,
        int quantity = 1,
        bool isCommander = false,
        bool isLocked = false,
        string? typeLine = null)
        => new()
        {
            Name = name,
            Quantity = quantity,
            TypeLine = typeLine ?? (isCommander ? "Legendary Creature" : "Spell"),
            IsCommander = isCommander,
            IsLocked = isLocked,
        };

    private static CutLabAnalysisContext CreateAnalysisContext(
        IReadOnlyList<CutLabPoolCard>? workingList = null,
        bool comboDataAvailable = true,
        bool categoryDataAvailable = true)
    {
        IReadOnlyList<CutLabPoolCard> cards = workingList ?? [Card("Commander", quantity: 1, isCommander: true, isLocked: true), Card("Counterspell", quantity: 1), Card("Basic Filler", quantity: 99, isLocked: true)];
        List<CutLabAnalyzedCard> analyzedCards = [];
        Dictionary<string, IReadOnlyList<string>> rolesByCardName = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> roleCounts = new(StringComparer.OrdinalIgnoreCase);

        foreach (CutLabPoolCard card in cards)
        {
            IReadOnlyList<string> roles = card.Name switch
            {
                "Arcane Signet" => ["ramp"],
                "Counterspell" => ["interaction-targeted"],
                "Persistent Petitioners" => ["draw"],
                "Wincon Sorcery" => ["wincons"],
                "Forest" => ["lands"],
                "Round 1 Card" => ["engines"],
                "Round 2 Card" => ["draw"],
                "Deferred Card" => ["payoffs"],
                _ => [],
            };

            rolesByCardName[card.Name] = roles;
            foreach (string role in roles)
            {
                roleCounts[role] = roleCounts.TryGetValue(role, out int count) ? count + card.Quantity : card.Quantity;
            }

            double manaValue = card.Name switch
            {
                "Arcane Signet" => 2,
                "Counterspell" => 2,
                "Persistent Petitioners" => 2,
                "Wincon Sorcery" => 4,
                "Heliod, Sun-Crowned" => 3,
                "Walking Ballista" => 4,
                "Demonic Consultation" => 1,
                "Tainted Pact" => 2,
                "Round 1 Card" => 1,
                "Helper Card" => 4,
                "Round 2 Card" => 3,
                "Support Card" => 4,
                "Deferred Card" => 5,
                _ => 1,
            };
            bool isLand = card.TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

            analyzedCards.Add(new CutLabAnalyzedCard(card.Name, manaValue, isLand, roles, [])
            {
                Quantity = card.Quantity,
            });
        }

        IReadOnlyList<SpellbookCombo> completeCombos = [];
        IReadOnlyList<SpellbookAlmostCombo> almostCombos = [];
        if (cards.Any(card => card.Name == "Round 1 Card") || cards.Any(card => card.Name == "Round 2 Card"))
        {
            almostCombos =
            [
                new SpellbookAlmostCombo("Missing Piece A", ["Round 1 Card", "Helper Card"], ["Win"], "Assemble both."),
                new SpellbookAlmostCombo("Missing Piece B", ["Round 2 Card", "Support Card"], ["Value"], "Assemble both."),
            ];
        }
        else if (cards.Any(card => card.Name == "Heliod, Sun-Crowned") && cards.Any(card => card.Name == "Walking Ballista"))
        {
            completeCombos =
            [
                new SpellbookCombo(["Heliod, Sun-Crowned", "Walking Ballista"], ["Infinite damage"], "Remove a counter to loop."),
            ];
        }
        else if (cards.Any(card => card.Name == "Demonic Consultation") && cards.Any(card => card.Name == "Tainted Pact"))
        {
            almostCombos =
            [
                new SpellbookAlmostCombo("Thassa's Oracle", ["Demonic Consultation", "Tainted Pact"], ["Win the game"], "Cast both."),
            ];
        }

        Dictionary<string, CutLabCardComboMembership> cardComboMembership = new(CutLabCardNames.Comparer);
        foreach (SpellbookCombo combo in completeCombos)
        {
            foreach (string cardName in combo.CardNames)
            {
                string normalizedCardName = CutLabCardNames.Normalize(cardName);
                if (!cardComboMembership.TryGetValue(normalizedCardName, out CutLabCardComboMembership? membership))
                {
                    membership = new CutLabCardComboMembership([], []);
                }

                cardComboMembership[normalizedCardName] = membership with
                {
                    CompleteCombos = membership.CompleteCombos.Concat([combo]).ToArray(),
                };
            }
        }

        foreach (SpellbookAlmostCombo combo in almostCombos)
        {
            foreach (string cardName in combo.CardsInDeck)
            {
                string normalizedCardName = CutLabCardNames.Normalize(cardName);
                if (!cardComboMembership.TryGetValue(normalizedCardName, out CutLabCardComboMembership? membership))
                {
                    membership = new CutLabCardComboMembership([], []);
                }

                cardComboMembership[normalizedCardName] = membership with
                {
                    NearCombos = membership.NearCombos.Concat([combo]).ToArray(),
                };
            }
        }

        return new CutLabAnalysisContext(
            analyzedCards,
            rolesByCardName,
            roleCounts,
            3,
            ManabaseMode.Focused,
            new CutLabClassificationContext(
                almostCombos,
                comboDataAvailable,
                categoryDataAvailable,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                cardComboMembership),
            cards
                .Select(card => new ScryfallCardData
                {
                    Name = card.Name,
                    TypeLine = card.TypeLine,
                    Cmc = card.Name switch
                    {
                        "Arcane Signet" => 2,
                        "Counterspell" => 2,
                        "Persistent Petitioners" => 2,
                        "Heliod, Sun-Crowned" => 3,
                        "Walking Ballista" => 4,
                        "Demonic Consultation" => 1,
                        "Tainted Pact" => 2,
                        "Round 1 Card" => 1,
                        "Helper Card" => 4,
                        "Round 2 Card" => 3,
                        "Support Card" => 4,
                        "Deferred Card" => 5,
                        _ => 1,
                    },
                })
                .ToArray());
    }

    private sealed class FakeAnalysisContextBuilder(Func<IReadOnlyList<CutLabPoolCard>, CutLabAnalysisContext> factory) : ICutLabAnalysisContextBuilder
    {
        public int BuildCalls { get; private set; }

        public bool ThrowOnBuild { get; init; }

        public Task<CutLabAnalysisContext> BuildAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            string playExperience,
            IReadOnlyList<string> commanderNames,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            CancellationToken cancellationToken = default)
        {
            BuildCalls++;
            if (ThrowOnBuild)
            {
                throw new Xunit.Sdk.XunitException("Analysis context builder should not run.");
            }

            return Task.FromResult(factory(workingList));
        }

        public bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards)
        {
            cards = null;
            return false;
        }

        public Task<IReadOnlyList<ScryfallCardData>> ResolvePoolCardsAsync(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
            string? poolKey = null,
            bool failOpenOnLookupErrors = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult(factory(workingList).ResolvedCards);

        public void PrimeResolvedCardsCache(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData> resolvedCards,
            IReadOnlyCollection<string>? unresolvedCardNames = null)
        {
        }

        public bool TrySeedDerivedPool(
            IReadOnlyList<CutLabPoolCard> workingList,
            IReadOnlyList<ScryfallCardData> sourceCards,
            out IReadOnlyList<ScryfallCardData>? seededCards)
        {
            seededCards = workingList
                .Select(card => sourceCards.FirstOrDefault(source => string.Equals(source.Name, card.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(card => card is not null)
                .Cast<ScryfallCardData>()
                .ToArray();
            return seededCards.Count == workingList.Count;
        }
    }

    private sealed class FakeSimulationService : ICutLabSimulationService
    {
        public int ComputeProposalDeltasCalls { get; private set; }

        public bool ThrowOnComputeProposalDeltas { get; init; }

        public Task<CutLabSimulationResult> BuildSnapshotResult(
            IReadOnlyList<CutLabPoolCard> workingList,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabSimulationResult());

        public Task<CutLabMetricSnapshot> BuildSnapshot(
            IReadOnlyList<CutLabPoolCard> workingList,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CutLabMetricSnapshot());

        public Task<CutLabProposalDeltas> ComputeProposalDeltas(
            IReadOnlyList<CutLabPoolCard> currentWorkingList,
            string candidateCardName,
            string? playExperience,
            int? trialsOverride = ICutLabSimulationService.InLoopTrials,
            string? poolKey = null,
            CutLabGoalSettings? goals = null,
            CancellationToken cancellationToken = default)
        {
            ComputeProposalDeltasCalls++;
            if (ThrowOnComputeProposalDeltas)
            {
                throw new Xunit.Sdk.XunitException("Simulation service should not run.");
            }

            return Task.FromResult(new CutLabProposalDeltas
            {
                CardName = candidateCardName,
                ChangedFamilyCount = 1,
                Deltas =
                [
                    new CutLabMetricDelta
                    {
                        Kind = CutLabMetricKind.CommanderByTurn,
                        Family = CutLabMetricFamily.CategoryByTurn,
                        Label = $"Commander by turn {goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn}",
                        Before = goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn,
                        After = (goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn) - 2,
                        Delta = -2,
                        Unit = CutLabMetricUnit.Percent,
                        Direction = CutLabMetricDirection.Down,
                        IsMeaningful = true,
                    },
                ],
            });
        }
    }
}
