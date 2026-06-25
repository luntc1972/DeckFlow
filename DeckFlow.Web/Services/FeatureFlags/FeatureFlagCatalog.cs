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
                "MQ-02: model how MUCH mana each source makes (Sol Ring = 2, Gilded Lotus = 3) on the affordability/curve side. Karsten color counts are untouched.",
            ["manabase.ramp-credit-v2"] =
                "MQ-03: narrow the land-target ramp credit to repeatable ramp and true card draw only — one-shot rituals and Treasure-makers no longer soften the land count.",
            ["manabase.color-aware-mulligan"] =
                "MQ-05: the castability simulation's London mulligan also mulligans color-screwed hands (a 2+ color deck wants 2 colors in its opening lands). Mono-color decks are unchanged.",
            ["manabase.land-ramp-sim"] =
                "MQ-03 (70-03b): repeatable land-ramp (Cultivate, Rampant Growth) puts its fetched land into the simulation as persistent colorless mana, so payoffs in ramp decks are not under-rated.",
            ["manabase.health-band-castability"] =
                "MQ-health-band: the composite-weakest color's worst-spell cast % feeds the health-band verdict. A color that is composite-worst and casts its worst spell below the mode threshold (80% Casual / 88% cEDH) counts as a color issue, tipping Solid→Workable. Seeded OFF; promote after regression-guard passes.",
            ["manabase.health-band-headline-floor"] =
                "MQ-health-band headline floor: a strong avg-on-curve result can narrowly promote a land-short Needs work verdict to Workable when exactly one soft color issue exists, worst-color castability clears the floor, and no hard-fail color/broad under-support signal is present. Seeded ON.",
        };

    /// <summary>
    /// Returns the operator description for <paramref name="key"/>, or an empty string when the
    /// key is not catalogued (so the view renders a blank cell rather than throwing).
    /// </summary>
    /// <param name="key">Dotted-namespace flag key.</param>
    public static string Describe(string key) =>
        Descriptions.TryGetValue(key, out string? description) ? description : string.Empty;
}
