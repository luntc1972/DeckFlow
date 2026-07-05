using System.Net;
using System.Text.RegularExpressions;
using DeckFlow.Core.Bracket;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Bracket;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Shared deterministic fixture decks, Scryfall override responses, and service-construction seams
/// for the four packet-service byte-identity suites (PKTSVC-04, plan 83-01). Every helper here reuses
/// the SAME test-seam pattern already proven by <c>DeckAnalysisPacketServiceTests.CreateService</c> /
/// <c>DeckComparisonServiceTests.CreateService</c> / <c>MetaGapServiceTests.CreateService</c> /
/// <c>DeckPrimerPacketServiceTests.CreateService</c> — a real <see cref="ScryfallCardResolver"/> (or
/// real deck-entry override delegates for Primer) with deterministic fixture responses, no live HTTP,
/// and the REAL per-AI prompt-variant registries (ChatGPT/Claude/Gemini) so ADR-0001's per-variant
/// prose is exercised exactly as production wires it.
///
/// This file introduces NO behavior change to any production service — it only builds fixtures and
/// captures golden output from the CURRENT (unrefactored) code, establishing the baseline every
/// Wave-2 migration in this phase is gated against.
/// </summary>
internal static class PacketByteIdentityFixtures
{
    public const string ChatGpt = "ChatGPT";
    public const string Claude = "Claude";
    public const string Gemini = "Gemini";

    /// <summary>The 3 AI platforms every byte-identity suite sweeps (mirrors ResultContractTests.cs:25).</summary>
    public static readonly string[] AiPlatforms = [ChatGpt, Claude, Gemini];

    // ---------------------------------------------------------------------------------------
    // Shared Scryfall card catalog. Used across Analysis/Comparison/MetaGap collection+search
    // fakes below. The catalog is intentionally returned IN FULL for every "collection" lookup
    // (mirroring DeckAnalysisPacketServiceTests.CreateCollectionResponse) — cards that exactly
    // match by Name resolve via the collection path; a submitted name that does NOT exactly
    // match any catalog Name (e.g. a single-slash DFC submission against a double-slash
    // canonical entry) naturally falls through to the fallback search/named path, which is
    // exactly the collection-miss/fallback behavior item 2 of the plan's path-coverage
    // requirements needs to lock down.
    // ---------------------------------------------------------------------------------------

