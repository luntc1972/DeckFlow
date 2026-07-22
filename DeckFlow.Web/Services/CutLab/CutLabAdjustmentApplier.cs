using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Pure immutable quantity-adjustment rules shared by the JSON and no-JS Cut Lab flows.</summary>
public static class CutLabAdjustmentApplier
{
    private const int MaxCopyDelta = 150;

    /// <summary>Applies one signed copy delta while preserving pool immutability and commander locks.</summary>
    /// <param name="state">Current working-session state.</param>
    /// <param name="cardName">Card receiving the adjustment.</param>
    /// <param name="delta">Signed quantity delta requested by the caller.</param>
    /// <param name="isAddedBasic">True when materializing a new basic not present in the imported pool.</param>
    /// <returns>A new state reflecting the merged quantity adjustment.</returns>
    public static CutLabState Apply(CutLabState state, string cardName, int delta, bool isAddedBasic)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);

        string normalizedCardName = CutLabCardNames.Normalize(cardName);
        CutLabPoolCard? poolCard = state.Pool.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase));
        if (poolCard is not null && poolCard.IsLocked)
        {
            throw new InvalidOperationException(CutLabMessages.NoChangeMessage);
        }

        if (isAddedBasic && !CutLabBasicLands.Contains(cardName))
        {
            throw new InvalidOperationException(CutLabMessages.NoChangeMessage);
        }

        bool canMaterializeAddedBasic = isAddedBasic || state.QuantityAdjustments.Any(adjustment =>
            adjustment.IsAddedBasic && string.Equals(adjustment.Name, cardName, StringComparison.OrdinalIgnoreCase));
        if (poolCard is null && !canMaterializeAddedBasic)
        {
            throw new InvalidOperationException(CutLabMessages.NoChangeMessage);
        }

        CutLabQuantityAdjustment? existingAdjustment = state.QuantityAdjustments
            .LastOrDefault(adjustment => string.Equals(adjustment.Name, cardName, StringComparison.OrdinalIgnoreCase));
        long existingDelta = existingAdjustment?.Delta ?? 0;
        long unclampedNetDelta = existingDelta + delta;
        int upperBound = Math.Min(CutLabLegality.LegalMax(cardName), MaxCopyDelta);
        int lowerBound = canMaterializeAddedBasic && poolCard is null ? 0 : -MaxCopyDelta;
        int netDelta = (int)Math.Clamp(unclampedNetDelta, lowerBound, upperBound);

        int baseQuantity = poolCard?.Quantity ?? 0;
        int resultingQuantity = Math.Clamp(baseQuantity + netDelta, 0, CutLabLegality.LegalMax(cardName));
        bool wouldExceedSingletonCap = resultingQuantity > 1;
        if ((delta > 0 || wouldExceedSingletonCap) && !CutLabLegality.IsLegalMultiple(cardName))
        {
            throw new InvalidOperationException(CutLabMessages.NoChangeMessage);
        }

        bool materializedAddedBasic = canMaterializeAddedBasic && poolCard is null;
        IReadOnlyList<CutLabQuantityAdjustment> nextAdjustments = state.QuantityAdjustments
            .Where(adjustment => !string.Equals(adjustment.Name, cardName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (netDelta != 0)
        {
            nextAdjustments = nextAdjustments
                .Concat(
                [
                    new CutLabQuantityAdjustment
                    {
                        Name = poolCard?.Name ?? cardName,
                        Delta = netDelta,
                        IsAddedBasic = materializedAddedBasic,
                    },
                ])
                .ToArray();
        }

        return CutLabLockRules.EnforceCommanderLock(state with
        {
            QuantityAdjustments = nextAdjustments,
        });
    }
}
