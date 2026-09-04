namespace DeckFlow.Core.Analysis;

/// <summary>
/// The twelve fixed generic deck-plan strategies a player can check for Cut Lab's plan profile.
/// Declaration order is stable and is the canonical display order.
/// </summary>
public enum DeckPlanStrategy
{
    /// <summary>Wins by assembling a specific card combination for an infinite or game-ending effect.</summary>
    Combo,

    /// <summary>Wins by repeatedly sacrificing creatures for incremental value and life loss.</summary>
    Aristocrats,

    /// <summary>Wins by stacking equipment and auras onto a single evasive threat for commander damage.</summary>
    Voltron,

    /// <summary>Wins by flooding the board with token creatures and anthem effects.</summary>
    Tokens,

    /// <summary>Wins through volume of instants and sorceries, triggering prowess and magecraft payoffs.</summary>
    Spellslinger,

    /// <summary>Wins by taxing and locking down opponents' resources while the deck operates around the restriction.</summary>
    Stax,

    /// <summary>Wins by discarding or milling large threats into the graveyard, then reanimating them cheaply.</summary>
    Reanimator,

    /// <summary>Wins by triggering landfall payoffs off extra land drops and land-matters synergies.</summary>
    Landfall,

    /// <summary>Wins by accumulating life total and converting it into payoffs.</summary>
    Lifegain,

    /// <summary>Wins by growing creatures with +1/+1 counters and proliferate effects.</summary>
    PlusOneCounters,

    /// <summary>Wins through repeated combat damage, extra combats, and haste-enabled beaters.</summary>
    Combat,

    /// <summary>Wins by controlling the board with counterspells, removal, and board wipes until a late-game payoff closes the game.</summary>
    Control,
}

/// <summary>
/// One catalog row: a fixed generic strategy, its stable slug, display copy, and the free-text
/// category needles that stand in for archetype membership when no EDHREC theme data is available.
/// </summary>
/// <param name="Strategy">The fixed strategy this row describes.</param>
/// <param name="Slug">Stable, URL/JSON-safe identifier resolved case-insensitively.</param>
/// <param name="DisplayName">User-facing name shown on the checkbox.</param>
/// <param name="Definition">One-line plain-language definition of the strategy.</param>
/// <param name="Consequence">One-line mechanical consequence of checking this strategy.</param>
/// <param name="CategoryNeedles">
/// Free-text substrings matched case-insensitively against a card's crowd-sourced category tags.
/// </param>
public sealed record DeckPlanStrategyEntry(
    DeckPlanStrategy Strategy,
    string Slug,
    string DisplayName,
    string Definition,
    string Consequence,
    IReadOnlyList<string> CategoryNeedles);

