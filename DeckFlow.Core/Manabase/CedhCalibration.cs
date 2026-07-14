using System.Text;

namespace DeckFlow.Core.Manabase;

/// <summary>One calibrated cEDH deck row comparing the old and new land targets.</summary>
public sealed record CedhCalibrationRow
{
    /// <summary>Create a calibration row; legacy 5-arg callers default ritual-credit target to <paramref name="newTarget"/>.</summary>
    public CedhCalibrationRow(
        string commanderKey,
        int actualLands,
        double oldTarget,
        double newTarget,
        bool hasBaseline)
        : this(commanderKey, actualLands, oldTarget, newTarget, newTarget, hasBaseline)
    {
    }

    /// <summary>Create a calibration row with an explicit ritual-credit target column.</summary>
    public CedhCalibrationRow(
        string commanderKey,
        int actualLands,
        double oldTarget,
        double newTarget,
        double newTargetWithRitualCredit,
        bool hasBaseline)
    {
        CommanderKey = commanderKey;
        ActualLands = actualLands;
        OldTarget = oldTarget;
        NewTarget = newTarget;
        NewTargetWithRitualCredit = newTargetWithRitualCredit;
        HasBaseline = hasBaseline;
    }

    /// <summary>Commander name or partner-pair key.</summary>
    public string CommanderKey { get; }

    /// <summary>Observed land count using the app's source classification.</summary>
    public int ActualLands { get; }

    /// <summary>Historic five-argument cEDH land target.</summary>
    public double OldTarget { get; }

    /// <summary>Enabled-context hybrid cEDH land target.</summary>
    public double NewTarget { get; }

    /// <summary>Enabled-context hybrid target with ritual land credit.</summary>
    public double NewTargetWithRitualCredit { get; }

    /// <summary>Whether the commander had a baseline sample of at least ten decks.</summary>
    public bool HasBaseline { get; }
}

/// <summary>Pure helper that aggregates calibration rows into a reportable cEDH target-comparison rollup.</summary>
public static class CedhCalibration
{
    private const int MinCommanderSamples = 10;
    private const double SafetyFloor = 22.0;
    private const double TargetCeiling = 45.0;
    private static readonly IReadOnlyList<TargetColumn> TargetColumns =
    [
        new(row => row.OldTarget),
        new(row => row.NewTarget),
        new(row => row.NewTargetWithRitualCredit),
    ];

