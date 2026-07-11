namespace DeckFlow.Core.Manabase;

/// <summary>
/// Maps a Scryfall card payload (<see cref="ScryfallCardData"/>) to a <see cref="CardFact"/>
/// for the mana-base classifier. Pure mapping — no HTTP. Handles multi-faced cards: the
/// front face drives the cast cost/type, oracle text is joined across faces (so the back's
/// "enters tapped" / land type is visible), and a land on any face sets
/// <see cref="CardFact.HasLandFace"/>.
/// </summary>
public static class ScryfallCardFactMapper
{
    /// <summary>Map one Scryfall card plus its deck quantity to a <see cref="CardFact"/>.</summary>
    /// <param name="card">The Scryfall payload.</param>
    /// <param name="quantity">Copies of this card in the deck.</param>
    /// <param name="isCommander">True if the card is in the command zone.</param>
    public static CardFact ToCardFact(ScryfallCardData card, int quantity, bool isCommander = false)
    {
        ArgumentNullException.ThrowIfNull(card);

        ScryfallFaceData? front = card.CardFaces is { Count: > 0 } ? card.CardFaces[0] : null;

        string typeLine = front?.TypeLine ?? card.TypeLine ?? string.Empty;
        // Prefer the front face's printed cost for the castable side; fall back to card level.
        string? manaCost = !string.IsNullOrWhiteSpace(front?.ManaCost) ? front!.ManaCost : card.ManaCost;

        // For multi-faced cards (split/aftermath/MDFC), Scryfall's root cmc is the COMBINED
        // value (Commit // Memory = 10), but we cast the front face ({3}{U} = 4). Derive the
        // value from the chosen front cost so the on-curve turn is right; single-faced cards
        // keep Scryfall's authoritative cmc.
        double manaValue = front is not null ? ManaCostParser.Parse(manaCost).ManaValue : card.Cmc;

        return new CardFact
        {
            Name = card.Name,
            Quantity = quantity,
            ManaCost = manaCost,
            ManaValue = manaValue,
            TypeLine = typeLine,
            OracleText = JoinOracleText(card),
            // Front-face text only — so a front-face permanent with a one-shot mana adventure/back
            // face does not read as repeatable ramp (MQ-03). Falls back to card-level for single-face.
            FrontFaceOracleText = front?.OracleText ?? card.OracleText,
            LandFaceOracleText = LandFaceOracleText(card),
            ProducedMana = card.ProducedMana ?? Array.Empty<string>(),
            Rarity = card.Rarity,
            Layout = card.Layout,
            HasLandFace = HasLandFace(card),
            IsCommander = isCommander,
            ManaAmount = ManaProductionAmount.Parse(JoinOracleText(card)),
            Power = ParseFixedPower(front?.Power ?? card.Power),
        };
    }

    // Parse a Scryfall printed power into a fixed int, or null when absent or variable ("*", "1+*",
    // "X"). Only fixed numeric power feeds the "greatest power among creatures" cost reducer; a
    // variable-power creature (a *goyf) contributes nothing, which is the conservative read.
    private static int? ParseFixedPower(string? power) =>
        int.TryParse(power, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int value)
            ? value
            : null;

    /// <summary>Map a deck's worth of cards, pairing each payload with its quantity.</summary>
    public static IReadOnlyList<CardFact> ToCardFacts(IEnumerable<DeckCardEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.Select(e => ToCardFact(e.Card, e.Quantity, e.IsCommander)).ToList();
    }

    private static string JoinOracleText(ScryfallCardData card)
    {
        if (card.CardFaces is { Count: > 0 } faces)
        {
            IEnumerable<string> parts = faces
                .Select(f => f.OracleText)
                .Where(t => !string.IsNullOrWhiteSpace(t))!;
            string joined = string.Join("\n", parts);
            return joined.Length > 0 ? joined : card.OracleText ?? string.Empty;
        }

        return card.OracleText ?? string.Empty;
    }

    private static bool HasLandFace(ScryfallCardData card)
    {
        if (card.CardFaces is { Count: > 0 } faces)
        {
            // Only a face that is playable AS a land from hand counts (efficacy R2 M6). The FRONT
            // face always does (Westvale Abbey, or a Land // Land MDFC). A BACK-face land is
            // playable from hand only on a modal DFC (Malakir Rebirth // Malakir Mire); on a
            // transform card (Search for Azcanta // Azcanta) the land back is reached only by
            // flipping the front — not castable from hand — so it earns no color-source / land
            // credit. The joined card-level type_line ("Enchantment // Land") is intentionally NOT
            // used here because it leaks a transform back into the land count.
            bool frontIsLand = ContainsLand(faces[0].TypeLine);
            bool mdfcBackIsLand = faces.Count > 1
                && string.Equals(card.Layout, "modal_dfc", StringComparison.OrdinalIgnoreCase)
                && ContainsLand(faces[1].TypeLine);
            return frontIsLand || mdfcBackIsLand;
        }

        return ContainsLand(card.TypeLine);
    }

    private static string? LandFaceOracleText(ScryfallCardData card)
    {
        if (card.CardFaces is { Count: > 0 } faces)
        {
            if (ContainsLand(faces[0].TypeLine))
            {
                return faces[0].OracleText;
            }

            if (faces.Count > 1 && ContainsLand(faces[1].TypeLine))
            {
                return faces[1].OracleText;
            }

            return null;
        }

        return ContainsLand(card.TypeLine) ? card.OracleText : null;
    }

    private static bool ContainsLand(string? typeLine) =>
        typeLine is not null && typeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A Scryfall card paired with its quantity and command-zone status in a deck.</summary>
public sealed record DeckCardEntry
{
    /// <summary>The resolved Scryfall card payload.</summary>
    public required ScryfallCardData Card { get; init; }

    /// <summary>Copies in the deck.</summary>
    public required int Quantity { get; init; }

    /// <summary>True if the card is the commander / in the command zone.</summary>
    public bool IsCommander { get; init; }
}
