namespace DeckFlow.Core.Manabase;

/// <summary>
/// Calibrated cEDH keep-shape turn caps and bridge thresholds. cEDH games end around turn 5 on
/// average, so a first payoff that does not materialize until turn 5 or later is a mulligan, not a
/// workable keep. These constants pin the D-03 / CONTEXT §5 doctrine behind named values.
/// </summary>
public static class CedhMulliganCalibration
{
    /// <summary>
    /// Shape A explosive-start cap: the commander or a Payoff/TutorCombo card must be deployable by
    /// turn 3 counting in-hand acceleration, because cEDH mulligan doctrine treats later first payoffs
    /// as too slow for a median turn-5 game. See D-03 / CONTEXT §5.
    /// </summary>
    public const int TurnCapExplosive = 3;

    /// <summary>
    /// Shape B engine-start cap: an Engine-role card must be castable by turn 2 so the hand starts
    /// accruing value early enough for cEDH pacing. See D-03 / CONTEXT §5.
    /// </summary>
    public const int TurnCapEngine = 2;

    /// <summary>
    /// Representative-line cap: never surface a line whose first meaningful plan card arrives on turn
    /// 5 or later, because that is outside the cEDH keep window for a median turn-5 game. See D-03 /
    /// CONTEXT §5.
    /// </summary>
    public const int RepresentativeLineTurnCap = 4;

    /// <summary>
    /// Shape C interaction-bridge minimum: a bridge keep needs at least two interaction pieces in hand
    /// so the hand can defend itself while bridging to a plan. See D-03 / CONTEXT §5.
    /// </summary>
    public const int BridgeInteractionMin = 2;

    /// <summary>
    /// Shape C development minimum: a bridge keep also needs at least two development pieces
    /// (lands + ramp) so it can continue making mana development while the interaction buys time. See
    /// D-03 / CONTEXT §5.
    /// </summary>
    public const int BridgeDevelopmentMin = 2;

    /// <summary>
    /// Return the representative-line turn cap for the active analysis mode. Casual mode remains
    /// uncapped; cEDH uses <see cref="RepresentativeLineTurnCap"/>.
    /// </summary>
    /// <param name="mode">The active manabase analysis mode.</param>
    public static int GetRepresentativeLineTurnCap(ManabaseMode mode) =>
        mode == ManabaseMode.Cedh ? RepresentativeLineTurnCap : int.MaxValue;
}
