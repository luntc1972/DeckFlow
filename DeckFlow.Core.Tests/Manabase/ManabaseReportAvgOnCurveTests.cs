using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Efficacy R2 M9: <see cref="ManabaseReport.AvgOnCurvePercent"/> is the mean over NON-commander
/// castability rows, so the results lens, the verdict, and the health band all quote one number.
/// </summary>
public sealed class ManabaseReportAvgOnCurveTests
{
    private static CardCastability Row(string name, int cast, bool isCommander = false) => new()
    {
        Name = name,
        ManaValue = 3,
        OnCurveTurn = 3,
        CastPercent = cast,
        LimitingFactor = "mana",
        IsCommander = isCommander,
    };

    private static ManabaseReport WithRows(params CardCastability[] rows) => new()
    {
        ActualLands = 37,
        TargetLands = 37,
        ColorFindings = System.Array.Empty<ColorSourceFinding>(),
        Summary = "test",
        Castability = rows,
    };

    [Fact]
    public void AvgOnCurvePercent_ExcludesCommanderRow()
    {
        // A hard 6-MV commander at 60% must not drag the deck's avg; the two spells average 90.
        ManabaseReport report = WithRows(
            Row("Hard Commander", 60, isCommander: true),
            Row("Spell A", 88),
            Row("Spell B", 92));

        Assert.Equal(90, report.AvgOnCurvePercent);
    }

    [Fact]
    public void AvgOnCurvePercent_AllCommander_FallsBackToFullSet()
    {
        // Degenerate: only commander rows tracked — fall back rather than report 0.
        ManabaseReport report = WithRows(
            Row("Commander A", 70, isCommander: true),
            Row("Commander B", 90, isCommander: true));

        Assert.Equal(80, report.AvgOnCurvePercent);
    }

    [Fact]
    public void AvgOnCurvePercent_Empty_IsZero()
    {
        Assert.Equal(0, WithRows().AvgOnCurvePercent);
    }
}