    public static IReadOnlyList<ScryfallCard> CardCatalog() =>
    [
        new("Kraum, Ludevic's Opus", "{3}{U}{R}", "Legendary Creature — Zombie Horror",
            "Flying, haste\nWhenever an opponent casts their second spell each turn, draw a card.",
            "4", "4", ["Flying", "Haste"], ["U", "R"], "c16", "Commander 2016", "39"),
        new("Passionate Archaeologist", "{2}{R}", "Legendary Enchantment — Background",
            "Commander creatures you own have \"Whenever you cast a spell from exile, this creature deals damage equal to that spell's mana value to target opponent.\"",
            null, null, ["Background"], ["R"], "clb", "Commander Legends: Battle for Baldur's Gate", "189"),
        new("Command Tower", null, "Land",
            "{T}: Add one mana of any color in your commander's color identity.",
            null, null, [], [], "c16", "Commander 2016", "285"),
        new("Arcane Signet", "{2}", "Artifact",
            "{T}: Add one mana of any color in your commander's color identity.",
            null, null, null, [], "eld", "Throne of Eldraine", "331"),
        new("Sol Ring", "{1}", "Artifact", "{T}: Add {C}{C}.",
            null, null, null, [], "c16", "Commander 2016", "272"),
        new("Ponder", "{U}", "Sorcery",
            "Look at the top three cards of your library, then put them back in any order. You may shuffle. Draw a card.",
            null, null, [], ["U"], "c21", "Commander 2021", "118"),
        new("Swords to Plowshares", "{W}", "Instant",
            "Exile target creature. Its controller gains life equal to its power.",
            null, null, null, ["W"], null, null, null),
        new(
            "Blex, Vexing Pest // Search for Blex",
            null,
            "Legendary Creature — Pest // Sorcery",
            null,
            null,
            null,
            ["Pest"],
            ["B", "G"],
            "tsr",
            "Tales of Middle-earth",
            "96",
            [
                new ScryfallCardFace(
                    "Blex, Vexing Pest",
                    "{2}{B}{G}",
                    "Legendary Creature — Pest",
                    "Other Pests, Bats, Insects, Snakes, and Spiders you control get +1/+1.",
                    "3",
                    "2"),
                new ScryfallCardFace(
                    "Search for Blex",
                    "{X}{2}{B/G}{B/G}",
                    "Sorcery",
                    "Look at the top five cards of your library. You may reveal any number of creature cards with mana value X or less from among them and put the revealed cards into your hand. Put the rest on the bottom of your library in a random order. You lose 3 life.",
                    null,
                    null)
            ]),
        new("Atraxa, Praetors' Voice", "{G}{W}{U}{B}", "Legendary Creature — Phyrexian Angel Horror",
            "Flying, vigilance, deathtouch, lifelink. At the beginning of your end step, proliferate.",
            "4", "4", ["Flying", "Vigilance", "Deathtouch", "Lifelink", "Proliferate"], ["G", "W", "U", "B"], null, null, null),
        new("Counterspell", "{U}{U}", "Instant", "Counter target spell.", null, null, [], ["U"], null, null, null),
        new("Wrath of God", "{2}{W}{W}", "Sorcery", "Destroy all creatures. They can't be regenerated.",
            null, null, [], ["W"], null, null, null),
        new("Perfect Defense // Denting Blows", "{2}{W}", "Instant", "Choose one.",
            null, null, [], ["W"], null, null, null,
            [
                new ScryfallCardFace("Perfect Defense", "{2}{W}", "Instant", "Prevent all combat damage that would be dealt this turn.", null, null),
                new ScryfallCardFace("Denting Blows", "{2}{R}", "Instant", "Denting Blows deals 4 damage to target creature.", null, null)
            ]),
    ];

    /// <summary>
    /// T-83-02 mitigation: <c>DeckAnalysisPacketService.cs:1253</c> and
    /// <c>DeckComparisonService.cs:576</c> both embed a LIVE <c>DateTime.UtcNow</c>-derived
    /// <c>generated_at_utc: ...Z</c> line directly into the artifact text under test. This is the
    /// ONLY non-deterministic byte in either artifact (TimingSummary is a separate, unasserted
    /// field). Normalize it to a fixed placeholder before comparing against a golden so the harness
    /// stays deterministic across runs without excluding real prompt content from the comparison.
    /// </summary>
    private static readonly Regex GeneratedAtUtcPattern = new(
        @"generated_at_utc: \d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z",
        RegexOptions.Compiled);

    public static string NormalizeVolatileTimestamps(string? text)
        => text is null ? string.Empty : GeneratedAtUtcPattern.Replace(text, "generated_at_utc: <TIMESTAMP>");

    /// <summary>
    /// EVERY prompt/context/decklist artifact under test is built with <c>StringBuilder.AppendLine</c>,
    /// which appends <c>Environment.NewLine</c> — "\r\n" on Windows (where these goldens were captured
    /// via <c>dotnet.exe</c>), "\n" on the Linux CI runner (<c>ubuntu-latest</c>, .github/workflows/ci.yml)
    /// that actually gates this repo. Without normalizing "\r\n" -> "\n" on BOTH the captured golden text
    /// and the live comparison value, this OS-dependent byte would make every byte-identity assertion
    /// fail on Linux CI even though the ACTUAL prompt content is unchanged. This is applied in addition
    /// to (never instead of) <see cref="NormalizeVolatileTimestamps"/> and does NOT touch a lone bare
    /// "\r" not followed by "\n" (the H3 whitespace fixture's bare-CR case is deliberately preserved).
    /// </summary>
    public static string NormalizeForGoldenComparison(string? text)
        => NormalizeVolatileTimestamps(text).Replace("\r\n", "\n", StringComparison.Ordinal);