    /// <summary>Build the cEDH calibration report from materialized deck rows.</summary>
    /// <param name="rows">Calibration rows to aggregate.</param>
    public static CedhCalibrationReport Build(IEnumerable<CedhCalibrationRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var materialized = rows.ToList();
        int sampleSize = materialized.Count;
        IReadOnlyList<VariantStats> variantStats = ComputeVariantStats(materialized);
        PairwiseDelta oldToNewDelta = ComputePairwiseDelta(materialized, TargetColumns[0], TargetColumns[1]);
        PairwiseDelta newToRitualCreditDelta = ComputePairwiseDelta(materialized, TargetColumns[1], TargetColumns[2]);

        IReadOnlyList<CedhCalibrationSegmentStats> segments =
        [
            BuildSegment("Baseline N>=10", materialized.Where(row => row.HasBaseline)),
            BuildSegment("No baseline", materialized.Where(row => !row.HasBaseline)),
        ];

        IReadOnlyList<CedhCalibrationCommanderRollup> commanders = materialized
            .GroupBy(row => row.CommanderKey, StringComparer.Ordinal)
            .Where(group => group.Count() >= MinCommanderSamples)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var commanderRows = group.ToList();
                IReadOnlyList<VariantStats> commanderStats = ComputeVariantStats(commanderRows);
                return new CedhCalibrationCommanderRollup
                {
                    CommanderKey = group.Key,
                    SampleSize = commanderRows.Count,
                    ActualLandsMean = commanderRows.Average(row => row.ActualLands),
                    OldTargetMean = commanderStats[0].Mean,
                    NewTargetMean = commanderStats[1].Mean,
                    NewTargetWithRitualCreditMean = commanderStats[2].Mean,
                    UnderOldCount = commanderStats[0].UnderCount,
                    UnderOldPercent = commanderStats[0].UnderPercent,
                    UnderNewCount = commanderStats[1].UnderCount,
                    UnderNewPercent = commanderStats[1].UnderPercent,
                    UnderRitualCreditCount = commanderStats[2].UnderCount,
                    UnderRitualCreditPercent = commanderStats[2].UnderPercent,
                };
            })
            .ToList();

        return new CedhCalibrationReport
        {
            SampleSize = sampleSize,
            ActualLandsMean = Average(materialized, row => row.ActualLands),
            OldTargetMean = variantStats[0].Mean,
            OldTargetMin = variantStats[0].Min,
            OldTargetMax = variantStats[0].Max,
            NewTargetMean = variantStats[1].Mean,
            NewTargetMin = variantStats[1].Min,
            NewTargetMax = variantStats[1].Max,
            NewTargetWithRitualCreditMean = variantStats[2].Mean,
            NewTargetWithRitualCreditMin = variantStats[2].Min,
            NewTargetWithRitualCreditMax = variantStats[2].Max,
            UnderOldCount = variantStats[0].UnderCount,
            UnderOldPercent = variantStats[0].UnderPercent,
            UnderNewCount = variantStats[1].UnderCount,
            UnderNewPercent = variantStats[1].UnderPercent,
            UnderRitualCreditCount = variantStats[2].UnderCount,
            UnderRitualCreditPercent = variantStats[2].UnderPercent,
            UnflaggedByNewCount = oldToNewDelta.UnflaggedCount,
            NewlyUnderCount = oldToNewDelta.NewlyUnderCount,
            UnflaggedByRitualCreditCount = newToRitualCreditDelta.UnflaggedCount,
            NewlyUnderRitualCreditCount = newToRitualCreditDelta.NewlyUnderCount,
            BaselineBackedCount = materialized.Count(row => row.HasBaseline),
            NoBaselineCount = materialized.Count(row => !row.HasBaseline),
            // Why: ritual-credit is the shipped effective target, and ritual credit only pushes targets down toward the floor.
            SafetyFloorHitCount = materialized.Count(row => Math.Abs(row.NewTargetWithRitualCredit - SafetyFloor) < 0.01),
            CeilingHitCount = materialized.Count(row => Math.Abs(row.NewTargetWithRitualCredit - TargetCeiling) < 0.01),
            Segments = segments,
            Commanders = commanders,
        };
    }

    /// <summary>Render the calibration report as the human markdown artifact consumed by the runbook.</summary>
    /// <param name="report">Report returned from <see cref="Build(IEnumerable{CedhCalibrationRow})"/>.</param>
    public static string RenderMarkdown(CedhCalibrationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        sb.AppendLine("# cEDH land-target calibration — old flat-28 vs new hybrid (flag ON)");
        sb.AppendLine();
        sb.AppendLine(
            FormattableString.Invariant(
                $"Sample: **{report.SampleSize}** cEDH decks | actual lands mean {report.ActualLandsMean:0.0}"));
        sb.AppendLine(
            FormattableString.Invariant(
                $"Old target: mean {report.OldTargetMean:0.0} (min {report.OldTargetMin:0.0} max {report.OldTargetMax:0.0})"));
        sb.AppendLine(
            FormattableString.Invariant(
                $"New target: mean {report.NewTargetMean:0.0} (min {report.NewTargetMin:0.0} max {report.NewTargetMax:0.0})"));
        sb.AppendLine(
            FormattableString.Invariant(
                $"New target with RitualCredit: mean {report.NewTargetWithRitualCreditMean:0.0} (min {report.NewTargetWithRitualCreditMin:0.0} max {report.NewTargetWithRitualCreditMax:0.0})"));
        sb.AppendLine();
        sb.AppendLine(
            FormattableString.Invariant(
                $"**Under-target (actual < target):** OLD {report.UnderOldCount}/{report.SampleSize} = **{report.UnderOldPercent:0.0}%** → NEW {report.UnderNewCount}/{report.SampleSize} = **{report.UnderNewPercent:0.0}%**"));
        sb.AppendLine(
            FormattableString.Invariant(
                $"**Under-target with RitualCredit:** {report.UnderRitualCreditCount}/{report.SampleSize} = **{report.UnderRitualCreditPercent:0.0}%**"));
        sb.AppendLine(
            FormattableString.Invariant(
                $"Decks the new target un-flags (were under, now OK): **{report.UnflaggedByNewCount}** | newly flagged under: {report.NewlyUnderCount}"));
        sb.AppendLine(
            FormattableString.Invariant(
                $"RitualCredit delta vs NEW: un-flags **{report.UnflaggedByRitualCreditCount}** | newly flagged under: {report.NewlyUnderRitualCreditCount}"));
        sb.AppendLine(
            FormattableString.Invariant(
                $"Baseline-backed (N≥10): {report.BaselineBackedCount} | recalibrated no-baseline: {report.NoBaselineCount}"));
        sb.AppendLine(
            FormattableString.Invariant(
                $"Safety-floor(22) hits: {report.SafetyFloorHitCount} | ceiling(45) hits: {report.CeilingHitCount}"));
        sb.AppendLine();
        sb.AppendLine("## Under-flag by segment");
        sb.AppendLine("| Segment | N | actual mean | old target | new target | RitualCredit | under OLD% | under NEW% | under RitualCredit% |");
        sb.AppendLine("|---------|---|------------|-----------|-----------|-----------|-----------|-----------|-----------|");
        foreach (CedhCalibrationSegmentStats segment in report.Segments)
        {
            sb.AppendLine(
                FormattableString.Invariant(
                    $"| {segment.Label} | {segment.SampleSize} | {segment.ActualLandsMean:0.0} | {segment.OldTargetMean:0.0} | {segment.NewTargetMean:0.0} | {segment.NewTargetWithRitualCreditMean:0.0} | {segment.UnderOldPercent:0.0}% | {segment.UnderNewPercent:0.0}% | {segment.UnderRitualCreditPercent:0.0}% |"));
        }

        sb.AppendLine();
        sb.AppendLine("## By commander (N≥10) — over-correction check (grindy Sisay/Tayam should stay ~healthy, not over-flagged-OK)");
        sb.AppendLine("| Commander | N | actual mean | old tgt | new tgt | RitualCredit | under OLD% | under NEW% | under RitualCredit% |");
        sb.AppendLine("|-----------|---|------------|--------|--------|--------|-----------|-----------|-----------|");
        foreach (CedhCalibrationCommanderRollup commander in report.Commanders)
        {
            string label = commander.CommanderKey.Length > 44 ? commander.CommanderKey[..44] : commander.CommanderKey;
            sb.AppendLine(
                FormattableString.Invariant(
                    $"| {EscapePipe(label)} | {commander.SampleSize} | {commander.ActualLandsMean:0.0} | {commander.OldTargetMean:0.0} | {commander.NewTargetMean:0.0} | {commander.NewTargetWithRitualCreditMean:0.0} | {commander.UnderOldPercent:0} | {commander.UnderNewPercent:0} | {commander.UnderRitualCreditPercent:0} |"));
        }

        return sb.ToString();
    }

    /// <summary>Render the one-line headline printed by the CLI after the markdown artifact is written.</summary>
    /// <param name="report">Report returned from <see cref="Build(IEnumerable{CedhCalibrationRow})"/>.</param>
    public static string RenderHeadline(CedhCalibrationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return FormattableString.Invariant(
            $"SampleSize={report.SampleSize}, UnderTarget={report.UnderOldPercent:0.0}% -> {report.UnderNewPercent:0.0}% -> RitualCredit {report.UnderRitualCreditPercent:0.0}%");
    }

    private static CedhCalibrationSegmentStats BuildSegment(string label, IEnumerable<CedhCalibrationRow> rows)
    {
        var materialized = rows.ToList();
        IReadOnlyList<VariantStats> variantStats = ComputeVariantStats(materialized);
        PairwiseDelta oldToNewDelta = ComputePairwiseDelta(materialized, TargetColumns[0], TargetColumns[1]);
        PairwiseDelta newToRitualCreditDelta = ComputePairwiseDelta(materialized, TargetColumns[1], TargetColumns[2]);
        return new CedhCalibrationSegmentStats
        {
            Label = label,
            SampleSize = materialized.Count,
            ActualLandsMean = Average(materialized, row => row.ActualLands),
            OldTargetMean = variantStats[0].Mean,
            NewTargetMean = variantStats[1].Mean,
            NewTargetWithRitualCreditMean = variantStats[2].Mean,
            UnderOldCount = variantStats[0].UnderCount,
            UnderOldPercent = variantStats[0].UnderPercent,
            UnderNewCount = variantStats[1].UnderCount,
            UnderNewPercent = variantStats[1].UnderPercent,
            UnderRitualCreditCount = variantStats[2].UnderCount,
            UnderRitualCreditPercent = variantStats[2].UnderPercent,
            UnflaggedByNewCount = oldToNewDelta.UnflaggedCount,
            NewlyUnderCount = oldToNewDelta.NewlyUnderCount,
            UnflaggedByRitualCreditCount = newToRitualCreditDelta.UnflaggedCount,
            NewlyUnderRitualCreditCount = newToRitualCreditDelta.NewlyUnderCount,
        };
    }

    /// <summary>Selector for one target variant.</summary>
    private readonly record struct TargetColumn(Func<CedhCalibrationRow, double> Selector);

    /// <summary>Computed stats for one target variant across a row set.</summary>
    private readonly record struct VariantStats(
        double Mean,
        double Min,
        double Max,
        int UnderCount,
        double UnderPercent);

    /// <summary>Under-target transition counts between adjacent target variants.</summary>
    private readonly record struct PairwiseDelta(int UnflaggedCount, int NewlyUnderCount);

    /// <summary>Compute the shared aggregate stats for each target variant.</summary>
    private static IReadOnlyList<VariantStats> ComputeVariantStats(IReadOnlyList<CedhCalibrationRow> rows) =>
        TargetColumns.Select(column => ComputeVariantStats(rows, column)).ToArray();

    /// <summary>Compute the shared aggregate stats for one target variant.</summary>
    private static VariantStats ComputeVariantStats(IReadOnlyList<CedhCalibrationRow> rows, TargetColumn column)
    {
        int sampleSize = rows.Count;
        Func<CedhCalibrationRow, double> selector = column.Selector;
        int underCount = rows.Count(row => row.ActualLands < selector(row));
        return new VariantStats(
            Average(rows, selector),
            sampleSize == 0 ? 0 : rows.Min(selector),
            sampleSize == 0 ? 0 : rows.Max(selector),
            underCount,
            sampleSize == 0 ? 0 : (100.0 * underCount / sampleSize));
    }

    /// <summary>Compute under-target transition counts between adjacent target variants.</summary>
    private static PairwiseDelta ComputePairwiseDelta(
        IReadOnlyList<CedhCalibrationRow> rows,
        TargetColumn current,
        TargetColumn next)
    {
        Func<CedhCalibrationRow, double> currentSelector = current.Selector;
        Func<CedhCalibrationRow, double> nextSelector = next.Selector;
        return new PairwiseDelta(
            rows.Count(row => row.ActualLands < currentSelector(row) && row.ActualLands >= nextSelector(row)),
            rows.Count(row => row.ActualLands >= currentSelector(row) && row.ActualLands < nextSelector(row)));
    }

    private static double Average(IReadOnlyList<CedhCalibrationRow> rows, Func<CedhCalibrationRow, double> selector) =>
        rows.Count == 0 ? 0 : rows.Average(selector);

    private static string EscapePipe(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}

