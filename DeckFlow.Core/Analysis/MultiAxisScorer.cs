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

        // Power: GC-dominant, with combo density and fast mana as modifiers.
        int powerBand;
        if (gameChangerCount >= 10)
        {
            powerBand = 5;
        }
        else if (gameChangerCount >= 4 && (twoCardComboCount >= 1 || stats.FastMana >= 6))
        {
            powerBand = 4;
        }
        else if (gameChangerCount >= 4
            || (gameChangerCount >= 1 && (twoCardComboCount >= 1 || stats.FastMana >= 4)))
        {
            powerBand = 3;
        }
        else if (gameChangerCount >= 1 || twoCardComboCount >= 1 || stats.FastMana >= 3)
        {
            powerBand = 2;
        }
        else if (stats.FastMana >= 1)
        {
            powerBand = 1;
        }
        else
        {
            powerBand = 0;
        }

        // Speed: low avg mana value + fast mana + early ramp/draw push the band up.
        int speedBand;
        if (avgMv < 2.0m && stats.FastMana >= 8 && stats.RampDrawUnderThreeMv >= 12)
        {
            speedBand = 5;
        }
        else if (avgMv <= 2.5m && stats.FastMana >= 5 && stats.RampDrawUnderThreeMv >= 8)
        {
            speedBand = 4;
        }
        else if (avgMv <= 3.0m && stats.FastMana >= 3 && stats.RampDrawUnderThreeMv >= 5)
        {
            speedBand = 3;
        }
        else if (avgMv <= 3.5m && stats.RampDrawUnderThreeMv >= 3)
        {
            speedBand = 2;
        }
        else if (stats.FastMana >= 1 || stats.RampDrawUnderThreeMv >= 1)
        {
            speedBand = 1;
        }
        else
        {
            speedBand = 0;
        }

        // Control: interaction pieces + board wipes + counters.
        int controlBand;
        if (stats.Interaction >= 19 && stats.Wipes >= 5 && stats.Counters >= 6)
        {
            controlBand = 5;
        }
        else if (stats.Interaction >= 13 && stats.Counters >= 3)
        {
            controlBand = 4;
        }
        else if (stats.Interaction >= 8 && stats.Wipes >= 1)
        {
            controlBand = 3;
        }
        else if (stats.Interaction >= 4)
        {
            controlBand = 2;
        }
        else if (stats.Interaction >= 1 || stats.Wipes >= 1 || stats.Counters >= 1)
        {
            controlBand = 1;
        }
        else
        {
            controlBand = 0;
        }

        // Consistency: tutors + combo redundancy + curve smoothness (low avg mana value).
        int consistencyBand;
        if (stats.Tutors >= 12 && twoCardComboCount >= 3 && avgMv < 2.0m)
        {
            consistencyBand = 5;
        }
        else if (stats.Tutors >= 8 && twoCardComboCount >= 2)
        {
            consistencyBand = 4;
        }
        else if (stats.Tutors >= 5 && twoCardComboCount >= 1)
        {
            consistencyBand = 3;
        }
        else if (stats.Tutors >= 2)
        {
            consistencyBand = 2;
        }
        else if (stats.Tutors >= 1 || twoCardComboCount >= 1)
        {
            consistencyBand = 1;
        }
        else
        {
            consistencyBand = 0;
        }

        powerBand = Math.Clamp(powerBand, 0, 5);
        speedBand = Math.Clamp(speedBand, 0, 5);
        controlBand = Math.Clamp(controlBand, 0, 5);
        consistencyBand = Math.Clamp(consistencyBand, 0, 5);

        var powerRationale = new DeckScoreRationale(
            $"{gameChangerCount} Game Changers, {comboText}, {stats.FastMana} fast-mana sources");
        var speedRationale = new DeckScoreRationale(
            $"avg MV {avgMvText}, {stats.FastMana} fast-mana, {stats.RampDrawUnderThreeMv} ramp/draw under 3 MV");
        var controlRationale = new DeckScoreRationale(
            $"{stats.Interaction} interaction pieces, {stats.Wipes} board wipes, {stats.Counters} counters");
        var consistencyRationale = new DeckScoreRationale(
            $"{stats.Tutors} tutors, {comboText}, smooth {avgMvText} curve");

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
