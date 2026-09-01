using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Direct tests for <see cref="CutLabFindingPresenter"/> section merging and ordering.</summary>
public sealed class CutLabFindingPresenterTests
{
    [Fact]
    public void BuildFindingGroups_MultipleFunctionalTwins_MergeIntoOneSection()
    {
        IReadOnlyList<CutLabFindingView> findings =
        [
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "Three ramp artifacts at mana value 3."),
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "Four draw enchantments at mana value 2."),
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "Three interaction instants at mana value 1."),
        ];

        IReadOnlyList<CutLabFindingGroupView> groups = CutLabFindingPresenter.BuildFindingGroups(findings);

        CutLabFindingGroupView twinsGroup = Assert.Single(groups);
        Assert.Equal(CutLabFindingKind.FunctionalTwins, twinsGroup.Kind);
        Assert.Equal(3, twinsGroup.Items.Count);
    }

    [Fact]
    public void BuildFindingGroups_FunctionalTwinsSection_PreservesArrivalOrder()
    {
        IReadOnlyList<CutLabFindingView> findings =
        [
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "Mana value 5 group."),
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "Mana value 3 group."),
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "Mana value 1 group."),
        ];

        IReadOnlyList<CutLabFindingGroupView> groups = CutLabFindingPresenter.BuildFindingGroups(findings);

        CutLabFindingGroupView twinsGroup = Assert.Single(groups);
        Assert.Equal(CutLabFindingKind.FunctionalTwins, twinsGroup.Kind);
        Assert.Equal(
            new[] { "Mana value 5 group.", "Mana value 3 group.", "Mana value 1 group." },
            twinsGroup.Items.Select(item => item.Lead).ToArray());
    }

    [Fact]
    public void BuildFindingGroups_FunctionalTwinsSection_IsInsertedAtFirstOccurrenceIndex()
    {
        IReadOnlyList<CutLabFindingView> findings =
        [
            Finding(CutLabFindingKind.CurveCongestion, "Curve congestion", "First curve finding."),
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "First twins finding."),
            Finding(CutLabFindingKind.CurveCongestion, "Curve congestion", "Second curve finding."),
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "Second twins finding."),
        ];

        IReadOnlyList<CutLabFindingGroupView> groups = CutLabFindingPresenter.BuildFindingGroups(findings);

        Assert.Equal(3, groups.Count);
        Assert.Equal(
            new[] { CutLabFindingKind.CurveCongestion, CutLabFindingKind.FunctionalTwins, CutLabFindingKind.CurveCongestion },
            groups.Select(group => group.Kind).ToArray());
        Assert.Equal(2, groups[1].Items.Count);
    }

    [Fact]
    public void BuildFindingGroups_FunctionalTwinsHeading_ComesFromTheFirstItem()
    {
        IReadOnlyList<CutLabFindingView> findings =
        [
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "First twins finding."),
            Finding(CutLabFindingKind.FunctionalTwins, "ZZZ second", "Second twins finding."),
            Finding(CutLabFindingKind.FunctionalTwins, "ZZZ third", "Third twins finding."),
        ];

        IReadOnlyList<CutLabFindingGroupView> groups = CutLabFindingPresenter.BuildFindingGroups(findings);

        CutLabFindingGroupView twinsGroup = Assert.Single(groups);
        Assert.Equal("Functional twins", twinsGroup.Heading);
    }

    [Fact]
    public void BuildFindingGroups_AllThreeMergedKinds_AppearInFirstOccurrenceOrder()
    {
        IReadOnlyList<CutLabFindingView> findings =
        [
            Finding(CutLabFindingKind.CurveCongestion, "Curve congestion", "Curve finding."),
            Finding(CutLabFindingKind.WeakFloorCase, "Weak floor cases", "First weak floor finding."),
            Finding(CutLabFindingKind.ComboProtected, "Combo-protected cards", "Combo finding."),
            Finding(CutLabFindingKind.FunctionalTwins, "Functional twins", "Twins finding."),
            Finding(CutLabFindingKind.WeakFloorCase, "Weak floor cases", "Second weak floor finding."),
        ];

        IReadOnlyList<CutLabFindingGroupView> groups = CutLabFindingPresenter.BuildFindingGroups(findings);

        Assert.Equal(
            new[]
            {
                CutLabFindingKind.CurveCongestion,
                CutLabFindingKind.WeakFloorCase,
                CutLabFindingKind.ComboProtected,
                CutLabFindingKind.FunctionalTwins,
            },
            groups.Select(group => group.Kind).ToArray());
        Assert.Equal(
            new[] { "First weak floor finding.", "Second weak floor finding." },
            groups[1].Items.Select(item => item.Lead).ToArray());
    }

    [Fact]
    public void BuildFindingGroups_TwoPreExistingMerges_AppearInFirstOccurrenceOrder_WithoutTwins()
    {
        IReadOnlyList<CutLabFindingView> findings =
        [
            Finding(CutLabFindingKind.CurveCongestion, "Curve congestion", "Curve finding."),
            Finding(CutLabFindingKind.WeakFloorCase, "Weak floor cases", "Weak floor finding."),
            Finding(CutLabFindingKind.ComboProtected, "Combo-protected cards", "Combo finding."),
        ];

        IReadOnlyList<CutLabFindingGroupView> groups = CutLabFindingPresenter.BuildFindingGroups(findings);

        Assert.Equal(
            new[] { CutLabFindingKind.CurveCongestion, CutLabFindingKind.WeakFloorCase, CutLabFindingKind.ComboProtected },
            groups.Select(group => group.Kind).ToArray());
        Assert.DoesNotContain(groups, group => group.Kind == CutLabFindingKind.FunctionalTwins);
    }

    [Fact]
    public void BuildFindings_TwinEvidence_CarriesTheManaValueSuffix()
    {
        IReadOnlyList<CutLabFinding> findings =
        [
            new CutLabFinding(
                CutLabFindingKind.FunctionalTwins,
                "Functional twins",
                "Three ramp artifacts at mana value 2.",
                [
                    new CutLabFindingEvidence("Twin A", 2),
                    new CutLabFindingEvidence("Twin B", 2.5),
                    new CutLabFindingEvidence("Twin C", null),
                ]),
        ];

        IReadOnlyList<CutLabFindingView> views = CutLabFindingPresenter.BuildFindings(findings);

        CutLabFindingView view = Assert.Single(views);
        Assert.Equal(new[] { "Twin A · MV 2", "Twin B · MV 2.5", "Twin C" }, view.Evidence.ToArray());
    }

    private static CutLabFindingView Finding(CutLabFindingKind kind, string heading, string lead)
        => new()
        {
            Kind = kind,
            Heading = heading,
            Lead = lead,
            Evidence = [],
        };

    /// <summary>
    /// Why: T-041-03 / D-01 / D-04. This runs the real detector (not a synthetic fixture) through
    /// BuildFindings to catch drift between the production heading/copy and this presenter, and
    /// pins the legacy strings' absence at the boundary the Razor and AJAX surfaces actually read.
    /// </summary>
    [Fact]
    public void BuildFindings_RealFunctionalTwinsDetectorOutput_UsesSlotCongestionAndStructuredRoles()
    {
        CutLabAnalyzedCard[] pool =
        [
            new("Twin A", 2, false, ["ramp"], []) { TypeLine = "Artifact" },
            new("Twin B", 2, false, ["ramp"], []) { TypeLine = "Artifact" },
            new("Twin C", 2, false, ["ramp"], []) { TypeLine = "Artifact" },
        ];
        CutLabStructuralFindingsResult result = CutLabStructuralFindings.Compute(
            pool,
            Array.Empty<SpellbookAlmostCombo>(),
            new Dictionary<string, int>(StringComparer.Ordinal),
            comboDataAvailable: false,
            categoryDataAvailable: false,
            twinsEnabled: true);

        IReadOnlyList<CutLabFindingView> views = CutLabFindingPresenter.BuildFindings(result.Findings);
        CutLabFindingView view = Assert.Single(views);

        Assert.Equal(CutLabFindingKind.FunctionalTwins, view.Kind);
        Assert.Equal("Slot Congestion", view.Heading);
        Assert.DoesNotContain("Functional twins", view.Heading, StringComparison.Ordinal);
        Assert.DoesNotContain("Functional twins", view.Lead, StringComparison.Ordinal);
        Assert.DoesNotContain("costliest group", view.Lead, StringComparison.Ordinal);
        Assert.Contains("exact mana value", view.Lead, StringComparison.Ordinal);
        Assert.Equal(["Ramp"], view.Roles);
    }
}