/// <summary>The aggregated cEDH calibration report used by tests and the CLI markdown writer.</summary>
public sealed record CedhCalibrationReport
{
    /// <summary>Total kept cEDH sample size.</summary>
    public int SampleSize { get; init; }

    /// <summary>Arithmetic mean of observed land counts.</summary>
    public double ActualLandsMean { get; init; }

    /// <summary>Arithmetic mean of the historic target.</summary>
    public double OldTargetMean { get; init; }

    /// <summary>Minimum historic target value.</summary>
    public double OldTargetMin { get; init; }

    /// <summary>Maximum historic target value.</summary>
    public double OldTargetMax { get; init; }

    /// <summary>Arithmetic mean of the enabled-context target.</summary>
    public double NewTargetMean { get; init; }

    /// <summary>Minimum enabled-context target value.</summary>
    public double NewTargetMin { get; init; }

    /// <summary>Maximum enabled-context target value.</summary>
    public double NewTargetMax { get; init; }

    /// <summary>Arithmetic mean of the ritual-credit target.</summary>
    public double NewTargetWithRitualCreditMean { get; init; }

    /// <summary>Minimum ritual-credit target value.</summary>
    public double NewTargetWithRitualCreditMin { get; init; }

    /// <summary>Maximum ritual-credit target value.</summary>
    public double NewTargetWithRitualCreditMax { get; init; }

