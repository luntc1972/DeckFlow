using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabViewModelWordingTests
{
    [Theory]
    [InlineData("Command Tower", "Command Tower")]
    [InlineData("Command Tower · MV 0", "Command Tower")]
    [InlineData("command tower · MV 0", "Command Tower")]
    public void FindLockableEvidenceCard_MatchesSupportedCardLabels(string evidence, string expected)
    {
        var model = new CutLabViewModel
        {
            Pool =
            [
                new CutLabPoolCard { Name = "Commander", IsCommander = true, IsLocked = true },
                new CutLabPoolCard { Name = "Command Tower" },
            ],
        };

        CutLabPoolCard? match = model.FindLockableEvidenceCard(evidence);

        Assert.NotNull(match);
        Assert.Equal(expected, match.Name);
    }

    [Theory]
    [InlineData("2 cards above the floor")]
    [InlineData("Command")]
    [InlineData("Commander")]
    public void FindLockableEvidenceCard_LeavesNonCardAndCommanderEvidenceInert(string evidence)
    {
        var model = new CutLabViewModel
        {
            Pool =
            [
                new CutLabPoolCard { Name = "Commander", IsCommander = true, IsLocked = true },
                new CutLabPoolCard { Name = "Command Tower" },
            ],
        };

        Assert.Null(model.FindLockableEvidenceCard(evidence));
    }

    [Theory]
    [InlineData(0, "0 cards")]
    [InlineData(1, "1 card")]
    [InlineData(2, "2 cards")]
    public void FormatCutsMadeCount_ReturnsExpectedCardWording(int count, string expected)
    {
        Assert.Equal(expected, CutLabViewModel.FormatCutsMadeCount(count));
    }

    [Theory]
    [InlineData(0, "0 cuts so far")]
    [InlineData(1, "1 cut so far")]
    [InlineData(2, "2 cuts so far")]
    public void FormatCutsAcceptedSoFar_ReturnsExpectedCutWording(int count, string expected)
    {
        Assert.Equal(expected, CutLabViewModel.FormatCutsAcceptedSoFar(count));
    }

    [Fact]
    public void From_BuildsThreeGoalRowsInFixedOrderWithDynamicLabels()
    {
        var request = new CutLabRequest
        {
            PlayExperience = "Focused",
        };
        var result = new CutLabProcessResult
        {
            State = new CutLabState
            {
                Goals = new CutLabGoalSettings
                {
                    CommanderByTurn = 5,
                    EngineByTurn = 4,
                    RepresentativeLineByTurn = 7,
                },
                BaselineSnapshot = BuildSnapshot(42.1, 65.4, 51.6),
            },
            CurrentSnapshot = BuildSnapshot(57.4, 71.2, 62.8),
        };

        CutLabViewModel model = CutLabViewModel.From(request, result);

        Assert.Collection(
            model.GoalRows,
            row =>
            {
                Assert.Equal(CutLabMetricKind.CommanderByTurn, row.Kind);
                Assert.Equal("commander", row.GoalKey);
                Assert.Equal("GoalCommanderByTurn", row.FieldName);
                Assert.Equal("Commander by turn 5", row.Label);
                Assert.Equal(5, row.TurnValue);
            },
            row =>
            {
                Assert.Equal(CutLabMetricKind.EngineByTurn, row.Kind);
                Assert.Equal("engine", row.GoalKey);
                Assert.Equal("GoalEngineByTurn", row.FieldName);
                Assert.Equal("Engine by turn 4", row.Label);
                Assert.Equal(4, row.TurnValue);
            },
            row =>
            {
                Assert.Equal(CutLabMetricKind.RepresentativeLineByTurn, row.Kind);
                Assert.Equal("representative-line", row.GoalKey);
                Assert.Equal("GoalPlanByTurn", row.FieldName);
                Assert.Equal("Representative line by turn 7", row.Label);
                Assert.Equal(7, row.TurnValue);
            });
    }

    [Fact]
    public void From_FlagsRepresentativeLineGoalAsUncappedForCasualPlayOnly()
    {
        var casualRequest = new CutLabRequest
        {
            PlayExperience = "Casual",
        };
        var casualResult = new CutLabProcessResult
        {
            State = new CutLabState
            {
                Goals = new CutLabGoalSettings(),
                BaselineSnapshot = BuildSnapshot(40, 50, 60),
            },
            CurrentSnapshot = BuildSnapshot(41, 51, 61),
        };

        CutLabViewModel casualModel = CutLabViewModel.From(casualRequest, casualResult);

        Assert.False(casualModel.GoalRows[0].IsUncappedInCasual);
        Assert.False(casualModel.GoalRows[1].IsUncappedInCasual);
        Assert.True(casualModel.GoalRows[2].IsUncappedInCasual);

        var cedhRequest = new CutLabRequest
        {
            PlayExperience = "cEDH",
        };

        CutLabViewModel cedhModel = CutLabViewModel.From(cedhRequest, casualResult);

        Assert.All(cedhModel.GoalRows, row => Assert.False(row.IsUncappedInCasual));
    }

    [Fact]
    public void From_BuildsWorkingListRowsFromDerivedListAndFiltersAddableBasics()
    {
        var request = new CutLabRequest
        {
            PlayExperience = "Focused",
        };
        var result = new CutLabProcessResult
        {
            HasResult = true,
            State = new CutLabState
            {
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
                        Name = "Island",
                        Quantity = 3,
                        TypeLine = "Basic Land — Island",
                    },
                    new CutLabPoolCard
                    {
                        Name = "Relentless Rats",
                        Quantity = 2,
                        TypeLine = "Creature — Rat",
                    },
                    new CutLabPoolCard
                    {
                        Name = "Sol Ring",
                        Quantity = 1,
                        TypeLine = "Artifact",
                    },
                ],
                Decisions =
                [
                    new CutLabDecision
                    {
                        CardName = "Sol Ring",
                        Kind = CutLabDecisionKind.Accepted,
                        Round = "round-1",
                        Ordinal = 1,
                    },
                ],
                QuantityAdjustments =
                [
                    new CutLabQuantityAdjustment
                    {
                        Name = "Island",
                        Delta = 2,
                    },
                    new CutLabQuantityAdjustment
                    {
                        Name = "Forest",
                        Delta = 1,
                        IsAddedBasic = true,
                    },
                ],
            },
            RoleAssignmentsByCardName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Commander"] = [],
                ["Island"] = ["lands"],
                ["Relentless Rats"] = ["engines"],
                ["Forest"] = ["lands"],
                ["Sol Ring"] = ["ramp"],
            },
        };

        CutLabViewModel model = CutLabViewModel.From(request, result);

        CutLabTunableRowView island = Assert.Single(model.WorkingListRows, row => row.Name == "Island");
        Assert.Equal(5, island.CurrentQuantity);
        Assert.True(island.IsLegalMultiple);
        Assert.Equal(CutLabLegality.LegalMax("Island"), island.LegalMax);
        Assert.False(island.IsAddedBasic);

        CutLabTunableRowView forest = Assert.Single(model.WorkingListRows, row => row.Name == "Forest");
        Assert.Equal(1, forest.CurrentQuantity);
        Assert.True(forest.IsAddedBasic);
        Assert.Equal("Lands", forest.RoleLabel);

        CutLabTunableRowView rats = Assert.Single(model.WorkingListRows, row => row.Name == "Relentless Rats");
        Assert.Equal(2, rats.CurrentQuantity);
        Assert.True(rats.IsLegalMultiple);
        Assert.Equal(CutLabLegality.LegalMax("Relentless Rats"), rats.LegalMax);

        Assert.DoesNotContain(model.WorkingListRows, row => row.Name == "Sol Ring");
        Assert.DoesNotContain(model.AddableBasics, name => string.Equals(name, "Island", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(model.AddableBasics, name => string.Equals(name, "Forest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Wastes", model.AddableBasics);
        string[] addableBasics = model.AddableBasics.ToArray();
        Assert.True(Array.IndexOf(addableBasics, "Plains") < Array.IndexOf(addableBasics, "Wastes"));
    }

    [Fact]
    public void From_UsesDerivedWorkingListForCurrentCount()
    {
        var request = new CutLabRequest
        {
            PlayExperience = "Focused",
        };
        var result = new CutLabProcessResult
        {
            HasResult = true,
            State = new CutLabState
            {
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
                        Name = "Island",
                        Quantity = 38,
                        TypeLine = "Basic Land — Island",
                    },
                ],
                QuantityAdjustments =
                [
                    new CutLabQuantityAdjustment
                    {
                        Name = "Island",
                        Delta = 2,
                    },
                ],
            },
            RoleAssignmentsByCardName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Commander"] = [],
                ["Island"] = ["lands"],
            },
        };

        CutLabViewModel model = CutLabViewModel.From(request, result);

        Assert.Equal(41, model.CurrentCount);
        Assert.Equal(40, Assert.Single(model.WorkingListRows, row => row.Name == "Island").CurrentQuantity);
    }

    [Fact]
    public void From_BuildsPoolStatusTextFromCommanderInclusiveBaselineCount()
    {
        var request = new CutLabRequest
        {
            PlayExperience = "Focused",
        };
        var result = new CutLabProcessResult
        {
            HasResult = true,
            State = new CutLabState
            {
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
                        Name = "Island",
                        Quantity = 148,
                        TypeLine = "Basic Land — Island",
                    },
                    new CutLabPoolCard
                    {
                        Name = "Mana Crypt",
                        Quantity = 1,
                        TypeLine = "Artifact",
                        IsLocked = true,
                    },
                ],
            },
            RoleAssignmentsByCardName = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Commander"] = [],
                ["Island"] = ["lands"],
                ["Mana Crypt"] = ["ramp"],
            },
        };

        CutLabViewModel model = CutLabViewModel.From(request, result);

        Assert.Equal(150, model.BaselineCount);
        Assert.Equal($"{model.BaselineCount} cards in pool · 2 locked", model.PoolStatusText);
    }

    private static CutLabMetricSnapshot BuildSnapshot(double commander, double engine, double representativeLine)
        => new()
        {
            Metrics =
            [
                BuildMetric(CutLabMetricKind.CommanderByTurn, "Commander by turn", commander),
                BuildMetric(CutLabMetricKind.EngineByTurn, "Engine by turn", engine),
                BuildMetric(CutLabMetricKind.RepresentativeLineByTurn, "Representative line by turn", representativeLine),
            ],
        };

    private static CutLabMetricValue BuildMetric(CutLabMetricKind kind, string label, double value)
        => new()
        {
            Kind = kind,
            Family = CutLabMetricFamily.CategoryByTurn,
            Label = label,
            Value = value,
            Unit = CutLabMetricUnit.Percent,
        };
}
