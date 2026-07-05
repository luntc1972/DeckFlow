using DeckFlow.Core.Content;
using DeckFlow.Studio;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Spend-cap display + session cap override (D-02/D-03), extracted from the <c>Harvest</c> page
/// code-behind (Phase 82 SRP split). The page keeps the display fields (bound in markup) and the
/// input parsing failure path; this coordinator owns the ledger reads and the override write.
/// Behavior is identical to the prior inline implementation.
/// </summary>
public sealed class SpendCapCoordinator
{
    private readonly ILlmSpendLedger _spendLedger;
    private readonly SessionCapOverride _capOverride;

    /// <summary>Creates the coordinator with the LLM spend ledger and the session cap override.</summary>
    public SpendCapCoordinator(ILlmSpendLedger spendLedger, SessionCapOverride capOverride)
    {
        ArgumentNullException.ThrowIfNull(spendLedger);
        ArgumentNullException.ThrowIfNull(capOverride);
        _spendLedger = spendLedger;
        _capOverride = capOverride;
    }

    /// <summary>Gets the effective monthly cap in USD (D-02).</summary>
    public decimal GetMonthlyCapUsd() => _spendLedger.GetMonthlyCapUsd();

    /// <summary>Gets the current-month spend total in USD for the given <c>yyyy-MM</c> key.</summary>
    public async Task<decimal> GetMonthlyTotalAsync(string monthKey) => await _spendLedger.GetMonthlyTotalAsync(monthKey);

    /// <summary>
    /// Validates and applies a session cap override from the operator input (D-03). Returns
    /// <see langword="false"/> without mutating state on invalid or negative input, matching the
    /// prior inline validation exactly.
    /// </summary>
    public bool TryRaiseCap(string? rawInput, out decimal newCap)
    {
        if (!decimal.TryParse(
                rawInput,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out newCap)
            || newCap < 0m)
        {
            return false;
        }

        // Why: OverrideUsd write propagates to both the page display (GetMonthlyCapUsd) and
        // the orchestrator's WouldExceedCapAsync call because both resolve the same singleton (D-03).
        _capOverride.OverrideUsd = newCap;
        return true;
    }
}