    /// <summary>Decks under the historic target.</summary>
    public int UnderOldCount { get; init; }

    /// <summary>Percentage of decks under the historic target.</summary>
    public double UnderOldPercent { get; init; }

    /// <summary>Decks under the enabled-context target.</summary>
    public int UnderNewCount { get; init; }

    /// <summary>Percentage of decks under the enabled-context target.</summary>
    public double UnderNewPercent { get; init; }

    /// <summary>Decks under the ritual-credit target.</summary>
    public int UnderRitualCreditCount { get; init; }

    /// <summary>Percentage of decks under the ritual-credit target.</summary>
    public double UnderRitualCreditPercent { get; init; }

    /// <summary>Decks under the old target but not the new target.</summary>
    public int UnflaggedByNewCount { get; init; }

    /// <summary>Decks not under the old target but under the new target.</summary>
    public int NewlyUnderCount { get; init; }

    /// <summary>Decks under the new target but not the ritual-credit target.</summary>
    public int UnflaggedByRitualCreditCount { get; init; }

    /// <summary>Decks not under the new target but under the ritual-credit target.</summary>
    public int NewlyUnderRitualCreditCount { get; init; }

    /// <summary>Decks backed by a commander baseline sample of at least ten.</summary>
    public int BaselineBackedCount { get; init; }

