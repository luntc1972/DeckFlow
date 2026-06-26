namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// Human-readable descriptions for the known runtime feature flags, surfaced on the
/// /Admin/Flags page so an operator can see what each toggle does without reading code.
/// Keep this in sync with the seed list in <see cref="FeatureFlagStore"/>; the
/// <c>FeatureFlagCatalogTests</c> guard fails if a seeded key has no description here.
/// Unknown keys (e.g. a flag added to the DB out-of-band) degrade gracefully to an empty
/// string via <see cref="Describe"/>.
/// </summary>
public static class FeatureFlagCatalog
{
    /// <summary>Flag key (dotted namespace) → one-line operator description.</summary>
    public static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scryfall.tagger.enabled"] =
                "Scrape Scryfall Tagger to enrich category suggestions. Off falls back to the other suggestion sources.",
            ["page.help.enabled"] =
                "Show the in-app Help section and its navigation link.",
            ["harvest.cron.enabled"] =
                "Run the scheduled background content-harvest job. Off pauses automated harvesting (manual harvest still works).",
            ["feature.categories.enabled"] =
                "Enable the Commander Categories page and the category-suggestion tools.",
            ["content.kb.enabled"] =
                "Serve the browsable Content Knowledge Base (creator videos) on the public site.",
            ["feature.manabase.enabled"] =
                "Enable the Mana Base analyzer tool and its navigation link.",
            ["analysis.reference.full-oracle-text"] =
                "Include full Oracle rules text for reference cards in the deck-analysis prompt (larger but more precise).",
            ["analysis.reference.deck-stats"] =
                "Append computed deck statistics to the deck-analysis prompt.",
            ["manabase.source-mana-quantity"] =
                "Count how much mana each source actually makes (Sol Ring makes 2, Gilded Lotus 3) when judging whether spells are affordable on curve. The colored-source counts behind the land recommendation stay the same.",
            ["manabase.ramp-credit-v2"] =
                "Only let repeatable ramp and real card draw lower the recommended land count. One-shot rituals and Treasure tokens no longer make a deck look like it needs fewer lands than it really does.",
            ["manabase.color-aware-mulligan"] =
                "When simulating opening hands, also mulligan hands that are color-screwed: a deck of two or more colors wants at least two of its colors among its starting lands. Mono-color decks are unaffected.",
            ["manabase.p1-grace-strict"] =
                "Require one-mana (turn-1) spells to be castable exactly on turn 1, with no one-turn-late forgiveness. Makes the score stricter for decks that can be color-screwed out of their one-drops. Spells on turn 2 and later keep the usual one-turn grace. Off by default.",
            ["manabase.land-ramp-sim"] =
                "Treat repeatable land ramp (Cultivate, Rampant Growth, and similar) as putting its fetched land onto the battlefield during the simulation, so expensive payoff spells in ramp-heavy decks are not under-rated.",
            ["manabase.health-band-castability"] =
                "Let the deck's weakest color affect the overall health rating: if that color's hardest spell is cast below the target (80% Casual, 88% cEDH), it counts as a color problem and can drop the verdict from Solid to Workable. Off by default until the regression check passes.",
            ["manabase.health-band-headline-floor"] =
                "Allow a deck with a strong average on-curve score to be nudged up from 'Needs work' to 'Workable', but only when it has a single minor color weakness, that color still casts acceptably, and there are no serious mana shortfalls. On by default.",
        };

    /// <summary>
    /// Returns the operator description for <paramref name="key"/>, or an empty string when the
    /// key is not catalogued (so the view renders a blank cell rather than throwing).
    /// </summary>
    /// <param name="key">Dotted-namespace flag key.</param>
    public static string Describe(string key) =>
        Descriptions.TryGetValue(key, out string? description) ? description : string.Empty;
}