    public static DeckEntry CreateDeckEntry(
        string name,
        int quantity,
        string board,
        string? setCode = null,
        string? collectorNumber = null,
        string? category = null)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = quantity,
            Board = board,
            SetCode = setCode,
            CollectorNumber = collectorNumber,
            Category = category
        };

    /// <summary>
    /// Whitespace-bearing values (tab, embedded newline, run of multiple spaces, bare CR) used to
    /// lock in each service's EXACT current whitespace-collapse behavior (H3) before any normalizer
    /// consolidation touches these services. Deliberately irregular so the per-service collapse
    /// asymmetries documented in 83-RESEARCH.md (Analysis/Comparison/MetaGap collapse newlines only;
    /// Primer collapses ANY whitespace run) are captured, not glossed over.
    /// </summary>
    public const string WhitespaceDeckName = "  Kraum\tPartner   Deck  ";
    public const string WhitespaceStrategyNotes = "Line one\nLine\ttwo\r\nLine  three   with   gaps\rTrailing bare CR";
    public const string WhitespaceMetaNotes = "Meta:\tgrindy\n\nSlow   pods\r stax-lite";

    // ---------------------------------------------------------------------------------------
    // Scryfall collection / search / named fakes shared by Analysis, Comparison, and MetaGap.
    // ---------------------------------------------------------------------------------------

    public static RestResponse<ScryfallCollectionResponse> CreateCollectionResponse(RestRequest request)
        => new(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallCollectionResponse(CardCatalog().ToList(), [])
        };

    public static RestResponse<ScryfallSearchResponse> CreateSearchResponse(RestRequest request)
    {
        var query = request.Parameters.FirstOrDefault(parameter => parameter.Name?.ToString() == "q")?.Value?.ToString() ?? string.Empty;
        var match = FindCatalogCard(query);
        return new RestResponse<ScryfallSearchResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallSearchResponse(match is null ? [] : [match])
        };
    }

    public static RestResponse<ScryfallCard> CreateNamedResponse(RestRequest request)
    {
        var fuzzy = request.Parameters.FirstOrDefault(parameter => parameter.Name?.ToString() == "fuzzy")?.Value?.ToString() ?? string.Empty;
        var match = FindCatalogCard(fuzzy);
        return new RestResponse<ScryfallCard>(request)
        {
            StatusCode = match is null ? HttpStatusCode.NotFound : HttpStatusCode.OK,
            Data = match
        };
    }

    /// <summary>
    /// Renamed/printed-name aliases: a submitted (localized/printed) name that Scryfall's fallback
    /// search resolves to a DIFFERENT canonical catalog card — mirrors the "Ya viene el coco" ->
    /// "Perfect Defense // Denting Blows" precedent already used by DeckComparisonServiceTests'
    /// per-card-fallback test, reused here for the Comparison byte-identity printed-name fixture.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> RenamedCardAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ya viene el coco"] = "Perfect Defense // Denting Blows",
        };

    private static ScryfallCard? FindCatalogCard(string query)
    {
        var normalizedQuery = NormalizeLookup(query);

        foreach (var (submitted, resolved) in RenamedCardAliases)
        {
            if (normalizedQuery.Contains(NormalizeLookup(submitted), StringComparison.Ordinal))
            {
                var aliasCard = CardCatalog().FirstOrDefault(card => string.Equals(card.Name, resolved, StringComparison.OrdinalIgnoreCase));
                if (aliasCard is not null)
                {
                    return aliasCard;
                }
            }
        }

        return CardCatalog().FirstOrDefault(card =>
            normalizedQuery.Contains(NormalizeLookup(card.Name), StringComparison.Ordinal)
            || (card.CardFaces?.Any(face => normalizedQuery.Contains(NormalizeLookup(face.Name), StringComparison.Ordinal)) ?? false));
    }

    private static string NormalizeLookup(string value)
        => value
            .Trim()
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal)
            .Replace("!", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Replace(" / ", " // ", StringComparison.Ordinal)
            .ToLowerInvariant();

    // ---------------------------------------------------------------------------------------
    // Analysis service construction seam — mirrors DeckAnalysisPacketServiceTests.CreateService,
    // but exposes IFeatureFlagCache so byte-identity tests can sweep every prompt-mutating flag.
    // ---------------------------------------------------------------------------------------

    public static DeckAnalysisPacketService CreateAnalysisService(
        IMoxfieldDeckImporter? moxfieldDeckImporter = null,
        IFeatureFlagCache? flagCache = null,
        ICommanderSpellbookService? spellbookService = null)
    {
        return new DeckAnalysisPacketService(
            new ScryfallCardResolver(
                new FakeScryfallRestClientFactory(new HttpClient { BaseAddress = new Uri("https://api.scryfall.com/") }),
                new FakeResiliencePipelineProvider(),
                executeCollectionAsyncOverride: (request, _) => Task.FromResult(CreateCollectionResponse(request)),
                executeSearchAsyncOverride: (request, _) => Task.FromResult(CreateSearchResponse(request)),
                executeNamedAsyncOverride: (request, _) => Task.FromResult(CreateNamedResponse(request))),
            new DeckEntryLoader(
                moxfieldDeckImporter ?? new FixtureMoxfieldDeckImporter([]),
                new FixtureArchidektDeckImporter(),
                new MoxfieldParser(),
                new ArchidektParser()),
            new FixtureMechanicLookupService(),
            new FixtureCommanderBanListService(),
            new FixtureScryfallSetService(),
            spellbookService ?? new FixtureCommanderSpellbookService(),
            new FixtureGameChangerCatalogService(),
            new AnalysisPromptVariantRegistry(new IAnalysisPromptVariant[]
            {
                new ChatGptAnalysisPromptVariant(),
                new ClaudeAnalysisPromptVariant(),
                new GeminiAnalysisPromptVariant(),
            }),
            new SetUpgradePromptVariantRegistry(new ISetUpgradePromptVariant[]
            {
                new ChatGptSetUpgradePromptVariant(),
                new ClaudeSetUpgradePromptVariant(),
                new GeminiSetUpgradePromptVariant(),
            }),
            new PacketSessionCache(),
            flagCache,
            NullLogger<DeckAnalysisPacketService>.Instance);
    }

    /// <summary>All 6 flag keys that can mutate an Analysis artifact (4 PromptMutatingAnalysisFlags
    /// entries + the 2 reference-block flags), explicitly OFF — the byte-identity baseline.</summary>
    public static FakeFeatureFlagCache AllAnalysisFlagsOff() => new(new Dictionary<string, bool>
    {
        [DeckAnalysisPacketService.CommandZoneAwarenessFlag] = false,
        [DeckAnalysisPacketService.MultiAxisScoreFlag] = false,
        [DeckAnalysisPacketService.InteractionAuditFlag] = false,
        [DeckAnalysisPacketService.WinConMapFlag] = false,
        [DeckAnalysisPacketService.ReferenceFullOracleFlag] = true, // enabled = legacy full-oracle-text (OFF gate state)
        [DeckAnalysisPacketService.ReferenceDeckStatsFlag] = false,
    });

    /// <summary>Baseline flag map with exactly ONE key flipped to its "on" state.</summary>
    public static FakeFeatureFlagCache WithSingleFlagOn(string flagKey)
    {
        var flags = AllAnalysisFlagsOff();
        flags.Flags[flagKey] = flagKey == DeckAnalysisPacketService.ReferenceFullOracleFlag
            ? false // "on" for this gate means the recency gate is enabled, i.e. IsEnabled() -> false
            : true;
        return flags;
    }

    public static FakeFeatureFlagCache AllFourMutatingFlagsOn()
    {
        var flags = AllAnalysisFlagsOff();
        flags.Flags[DeckAnalysisPacketService.CommandZoneAwarenessFlag] = true;
        flags.Flags[DeckAnalysisPacketService.MultiAxisScoreFlag] = true;
        flags.Flags[DeckAnalysisPacketService.InteractionAuditFlag] = true;
        flags.Flags[DeckAnalysisPacketService.WinConMapFlag] = true;
        return flags;
    }

    // ---------------------------------------------------------------------------------------
    // Analysis fixture decks.
    // ---------------------------------------------------------------------------------------

    /// <summary>Baseline Kraum-commander fixture deck: commander + 4 mainboard cards, no versions, no
    /// possible-includes, no DFC/renamed cards. Used by the flag-sweep matrix.</summary>
    public static List<DeckEntry> BaselineEntries() =>
    [
        CreateDeckEntry("Kraum, Ludevic's Opus", 1, "commander", "c16", "39"),
        CreateDeckEntry("Command Tower", 1, "mainboard", "c16", "285"),
        CreateDeckEntry("Arcane Signet", 1, "mainboard", "eld", "331"),
        CreateDeckEntry("Sol Ring", 1, "mainboard", "c16", "272"),
        CreateDeckEntry("Ponder", 1, "mainboard", "c21", "118"),
    ];

    /// <summary>Companion fixture (Kraum + Background) used for the ALL-4-mutating-flags-ON case (M1)
    /// so shared combo-fetch/enrichment ordering under interacting flags is exercised.</summary>
    public static List<DeckEntry> CompanionEntries() =>
    [
        CreateDeckEntry("Kraum, Ludevic's Opus", 1, "commander", "c16", "39"),
        CreateDeckEntry("Passionate Archaeologist", 1, "commander", "clb", "189"),
        CreateDeckEntry("Command Tower", 1, "mainboard", "c16", "285"),
        CreateDeckEntry("Arcane Signet", 1, "mainboard", "eld", "331"),
        CreateDeckEntry("Sol Ring", 1, "mainboard", "c16", "272"),
        CreateDeckEntry("Ponder", 1, "mainboard", "c21", "118"),
    ];

    /// <summary>
    /// Versioned-decklist + single-slash-collection-miss fixture (path-coverage items 1 + 2 combined):
    /// commander + mainboard entries WITH SetCode/CollectorNumber, one Possible-Includes (maybeboard)
    /// entry, and a single-slash-submitted DFC card ("Blex, Vexing Pest / Search for Blex") whose
    /// canonical catalog entry is double-slash — the collection lookup returns the double-slash
    /// card.Name, which does NOT match the original single-slash submission, so it falls through to
    /// SearchPrintingFallbackCardAsync and the resolved oracle name differs from the submitted name
    /// (triggering the "[printed as: X]" annotation).
    /// </summary>
    public static List<DeckEntry> VersionedDecklistWithSingleSlashMissEntries() =>
    [
        CreateDeckEntry("Kraum, Ludevic's Opus", 1, "commander", "c16", "39"),
        CreateDeckEntry("Sol Ring", 1, "mainboard", "c16", "272"),
        CreateDeckEntry("Blex, Vexing Pest / Search for Blex", 1, "mainboard", "tsr", "96"),
        CreateDeckEntry("Swords to Plowshares", 1, "maybeboard"),
    ];

    // ---------------------------------------------------------------------------------------
    // Primer service construction seam — mirrors DeckPrimerPacketServiceTests.CreateService's
    // second internal ctor, but wires the REAL ChatGPT/Claude/Gemini primer prompt variants
    // (rather than the test-only fake variant) so PromptTextsByPlatform is exercised exactly as
    // production wires it (per the plan's "real prompt-variant registries with all 3 concrete
    // variants" seam requirement).
    // ---------------------------------------------------------------------------------------

    public static DeckPrimerPacketService CreatePrimerService(
        List<DeckEntry>? entries = null,
        CommanderSpellbookResult? comboResult = null,
        IReadOnlyList<CategoryKnowledgeRow>? categoryRows = null,
        IReadOnlyList<EdhTop16Entry>? topArchetypes = null)
    {
        var fixtureEntries = entries ?? PrimerBaselineEntries();
        return new DeckPrimerPacketService(
            new PrimerPromptVariantRegistry(new IPrimerPromptVariant[]
            {
                new ChatGptPrimerPromptVariant(),
                new ClaudePrimerPromptVariant(),
                new GeminiPrimerPromptVariant(),
            }),
            new PacketSessionCache(),
            loadDeckEntriesAsyncOverride: (_, _) => Task.FromResult(fixtureEntries.Select(CloneEntry).ToList()),
            findCombosAsyncOverride: (_, _) => Task.FromResult(comboResult),
            getTopArchetypesAsyncOverride: (_, _) => Task.FromResult(topArchetypes ?? (IReadOnlyList<EdhTop16Entry>)Array.Empty<EdhTop16Entry>()),
            getCategoryRowsForCommanderAsyncOverride: (_, _) => Task.FromResult(categoryRows ?? (IReadOnlyList<CategoryKnowledgeRow>)Array.Empty<CategoryKnowledgeRow>()),
            geminiEnabled: true);
    }

    private static DeckEntry CloneEntry(DeckEntry entry)
        => CreateDeckEntry(entry.Name, entry.Quantity, entry.Board, entry.SetCode, entry.CollectorNumber, entry.Category);

    public static List<DeckEntry> PrimerBaselineEntries() =>
    [
        CreateDeckEntry("Kraum, Ludevic's Opus", 1, "commander"),
        CreateDeckEntry("Sol Ring", 1, "mainboard"),
        CreateDeckEntry("Arcane Signet", 1, "mainboard"),
        CreateDeckEntry("Command Tower", 1, "mainboard"),
    ];

    // ---------------------------------------------------------------------------------------
    // Minimal fixture doubles (Analysis-only dependencies not otherwise covered by shared
    // TestDoubles). Kept internal/private to this file so no existing test double is touched.
    // ---------------------------------------------------------------------------------------

    private sealed class FixtureMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        private readonly List<DeckEntry> _entries;

        public FixtureMoxfieldDeckImporter(List<DeckEntry> entries) => _entries = entries;

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(_entries.Select(CloneEntry).ToList());

        public Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MoxfieldImportResult(
                ImportAsync(urlOrDeckId, cancellationToken).GetAwaiter().GetResult(),
                MoxfieldImportSource.Direct,
                DetectedCompanionName: null));
    }

    private sealed class FixtureArchidektDeckImporter : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class FixtureMechanicLookupService : IMechanicLookupService
    {
        public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
            => Task.FromResult(new MechanicLookupResult(
                mechanicName,
                true,
                mechanicName,
                "702.108",
                "Exact rules section",
                "702.108a Test rules text.",
                "A keyword ability used for fixture determinism.",
                "https://magic.wizards.com/en/rules",
                "https://media.wizards.com/test.txt"));
    }

    private sealed class FixtureScryfallSetService : IScryfallSetService
    {
        public Task<IReadOnlyList<ScryfallSetOption>> GetSetsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScryfallSetOption>>([new ScryfallSetOption("dsk", "Test Set", "2026-01-01")]);

        public Task<string> BuildSetPacketAsync(IReadOnlyList<string> setCodes, IReadOnlyList<string>? commanderColorIdentity = null, CancellationToken cancellationToken = default)
            => Task.FromResult("set_packet:\ngenerated_at_utc: 2026-03-26T00:00:00Z\nsets:\n- Test Set (DSK)\n");
    }

    private sealed class FixtureCommanderBanListService : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["Dockside Extortionist", "Mana Crypt"]);
    }

    private sealed class FixtureCommanderSpellbookService : ICommanderSpellbookService
    {
        public Task<CommanderSpellbookResult?> FindCombosAsync(
            IReadOnlyList<DeckEntry> entries,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CommanderSpellbookResult?>(null);
    }

    private sealed class FixtureGameChangerCatalogService : IGameChangerCatalogService
    {
        private static readonly GameChangerCatalog Catalog = new(
            new DateOnly(2026, 2, 1),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<BracketTier>());

        public GameChangerCatalog GetCatalog() => Catalog;
    }
}
