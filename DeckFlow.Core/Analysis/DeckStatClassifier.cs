namespace DeckFlow.Core.Analysis;

/// <summary>
/// Pure static deck-stat classifiers used to tally role counts (ramp, draw, interaction,
/// board wipes, recursion, closing power) in a deck's mainboard.  These are pure CPU domain
/// logic; their inputs (typeLine, oracleText) come from Scryfall card data.
/// </summary>
public static class DeckStatClassifier
{
    /// <summary>
    /// Returns <see langword="true"/> when the card is a ramp source: a land, an explicit
    /// mana-add effect, a land-search, or a Treasure producer.
    /// </summary>
    /// <param name="typeLine">Card type line (e.g. "Artifact — Treasure").</param>
    /// <param name="oracleText">Normalized oracle text.</param>
    public static bool IsRampCard(string typeLine, string oracleText)
        => typeLine.Contains("Land", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("add one mana", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("add two mana", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("search your library for a basic land", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("search your library for up to", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("land", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("Treasure token", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("create a Treasure", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card draws cards or creates clue tokens.
    /// </summary>
    /// <param name="oracleText">Normalized oracle text.</param>
    public static bool IsDrawCard(string oracleText)
        => oracleText.Contains("draw a card", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("draw two cards", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("draw X cards", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("investigate", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("connive", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card is an interaction piece: an instant, or a
    /// spell that destroys, exiles, counters, bounces, or fights.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Normalized oracle text.</param>
    public static bool IsInteractionCard(string typeLine, string oracleText)
        => typeLine.Contains("Instant", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("destroy target", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("exile target", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("counter target", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("return target spell", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("fight target", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card clears multiple permanents at once.
    /// </summary>
    /// <param name="oracleText">Normalized oracle text.</param>
    public static bool IsBoardWipeCard(string oracleText)
        => oracleText.Contains("destroy all creatures", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("destroy all artifacts", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("destroy all enchantments", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("each creature", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("gets -", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("exile all", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card returns cards from the graveyard.
    /// </summary>
    /// <param name="oracleText">Normalized oracle text.</param>
    public static bool IsRecursionCard(string oracleText)
        => oracleText.Contains("return target card from your graveyard", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("return all land cards from your graveyard", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("return target permanent card from your graveyard", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("reanimate", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("from your graveyard to your hand", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card is a win condition, extra-turn effect,
    /// damage doubler, or combat-draw engine.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Normalized oracle text.</param>
    public static bool IsClosingPowerCard(string typeLine, string oracleText)
        => oracleText.Contains("each opponent loses", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("you win the game", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("extra turn", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("double strike", StringComparison.OrdinalIgnoreCase)
            || typeLine.Contains("Craterhoof", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("combat damage to a player", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("draw", StringComparison.OrdinalIgnoreCase)
            || oracleText.Contains("whenever this creature attacks", StringComparison.OrdinalIgnoreCase) && oracleText.Contains("+X/+X", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card tutors a card from the library, excluding
    /// land-fetch ramp (basic-land search, generic land-card search, or land onto the battlefield).
    /// </summary>
    /// <param name="oracleText">Normalized oracle text.</param>
    public static bool IsTutorCard(string oracleText)
        => oracleText.Contains("search your library for", StringComparison.OrdinalIgnoreCase)
            && !oracleText.Contains("basic land", StringComparison.OrdinalIgnoreCase)
            // Exclude land-fetch ramp ("a land card") but NOT nonland tutors: strip "nonland card"
            // first so its trailing "land card" substring does not trip the land-fetch exclusion.
            && !oracleText.Replace("nonland card", " ", StringComparison.OrdinalIgnoreCase)
                .Contains("land card", StringComparison.OrdinalIgnoreCase)
            && !oracleText.Contains("land onto the battlefield", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> when the card is fast mana: a zero-mana-value artifact that
    /// produces mana (e.g. Mana Crypt, Jeweled Lotus). Mana rocks with MV &gt;= 1 (e.g. Sol Ring) are excluded.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Normalized oracle text.</param>
    /// <param name="manaCost">Mana cost string (e.g. "{1}"); blank for zero-cost artifacts.</param>
    public static bool IsFastManaCard(string typeLine, string oracleText, string manaCost)
        => DeckStatAggregator.EstimateManaValue(manaCost) == 0
            && typeLine.Contains("Artifact", StringComparison.OrdinalIgnoreCase)
            && (oracleText.Contains("{T}: Add", StringComparison.OrdinalIgnoreCase)
                || oracleText.Contains("Add {", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns <see langword="true"/> when the card is a ramp or draw piece with estimated mana value
    /// of 2 or less — the early acceleration/consistency signal the multi-axis scorer consumes.
    /// </summary>
    /// <param name="typeLine">Card type line.</param>
    /// <param name="oracleText">Normalized oracle text.</param>
    /// <param name="manaCost">Mana cost string (e.g. "{1}{U}").</param>
    public static bool IsRampOrDrawUnderThreeMv(string typeLine, string oracleText, string manaCost)
        => DeckStatAggregator.EstimateManaValue(manaCost) <= 2
            && (IsRampCard(typeLine, oracleText) || IsDrawCard(oracleText));

    /// <summary>
    /// Returns <see langword="true"/> when the card counters a target spell. Ability counters
    /// (e.g. "counter target activated or triggered ability") are excluded.
    /// </summary>
    /// <param name="oracleText">Normalized oracle text.</param>
    public static bool IsCounterspellCard(string oracleText)
        => oracleText.Contains("counter target spell", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a single mana symbol token (the text between <c>{</c> and <c>}</c>) into its
    /// converted mana cost contribution.  Numeric tokens return their integer value; X returns 0;
    /// hybrid symbols (containing '/') return 1; everything else returns 1.
    /// </summary>
    /// <param name="token">Token text without braces (e.g. "3", "X", "W/U").</param>
    public static int ParseManaToken(string token)
    {
        if (int.TryParse(token, out var numeric))
        {
            return numeric;
        }

        if (token.Contains('/', StringComparison.Ordinal))
        {
            return 1;
        }

        return token.Equals("X", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }
}
