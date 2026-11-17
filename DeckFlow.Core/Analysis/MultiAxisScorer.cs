using System.Globalization;

namespace DeckFlow.Core.Analysis;

/// <summary>
/// Pure static scorer that maps a <see cref="DeckStatSummary"/> plus the bracket-classifier
/// signals (Game Changers count, two-card combo density, bracket number) into four coarse
/// 0-5 magnitude bands with inline rationale and a bracket cross-check (SCORE-01/02/03).
/// </summary>
/// <remarks>
/// No DI, no HTTP, no decimals exposed — a deterministic transform over integer signals.
/// Band cutpoints are heuristic estimates disclosed to the user as "re-check and refine";
/// the golden cEDH-vs-battlecruiser test guards against gross miscalibration.
/// </remarks>
public static class MultiAxisScorer
{
    private const string NoRuleMatched = "None of the higher-band rules matched.";

    private const int Power5GameChangerCount = 10;
    private const int Power4GameChangerCount = 4;
    private const int Power4TwoCardComboCount = 1;
    private const int Power4FastMana = 6;
    private const int Power3GameChangerCount = 4;
    private const int Power3GameChangerCountAlternative = 1;
    private const int Power3TwoCardComboCount = 1;
    private const int Power3FastMana = 4;
    private const int Power2GameChangerCount = 1;
    private const int Power2TwoCardComboCount = 1;
    private const int Power2FastMana = 3;
    private const int Power1FastMana = 1;
    private const decimal Speed5AvgMv = 2.0m;
    private const int Speed5FastMana = 8;
    private const int Speed5RampDrawUnderThreeMv = 12;
    private const decimal Speed4AvgMv = 2.5m;
    private const int Speed4FastMana = 5;
    private const int Speed4RampDrawUnderThreeMv = 8;
    private const decimal Speed3AvgMv = 3.0m;
    private const int Speed3FastMana = 3;
    private const int Speed3RampDrawUnderThreeMv = 5;
    private const decimal Speed2AvgMv = 3.5m;
    private const int Speed2RampDrawUnderThreeMv = 3;
    private const int Speed1FastMana = 1;
    private const int Speed1RampDrawUnderThreeMv = 1;
    private const int Control5Interaction = 19;
    private const int Control5Wipes = 5;
    private const int Control5Counters = 6;
    private const int Control4Interaction = 13;
    private const int Control4Counters = 3;
    private const int Control3Interaction = 8;
    private const int Control3Wipes = 1;
    private const int Control2Interaction = 4;
    private const int Control1Interaction = 1;
    private const int Control1Wipes = 1;
    private const int Control1Counters = 1;
    private const int Consistency5Tutors = 12;
    private const int Consistency5TwoCardComboCount = 3;
    private const decimal Consistency5AvgMv = 2.0m;
    private const int Consistency4Tutors = 8;
    private const int Consistency4TwoCardComboCount = 2;
    private const int Consistency3Tutors = 5;
    private const int Consistency3TwoCardComboCount = 1;
    private const int Consistency2Tutors = 2;
    private const int Consistency1Tutors = 1;
    private const int Consistency1TwoCardComboCount = 1;

    // Why: Power thresholds intentionally do not alias BracketRubricThresholds;
    // they are separate scales that agree today.
    private static readonly Rule[] PowerRules =
    [
        new(5, static input => input.GameChangerCount >= Power5GameChangerCount,
            FormattableString.Invariant($"{Power5GameChangerCount}+ Game Changers")),
        new(4, static input => input.GameChangerCount >= Power4GameChangerCount && (input.TwoCardComboCount >= Power4TwoCardComboCount || input.Stats.FastMana >= Power4FastMana),
            FormattableString.Invariant($"{Power4GameChangerCount}+ Game Changers and ({Power4TwoCardComboCount}+ two-card combos or {Power4FastMana}+ fast mana)")),
        new(3, static input => input.GameChangerCount >= Power3GameChangerCount || (input.GameChangerCount >= Power3GameChangerCountAlternative && (input.TwoCardComboCount >= Power3TwoCardComboCount || input.Stats.FastMana >= Power3FastMana)),
            FormattableString.Invariant($"{Power3GameChangerCount}+ Game Changers or ({Power3GameChangerCountAlternative}+ Game Changers and ({Power3TwoCardComboCount}+ two-card combos or {Power3FastMana}+ fast mana))")),
        new(2, static input => input.GameChangerCount >= Power2GameChangerCount || input.TwoCardComboCount >= Power2TwoCardComboCount || input.Stats.FastMana >= Power2FastMana,
            FormattableString.Invariant($"{Power2GameChangerCount}+ Game Changers or {Power2TwoCardComboCount}+ two-card combos or {Power2FastMana}+ fast mana")),
        new(1, static input => input.Stats.FastMana >= Power1FastMana,
            FormattableString.Invariant($"{Power1FastMana}+ fast mana")),
    ];

