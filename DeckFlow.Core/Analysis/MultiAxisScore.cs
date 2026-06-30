namespace DeckFlow.Core.Analysis;

/// <summary>
/// The four-axis coarse band score for a Commander deck (SCORE-01/02/03).
/// Each band is a magnitude bucket in 0-5 (not a quality score); a
/// <see cref="DeckScoreRationale"/> carries the signal values that produced it.
/// </summary>
/// <param name="PowerBand">Power magnitude band, 0-5 (Game Changers + combo density + fast mana).</param>
/// <param name="SpeedBand">Speed magnitude band, 0-5 (avg mana value + fast mana + early ramp/draw).</param>
/// <param name="ControlBand">Control magnitude band, 0-5 (interaction + board wipes + counters).</param>
/// <param name="ConsistencyBand">Consistency magnitude band, 0-5 (tutors + combo redundancy + curve smoothness).</param>
/// <param name="PowerRationale">Signals that produced <paramref name="PowerBand"/>.</param>
/// <param name="SpeedRationale">Signals that produced <paramref name="SpeedBand"/>.</param>
/// <param name="ControlRationale">Signals that produced <paramref name="ControlBand"/>.</param>
/// <param name="ConsistencyRationale">Signals that produced <paramref name="ConsistencyBand"/>.</param>
/// <param name="BracketNumber">The deck's bracket classification number (1-5) cross-checked against the bands.</param>
/// <param name="BracketCrossCheckText">Plain-language agreement or divergence note vs the bracket number.</param>
/// <param name="ScoreAlignsBracket">
/// <see langword="true"/> when no axis contradicts the bracket number; <see langword="false"/> when a
/// band diverges (e.g. Power 5 with Bracket 2), in which case <see cref="BracketCrossCheckText"/> names it.
/// </param>
public sealed record DeckMultiAxisScore(
    int PowerBand,
    int SpeedBand,
    int ControlBand,
    int ConsistencyBand,
    DeckScoreRationale PowerRationale,
    DeckScoreRationale SpeedRationale,
    DeckScoreRationale ControlRationale,
    DeckScoreRationale ConsistencyRationale,
    int BracketNumber,
    string BracketCrossCheckText,
    bool ScoreAlignsBracket);

/// <summary>The signals that produced a single axis band, as a terse ASCII signal line.</summary>
/// <param name="SignalText">Comma-separated signal values, ASCII only (no em/en dashes), paste-safe.</param>
public sealed record DeckScoreRationale(string SignalText);
