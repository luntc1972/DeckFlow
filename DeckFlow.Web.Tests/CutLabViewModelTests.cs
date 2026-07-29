using DeckFlow.Web.Models;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for direct Cut Lab floor-row view-model projection.</summary>
public sealed class CutLabViewModelTests
{
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

    private static IReadOnlyList<CutLabFloorRowView> BuildRows(
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors,
        IReadOnlyDictionary<string, int>? countsByRole = null)
        => CutLabViewModel.BuildFloorRows(
            resolvedFloors,
            countsByRole ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            "Focused");

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