    private static readonly Rule[] SpeedRules =
    [
        new(5, static input => input.Stats.AverageManaValue < Speed5AvgMv && input.Stats.FastMana >= Speed5FastMana && input.Stats.RampDrawUnderThreeMv >= Speed5RampDrawUnderThreeMv,
            FormattableString.Invariant($"average mana value < {Speed5AvgMv} and {Speed5FastMana}+ fast mana and {Speed5RampDrawUnderThreeMv}+ ramp/draw under 3 MV")),
        new(4, static input => input.Stats.AverageManaValue <= Speed4AvgMv && input.Stats.FastMana >= Speed4FastMana && input.Stats.RampDrawUnderThreeMv >= Speed4RampDrawUnderThreeMv,
            FormattableString.Invariant($"average mana value <= {Speed4AvgMv} and {Speed4FastMana}+ fast mana and {Speed4RampDrawUnderThreeMv}+ ramp/draw under 3 MV")),
        new(3, static input => input.Stats.AverageManaValue <= Speed3AvgMv && input.Stats.FastMana >= Speed3FastMana && input.Stats.RampDrawUnderThreeMv >= Speed3RampDrawUnderThreeMv,
            FormattableString.Invariant($"average mana value <= {Speed3AvgMv} and {Speed3FastMana}+ fast mana and {Speed3RampDrawUnderThreeMv}+ ramp/draw under 3 MV")),
        new(2, static input => input.Stats.AverageManaValue <= Speed2AvgMv && input.Stats.RampDrawUnderThreeMv >= Speed2RampDrawUnderThreeMv,
            FormattableString.Invariant($"average mana value <= {Speed2AvgMv} and {Speed2RampDrawUnderThreeMv}+ ramp/draw under 3 MV")),
        new(1, static input => input.Stats.FastMana >= Speed1FastMana || input.Stats.RampDrawUnderThreeMv >= Speed1RampDrawUnderThreeMv,
            FormattableString.Invariant($"{Speed1FastMana}+ fast mana or {Speed1RampDrawUnderThreeMv}+ ramp/draw under 3 MV")),
    ];

    private static readonly Rule[] ControlRules =
    [
        new(5, static input => input.Stats.Interaction >= Control5Interaction && input.Stats.Wipes >= Control5Wipes && input.Stats.Counters >= Control5Counters,
            FormattableString.Invariant($"{Control5Interaction}+ interaction pieces and {Control5Wipes}+ board wipes and {Control5Counters}+ counters")),
        new(4, static input => input.Stats.Interaction >= Control4Interaction && input.Stats.Counters >= Control4Counters,
            FormattableString.Invariant($"{Control4Interaction}+ interaction pieces and {Control4Counters}+ counters")),
        new(3, static input => input.Stats.Interaction >= Control3Interaction && input.Stats.Wipes >= Control3Wipes,
            FormattableString.Invariant($"{Control3Interaction}+ interaction pieces and {Control3Wipes}+ board wipes")),
        new(2, static input => input.Stats.Interaction >= Control2Interaction,
            FormattableString.Invariant($"{Control2Interaction}+ interaction pieces")),
        new(1, static input => input.Stats.Interaction >= Control1Interaction || input.Stats.Wipes >= Control1Wipes || input.Stats.Counters >= Control1Counters,
            FormattableString.Invariant($"{Control1Interaction}+ interaction pieces or {Control1Wipes}+ board wipes or {Control1Counters}+ counters")),
    ];

    private static readonly Rule[] ConsistencyRules =
    [
        new(5, static input => input.Stats.Tutors >= Consistency5Tutors && input.TwoCardComboCount >= Consistency5TwoCardComboCount && input.Stats.AverageManaValue < Consistency5AvgMv,
            FormattableString.Invariant($"{Consistency5Tutors}+ tutors and {Consistency5TwoCardComboCount}+ two-card combos and average mana value < {Consistency5AvgMv}")),
        new(4, static input => input.Stats.Tutors >= Consistency4Tutors && input.TwoCardComboCount >= Consistency4TwoCardComboCount,
            FormattableString.Invariant($"{Consistency4Tutors}+ tutors and {Consistency4TwoCardComboCount}+ two-card combos")),
        new(3, static input => input.Stats.Tutors >= Consistency3Tutors && input.TwoCardComboCount >= Consistency3TwoCardComboCount,
            FormattableString.Invariant($"{Consistency3Tutors}+ tutors and {Consistency3TwoCardComboCount}+ two-card combos")),
        new(2, static input => input.Stats.Tutors >= Consistency2Tutors,
            FormattableString.Invariant($"{Consistency2Tutors}+ tutors")),
        new(1, static input => input.Stats.Tutors >= Consistency1Tutors || input.TwoCardComboCount >= Consistency1TwoCardComboCount,
            FormattableString.Invariant($"{Consistency1Tutors}+ tutors or {Consistency1TwoCardComboCount}+ two-card combos")),
    ];

