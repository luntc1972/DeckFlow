namespace DeckFlow.Core.Analysis;

/// <summary>
/// Aggregates deck cards into card-backed interaction buckets with review-tier weak-signal matches.
/// </summary>
public static class InteractionAuditAggregator
{
    /// <summary>
    /// Computes an interaction audit for the supplied cards.
    /// </summary>
    /// <param name="cards">Cards to audit.</param>
    public static InteractionAudit Compute(IEnumerable<InteractionCardInput> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        // Only the targeted-removal bucket has a review tier (pseudo-removal + self-target land here);
        // the other four buckets are confident-only, so they carry an empty review list.
        var targetedRemovalConfident = new List<InteractionCard>();
        var targetedRemovalReview = new List<InteractionCard>();
        var boardWipesConfident = new List<InteractionCard>();
        var counterspellsConfident = new List<InteractionCard>();
        var protectionRecursionConfident = new List<InteractionCard>();
        var staxTaxationConfident = new List<InteractionCard>();

        foreach (var card in cards)
        {
            var quantity = card.Quantity;
            if (quantity <= 0)
            {
                continue;
            }

            var name = card.Name ?? string.Empty;
            var typeLine = card.TypeLine ?? string.Empty;
            var oracleText = card.OracleText ?? string.Empty;
            var interactionCard = new InteractionCard(name, quantity);

            if (DeckStatClassifier.IsBoardWipeCard(oracleText))
            {
                boardWipesConfident.Add(interactionCard);
            }
            else if (DeckStatClassifier.IsCounterspellCard(oracleText))
            {
                counterspellsConfident.Add(interactionCard);
            }
            else if (DeckStatClassifier.IsSelfTargetedInteraction(typeLine, oracleText)
                || DeckStatClassifier.IsPseudoRemovalCard(typeLine, oracleText))
            {
                // Weak/temporary answers (self-target, bounce, tuck, temporary exile-and-return) route to
                // the review tier. Checked BEFORE IsTargetedRemovalCard so temporary exile is not read as hard removal.
                targetedRemovalReview.Add(interactionCard);
            }
            else if (DeckStatClassifier.IsTargetedRemovalCard(typeLine, oracleText))
            {
                targetedRemovalConfident.Add(interactionCard);
            }

            if (DeckStatClassifier.IsRecursionCard(oracleText) || DeckStatClassifier.IsProtectionCard(name, oracleText))
            {
                protectionRecursionConfident.Add(interactionCard);
            }

            if (StaxProtectionCatalog.IsStax(name))
            {
                staxTaxationConfident.Add(interactionCard);
            }
        }

        var coverageGaps = new List<string>();
        if (counterspellsConfident.Count == 0)
        {
            coverageGaps.Add("0 counterspells");
        }

        if (boardWipesConfident.Count == 0)
        {
            coverageGaps.Add("no board wipes");
        }

        if (targetedRemovalConfident.Count == 0)
        {
            coverageGaps.Add("no targeted removal");
        }

        if (protectionRecursionConfident.Count == 0)
        {
            coverageGaps.Add("no protection or recursion (possible graveyard-hate / protection gap)");
        }

        if (staxTaxationConfident.Count == 0)
        {
            coverageGaps.Add("no stax or taxation");
        }

        return new InteractionAudit(
            new InteractionBucketResult(targetedRemovalConfident, targetedRemovalReview),
            new InteractionBucketResult(boardWipesConfident, Array.Empty<InteractionCard>()),
            new InteractionBucketResult(counterspellsConfident, Array.Empty<InteractionCard>()),
            new InteractionBucketResult(protectionRecursionConfident, Array.Empty<InteractionCard>()),
            new InteractionBucketResult(staxTaxationConfident, Array.Empty<InteractionCard>()),
            coverageGaps);
    }
}
