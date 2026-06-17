namespace DeckFlow.Studio;

/// <summary>
/// app-scoped, in-memory monthly cap override for the LLM spend ledger.
/// DeckFlow Studio is a single-operator local tool; this override applies to the entire
/// running app process, not to an individual circuit or user session.
/// The operator can raise the cap from the Harvest page for the current Studio session only.
/// Resets to the environment/default ($15.00) on Studio restart (D-03).
/// </summary>
public sealed class SessionCapOverride
{
    /// <summary>
    /// Operator-raised monthly cap in USD. When <see langword="null"/>, the ledger uses
    /// the <c>DECKFLOW_LLM_MONTHLY_CAP_USD</c> environment variable or the $15.00 default.
    /// </summary>
    public decimal? OverrideUsd { get; set; }
}