    private readonly record struct ScoreInput(DeckStatSummary Stats, int GameChangerCount, int TwoCardComboCount);

    private sealed record Rule(int Band, Func<ScoreInput, bool> Predicate, string Description);

    private static (int Band, string Description) Evaluate(Rule[] rules, ScoreInput input)
    {
        foreach (var rule in rules)
        {
            if (rule.Predicate(input))
            {
                return (rule.Band, rule.Description);
            }
        }

        return (0, NoRuleMatched);
    }

    /// <summary>
    /// Scores a deck on the Power/Speed/Control/Consistency axes from its already-computed signals.
    /// </summary>
    /// <param name="stats">Pre-computed deck composition stats (see <see cref="DeckStatAggregator.Compute"/>).</param>
    /// <param name="gameChangerCount">Number of Game Changers detected by the bracket classifier.</param>
    /// <param name="twoCardComboCount">Number of two-card combos detected; 0 when none or unavailable.</param>
    /// <param name="comboDetectionAvailable">
    /// <see langword="false"/> when combo detection was unavailable. When false the rationale discloses
    /// "combo data unavailable" rather than asserting "0 combos" (null-vs-empty semantics).
    /// </param>
    /// <param name="bracketNumber">The deck's bracket classification number (1-5) for the cross-check.</param>
    /// <returns>The four-axis <see cref="DeckMultiAxisScore"/> with rationale and bracket cross-check.</returns>
    public static DeckMultiAxisScore Score(
        DeckStatSummary stats,
        int gameChangerCount,
        int twoCardComboCount,
        bool comboDetectionAvailable,
        int bracketNumber)
    {
        ArgumentNullException.ThrowIfNull(stats);

        var avgMv = stats.AverageManaValue;
        var comboText = comboDetectionAvailable
            ? $"{twoCardComboCount} two-card combos"
            : "combo data unavailable";
        var avgMvText = avgMv.ToString(CultureInfo.InvariantCulture);

        var input = new ScoreInput(stats, gameChangerCount, twoCardComboCount);
        var (powerBand, powerRule) = Evaluate(PowerRules, input);
        var (speedBand, speedRule) = Evaluate(SpeedRules, input);
        var (controlBand, controlRule) = Evaluate(ControlRules, input);
        var (consistencyBand, consistencyRule) = Evaluate(ConsistencyRules, input);

        powerBand = Math.Clamp(powerBand, 0, 5);
        speedBand = Math.Clamp(speedBand, 0, 5);
        controlBand = Math.Clamp(controlBand, 0, 5);
        consistencyBand = Math.Clamp(consistencyBand, 0, 5);

        var powerRationale = new DeckScoreRationale(
            $"{gameChangerCount} Game Changers, {comboText}, {stats.FastMana} fast-mana sources", powerRule);
        var speedRationale = new DeckScoreRationale(
            $"avg MV {avgMvText}, {stats.FastMana} fast-mana, {stats.RampDrawUnderThreeMv} ramp/draw under 3 MV", speedRule);
        var controlRationale = new DeckScoreRationale(
            $"{stats.Interaction} interaction pieces, {stats.Wipes} board wipes, {stats.Counters} counters", controlRule);
        var consistencyRationale = new DeckScoreRationale(
            $"{stats.Tutors} tutors, {comboText}, avg mana value {avgMvText}", consistencyRule);

        // Bracket cross-check: a high Power band with a low bracket (or vice versa) is a contradiction
        // worth surfacing so a miscalibrated band degrades into an AI correction, not a silent error.
        var scoreAlignsBracket = !((powerBand >= 4 && bracketNumber <= 2)
            || (powerBand <= 1 && bracketNumber >= 4));
        var bracketCrossCheckText = scoreAlignsBracket
            ? $"Score aligns with the Bracket {bracketNumber} classification."
            : $"Score and bracket disagree - verify with your AI. Power reads {powerBand} "
                + $"({BandLabel(powerBand)}) but the deck classified as Bracket {bracketNumber}. "
                + "Re-check Game Changers membership and combo count before trusting either figure.";

        return new DeckMultiAxisScore(
            powerBand,
            speedBand,
            controlBand,
            consistencyBand,
            powerRationale,
            speedRationale,
            controlRationale,
            consistencyRationale,
            bracketNumber,
            bracketCrossCheckText,
            scoreAlignsBracket);
    }

    /// <summary>
    /// Maps a 0-5 band integer to its magnitude label. Values above 5 clamp to "Extreme";
    /// the labels are a magnitude ladder (None..Extreme), not a quality scale.
    /// </summary>
    /// <param name="band">The band value; 0-5 expected, higher values clamp to "Extreme".</param>
    /// <returns>The band label word.</returns>
    public static string BandLabel(int band) => band switch
    {
        0 => "None",
        1 => "Low",
        2 => "Modest",
        3 => "Moderate",
        4 => "High",
        _ => "Extreme",
    };
}
