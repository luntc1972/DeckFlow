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

    /// <summary>Build the cEDH calibration report from materialized deck rows.</summary>
    /// <param name="rows">Calibration rows to aggregate.</param>
    public static CedhCalibrationReport Build(IEnumerable<CedhCalibrationRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var materialized = rows.ToList();
        int sampleSize = materialized.Count;
        int underOld = materialized.Count(row => row.ActualLands < row.OldTarget);
        int underNew = materialized.Count(row => row.ActualLands < row.NewTarget);
        int underRitualCredit = materialized.Count(row => row.ActualLands < row.NewTargetWithRitualCredit);
        int unflaggedByNew = materialized.Count(row => row.ActualLands < row.OldTarget && row.ActualLands >= row.NewTarget);
        int newlyUnder = materialized.Count(row => row.ActualLands >= row.OldTarget && row.ActualLands < row.NewTarget);
        int unflaggedByRitualCredit = materialized.Count(row => row.ActualLands < row.NewTarget && row.ActualLands >= row.NewTargetWithRitualCredit);
        int newlyUnderRitualCredit = materialized.Count(row => row.ActualLands >= row.NewTarget && row.ActualLands < row.NewTargetWithRitualCredit);

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
                return new CedhCalibrationCommanderRollup
                {
                    CommanderKey = group.Key,
                    SampleSize = commanderRows.Count,
                    ActualLandsMean = commanderRows.Average(row => row.ActualLands),
                    OldTargetMean = commanderRows.Average(row => row.OldTarget),
                    NewTargetMean = commanderRows.Average(row => row.NewTarget),
                    NewTargetWithRitualCreditMean = commanderRows.Average(row => row.NewTargetWithRitualCredit),
                    UnderOldCount = commanderRows.Count(row => row.ActualLands < row.OldTarget),
                    UnderOldPercent = Percent(commanderRows, row => row.ActualLands < row.OldTarget),
                    UnderNewCount = commanderRows.Count(row => row.ActualLands < row.NewTarget),
                    UnderNewPercent = Percent(commanderRows, row => row.ActualLands < row.NewTarget),
                    UnderRitualCreditCount = commanderRows.Count(row => row.ActualLands < row.NewTargetWithRitualCredit),
                    UnderRitualCreditPercent = Percent(commanderRows, row => row.ActualLands < row.NewTargetWithRitualCredit),
                };
            })
            .ToList();

        return new CedhCalibrationReport
        {
            SampleSize = sampleSize,
            ActualLandsMean = Average(materialized, row => row.ActualLands),
            OldTargetMean = Average(materialized, row => row.OldTarget),
            OldTargetMin = sampleSize == 0 ? 0 : materialized.Min(row => row.OldTarget),
            OldTargetMax = sampleSize == 0 ? 0 : materialized.Max(row => row.OldTarget),
            NewTargetMean = Average(materialized, row => row.NewTarget),
            NewTargetMin = sampleSize == 0 ? 0 : materialized.Min(row => row.NewTarget),
            NewTargetMax = sampleSize == 0 ? 0 : materialized.Max(row => row.NewTarget),
            NewTargetWithRitualCreditMean = Average(materialized, row => row.NewTargetWithRitualCredit),
            NewTargetWithRitualCreditMin = sampleSize == 0 ? 0 : materialized.Min(row => row.NewTargetWithRitualCredit),
            NewTargetWithRitualCreditMax = sampleSize == 0 ? 0 : materialized.Max(row => row.NewTargetWithRitualCredit),
            UnderOldCount = underOld,
            UnderOldPercent = Percent(materialized, row => row.ActualLands < row.OldTarget),
            UnderNewCount = underNew,
            UnderNewPercent = Percent(materialized, row => row.ActualLands < row.NewTarget),
            UnderRitualCreditCount = underRitualCredit,
            UnderRitualCreditPercent = Percent(materialized, row => row.ActualLands < row.NewTargetWithRitualCredit),
            UnflaggedByNewCount = unflaggedByNew,
            NewlyUnderCount = newlyUnder,
            UnflaggedByRitualCreditCount = unflaggedByRitualCredit,
            NewlyUnderRitualCreditCount = newlyUnderRitualCredit,
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
        return new CedhCalibrationSegmentStats
        {
            Label = label,
            SampleSize = materialized.Count,
            ActualLandsMean = Average(materialized, row => row.ActualLands),
            OldTargetMean = Average(materialized, row => row.OldTarget),
            NewTargetMean = Average(materialized, row => row.NewTarget),
            NewTargetWithRitualCreditMean = Average(materialized, row => row.NewTargetWithRitualCredit),
            UnderOldCount = materialized.Count(row => row.ActualLands < row.OldTarget),
            UnderOldPercent = Percent(materialized, row => row.ActualLands < row.OldTarget),
            UnderNewCount = materialized.Count(row => row.ActualLands < row.NewTarget),
            UnderNewPercent = Percent(materialized, row => row.ActualLands < row.NewTarget),
            UnderRitualCreditCount = materialized.Count(row => row.ActualLands < row.NewTargetWithRitualCredit),
            UnderRitualCreditPercent = Percent(materialized, row => row.ActualLands < row.NewTargetWithRitualCredit),
            UnflaggedByNewCount = materialized.Count(row => row.ActualLands < row.OldTarget && row.ActualLands >= row.NewTarget),
            NewlyUnderCount = materialized.Count(row => row.ActualLands >= row.OldTarget && row.ActualLands < row.NewTarget),
            UnflaggedByRitualCreditCount = materialized.Count(row => row.ActualLands < row.NewTarget && row.ActualLands >= row.NewTargetWithRitualCredit),
            NewlyUnderRitualCreditCount = materialized.Count(row => row.ActualLands >= row.NewTarget && row.ActualLands < row.NewTargetWithRitualCredit),
        };
    }

    private static double Average(IEnumerable<CedhCalibrationRow> rows, Func<CedhCalibrationRow, double> selector)
    {
        var materialized = rows.ToList();
        return materialized.Count == 0 ? 0 : materialized.Average(selector);
    }

    private static double Percent(IEnumerable<CedhCalibrationRow> rows, Func<CedhCalibrationRow, bool> predicate)
    {
        var materialized = rows.ToList();
        return materialized.Count == 0 ? 0 : (100.0 * materialized.Count(predicate) / materialized.Count);
    }

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