/// <summary>
/// The fixed twelve-entry generic strategy catalog and its role-proxy category matcher. Dependency-
/// free — <c>DeckFlow.Core</c> carries zero project references, so this type must not reach into
/// <c>DeckFlow.Web</c>.
/// </summary>
public static class DeckPlanStrategyCatalog
{
    /// <summary>
    /// The twelve fixed generic strategies, in stable declaration order matching
    /// <see cref="DeckPlanStrategy"/>.
    /// </summary>
    public static IReadOnlyList<DeckPlanStrategyEntry> Entries { get; } =
    [
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Combo,
            "combo",
            "Combo",
            "Wins by assembling a specific card combination that generates an infinite or game-ending effect.",
            "Protects tutors and combo pieces, and raises the protection and wincons floors.",
            ["combo", "tutor"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Aristocrats,
            "aristocrats",
            "Aristocrats",
            "Wins by repeatedly sacrificing creatures for incremental value and life loss.",
            "Protects sacrifice outlets and drains, and raises the engines and payoffs floors.",
            ["sacrifice", "sac outlet", "aristocrat", "drain", "recursion"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Voltron,
            "voltron",
            "Voltron",
            "Wins by stacking equipment and auras onto a single evasive threat for commander damage.",
            "Protects equipment, auras, and the primary threat, and raises the protection floor.",
            ["equipment", "aura", "voltron", "commander damage"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Tokens,
            "tokens",
            "Tokens",
            "Wins by flooding the board with token creatures and anthem effects.",
            "Protects token generators and anthems, and raises the payoffs floor.",
            ["token", "anthem", "go wide", "populate"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Spellslinger,
            "spellslinger",
            "Spellslinger",
            "Wins through volume of instants and sorceries, triggering prowess and magecraft payoffs.",
            "Protects cheap spells and payoffs, and raises the draw floor.",
            ["spellslinger", "prowess", "storm", "magecraft", "instant", "sorcery"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Stax,
            "stax",
            "Stax",
            "Wins by taxing and locking down opponents' resources while the deck operates around the restriction.",
            "Protects tax effects and hatebears, and raises the interaction floor.",
            ["stax", "tax", "hatebear", "prison", "lock"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Reanimator,
            "reanimator",
            "Reanimator",
            "Wins by discarding or milling large threats into the graveyard, then reanimating them cheaply.",
            "Protects reanimation spells and self-mill enablers, and raises the engine floor.",
            ["reanimat", "graveyard", "self-mill", "discard"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Landfall,
            "landfall",
            "Landfall",
            "Wins by triggering landfall payoffs off extra land drops and land-matters synergies.",
            "Protects extra-land effects and landfall payoffs, and raises the ramp floor.",
            ["landfall", "lands matter", "extra land"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Lifegain,
            "lifegain",
            "Lifegain",
            "Wins by accumulating life total and converting it into payoffs.",
            "Protects lifegain sources and payoffs, and raises the payoffs floor.",
            ["lifegain", "life gain", "lifelink"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.PlusOneCounters,
            "counters",
            "+1/+1 Counters",
            "Wins by growing creatures with +1/+1 counters and proliferate effects.",
            "Protects counter sources and proliferate effects, and raises the payoffs floor.",
            ["+1/+1", "counters", "proliferate"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Combat,
            "combat",
            "Combat",
            "Wins through repeated combat damage, extra combats, and haste-enabled beaters.",
            "Protects combat enablers and wincon creatures, and raises the wincon-creature floor.",
            ["combat", "extra combat", "attack", "haste", "beater", "battlecruiser"]),
        new DeckPlanStrategyEntry(
            DeckPlanStrategy.Control,
            "control",
            "Control",
            "Wins by controlling the board with counterspells, removal, and board wipes until a late-game payoff closes the game.",
            "Protects interaction and board wipes, and raises the interaction floor.",
            ["control", "counterspell", "removal", "wipe", "board wipe"]),
    ];

    /// <summary>Resolves a catalog entry by slug, case-insensitively.</summary>
    /// <param name="slug">The slug to resolve.</param>
    /// <param name="entry">The resolved entry, or the default value when not found.</param>
    /// <returns><see langword="true"/> when the slug resolves to a catalog entry.</returns>
    public static bool TryGetBySlug(string slug, out DeckPlanStrategyEntry entry)
    {
        ArgumentNullException.ThrowIfNull(slug);

        foreach (var candidate in Entries)
        {
            if (string.Equals(candidate.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null!;
        return false;
    }

    /// <summary>
    /// Determines whether any of a card's free-text category tags stand in for <paramref name="entry"/>'s
    /// strategy via case-insensitive substring matching. Archidekt category tags are user-typed free
    /// text, not a controlled vocabulary, so this deliberately never uses exact string equality.
    /// </summary>
    /// <param name="entry">The catalog entry whose role-proxy needles are checked.</param>
    /// <param name="categories">A card's crowd-sourced free-text category tags (may be empty).</param>
    /// <returns><see langword="true"/> when at least one category matches at least one needle.</returns>
    public static bool MatchesCategories(DeckPlanStrategyEntry entry, IReadOnlyList<string> categories)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(categories);

        foreach (var category in categories)
        {
            if (string.IsNullOrEmpty(category))
            {
                continue;
            }

            foreach (var needle in entry.CategoryNeedles)
            {
                if (!category.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Why: spike 002 found that "counterspell" contains the literal substring "counters"
                // (c-o-u-n-t-e-r-s-p-e-l-l), which would otherwise wrongly satisfy the PlusOneCounters
                // "counters" needle. A counterspell/countermagic tag must never count toward +1/+1
                // counters, even though a real "Counters" or "+1/+1 Counters" tag still must.
                if (entry.Strategy == DeckPlanStrategy.PlusOneCounters
                    && string.Equals(needle, "counters", StringComparison.OrdinalIgnoreCase)
                    && (category.Contains("counterspell", StringComparison.OrdinalIgnoreCase)
                        || category.Contains("countermagic", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }
}
