namespace DeckFlow.Core.Analysis;

/// <summary>
/// One card's contribution to an interaction audit bucket.
/// </summary>
/// <param name="Quantity">Number of copies of this card in the deck slot being audited.</param>
/// <param name="Name">Card name.</param>
/// <param name="TypeLine">Card type line.</param>
/// <param name="OracleText">Normalized oracle text.</param>
/// <param name="ManaCost">Mana cost string.</param>
public readonly record struct InteractionCardInput(int Quantity, string Name, string TypeLine, string OracleText, string ManaCost);

/// <summary>
/// A card name and quantity captured in an interaction audit bucket.
/// </summary>
/// <param name="Name">Card name.</param>
/// <param name="Quantity">Number of copies of this card.</param>
public readonly record struct InteractionCard(string Name, int Quantity);

/// <summary>
/// Confident and review-tier cards for one interaction bucket.
/// </summary>
/// <param name="Confident">Cards confidently classified into the bucket.</param>
/// <param name="Review">Borderline cards that should be reviewed by the AI.</param>
public sealed record InteractionBucketResult(IReadOnlyList<InteractionCard> Confident, IReadOnlyList<InteractionCard> Review);

/// <summary>
/// Card-backed interaction audit buckets plus coverage-gap advisories.
/// </summary>
/// <param name="TargetedRemoval">Targeted removal cards, with pseudo/self-target effects in review.</param>
/// <param name="BoardWipes">Board wipe cards.</param>
/// <param name="Counterspells">Counterspell cards.</param>
/// <param name="ProtectionRecursion">Protection and recursion cards.</param>
/// <param name="StaxTaxation">Stax and taxation cards.</param>
/// <param name="CoverageGaps">Advisory strings for buckets with no confident cards.</param>
public sealed record InteractionAudit(
    InteractionBucketResult TargetedRemoval,
    InteractionBucketResult BoardWipes,
    InteractionBucketResult Counterspells,
    InteractionBucketResult ProtectionRecursion,
    InteractionBucketResult StaxTaxation,
    IReadOnlyList<string> CoverageGaps);