    /// <summary>Decks that fell back to the no-baseline path.</summary>
    public int NoBaselineCount { get; init; }

    /// <summary>Ritual-credit target rows clamped to the safety floor.</summary>
    public int SafetyFloorHitCount { get; init; }

    /// <summary>Ritual-credit target rows clamped to the ceiling.</summary>
    public int CeilingHitCount { get; init; }

    /// <summary>Baseline-backed vs no-baseline segment stats.</summary>
    public required IReadOnlyList<CedhCalibrationSegmentStats> Segments { get; init; }

    /// <summary>Commander rollups for commanders with at least ten samples.</summary>
    public required IReadOnlyList<CedhCalibrationCommanderRollup> Commanders { get; init; }
}

/// <summary>Per-segment cEDH calibration summary stats.</summary>
public sealed record CedhCalibrationSegmentStats
{
    /// <summary>Human-readable segment label.</summary>
    public required string Label { get; init; }

    /// <summary>Number of rows in the segment.</summary>
    public int SampleSize { get; init; }

    /// <summary>Arithmetic mean of observed land counts in the segment.</summary>
    public double ActualLandsMean { get; init; }

    /// <summary>Arithmetic mean of the historic target in the segment.</summary>
    public double OldTargetMean { get; init; }

    /// <summary>Arithmetic mean of the enabled-context target in the segment.</summary>
    public double NewTargetMean { get; init; }

    /// <summary>Arithmetic mean of the ritual-credit target in the segment.</summary>
    public double NewTargetWithRitualCreditMean { get; init; }

    /// <summary>Decks under the historic target in the segment.</summary>
    public int UnderOldCount { get; init; }

    /// <summary>Percentage under the historic target in the segment.</summary>
    public double UnderOldPercent { get; init; }

    /// <summary>Decks under the enabled-context target in the segment.</summary>
    public int UnderNewCount { get; init; }

    /// <summary>Percentage under the enabled-context target in the segment.</summary>
    public double UnderNewPercent { get; init; }

    /// <summary>Decks under the ritual-credit target in the segment.</summary>
    public int UnderRitualCreditCount { get; init; }

    /// <summary>Percentage under the ritual-credit target in the segment.</summary>
    public double UnderRitualCreditPercent { get; init; }

    /// <summary>Decks un-flagged by the new target in the segment.</summary>
    public int UnflaggedByNewCount { get; init; }

    /// <summary>Decks newly flagged under the new target in the segment.</summary>
    public int NewlyUnderCount { get; init; }

    /// <summary>Decks un-flagged by the ritual-credit target in the segment.</summary>
    public int UnflaggedByRitualCreditCount { get; init; }

    /// <summary>Decks newly flagged under the ritual-credit target in the segment.</summary>
    public int NewlyUnderRitualCreditCount { get; init; }
}

/// <summary>Commander-specific cEDH calibration rollup for commanders with enough samples.</summary>
public sealed record CedhCalibrationCommanderRollup
{
    /// <summary>Commander name or partner-pair key.</summary>
    public required string CommanderKey { get; init; }

    /// <summary>Number of rows contributing to the commander rollup.</summary>
    public int SampleSize { get; init; }

    /// <summary>Arithmetic mean of observed land counts.</summary>
    public double ActualLandsMean { get; init; }

    /// <summary>Arithmetic mean of the historic target.</summary>
    public double OldTargetMean { get; init; }

    /// <summary>Arithmetic mean of the enabled-context target.</summary>
    public double NewTargetMean { get; init; }

    /// <summary>Arithmetic mean of the ritual-credit target.</summary>
    public double NewTargetWithRitualCreditMean { get; init; }

    /// <summary>Decks under the historic target.</summary>
    public int UnderOldCount { get; init; }

    /// <summary>Percentage of decks under the historic target.</summary>
    public double UnderOldPercent { get; init; }

    /// <summary>Decks under the enabled-context target.</summary>
    public int UnderNewCount { get; init; }

    /// <summary>Percentage of decks under the enabled-context target.</summary>
    public double UnderNewPercent { get; init; }

    /// <summary>Decks under the ritual-credit target.</summary>
    public int UnderRitualCreditCount { get; init; }

    /// <summary>Percentage of decks under the ritual-credit target.</summary>
    public double UnderRitualCreditPercent { get; init; }
}
