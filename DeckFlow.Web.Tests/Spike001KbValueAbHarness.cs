using System.Net;
using System.Text;
using System.Text.Json;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class Spike001KbValueAbHarness
{
    [Fact]
    public async Task EmitAbPrompts()
    {
        var outputDirectory = ResolveSpikeOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        var withContextService = CreateService(new FakeContentKbRelevanceService
        {
            Result =
            [
                new ContentKbExcerpt
                {
                    Source = "Salubrious Snail",
                    Title = "The 5 Most Common Deckbuilding Mistakes",
                    VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    TimestampLabel = "01:42",
                    Excerpt = "Mistake #5: Your deck is unfocused on the macro level. Players build around a central synergy pillar but add payoffs shooting in too many directions; fix with restraint and specificity.",
                    HarvestDate = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
                    Score = 4.91
                },
                new ContentKbExcerpt
                {
                    Source = "Salubrious Snail",
                    Title = "The 5 Most Common Deckbuilding Mistakes",
                    VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    TimestampLabel = "03:18",
                    Excerpt = "Crank up the specificity. If you cruise through Scryfall on a synergy search term and add everything, you dilute the deck — be selective.",
                    HarvestDate = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
                    Score = 4.73
                },
                new ContentKbExcerpt
                {
                    Source = "Salubrious Snail",
                    Title = "The 5 Most Common Deckbuilding Mistakes",
                    VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    TimestampLabel = "06:05",
                    Excerpt = "Mistake #4: Your deck is vulnerable to opposing hate. Glass-cannon decks can be powerful but fold to a single piece of interaction; build redundancy and resilience.",
                    HarvestDate = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
                    Score = 4.65
                },
                new ContentKbExcerpt
                {
                    Source = "Salubrious Snail",
                    Title = "The 5 Most Common Deckbuilding Mistakes",
                    VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    TimestampLabel = "07:44",
                    Excerpt = "On durability without protection spells: haste prevents a creature eating dirt before doing anything, but real durability comes from protection, not just speed.",
                    HarvestDate = new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
                    Score = 4.52
                }
            ]
        });
        var baselineService = CreateService(new FakeContentKbRelevanceService { Result = null });
        var request = CreateAnalysisRequest();

        var withContextResult = await withContextService.BuildAsync(request);
        var baselineResult = await baselineService.BuildAsync(CreateAnalysisRequest());

        var withContextPath = Path.Combine(outputDirectory, "with-context.txt");
        var baselinePath = Path.Combine(outputDirectory, "baseline.txt");

        await File.WriteAllTextAsync(withContextPath, withContextResult.AnalysisPromptText);
        await File.WriteAllTextAsync(baselinePath, baselineResult.AnalysisPromptText);

        Assert.False(string.IsNullOrEmpty(withContextResult.AnalysisPromptText));
        Assert.False(string.IsNullOrEmpty(baselineResult.AnalysisPromptText));
        Assert.Contains("## Expert Context", withContextResult.AnalysisPromptText, StringComparison.Ordinal);
        Assert.DoesNotContain("## Expert Context", baselineResult.AnalysisPromptText, StringComparison.Ordinal);

        var withContextInfo = new FileInfo(withContextPath);
        var baselineInfo = new FileInfo(baselinePath);
        Assert.True(withContextInfo.Exists && withContextInfo.Length > 0);
        Assert.True(baselineInfo.Exists && baselineInfo.Length > 0);
    }

    [Fact]
    public async Task EmitRealRetrievalPromptAllDecks()
    {
        var outputDirectory = ResolveSpikeOutputDirectory();
        Directory.CreateDirectory(outputDirectory);
        var artifactsRoot = Path.Combine(ResolveRepoRoot(), "artifacts");

        var rowsJson = await File.ReadAllTextAsync(Path.Combine(artifactsRoot, "spike-rows.json"));
        var rows = JsonSerializer.Deserialize<List<ContentSiteIndexRow>>(
            rowsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var store = new FakeContentSiteIndexStore();
        store.Rows.AddRange(rows);

        var relevance = new ContentKbRelevanceService(
            store,
            resolveArtifactPath: relativePath => Path.Combine(artifactsRoot, relativePath),
            flagCache: new FakeFeatureFlagCache(new Dictionary<string, bool> { ["content.kb.enabled"] = true }),
            archetypeDeriver: new ContentKbArchetypeDeriver(new FakeCategoryKnowledgeStore()),
            logger: null);

        foreach (var deck in GetRealRetrievalDecks())
        {
            var clips = await relevance.GetRelevantClipsAsync(deck.CommanderName, deck.TargetCommanderBracket, deck.Archetypes);

            var baselineResult = await CreateService(new FakeContentKbRelevanceService { Result = null }, deck.Cards)
                .BuildAsync(CreateAnalysisRequest(deck.DeckSource, deck.TargetCommanderBracket));

            var withContextResult = await CreateService(new FakeContentKbRelevanceService { Result = clips }, deck.Cards)
                .BuildAsync(CreateAnalysisRequest(deck.DeckSource, deck.TargetCommanderBracket));

            var hasClips = clips is { Count: > 0 };
            var baselinePath = Path.Combine(outputDirectory, $"baseline-{deck.Slug}.txt");
            var withContextPath = Path.Combine(outputDirectory, $"with-context-{deck.Slug}.txt");
            var selectedClipsPath = Path.Combine(outputDirectory, $"selected-clips-{deck.Slug}.txt");
            var withContextText = hasClips ? withContextResult.AnalysisPromptText : baselineResult.AnalysisPromptText;

            await File.WriteAllTextAsync(baselinePath, baselineResult.AnalysisPromptText);
            await File.WriteAllTextAsync(withContextPath, withContextText);
            await File.WriteAllTextAsync(selectedClipsPath, BuildSelectedClipsTrace(deck, clips, store.Rows.Count));

            Assert.False(string.IsNullOrEmpty(baselineResult.AnalysisPromptText));
            Assert.False(string.IsNullOrEmpty(withContextText));
            Assert.DoesNotContain("## Expert Context", baselineResult.AnalysisPromptText, StringComparison.Ordinal);

            if (hasClips)
            {
                Assert.Contains("## Expert Context", withContextText, StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal(baselineResult.AnalysisPromptText, withContextText);
            }

            Assert.True(new FileInfo(baselinePath).Exists && new FileInfo(baselinePath).Length > 0);
            Assert.True(new FileInfo(withContextPath).Exists && new FileInfo(withContextPath).Length > 0);
            Assert.True(new FileInfo(selectedClipsPath).Exists && new FileInfo(selectedClipsPath).Length > 0);
        }
    }

    [Fact]
    public async Task EmitRealRetrievalPrompt()
    {
        // Gold A/B: run the REAL ContentKbRelevanceService scorer over the reconstructed prod
        // corpus (82 visible rows, snail-heavy), let it select clips for this deck, and assemble
        // the with-context prompt from the REAL selection — not a hardcoded clip set.
        var outputDirectory = ResolveSpikeOutputDirectory();
        Directory.CreateDirectory(outputDirectory);
        var artifactsRoot = Path.Combine(ResolveRepoRoot(), "artifacts");

        var rowsJson = await File.ReadAllTextAsync(Path.Combine(artifactsRoot, "spike-rows.json"));
        var rows = JsonSerializer.Deserialize<List<ContentSiteIndexRow>>(
            rowsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var store = new FakeContentSiteIndexStore();
        store.Rows.AddRange(rows);

        var relevance = new ContentKbRelevanceService(
            store,
            resolveArtifactPath: relativePath => Path.Combine(artifactsRoot, relativePath),
            flagCache: new FakeFeatureFlagCache(new Dictionary<string, bool> { ["content.kb.enabled"] = true }),
            archetypeDeriver: new ContentKbArchetypeDeriver(new FakeCategoryKnowledgeStore()),
            logger: null);

        // Atraxa goodstuff: ramp + control + value-engine + midrange (explicit, bypasses the
        // category-knowledge deriver which would otherwise need a populated store).
        var deckArchetypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ramp", "control", "value-engine", "midrange"
        };

        var clips = await relevance.GetRelevantClipsAsync("Atraxa, Praetors' Voice", "Upgraded", deckArchetypes);

        Assert.NotNull(clips);
        Assert.NotEmpty(clips!);

        var sb = new StringBuilder();
        sb.AppendLine($"Real ContentKbRelevanceService selection — Atraxa, Praetors' Voice / Upgraded — {clips!.Count} clips");
        sb.AppendLine($"(corpus: {store.Rows.Count} visible rows; deck archetypes: {string.Join(", ", deckArchetypes)})");
        sb.AppendLine();
        foreach (var clip in clips)
        {
            sb.AppendLine($"- score={clip.Score:F2} | {clip.Source} — {clip.Title} [{clip.TimestampLabel}]");
            sb.AppendLine($"  {clip.Excerpt}");
            sb.AppendLine();
        }
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "selected-clips-real.txt"), sb.ToString());

        var service = CreateService(new FakeContentKbRelevanceService { Result = clips });
        var result = await service.BuildAsync(CreateAnalysisRequest());
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "with-context-real.txt"), result.AnalysisPromptText);

        Assert.Contains("## Expert Context", result.AnalysisPromptText, StringComparison.Ordinal);
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DeckFlow.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root containing DeckFlow.sln.");
    }

    private static string ResolveSpikeOutputDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DeckFlow.sln")))
            {
                return Path.Combine(current.FullName, ".planning", "spikes", "001-kb-value-ab");
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root containing DeckFlow.sln.");
    }

    private static DeckAnalysisPacketService CreateService(FakeContentKbRelevanceService relevanceService)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.scryfall.test")
        };

        return new DeckAnalysisPacketService(
            new FakeScryfallRestClientFactory(httpClient),
            new FakeResiliencePipelineProvider(),
            new FakeMoxfieldDeckImporter(),
            new FakeArchidektDeckImporter(),
            new MoxfieldParser(),
            new ArchidektParser(),
            new FakeMechanicLookupService(),
            new FakeCommanderBanListService(),
            new FakeScryfallSetService(),
            new FakeCommanderSpellbookService(),
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
            relevanceService,
            logger: null,
            restClientOverride: null,
            executeCollectionAsyncOverride: static (request, _) => Task.FromResult(CreateCollectionResponse(request)),
            executeSearchAsyncOverride: static (request, _) => Task.FromResult(CreateSearchResponse(request)),
            executeNamedAsyncOverride: static (request, _) => Task.FromResult(CreateNamedResponse(request)));
    }

    private static DeckAnalysisPacketService CreateService(IContentKbRelevanceService relevanceService, IReadOnlyList<ScryfallCard> cards)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.scryfall.test")
        };

        return new DeckAnalysisPacketService(
            new FakeScryfallRestClientFactory(httpClient),
            new FakeResiliencePipelineProvider(),
            new FakeMoxfieldDeckImporter(),
            new FakeArchidektDeckImporter(),
            new MoxfieldParser(),
            new ArchidektParser(),
            new FakeMechanicLookupService(),
            new FakeCommanderBanListService(),
            new FakeScryfallSetService(),
            new FakeCommanderSpellbookService(),
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
            relevanceService,
            logger: null,
            restClientOverride: null,
            executeCollectionAsyncOverride: (request, _) => Task.FromResult(CreateCollectionResponse(request, cards)),
            executeSearchAsyncOverride: (request, _) => Task.FromResult(CreateSearchResponse(request, cards)),
            executeNamedAsyncOverride: (request, _) => Task.FromResult(CreateNamedResponse(request, cards)));
    }

    private static DeckAnalysisRequest CreateAnalysisRequest()
        => CreateAnalysisRequest(
            """
Commander
1 Atraxa, Praetors' Voice

1 Teferi, Hero of Dominaria
1 Vraska, Golgari Queen
1 Kaya, Orzhov Usurper
1 Nissa, Voice of Zendikar
1 Vivien, Monsters' Advocate
1 Elspeth, Sun's Champion
1 Garruk Wildspeaker
1 Karn Liberated
1 Ugin, the Spirit Dragon
1 Doubling Season
1 Evolution Sage
1 Flux Channeler
1 Inexorable Tide
1 Deepglow Skate
1 Hardened Scales
1 Vorinclex, Monstrous Raider
1 Hadana's Climb
1 The Ozolith
1 Conclave Mentor
1 Forgotten Ancient
1 Birds of Paradise
1 Sakura-Tribe Elder
1 Eternal Witness
1 Solemn Simulacrum
1 Esper Sentinel
1 Spark Double
1 Oracle of Mul Daya
1 Knight of Autumn
1 Reclamation Sage
1 Cytoplast Root-Kin
1 Tatyova, Benthic Druid
1 Sol Ring
1 Arcane Signet
1 Fellwar Stone
1 Chromatic Lantern
1 Cultivate
1 Kodama's Reach
1 Farseek
1 Nature's Lore
1 Rampant Growth
1 Skyshroud Claim
1 Rhystic Study
1 Sylvan Library
1 Mystic Remora
1 Fact or Fiction
1 Tezzeret's Gambit
1 Painful Truths
1 Swords to Plowshares
1 Path to Exile
1 Anguished Unmaking
1 Despark
1 Generous Gift
1 Beast Within
1 Cyclonic Rift
1 Counterspell
1 Swan Song
1 Assassin's Trophy
1 Vindicate
1 Putrefy
1 Toxic Deluge
1 Supreme Verdict
1 Damn
1 Command Tower
1 Exotic Orchard
1 City of Brass
1 Mana Confluence
1 Spara's Headquarters
1 Raffine's Tower
1 Xander's Lounge
1 Plaza of Heroes
1 Breeding Pool
1 Watery Grave
1 Overgrown Tomb
1 Hallowed Fountain
1 Godless Shrine
1 Temple Garden
1 Drowned Catacomb
1 Glacial Fortress
1 Isolated Chapel
1 Hinterland Harbor
1 Sunpetal Grove
1 Woodland Cemetery
1 Karn's Bastion
1 Reliquary Tower
1 Yavimaya, Cradle of Growth
1 Bojuka Bog
3 Forest
3 Island
3 Swamp
3 Plains
""",
            "Upgraded");

    private static DeckAnalysisRequest CreateAnalysisRequest(string deckSource, string targetCommanderBracket)
    {
        return new DeckAnalysisRequest
        {
            WorkflowStep = 2,
            DeckSource = deckSource,
            TargetCommanderBracket = targetCommanderBracket,
            SelectedAnalysisQuestions = ["strengths-weaknesses"],
            TargetAiPlatform = "ChatGPT"
        };
    }

    private static RestResponse<ScryfallCollectionResponse> CreateCollectionResponse(RestRequest request)
    {
        return new RestResponse<ScryfallCollectionResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallCollectionResponse(GetDefaultTestCards().ToList(), [])
        };
    }

    private static RestResponse<ScryfallCollectionResponse> CreateCollectionResponse(RestRequest request, IReadOnlyList<ScryfallCard> cards)
    {
        return new RestResponse<ScryfallCollectionResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallCollectionResponse(cards.ToList(), [])
        };
    }

    private static RestResponse<ScryfallSearchResponse> CreateSearchResponse(RestRequest request)
    {
        var query = request.Parameters.FirstOrDefault(parameter => parameter.Name?.ToString() == "q")?.Value?.ToString() ?? string.Empty;
        var match = FindDefaultCard(query);
        return new RestResponse<ScryfallSearchResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallSearchResponse(match is null ? [] : [match])
        };
    }

    private static RestResponse<ScryfallSearchResponse> CreateSearchResponse(RestRequest request, IReadOnlyList<ScryfallCard> cards)
    {
        var query = request.Parameters.FirstOrDefault(parameter => parameter.Name?.ToString() == "q")?.Value?.ToString() ?? string.Empty;
        var match = FindDefaultCard(query, cards);
        return new RestResponse<ScryfallSearchResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallSearchResponse(match is null ? [] : [match])
        };
    }

    private static RestResponse<ScryfallCard> CreateNamedResponse(RestRequest request)
    {
        var fuzzy = request.Parameters.FirstOrDefault(parameter => parameter.Name?.ToString() == "fuzzy")?.Value?.ToString() ?? string.Empty;
        var match = FindDefaultCard(fuzzy);
        return new RestResponse<ScryfallCard>(request)
        {
            StatusCode = match is null ? HttpStatusCode.NotFound : HttpStatusCode.OK,
            Data = match
        };
    }

    private static RestResponse<ScryfallCard> CreateNamedResponse(RestRequest request, IReadOnlyList<ScryfallCard> cards)
    {
        var fuzzy = request.Parameters.FirstOrDefault(parameter => parameter.Name?.ToString() == "fuzzy")?.Value?.ToString() ?? string.Empty;
        var match = FindDefaultCard(fuzzy, cards);
        return new RestResponse<ScryfallCard>(request)
        {
            StatusCode = match is null ? HttpStatusCode.NotFound : HttpStatusCode.OK,
            Data = match
        };
    }

    private static ScryfallCard? FindDefaultCard(string query)
    {
        var normalizedQuery = query.Trim().ToUpperInvariant();
        return GetDefaultTestCards().FirstOrDefault(card =>
            normalizedQuery.Contains(card.Name.ToUpperInvariant(), StringComparison.Ordinal));
    }

    private static ScryfallCard? FindDefaultCard(string query, IReadOnlyList<ScryfallCard> cards)
    {
        var normalizedQuery = query.Trim().ToUpperInvariant();
        return cards.FirstOrDefault(card =>
            normalizedQuery.Contains(card.Name.ToUpperInvariant(), StringComparison.Ordinal));
    }

    private static string BuildSelectedClipsTrace(DeckFixture deck, IReadOnlyList<ContentKbExcerpt>? clips, int visibleRowCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Real ContentKbRelevanceService selection — {deck.CommanderName} / {deck.TargetCommanderBracket} — {(clips?.Count ?? 0)} clips");
        sb.AppendLine($"(corpus: {visibleRowCount} visible rows; deck archetypes: {string.Join(", ", deck.Archetypes)})");
        sb.AppendLine();

        if (clips is null || clips.Count == 0)
        {
            sb.AppendLine("0 clips (cold-start)");
            return sb.ToString();
        }

        foreach (var clip in clips)
        {
            sb.AppendLine($"- score={clip.Score:F2} | {clip.Source} — {clip.Title} [{clip.TimestampLabel}]");
            sb.AppendLine($"  {clip.Excerpt}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static IReadOnlyList<DeckFixture> GetRealRetrievalDecks()
    {
        var atraxaRequest = CreateAnalysisRequest();
        return
        [
            new DeckFixture(
                "atraxa",
                "Atraxa, Praetors' Voice",
                atraxaRequest.TargetCommanderBracket,
                atraxaRequest.DeckSource,
                GetDefaultTestCards(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ramp", "control", "value-engine", "midrange"
                }),
            new DeckFixture("light-paws", "Light-Paws, Emperor's Voice", "Optimized", GetLightPawsDeckSource(), GetLightPawsCards(), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "aggro", "voltron" }),
            new DeckFixture("kinnan", "Kinnan, Bonder Prodigy", "cEDH", GetKinnanDeckSource(), GetKinnanCards(), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "combo", "control" }),
            new DeckFixture("talrand", "Talrand, Sky Summoner", "Upgraded", GetTalrandDeckSource(), GetTalrandCards(), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "control", "stax" }),
            new DeckFixture("aesi", "Aesi, Tyrant of Gyre Strait", "Core", GetAesiDeckSource(), GetAesiCards(), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lands", "ramp" }),
        ];
    }

    private static IReadOnlyList<ScryfallCard> GetDefaultTestCards() =>
    [
        new("Atraxa, Praetors' Voice", "{G}{W}{U}{B}", "Legendary Creature — Phyrexian Angel Horror", "Flying, vigilance, deathtouch, lifelink\nAt the beginning of your end step, proliferate. (Choose any number of permanents and/or players, then give each another counter of each kind already there.)", "4", "4", ["Deathtouch", "Flying", "Lifelink", "Vigilance", "Proliferate"], ["B", "G", "U", "W"], "2xm", "Double Masters", "190"),
        new("Teferi, Hero of Dominaria", "{3}{W}{U}", "Legendary Planeswalker — Teferi", "+1: Draw a card. At the beginning of the next end step, untap up to two lands.\n−3: Put target nonland permanent into its owner's library third from the top.\n−8: You get an emblem with \"Whenever you draw a card, exile target permanent an opponent controls.\"", null, null, [], ["U", "W"], "dom", "Dominaria", "207"),
        new("Vraska, Golgari Queen", "{2}{B}{G}", "Legendary Planeswalker — Vraska", "+2: You may sacrifice another permanent. If you do, you gain 1 life and draw a card.\n−3: Destroy target nonland permanent with mana value 3 or less.\n−9: You get an emblem with \"Whenever a creature you control deals combat damage to a player, that player loses the game.\"", null, null, [], ["B", "G"], "grn", "Guilds of Ravnica", "213"),
        new("Kaya, Orzhov Usurper", "{1}{W}{B}", "Legendary Planeswalker — Kaya", "+1: Exile up to two target cards from a single graveyard. You gain 2 life if at least one creature card was exiled this way.\n−1: Exile target nonland permanent with mana value 1 or less.\n−5: Kaya deals damage to target player equal to the number of cards that player owns in exile and you gain that much life.", null, null, [], ["B", "W"], "rvr", "Ravnica Remastered", "194"),
        new("Nissa, Voice of Zendikar", "{1}{G}{G}", "Legendary Planeswalker — Nissa", "+1: Create a 0/1 green Plant creature token.\n−2: Put a +1/+1 counter on each creature you control.\n−7: You gain X life and draw X cards, where X is the number of lands you control.", null, null, [], ["G"], "ddr", "Duel Decks: Nissa vs. Ob Nixilis", "1"),
        new("Vivien, Monsters' Advocate", "{3}{G}{G}", "Legendary Planeswalker — Vivien", "You may look at the top card of your library any time.\nYou may cast creature spells from the top of your library.\n+1: Create a 3/3 green Beast creature token. Put your choice of a vigilance counter, a reach counter, or a trample counter on it.\n−2: When you next cast a creature spell this turn, search your library for a creature card with lesser mana value, put it onto the battlefield, then shuffle.", null, null, [], ["G"], "iko", "Ikoria: Lair of Behemoths", "175"),
        new("Elspeth, Sun's Champion", "{4}{W}{W}", "Legendary Planeswalker — Elspeth", "+1: Create three 1/1 white Soldier creature tokens.\n−3: Destroy all creatures with power 4 or greater.\n−7: You get an emblem with \"Creatures you control get +2/+2 and have flying.\"", null, null, [], ["W"], "mkc", "Murders at Karlov Manor Commander", "62"),
        new("Garruk Wildspeaker", "{2}{G}{G}", "Legendary Planeswalker — Garruk", "+1: Untap two target lands.\n−1: Create a 3/3 green Beast creature token.\n−4: Creatures you control get +3/+3 and gain trample until end of turn.", null, null, [], ["G"], "cmd", "Commander 2011", "157"),
        new("Karn Liberated", "{7}", "Legendary Planeswalker — Karn", "+4: Target player exiles a card from their hand.\n−3: Exile target permanent.\n−14: Restart the game, leaving in exile all non-Aura permanent cards exiled with Karn. Then put those cards onto the battlefield under your control.", null, null, [], [], "2xm", "Double Masters", "1"),
        new("Ugin, the Spirit Dragon", "{8}", "Legendary Planeswalker — Ugin", "+2: Ugin deals 3 damage to any target.\n−X: Exile each permanent with mana value X or less that's one or more colors.\n−10: You gain 7 life, draw seven cards, then put up to seven permanent cards from your hand onto the battlefield.", null, null, [], [], "m21", "Core Set 2021", "1"),
        new("Doubling Season", "{4}{G}", "Enchantment", "If an effect would create one or more tokens under your control, it creates twice that many of those tokens instead.\nIf an effect would put one or more counters on a permanent you control, it puts twice that many of those counters on that permanent instead.", null, null, [], ["G"], "fdn", "Foundations", "216"),
        new("Evolution Sage", "{2}{G}", "Creature — Elf Druid", "Landfall — Whenever a land you control enters, proliferate. (Choose any number of permanents and/or players, then give each another counter of each kind already there.)", "3", "2", ["Proliferate", "Landfall"], ["G"], "ecc", "Lorwyn Eclipsed Commander", "105"),
        new("Flux Channeler", "{2}{U}", "Creature — Human Wizard", "Whenever you cast a noncreature spell, proliferate. (Choose any number of permanents and/or players, then give each another counter of each kind already there.)", "2", "2", ["Proliferate"], ["U"], "cmm", "Commander Masters", "847"),
        new("Inexorable Tide", "{3}{U}{U}", "Enchantment", "Whenever you cast a spell, proliferate. (Choose any number of permanents and/or players, then give each another counter of each kind already there.)", null, null, ["Proliferate"], ["U"], "mm2", "Modern Masters 2015", "49"),
        new("Deepglow Skate", "{4}{U}", "Creature — Fish", "When this creature enters, double the number of each kind of counter on any number of target permanents.", "3", "3", ["Double"], ["U"], "eoc", "Edge of Eternities Commander", "70"),
        new("Hardened Scales", "{G}", "Enchantment", "If one or more +1/+1 counters would be put on a creature you control, that many plus one +1/+1 counters are put on it instead.", null, null, [], ["G"], "soc", "Secrets of Strixhaven Commander", "272"),
        new("Vorinclex, Monstrous Raider", "{4}{G}{G}", "Legendary Creature — Phyrexian Praetor", "Trample, haste\nIf you would put one or more counters on a permanent or player, put twice that many of each of those kinds of counters on that permanent or player instead.\nIf an opponent would put one or more counters on a permanent or player, they put half that many of each of those kinds of counters on that permanent or player instead, rounded down.", "6", "6", ["Haste", "Trample"], ["G"], "khm", "Kaldheim", "199"),
        new("Hadana's Climb", "{1}{G}{U}", "Legendary Enchantment // Legendary Land", "At the beginning of combat on your turn, put a +1/+1 counter on target creature you control. Then if that creature has three or more +1/+1 counters on it, transform Hadana's Climb.\n//\n(Transforms from Hadana's Climb.)\n{T}: Add one mana of any color.\n{1}{G}{U}, {T}: Target creature you control gains flying and gets +X/+X until end of turn, where X is its power.", null, null, ["Transform"], ["G", "U"], "rix", "Rivals of Ixalan", "158"),
        new("The Ozolith", "{1}", "Legendary Artifact", "Whenever a creature you control leaves the battlefield, if it had counters on it, put those counters on The Ozolith.\nAt the beginning of combat on your turn, if The Ozolith has counters on it, you may move all counters from The Ozolith onto target creature.", null, null, [], [], "iko", "Ikoria: Lair of Behemoths", "237"),
        new("Conclave Mentor", "{G}{W}", "Creature — Centaur Cleric", "If one or more +1/+1 counters would be put on a creature you control, that many plus one +1/+1 counters are put on that creature instead.\nWhen this creature dies, you gain life equal to its power.", "2", "2", [], ["G", "W"], "moc", "March of the Machine Commander", "320"),
        new("Forgotten Ancient", "{3}{G}", "Creature — Elemental", "Whenever a player casts a spell, you may put a +1/+1 counter on this creature.\nAt the beginning of your upkeep, you may move any number of +1/+1 counters from this creature onto other creatures.", "0", "3", [], ["G"], "soc", "Secrets of Strixhaven Commander", "267"),
        new("Birds of Paradise", "{G}", "Creature — Bird", "Flying\n{T}: Add one mana of any color.", "0", "1", ["Flying"], ["G"], "rvr", "Ravnica Remastered", "133"),
        new("Sakura-Tribe Elder", "{1}{G}", "Creature — Snake Shaman", "Sacrifice this creature: Search your library for a basic land card, put that card onto the battlefield tapped, then shuffle.", "1", "1", [], ["G"], "soc", "Secrets of Strixhaven Commander", "285"),
        new("Eternal Witness", "{1}{G}{G}", "Creature — Human Shaman", "When this creature enters, you may return target card from your graveyard to your hand.", "2", "1", [], ["G"], "cmm", "Commander Masters", "286"),
        new("Solemn Simulacrum", "{4}", "Artifact Creature — Golem", "When this creature enters, you may search your library for a basic land card, put that card onto the battlefield tapped, then shuffle.\nWhen this creature dies, you may draw a card.", "2", "2", [], [], "soc", "Secrets of Strixhaven Commander", "355"),
        new("Esper Sentinel", "{W}", "Artifact Creature — Human Soldier", "Whenever an opponent casts their first noncreature spell each turn, draw a card unless that player pays {X}, where X is this creature's power.", "1", "1", [], ["W"], "mh2", "Modern Horizons 2", "12"),
        new("Spark Double", "{3}{U}", "Creature — Illusion", "You may have this creature enter as a copy of a creature or planeswalker you control, except it enters with an additional +1/+1 counter on it if it's a creature, it enters with an additional loyalty counter on it if it's a planeswalker, and it isn't legendary.", "0", "0", [], ["U"], "rvr", "Ravnica Remastered", "62"),
        new("Oracle of Mul Daya", "{3}{G}", "Creature — Elf Shaman", "You may play an additional land on each of your turns.\nPlay with the top card of your library revealed.\nYou may play lands from the top of your library.", "2", "2", [], ["G"], "eoc", "Edge of Eternities Commander", "102"),
        new("Knight of Autumn", "{1}{G}{W}", "Creature — Dryad Knight", "When this creature enters, choose one —\n• Put two +1/+1 counters on this creature.\n• Destroy target artifact or enchantment.\n• You gain 4 life.", "2", "1", [], ["G", "W"], "afc", "Forgotten Realms Commander", "187"),
        new("Reclamation Sage", "{2}{G}", "Creature — Elf Shaman", "When this creature enters, you may destroy target artifact or enchantment.", "2", "1", [], ["G"], "fdn", "Foundations", "231"),
        new("Cytoplast Root-Kin", "{2}{G}{G}", "Creature — Elemental Mutant", "Graft 4 (This creature enters with four +1/+1 counters on it. Whenever another creature enters, you may move a +1/+1 counter from this creature onto it.)\nWhen this creature enters, put a +1/+1 counter on each other creature you control with a +1/+1 counter on it.\n{2}: Move a +1/+1 counter from target creature you control onto this creature.", "0", "0", ["Graft"], ["G"], "mm2", "Modern Masters 2015", "143"),
        new("Tatyova, Benthic Druid", "{3}{G}{U}", "Legendary Creature — Merfolk Druid", "Landfall — Whenever a land you control enters, you gain 1 life and draw a card.", "3", "3", ["Landfall"], ["G", "U"], "fdn", "Foundations", "247"),
        new("Sol Ring", "{1}", "Artifact", "{T}: Add {C}{C}.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "211"),
        new("Arcane Signet", "{2}", "Artifact", "{T}: Add one mana of any color in your commander's color identity.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "191"),
        new("Fellwar Stone", "{2}", "Artifact", "{T}: Add one mana of any color that a land an opponent controls could produce.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "285"),
        new("Chromatic Lantern", "{3}", "Artifact", "Lands you control have \"{T}: Add one mana of any color.\"\n{T}: Add one mana of any color.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "195"),
        new("Cultivate", "{2}{G}", "Sorcery", "Search your library for up to two basic land cards, reveal those cards, put one onto the battlefield tapped and the other into your hand, then shuffle.", null, null, [], ["G"], "msc", "Marvel Super Heroes Commander", "172"),
        new("Kodama's Reach", "{2}{G}", "Sorcery — Arcane", "Search your library for up to two basic land cards, reveal those cards, put one onto the battlefield tapped and the other into your hand, then shuffle.", null, null, [], ["G"], "ecc", "Lorwyn Eclipsed Commander", "113"),
        new("Farseek", "{1}{G}", "Sorcery", "Search your library for a Plains, Island, Swamp, or Mountain card, put it onto the battlefield tapped, then shuffle.", null, null, [], ["G"], "msc", "Marvel Super Heroes Commander", "173"),
        new("Nature's Lore", "{1}{G}", "Sorcery", "Search your library for a Forest card, put that card onto the battlefield, then shuffle.", null, null, [], ["G"], "soc", "Secrets of Strixhaven Commander", "278"),
        new("Rampant Growth", "{1}{G}", "Sorcery", "Search your library for a basic land card, put that card onto the battlefield tapped, then shuffle.", null, null, [], ["G"], "tdc", "Tarkir: Dragonstorm Commander", "265"),
        new("Skyshroud Claim", "{3}{G}", "Sorcery", "Search your library for up to two Forest cards, put them onto the battlefield, then shuffle.", null, null, [], ["G"], "eoc", "Edge of Eternities Commander", "107"),
        new("Rhystic Study", "{2}{U}", "Enchantment", "Whenever an opponent casts a spell, you may draw a card unless that player pays {1}.", null, null, [], ["U"], "j22", "Jumpstart 2022", "114"),
        new("Sylvan Library", "{1}{G}", "Enchantment", "At the beginning of your draw step, you may draw two additional cards. If you do, choose two cards in your hand drawn this turn. For each of those cards, pay 4 life or put the card on top of your library.", null, null, [], ["G"], "dmr", "Dominaria Remastered", "179"),
        new("Mystic Remora", "{U}", "Enchantment", "Cumulative upkeep {1} (At the beginning of your upkeep, put an age counter on this permanent, then sacrifice it unless you pay its upkeep cost for each age counter on it.)\nWhenever an opponent casts a noncreature spell, you may draw a card unless that player pays {4}.", null, null, ["Cumulative upkeep"], ["U"], "dmr", "Dominaria Remastered", "59"),
        new("Fact or Fiction", "{3}{U}", "Instant", "Reveal the top five cards of your library. An opponent separates those cards into two piles. Put one pile into your hand and the other into your graveyard.", null, null, [], ["U"], "cmm", "Commander Masters", "91"),
        new("Tezzeret's Gambit", "{3}{U/P}", "Sorcery", "({U/P} can be paid with either {U} or 2 life.)\nDraw two cards, then proliferate. (Choose any number of permanents and/or players, then give each another counter of each kind already there.)", null, null, ["Proliferate"], ["U"], "eoc", "Edge of Eternities Commander", "47"),
        new("Painful Truths", "{2}{B}", "Sorcery", "Converge — You draw X cards and lose X life, where X is the number of colors of mana spent to cast this spell.", null, null, ["Converge"], ["B"], "ecc", "Lorwyn Eclipsed Commander", "82"),
        new("Swords to Plowshares", "{W}", "Instant", "Exile target creature. Its controller gains life equal to its power.", null, null, [], ["W"], "msc", "Marvel Super Heroes Commander", "143"),
        new("Path to Exile", "{W}", "Instant", "Exile target creature. Its controller may search their library for a basic land card, put that card onto the battlefield tapped, then shuffle.", null, null, [], ["W"], "msc", "Marvel Super Heroes Commander", "141"),
        new("Anguished Unmaking", "{1}{W}{B}", "Instant", "Exile target nonland permanent. You lose 3 life.", null, null, [], ["B", "W"], "soc", "Secrets of Strixhaven Commander", "293"),
        new("Despark", "{W}{B}", "Instant", "Exile target permanent with mana value 4 or greater.", null, null, [], ["B", "W"], "tdc", "Tarkir: Dragonstorm Commander", "284"),
        new("Generous Gift", "{2}{W}", "Instant", "Destroy target permanent. Its controller creates a 3/3 green Elephant creature token.", null, null, [], ["W"], "lcc", "The Lost Caverns of Ixalan Commander", "128"),
        new("Beast Within", "{2}{G}", "Instant", "Destroy target permanent. Its controller creates a 3/3 green Beast creature token.", null, null, [], ["G"], "soc", "Secrets of Strixhaven Commander", "263"),
        new("Cyclonic Rift", "{1}{U}", "Instant", "Return target nonland permanent you don't control to its owner's hand.\nOverload {6}{U} (You may cast this spell for its overload cost. If you do, change \"target\" in its text to \"each.\")", null, null, ["Overload"], ["U"], "rvr", "Ravnica Remastered", "40"),
        new("Counterspell", "{U}{U}", "Instant", "Counter target spell.", null, null, [], ["U"], "dsc", "Duskmourn: House of Horror Commander", "114"),
        new("Swan Song", "{U}", "Instant", "Counter target enchantment, instant, or sorcery spell. Its controller creates a 2/2 blue Bird creature token with flying.", null, null, [], ["U"], "eoc", "Edge of Eternities Commander", "46"),
        new("Assassin's Trophy", "{B}{G}", "Instant", "Destroy target permanent an opponent controls. Its controller may search their library for a basic land card, put it onto the battlefield, then shuffle.", null, null, [], ["B", "G"], "soc", "Secrets of Strixhaven Commander", "294"),
        new("Vindicate", "{1}{W}{B}", "Sorcery", "Destroy target permanent.", null, null, [], ["B", "W"], "mh2", "Modern Horizons 2", "294"),
        new("Putrefy", "{1}{B}{G}", "Instant", "Destroy target artifact or creature. It can't be regenerated.", null, null, [], ["B", "G"], "ecc", "Lorwyn Eclipsed Commander", "131"),
        new("Toxic Deluge", "{2}{B}", "Sorcery", "As an additional cost to cast this spell, pay X life.\nAll creatures get -X/-X until end of turn.", null, null, [], ["B"], "soc", "Secrets of Strixhaven Commander", "120"),
        new("Supreme Verdict", "{1}{W}{W}{U}", "Sorcery", "This spell can't be countered.\nDestroy all creatures.", null, null, [], ["U", "W"], "clu", "Ravnica: Clue Edition", "211"),
        new("Damn", "{B}{B}", "Sorcery", "Destroy target creature. A creature destroyed this way can't be regenerated.\nOverload {2}{W}{W} (You may cast this spell for its overload cost. If you do, change \"target\" in its text to \"each.\")", null, null, ["Overload"], ["B", "W"], "lcc", "The Lost Caverns of Ixalan Commander", "191"),
        new("Command Tower", "", "Land", "{T}: Add one mana of any color in your commander's color identity.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "233"),
        new("Exotic Orchard", "", "Land", "{T}: Add one mana of any color that a land an opponent controls could produce.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "241"),
        new("City of Brass", "", "Land", "Whenever this land becomes tapped, it deals 1 damage to you.\n{T}: Add one mana of any color.", null, null, [], [], "tmc", "Teenage Mutant Ninja Turtles Eternal", "62"),
        new("Mana Confluence", "", "Land", "{T}, Pay 1 life: Add one mana of any color.", null, null, [], [], "jou", "Journey into Nyx", "163"),
        new("Spara's Headquarters", "", "Land — Forest Plains Island", "({T}: Add {G}, {W}, or {U}.)\nThis land enters tapped.\nCycling {3} ({3}, Discard this card: Draw a card.)", null, null, ["Cycling"], ["G", "U", "W"], "snc", "Streets of New Capenna", "257"),
        new("Raffine's Tower", "", "Land — Plains Island Swamp", "({T}: Add {W}, {U}, or {B}.)\nThis land enters tapped.\nCycling {3} ({3}, Discard this card: Draw a card.)", null, null, ["Cycling"], ["B", "U", "W"], "snc", "Streets of New Capenna", "254"),
        new("Xander's Lounge", "", "Land — Island Swamp Mountain", "({T}: Add {U}, {B}, or {R}.)\nThis land enters tapped.\nCycling {3} ({3}, Discard this card: Draw a card.)", null, null, ["Cycling"], ["B", "R", "U"], "snc", "Streets of New Capenna", "260"),
        new("Plaza of Heroes", "", "Land", "{T}: Add {C}.\n{T}: Add one mana of any color. Spend this mana only to cast a legendary spell.\n{T}: Add one mana of any color among legendary permanents you control.\n{3}, {T}, Exile this land: Target legendary creature gains hexproof and indestructible until end of turn.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "255"),
        new("Breeding Pool", "", "Land — Forest Island", "({T}: Add {G} or {U}.)\nAs this land enters, you may pay 2 life. If you don't, it enters tapped.", null, null, [], ["G", "U"], "eoe", "Edge of Eternities", "251"),
        new("Watery Grave", "", "Land — Island Swamp", "({T}: Add {U} or {B}.)\nAs this land enters, you may pay 2 life. If you don't, it enters tapped.", null, null, [], ["B", "U"], "eoe", "Edge of Eternities", "261"),
        new("Overgrown Tomb", "", "Land — Swamp Forest", "({T}: Add {B} or {G}.)\nAs this land enters, you may pay 2 life. If you don't, it enters tapped.", null, null, [], ["B", "G"], "ecl", "Lorwyn Eclipsed", "266"),
        new("Hallowed Fountain", "", "Land — Plains Island", "({T}: Add {W} or {U}.)\nAs this land enters, you may pay 2 life. If you don't, it enters tapped.", null, null, [], ["U", "W"], "ecl", "Lorwyn Eclipsed", "265"),
        new("Godless Shrine", "", "Land — Plains Swamp", "({T}: Add {W} or {B}.)\nAs this land enters, you may pay 2 life. If you don't, it enters tapped.", null, null, [], ["B", "W"], "eoe", "Edge of Eternities", "254"),
        new("Temple Garden", "", "Land — Forest Plains", "({T}: Add {G} or {W}.)\nAs this land enters, you may pay 2 life. If you don't, it enters tapped.", null, null, [], ["G", "W"], "ecl", "Lorwyn Eclipsed", "268"),
        new("Drowned Catacomb", "", "Land", "This land enters tapped unless you control an Island or a Swamp.\n{T}: Add {U} or {B}.", null, null, [], ["B", "U"], "otc", "Outlaws of Thunder Junction Commander", "290"),
        new("Glacial Fortress", "", "Land", "This land enters tapped unless you control a Plains or an Island.\n{T}: Add {W} or {U}.", null, null, [], ["U", "W"], "msc", "Marvel Super Heroes Commander", "248"),
        new("Isolated Chapel", "", "Land", "This land enters tapped unless you control a Plains or a Swamp.\n{T}: Add {W} or {B}.", null, null, [], ["B", "W"], "soc", "Secrets of Strixhaven Commander", "382"),
        new("Hinterland Harbor", "", "Land", "This land enters tapped unless you control a Forest or an Island.\n{T}: Add {G} or {U}.", null, null, [], ["G", "U"], "msc", "Marvel Super Heroes Commander", "250"),
        new("Sunpetal Grove", "", "Land", "This land enters tapped unless you control a Forest or a Plains.\n{T}: Add {G} or {W}.", null, null, [], ["G", "W"], "msc", "Marvel Super Heroes Commander", "272"),
        new("Woodland Cemetery", "", "Land", "This land enters tapped unless you control a Swamp or a Forest.\n{T}: Add {B} or {G}.", null, null, [], ["B", "G"], "soc", "Secrets of Strixhaven Commander", "424"),
        new("Karn's Bastion", "", "Land", "{T}: Add {C}.\n{4}, {T}: Proliferate. (Choose any number of permanents and/or players, then give each another counter of each kind already there.)", null, null, ["Proliferate"], [], "eoc", "Edge of Eternities Commander", "163"),
        new("Reliquary Tower", "", "Land", "You have no maximum hand size.\n{T}: Add {C}.", null, null, [], [], "soc", "Secrets of Strixhaven Commander", "398"),
        new("Yavimaya, Cradle of Growth", "", "Legendary Land", "Each land is a Forest in addition to its other land types.", null, null, [], [], "mh2", "Modern Horizons 2", "261"),
        new("Bojuka Bog", "", "Land", "This land enters tapped.\nWhen this land enters, exile target player's graveyard.\n{T}: Add {B}.", null, null, [], ["B"], "soc", "Secrets of Strixhaven Commander", "363"),
        new("Forest", "", "Basic Land — Forest", "({T}: Add {G}.)", null, null, [], ["G"], "hob", "The Hobbit", "198"),
        new("Island", "", "Basic Land — Island", "({T}: Add {U}.)", null, null, [], ["U"], "hob", "The Hobbit", "195"),
        new("Swamp", "", "Basic Land — Swamp", "({T}: Add {B}.)", null, null, [], ["B"], "hob", "The Hobbit", "196"),
        new("Plains", "", "Basic Land — Plains", "({T}: Add {W}.)", null, null, [], ["W"], "hob", "The Hobbit", "194"),
    ];

    private static IReadOnlyList<ScryfallCard> GetLightPawsCards() =>
    [
        new("Light-Paws, Emperor's Voice", "{1}{W}", "Legendary Creature — Fox Advisor", "Whenever an Aura you control enters, if you cast it, you may search your library for an Aura card with mana value less than or equal to that Aura and with a different name than each Aura you control, put that card onto the battlefield attached to Light-Paws, then shuffle.", "2", "2", [], ["W"], "neo", "Kamigawa: Neon Dynasty", "25"),
        new("Arcane Signet", "{2}", "Artifact", "{T}: Add one mana of any color in your commander's color identity.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "191"),
        new("Chrome Mox", "{0}", "Artifact", "Imprint — When this artifact enters, you may exile a nonartifact, nonland card from your hand.\n{T}: Add one mana of any of the exiled card's colors.", null, null, ["Imprint"], [], "2xm", "Double Masters", "240"),
        new("Mind Stone", "{2}", "Artifact", "{T}: Add {C}.\n{1}, {T}, Sacrifice this artifact: Draw a card.", null, null, [], [], "soc", "Secrets of Strixhaven Commander", "352"),
        new("Mox Amber", "{0}", "Legendary Artifact", "{T}: Add one mana of any color among legendary creatures and planeswalkers you control.", null, null, [], [], "dom", "Dominaria", "224"),
        new("Mox Diamond", "{0}", "Artifact", "If this artifact would enter, you may discard a land card instead. If you do, put this artifact onto the battlefield. If you don't, put it into its owner's graveyard.\n{T}: Add one mana of any color.", null, null, [], [], "tpr", "Tempest Remastered", "228"),
        new("Sol Ring", "{1}", "Artifact", "{T}: Add {C}{C}.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "211"),
        new("Thought Vessel", "{2}", "Artifact", "You have no maximum hand size.\n{T}: Add {C}.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "222"),
        new("Esper Sentinel", "{W}", "Artifact Creature — Human Soldier", "Whenever an opponent casts their first noncreature spell each turn, draw a card unless that player pays {X}, where X is this creature's power.", "1", "1", [], ["W"], "mh2", "Modern Horizons 2", "12"),
        new("Hero of Iroas", "{1}{W}", "Creature — Human Soldier", "Aura spells you cast cost {1} less to cast.\nHeroic — Whenever you cast a spell that targets this creature, put a +1/+1 counter on this creature.", "2", "2", ["Heroic"], ["W"], "uma", "Ultimate Masters", "20"),
        new("Kor Spiritdancer", "{1}{W}", "Creature — Kor Wizard", "This creature gets +2/+2 for each Aura attached to it.\nWhenever you cast an Aura spell, you may draw a card.", "0", "2", [], ["W"], "soc", "Secrets of Strixhaven Commander", "152"),
        new("Mesa Enchantress", "{1}{W}{W}", "Creature — Human Druid", "Whenever you cast an enchantment spell, you may draw a card.", "0", "2", [], ["W"], "dsc", "Duskmourn: House of Horror Commander", "68"),
        new("Ondu Spiritdancer", "{4}{W}", "Creature — Kor Cleric", "Whenever an enchantment you control enters, you may create a token that's a copy of it. Do this only once each turn.", "3", "3", [], ["W"], "dsc", "Duskmourn: House of Horror Commander", "101"),
        new("Pearl-Ear, Imperial Advisor", "{1}{W}{W}", "Legendary Creature — Fox Advisor", "Lifelink\nEnchantment spells you cast have affinity for Auras. (They cost {1} less to cast for each Aura you control.)\nWhenever you cast an Aura spell that targets a modified permanent you control, draw a card. (Equipment, Auras you control, and counters are modifications.)", "3", "4", ["Lifelink"], ["W"], "soc", "Secrets of Strixhaven Commander", "160"),
        new("Sram, Senior Edificer", "{1}{W}", "Legendary Creature — Dwarf Advisor", "Whenever you cast an Aura, Equipment, or Vehicle spell, draw a card.", "2", "2", [], ["W"], "soc", "Secrets of Strixhaven Commander", "176"),
        new("Starfield Mystic", "{1}{W}", "Creature — Human Cleric", "Enchantment spells you cast cost {1} less to cast.\nWhenever an enchantment you control is put into a graveyard from the battlefield, put a +1/+1 counter on this creature.", "2", "2", [], ["W"], "soc", "Secrets of Strixhaven Commander", "177"),
        new("Transcendent Envoy", "{1}{W}", "Enchantment Creature — Griffin", "Flying\nAura spells you cast cost {1} less to cast.", "1", "2", ["Flying"], ["W"], "soc", "Secrets of Strixhaven Commander", "183"),
        new("All That Glitters", "{1}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature gets +1/+1 for each artifact and/or enchantment you control.", null, null, ["Enchant"], ["W"], "cmm", "Commander Masters", "9"),
        new("Angelic Destiny", "{2}{W}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature gets +4/+4, has flying and first strike, and is an Angel in addition to its other types.\nWhen enchanted creature dies, return this card to its owner's hand.", null, null, ["Enchant"], ["W"], "soc", "Secrets of Strixhaven Commander", "134"),
        new("Armored Ascension", "{3}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature gets +1/+1 for each Plains you control and has flying.", null, null, ["Enchant"], ["W"], "m11", "Magic 2011", "5"),
        new("Benevolent Blessing", "{1}{W}", "Enchantment — Aura", "Flash\nEnchant creature\nAs this Aura enters, choose a color.\nEnchanted creature has protection from the chosen color. This effect doesn't remove Auras and Equipment you control that are already attached to it.", null, null, ["Enchant", "Flash"], ["W"], "cmr", "Commander Legends", "13"),
        new("Chains of Custody", "{2}{W}", "Enchantment — Aura", "Enchant creature you control\nWhen this Aura enters, exile target nonland permanent an opponent controls until this Aura leaves the battlefield.\nEnchanted creature has ward {2}. (Whenever it becomes the target of a spell or ability an opponent controls, counter it unless that player pays {2}.)", null, null, ["Enchant"], ["W"], "soc", "Secrets of Strixhaven Commander", "139"),
        new("Darksteel Mutation", "{1}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature is an Insect artifact creature with base power and toughness 0/1 and has indestructible, and it loses all other abilities, card types, and creature types.", null, null, ["Enchant"], ["W"], "soc", "Secrets of Strixhaven Commander", "142"),
        new("Daybreak Coronet", "{W}{W}", "Enchantment — Aura", "Enchant creature with another Aura attached to it\nEnchanted creature gets +3/+3 and has first strike, vigilance, and lifelink. (Damage dealt by the creature also causes its controller to gain that much life.)", null, null, ["Enchant"], ["W"], "uma", "Ultimate Masters", "14"),
        new("Entangler", "{2}{W}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature can block any number of creatures.", null, null, ["Enchant"], ["W"], "pcy", "Prophecy", "7"),
        new("Ethereal Armor", "{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature gets +1/+1 for each enchantment you control and has first strike.", null, null, ["Enchant"], ["W"], "dsk", "Duskmourn: House of Horror", "7"),
        new("Faith Unbroken", "{3}{W}", "Enchantment — Aura", "Enchant creature you control\nWhen this Aura enters, exile target creature an opponent controls until this Aura leaves the battlefield.\nEnchanted creature gets +2/+2.", null, null, ["Enchant"], ["W"], "inr", "Innistrad Remastered", "21"),
        new("Feather of Flight", "{1}{W}", "Enchantment — Aura", "Flash\nEnchant creature\nWhen this Aura enters, draw a card.\nEnchanted creature gets +1/+0 and has flying.", null, null, ["Enchant", "Flash"], ["W"], "blb", "Bloomburrow", "13"),
        new("Felidar Umbra", "{1}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature has lifelink.\n{1}{W}: Attach this Aura to target creature you control.\nUmbra armor (If enchanted creature would be destroyed, instead remove all damage from it and destroy this Aura.)", null, null, ["Umbra armor", "Enchant"], ["W"], "pca", "Planechase Anthology", "6"),
        new("Idolized", "{1}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature has \"Whenever this creature attacks alone, it gets +X/+X until end of turn, where X is the number of nonland permanents you control.\"", null, null, ["Enchant"], ["W"], "pip", "Fallout", "17"),
        new("Mantle of the Ancients", "{3}{W}{W}", "Enchantment — Aura", "Enchant creature you control\nWhen this Aura enters, return any number of target Aura and/or Equipment cards from your graveyard to the battlefield attached to enchanted creature.\nEnchanted creature gets +1/+1 for each Aura and Equipment attached to it.", null, null, ["Enchant"], ["W"], "afc", "Forgotten Realms Commander", "8"),
        new("Mask of Law and Grace", "{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature has protection from black and from red.", null, null, ["Enchant"], ["W"], "uds", "Urza's Destiny", "11"),
        new("On Serra's Wings", "{3}{W}", "Legendary Enchantment — Aura", "Enchant creature\nEnchanted creature is legendary, gets +1/+1, and has flying, vigilance, and lifelink.", null, null, ["Enchant"], ["W"], "cmr", "Commander Legends", "380"),
        new("Ossification", "{1}{W}", "Enchantment — Aura", "Enchant basic land you control\nWhen this Aura enters, exile target creature or planeswalker an opponent controls until this Aura leaves the battlefield.", null, null, ["Enchant"], ["W"], "one", "Phyrexia: All Will Be One", "26"),
        new("Pariah", "{2}{W}", "Enchantment — Aura", "Enchant creature\nAll damage that would be dealt to you is dealt to enchanted creature instead.", null, null, ["Enchant"], ["W"], "cn2", "Conspiracy: Take the Crown", "95"),
        new("Reprobation", "{1}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature loses all abilities and is a Coward creature with base power and toughness 0/1. (It keeps all supertypes but loses all other types and creature types.)", null, null, ["Enchant"], ["W"], "mh1", "Modern Horizons", "23"),
        new("Sage's Reverie", "{3}{W}", "Enchantment — Aura", "Enchant creature\nWhen this Aura enters, draw a card for each Aura you control that's attached to a creature.\nEnchanted creature gets +1/+1 for each Aura you control that's attached to a creature.", null, null, ["Enchant"], ["W"], "soc", "Secrets of Strixhaven Commander", "165"),
        new("Shardmage's Rescue", "{W}", "Enchantment — Aura", "Flash\nEnchant creature you control\nAs long as this Aura entered this turn, enchanted creature has hexproof.\nEnchanted creature gets +1/+1.", null, null, ["Enchant", "Flash"], ["W"], "dsk", "Duskmourn: House of Horror", "29"),
        new("Sheltered by Ghosts", "{1}{W}", "Enchantment — Aura", "Enchant creature you control\nWhen this Aura enters, exile target nonland permanent an opponent controls until this Aura leaves the battlefield.\nEnchanted creature gets +1/+0 and has lifelink and ward {2}.", null, null, ["Enchant"], ["W"], "soc", "Secrets of Strixhaven Commander", "171"),
        new("Shield of Duty and Reason", "{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature has protection from green and from blue.", null, null, ["Enchant"], ["W"], "apc", "Apocalypse", "16"),
        new("Shielded by Faith", "{1}{W}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature has indestructible.\nWhenever a creature enters, you may attach this Aura to that creature.", null, null, ["Enchant"], ["W"], "soc", "Secrets of Strixhaven Commander", "172"),
        new("Sigarda's Aid", "{W}", "Enchantment", "You may cast Aura and Equipment spells as though they had flash.\nWhenever an Equipment you control enters, you may attach it to target creature you control.", null, null, [], ["W"], "cmr", "Commander Legends", "384"),
        new("Songbirds' Blessing", "{3}{W}", "Enchantment — Aura", "Enchant creature\nWhenever enchanted creature attacks, reveal cards from the top of your library until you reveal an Aura card. You may put that card onto the battlefield. If you don't, put it into your hand. Put the rest on the bottom of your library in a random order.", null, null, ["Enchant"], ["W"], "soc", "Secrets of Strixhaven Commander", "174"),
        new("Sphere of Safety", "{4}{W}", "Enchantment", "Creatures can't attack you or planeswalkers you control unless their controller pays {X} for each of those creatures, where X is the number of enchantments you control.", null, null, [], ["W"], "dsc", "Duskmourn: House of Horror Commander", "104"),
        new("Spirit Link", "{W}", "Enchantment — Aura", "Enchant creature (Target a creature as you cast this. This card enters attached to that creature.)\nWhenever enchanted creature deals damage, you gain that much life.", null, null, ["Enchant"], ["W"], "dmr", "Dominaria Remastered", "29"),
        new("Spirit Mantle", "{1}{W}", "Enchantment — Aura", "Enchant creature\nEnchanted creature gets +1/+1 and has protection from creatures.", null, null, ["Enchant"], ["W"], "soc", "Secrets of Strixhaven Commander", "175"),
        new("Tempest Technique", "{3}{W}", "Enchantment — Aura", "Storm (When you cast this spell, copy it for each spell cast before it this turn. You may choose new targets for the copies. Copies become tokens.)\nEnchant creature you control\nEnchanted creature gets +1/+1 for each enchantment you control.", null, null, ["Storm", "Enchant"], ["W"], "tdc", "Tarkir: Dragonstorm Commander", "16"),
        new("Timely Ward", "{2}{W}", "Enchantment — Aura", "You may cast this spell as though it had flash if it targets a commander.\nEnchant creature\nEnchanted creature has indestructible.", null, null, ["Enchant"], ["W"], "dsc", "Duskmourn: House of Horror Commander", "107"),
        new("Twinblade Blessing", "{1}{W}{W}", "Enchantment — Aura", "Flash (You may cast this spell any time you could cast an instant.)\nEnchant creature\nEnchanted creature has double strike. (It deals both first-strike and regular combat damage.)", null, null, ["Enchant", "Flash"], ["W"], "fdn", "Foundations", "26"),
        new("Disenchant", "{1}{W}", "Instant", "Destroy target artifact or enchantment.", null, null, [], ["W"], "fdn", "Foundations", "572"),
        new("Enlightened Tutor", "{W}", "Instant", "Search your library for an artifact or enchantment card, reveal it, put it on top, then shuffle.", null, null, [], ["W"], "dmr", "Dominaria Remastered", "6"),
        new("Galadriel's Dismissal", "{W}", "Instant", "Kicker {2}{W} (You may pay an additional {2}{W} as you cast this spell.)\nTarget creature phases out. If this spell was kicked, each creature target player controls phases out instead. (Treat phased-out creatures and anything attached to them as though they don't exist until their controller's next turn.)", null, null, ["Kicker"], ["W"], "ltc", "Tales of Middle-earth Commander", "500"),
        new("Path to Exile", "{W}", "Instant", "Exile target creature. Its controller may search their library for a basic land card, put that card onto the battlefield tapped, then shuffle.", null, null, [], ["W"], "msc", "Marvel Super Heroes Commander", "141"),
        new("Razorgrass Ambush // Razorgrass Field", "{1}{W}", "Instant // Land", "Razorgrass Ambush deals 3 damage to target attacking or blocking creature.\n//\nAs this land enters, you may pay 3 life. If you don't, it enters tapped.\n{T}: Add {W}.", null, null, [], ["W"], "mh3", "Modern Horizons 3", "238"),
        new("Stroke of Midnight", "{2}{W}", "Instant", "Destroy target nonland permanent. Its controller creates a 1/1 white Human creature token.", null, null, [], ["W"], "tdc", "Tarkir: Dragonstorm Commander", "132"),
        new("Swords to Plowshares", "{W}", "Instant", "Exile target creature. Its controller gains life equal to its power.", null, null, [], ["W"], "msc", "Marvel Super Heroes Commander", "143"),
        new("Teferi's Protection", "{2}{W}", "Instant", "Until your next turn, your life total can't change and you gain protection from everything. All permanents you control phase out. (While they're phased out, they're treated as though they don't exist. They phase in before you untap during your untap step.)\nExile Teferi's Protection.", null, null, [], ["W"], "2x2", "Double Masters 2022", "32"),
        new("Ancient Tomb", "", "Land", "{T}: Add {C}{C}. This land deals 2 damage to you.", null, null, [], [], "uma", "Ultimate Masters", "236"),
        new("Eiganjo, Seat of the Empire", "", "Legendary Land", "{T}: Add {W}.\nChannel — {2}{W}, Discard this card: It deals 4 damage to target attacking or blocking creature. This ability costs {1} less to activate for each legendary creature you control.", null, null, ["Channel"], ["W"], "neo", "Kamigawa: Neon Dynasty", "268"),
        new("Gemstone Caverns", "", "Legendary Land", "If this card is in your opening hand and you're not the starting player, you may begin the game with Gemstone Caverns on the battlefield with a luck counter on it. If you do, exile a card from your hand.\n{T}: Add {C}. If Gemstone Caverns has a luck counter on it, instead add one mana of any color.", null, null, [], [], "tsr", "Time Spiral Remastered", "280"),
        new("Hall of Heliod's Generosity", "", "Legendary Land", "{T}: Add {C}.\n{1}{W}, {T}: Put target enchantment card from your graveyard on top of your library.", null, null, [], ["W"], "dsc", "Duskmourn: House of Horror Commander", "283"),
        new("Mistveil Plains", "", "Land — Plains", "({T}: Add {W}.)\nThis land enters tapped.\n{W}, {T}: Put target card from your graveyard on the bottom of your library. Activate only if you control two or more white permanents.", null, null, [], ["W"], "soc", "Secrets of Strixhaven Commander", "386"),
        new("Nykthos, Shrine to Nyx", "", "Legendary Land", "{T}: Add {C}.\n{2}, {T}: Choose a color. Add an amount of mana of that color equal to your devotion to that color. (Your devotion to a color is the number of mana symbols of that color in the mana costs of permanents you control.)", null, null, [], [], "ths", "Theros", "223"),
        new("Plains", "", "Basic Land — Plains", "({T}: Add {W}.)", null, null, [], ["W"], "hob", "The Hobbit", "194"),
        new("Plaza of Heroes", "", "Land", "{T}: Add {C}.\n{T}: Add one mana of any color. Spend this mana only to cast a legendary spell.\n{T}: Add one mana of any color among legendary permanents you control.\n{3}, {T}, Exile this land: Target legendary creature gains hexproof and indestructible until end of turn.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "255"),
        new("Rogue's Passage", "", "Land", "{T}: Add {C}.\n{4}, {T}: Target creature can't be blocked this turn.", null, null, [], [], "soc", "Secrets of Strixhaven Commander", "400"),
        new("Serra's Sanctum", "", "Legendary Land", "{T}: Add {W} for each enchantment you control.", null, null, [], ["W"], "usg", "Urza's Saga", "325"),
        new("Cut a Deal", "{2}{W}", "Sorcery", "Each opponent draws a card, then you draw a card for each opponent who drew a card this way.", null, null, [], ["W"], "msc", "Marvel Super Heroes Commander", "127"),
        new("Divine Reckoning", "{2}{W}{W}", "Sorcery", "Each player chooses a creature they control. Destroy the rest.\nFlashback {5}{W}{W} (You may cast this card from your graveyard for its flashback cost. Then exile it.)", null, null, ["Flashback"], ["W"], "c19", "Commander 2019", "62"),
        new("Open the Armory", "{1}{W}", "Sorcery", "Search your library for an Aura or Equipment card, reveal it, put it into your hand, then shuffle.", null, null, [], ["W"], "cmr", "Commander Legends", "34"),
        new("Promise of Loyalty", "{4}{W}", "Sorcery", "Each player puts a vow counter on a creature they control and sacrifices the rest. Each of those creatures can't attack you or planeswalkers you control for as long as it has a vow counter on it.", null, null, [], ["W"], "msc", "Marvel Super Heroes Commander", "142"),
        new("Replenish", "{3}{W}", "Sorcery", "Return all enchantment cards from your graveyard to the battlefield. (Auras with nothing to enchant remain in your graveyard.)", null, null, [], ["W"], "uds", "Urza's Destiny", "15"),
        new("Single Combat", "{3}{W}{W}", "Sorcery", "Each player chooses a creature or planeswalker they control, then sacrifices the rest. Players can't cast creature or planeswalker spells until the end of your next turn.", null, null, [], ["W"], "war", "War of the Spark", "30"),
        new("Winds of Rath", "{3}{W}{W}", "Sorcery", "Destroy all creatures that aren't enchanted. They can't be regenerated.", null, null, [], ["W"], "soc", "Secrets of Strixhaven Commander", "185"),
    ];

    private static string GetLightPawsDeckSource() => """
Commander
1 Light-Paws, Emperor's Voice
1 Arcane Signet
1 Chrome Mox
1 Mind Stone
1 Mox Amber
1 Mox Diamond
1 Sol Ring
1 Thought Vessel
1 Esper Sentinel
1 Hero of Iroas
1 Kor Spiritdancer
1 Mesa Enchantress
1 Ondu Spiritdancer
1 Pearl-Ear, Imperial Advisor
1 Sram, Senior Edificer
1 Starfield Mystic
1 Transcendent Envoy
1 All That Glitters
1 Angelic Destiny
1 Armored Ascension
1 Benevolent Blessing
1 Chains of Custody
1 Darksteel Mutation
1 Daybreak Coronet
1 Entangler
1 Ethereal Armor
1 Faith Unbroken
1 Feather of Flight
1 Felidar Umbra
1 Idolized
1 Mantle of the Ancients
1 Mask of Law and Grace
1 On Serra's Wings
1 Ossification
1 Pariah
1 Reprobation
1 Sage's Reverie
1 Shardmage's Rescue
1 Sheltered by Ghosts
1 Shield of Duty and Reason
1 Shielded by Faith
1 Sigarda's Aid
1 Songbirds' Blessing
1 Sphere of Safety
1 Spirit Link
1 Spirit Mantle
1 Tempest Technique
1 Timely Ward
1 Twinblade Blessing
1 Disenchant
1 Enlightened Tutor
1 Galadriel's Dismissal
1 Path to Exile
1 Razorgrass Ambush // Razorgrass Field
1 Stroke of Midnight
1 Swords to Plowshares
1 Teferi's Protection
1 Ancient Tomb
1 Eiganjo, Seat of the Empire
1 Gemstone Caverns
1 Hall of Heliod's Generosity
1 Mistveil Plains
1 Nykthos, Shrine to Nyx
27 Plains
1 Plaza of Heroes
1 Rogue's Passage
1 Serra's Sanctum
1 Cut a Deal
1 Divine Reckoning
1 Open the Armory
1 Promise of Loyalty
1 Replenish
1 Single Combat
1 Winds of Rath
""";

    private static IReadOnlyList<ScryfallCard> GetKinnanCards() =>
    [
        new("Kinnan, Bonder Prodigy", "{G}{U}", "Legendary Creature — Human Druid", "Whenever you tap a nonland permanent for mana, add one mana of any type that permanent produced.\n{5}{G}{U}: Look at the top five cards of your library. You may put a non-Human creature card from among them onto the battlefield. Put the rest on the bottom of your library in a random order.", "2", "2", [], ["G", "U"], "iko", "Ikoria: Lair of Behemoths", "192"),
        new("Arcane Signet", "{2}", "Artifact", "{T}: Add one mana of any color in your commander's color identity.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "191"),
        new("Basalt Monolith", "{3}", "Artifact", "This artifact doesn't untap during your untap step.\n{T}: Add {C}{C}{C}.\n{3}: Untap this artifact.", null, null, [], [], "2xm", "Double Masters", "232"),
        new("Chrome Mox", "{0}", "Artifact", "Imprint — When this artifact enters, you may exile a nonartifact, nonland card from your hand.\n{T}: Add one mana of any of the exiled card's colors.", null, null, ["Imprint"], [], "2xm", "Double Masters", "240"),
        new("Fellwar Stone", "{2}", "Artifact", "{T}: Add one mana of any color that a land an opponent controls could produce.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "285"),
        new("Lotus Petal", "{0}", "Artifact", "{T}, Sacrifice this artifact: Add one mana of any color.", null, null, [], [], "tpr", "Tempest Remastered", "225"),
        new("Mana Vault", "{1}", "Artifact", "This artifact doesn't untap during your untap step.\nAt the beginning of your upkeep, you may pay {4}. If you do, untap this artifact.\nAt the beginning of your draw step, if this artifact is tapped, it deals 1 damage to you.\n{T}: Add {C}{C}{C}.", null, null, [], [], "2x2", "Double Masters 2022", "308"),
        new("Mirage Mirror", "{3}", "Artifact", "{2}: This artifact becomes a copy of target artifact, creature, enchantment, or land until end of turn.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "206"),
        new("Moonsilver Key", "{2}", "Artifact", "{1}, {T}, Sacrifice this artifact: Search your library for an artifact card with a mana ability or a basic land card, reveal it, put it into your hand, then shuffle.", null, null, [], [], "mid", "Innistrad: Midnight Hunt", "255"),
        new("Mox Amber", "{0}", "Legendary Artifact", "{T}: Add one mana of any color among legendary creatures and planeswalkers you control.", null, null, [], [], "dom", "Dominaria", "224"),
        new("Mox Diamond", "{0}", "Artifact", "If this artifact would enter, you may discard a land card instead. If you do, put this artifact onto the battlefield. If you don't, put it into its owner's graveyard.\n{T}: Add one mana of any color.", null, null, [], [], "tpr", "Tempest Remastered", "228"),
        new("Mox Opal", "{0}", "Legendary Artifact", "Metalcraft — {T}: Add one mana of any color. Activate only if you control three or more artifacts.", null, null, ["Metalcraft"], [], "2xm", "Double Masters", "275"),
        new("Simic Signet", "{2}", "Artifact", "{1}, {T}: Add {G}{U}.", null, null, [], ["G", "U"], "dsc", "Duskmourn: House of Horror Commander", "252"),
        new("Sol Ring", "{1}", "Artifact", "{T}: Add {C}{C}.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "211"),
        new("Springleaf Drum", "{1}", "Artifact", "{T}, Tap an untapped creature you control: Add one mana of any color.", null, null, [], [], "ecl", "Lorwyn Eclipsed", "260"),
        new("Talisman of Curiosity", "{2}", "Artifact", "{T}: Add {C}.\n{T}: Add {G} or {U}. This artifact deals 1 damage to you.", null, null, [], ["G", "U"], "mkc", "Murders at Karlov Manor Commander", "241"),
        new("The One Ring", "{4}", "Legendary Artifact", "Indestructible\nWhen The One Ring enters, if you cast it, you gain protection from everything until your next turn.\nAt the beginning of your upkeep, you lose 1 life for each burden counter on The One Ring.\n{T}: Put a burden counter on The One Ring, then draw a card for each burden counter on The One Ring.", null, null, ["Indestructible"], [], "ltr", "The Lord of the Rings: Tales of Middle-earth", "246"),
        new("Treasure Vault", "", "Artifact Land", "{T}: Add {C}.\n{X}{X}, {T}, Sacrifice this land: Create X Treasure tokens.", null, null, ["Treasure"], [], "afr", "Adventures in the Forgotten Realms", "261"),
        new("Invasion of Ikoria // Zilortha, Apex of Ikoria", "{X}{G}{G}", "Battle — Siege // Legendary Creature — Dinosaur", "(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's defeated, exile it, then cast it transformed.)\nWhen this Siege enters, search your library and/or graveyard for a non-Human creature card with mana value X or less and put it onto the battlefield. If you search your library this way, shuffle.\n//\nReach\nFor each non-Human creature you control, you may have that creature assign its combat damage as though it weren't blocked.", null, null, ["Reach", "Transform"], ["G"], "mom", "March of the Machine", "190"),
        new("Birds of Paradise", "{G}", "Creature — Bird", "Flying\n{T}: Add one mana of any color.", "0", "1", ["Flying"], ["G"], "rvr", "Ravnica Remastered", "133"),
        new("Bloom Tender", "{1}{G}", "Creature — Elf Druid", "Vivid — {T}: For each color among permanents you control, add one mana of that color.", "1", "1", ["Vivid"], ["G"], "ecl", "Lorwyn Eclipsed", "166"),
        new("Clever Impersonator", "{2}{U}{U}", "Creature — Shapeshifter", "You may have this creature enter as a copy of any nonland permanent on the battlefield.", "0", "0", [], ["U"], "c19", "Commander 2019", "82"),
        new("Consecrated Sphinx", "{4}{U}{U}", "Creature — Sphinx", "Flying\nWhenever an opponent draws a card, you may draw two cards.", "4", "6", ["Flying"], ["U"], "2x2", "Double Masters 2022", "43"),
        new("Delighted Halfling", "{G}", "Creature — Halfling Citizen", "{T}: Add {C}.\n{T}: Add one mana of any color. Spend this mana only to cast a legendary spell, and that spell can't be countered.", "1", "2", [], ["G"], "ltr", "The Lord of the Rings: Tales of Middle-earth", "158"),
        new("Drift of Phantasms", "{2}{U}", "Creature — Spirit", "Defender (This creature can't attack.)\nFlying\nTransmute {1}{U}{U} ({1}{U}{U}, Discard this card: Search your library for a card with the same mana value as this card, reveal it, put it into your hand, then shuffle. Transmute only as a sorcery.)", "0", "5", ["Flying", "Defender", "Transmute"], ["U"], "rvr", "Ravnica Remastered", "42"),
        new("Elvish Mystic", "{G}", "Creature — Elf Druid", "{T}: Add {G}.", "1", "1", [], ["G"], "soc", "Secrets of Strixhaven Commander", "266"),
        new("Elvish Spirit Guide", "{2}{G}", "Creature — Elf Spirit", "Exile this creature from your hand: Add {G}.", "2", "2", [], ["G"], "dmr", "Dominaria Remastered", "157"),
        new("Endurance", "{1}{G}{G}", "Creature — Elemental Incarnation", "Flash\nReach\nWhen this creature enters, up to one target player puts all the cards from their graveyard on the bottom of their library in a random order.\nEvoke—Exile a green card from your hand.", "3", "4", ["Reach", "Evoke", "Flash"], ["G"], "ecc", "Lorwyn Eclipsed Commander", "51"),
        new("Enduring Vitality", "{1}{G}{G}", "Enchantment Creature — Elk Glimmer", "Vigilance\nCreatures you control have \"{T}: Add one mana of any color.\"\nWhen Enduring Vitality dies, if it was a creature, return it to the battlefield under its owner's control. It's an enchantment. (It's not a creature.)", "3", "3", ["Vigilance"], ["G"], "dsk", "Duskmourn: House of Horror", "176"),
        new("Faerie Mastermind", "{1}{U}", "Creature — Faerie Rogue", "Flash\nFlying\nWhenever an opponent draws their second card each turn, you draw a card.\n{3}{U}: Each player draws a card.", "2", "1", ["Flying", "Flash"], ["U"], "soc", "Secrets of Strixhaven Commander", "114"),
        new("Flesh Duplicate", "{U}{U}", "Creature — Shapeshifter Rebel", "You may have this creature enter as a copy of any creature on the battlefield, except it has vanishing 3 if that creature doesn't have vanishing. (A permanent with vanishing 3 enters with three time counters on it. At the beginning of your upkeep, remove a time counter from it. When the last is removed, sacrifice it.)", "0", "0", [], ["U"], "who", "Doctor Who", "44"),
        new("Fyndhorn Elves", "{G}", "Creature — Elf Druid", "{T}: Add {G}.", "1", "1", [], ["G"], "cmr", "Commander Legends", "228"),
        new("High Fae Trickster", "{3}{U}", "Creature — Faerie Wizard", "Flash (You may cast this spell any time you could cast an instant.)\nFlying\nYou may cast spells as though they had flash.", "4", "2", ["Flying", "Flash"], ["U"], "fdn", "Foundations", "40"),
        new("Hullbreaker Horror", "{5}{U}{U}", "Creature — Kraken Horror", "Flash\nThis spell can't be countered.\nWhenever you cast a spell, choose up to one —\n• Return target spell you don't control to its owner's hand.\n• Return target nonland permanent to its owner's hand.", "7", "8", ["Flash"], ["U"], "inr", "Innistrad Remastered", "68"),
        new("Hydroelectric Specimen // Hydroelectric Laboratory", "{2}{U}", "Creature — Weird // Land", "Flash\nWhen this creature enters, you may change the target of target instant or sorcery spell with a single target to this creature.\n//\nAs this land enters, you may pay 3 life. If you don't, it enters tapped.\n{T}: Add {U}.", null, null, ["Flash"], ["U"], "mh3", "Modern Horizons 3", "240"),
        new("Llanowar Elves", "{G}", "Creature — Elf Druid", "{T}: Add {G}.", "1", "1", [], ["G"], "fdn", "Foundations", "227"),
        new("Nezahal, Primal Tide", "{5}{U}{U}", "Legendary Creature — Elder Dinosaur", "This spell can't be countered.\nYou have no maximum hand size.\nWhenever an opponent casts a noncreature spell, draw a card.\nDiscard three cards: Exile Nezahal. Return it to the battlefield tapped under its owner's control at the beginning of the next end step.", "7", "7", [], ["U"], "cmr", "Commander Legends", "401"),
        new("Nyxbloom Ancient", "{4}{G}{G}{G}", "Enchantment Creature — Elemental", "Trample\nIf you tap a permanent for mana, it produces three times as much of that mana instead.", "5", "5", ["Trample"], ["G"], "thb", "Theros Beyond Death", "190"),
        new("Phyrexian Metamorph", "{3}{U/P}", "Artifact Creature — Phyrexian Shapeshifter", "({U/P} can be paid with either {U} or 2 life.)\nYou may have this creature enter as a copy of any artifact or creature on the battlefield, except it's an artifact in addition to its other types.", "0", "0", [], ["U"], "eoc", "Edge of Eternities Commander", "75"),
        new("Seedborn Muse", "{3}{G}{G}", "Creature — Spirit", "Untap all permanents you control during each other player's untap step.", "2", "4", [], ["G"], "tdc", "Tarkir: Dragonstorm Commander", "268"),
        new("Thrasios, Triton Hero", "{G}{U}", "Legendary Creature — Merfolk Wizard", "{4}: Scry 1, then reveal the top card of your library. If it's a land card, put it onto the battlefield tapped. Otherwise, draw a card.\nPartner (You can have two commanders if both have partner.)", "1", "3", ["Partner", "Scry"], ["G", "U"], "c16", "Commander 2016", "46"),
        new("Tidespout Tyrant", "{5}{U}{U}{U}", "Creature — Djinn", "Flying\nWhenever you cast a spell, return target permanent to its owner's hand.", "5", "5", ["Flying"], ["U"], "rvr", "Ravnica Remastered", "63"),
        new("Trophy Mage", "{2}{U}", "Creature — Human Wizard", "When this creature enters, you may search your library for an artifact card with mana value 3, reveal it, put it into your hand, then shuffle.", "2", "2", [], ["U"], "ddu", "Duel Decks: Elves vs. Inventors", "42"),
        new("Valley Floodcaller", "{2}{U}", "Creature — Otter Wizard", "Flash\nYou may cast noncreature spells as though they had flash.\nWhenever you cast a noncreature spell, Birds, Frogs, Otters, and Rats you control get +1/+1 until end of turn. Untap them.", "2", "2", ["Flash"], ["U"], "blb", "Bloomburrow", "79"),
        new("Void Winnower", "{9}", "Creature — Eldrazi", "Your opponents can't cast spells with even mana values. (Zero is even.)\nYour opponents can't block with creatures with even mana values.", "11", "9", [], [], "bfz", "Battle for Zendikar", "17"),
        new("Amphibian Downpour", "{2}{U}", "Enchantment — Aura", "Flash\nStorm (When you cast this spell, copy it for each spell cast before it this turn. You may choose new targets for the copies. Copies become tokens.)\nEnchant creature\nEnchanted creature loses all abilities and is a blue Frog creature with base power and toughness 1/1.", null, null, ["Storm", "Enchant", "Flash"], ["U"], "mh3", "Modern Horizons 3", "51"),
        new("Copy Enchantment", "{2}{U}", "Enchantment", "You may have this enchantment enter as a copy of any enchantment on the battlefield.", null, null, [], ["U"], "rvr", "Ravnica Remastered", "39"),
        new("Mirrormade", "{1}{U}{U}", "Enchantment", "You may have this enchantment enter as a copy of any artifact or enchantment on the battlefield.", null, null, [], ["U"], "dsc", "Duskmourn: House of Horror Commander", "120"),
        new("Mystic Remora", "{U}", "Enchantment", "Cumulative upkeep {1} (At the beginning of your upkeep, put an age counter on this permanent, then sacrifice it unless you pay its upkeep cost for each age counter on it.)\nWhenever an opponent casts a noncreature spell, you may draw a card unless that player pays {4}.", null, null, ["Cumulative upkeep"], ["U"], "dmr", "Dominaria Remastered", "59"),
        new("Rhystic Study", "{2}{U}", "Enchantment", "Whenever an opponent casts a spell, you may draw a card unless that player pays {1}.", null, null, [], ["U"], "j22", "Jumpstart 2022", "114"),
        new("Sylvan Library", "{1}{G}", "Enchantment", "At the beginning of your draw step, you may draw two additional cards. If you do, choose two cards in your hand drawn this turn. For each of those cards, pay 4 life or put the card on top of your library.", null, null, [], ["G"], "dmr", "Dominaria Remastered", "179"),
        new("Chain of Vapor", "{U}", "Instant", "Return target nonland permanent to its owner's hand. Then that permanent's controller may sacrifice a land of their choice. If the player does, they may copy this spell and may choose a new target for that copy.", null, null, [], ["U"], "c16", "Commander 2016", "84"),
        new("Chord of Calling", "{X}{G}{G}{G}", "Instant", "Convoke (Your creatures can help cast this spell. Each creature you tap while casting this spell pays for {1} or one mana of that creature's color.)\nSearch your library for a creature card with mana value X or less, put it onto the battlefield, then shuffle.", null, null, ["Convoke"], ["G"], "rvr", "Ravnica Remastered", "134"),
        new("Crop Rotation", "{G}", "Instant", "As an additional cost to cast this spell, sacrifice a land.\nSearch your library for a land card, put that card onto the battlefield, then shuffle.", null, null, [], ["G"], "dmr", "Dominaria Remastered", "154"),
        new("Cyclonic Rift", "{1}{U}", "Instant", "Return target nonland permanent you don't control to its owner's hand.\nOverload {6}{U} (You may cast this spell for its overload cost. If you do, change \"target\" in its text to \"each.\")", null, null, ["Overload"], ["U"], "rvr", "Ravnica Remastered", "40"),
        new("Dispel", "{U}", "Instant", "Counter target instant spell.", null, null, [], ["U"], "bfz", "Battle for Zendikar", "76"),
        new("Dramatic Reversal", "{1}{U}", "Instant", "Untap all nonland permanents you control.", null, null, [], ["U"], "tle", "Avatar: The Last Airbender Eternal", "158"),
        new("Fierce Guardianship", "{2}{U}", "Instant", "If you control a commander, you may cast this spell without paying its mana cost.\nCounter target noncreature spell.", null, null, [], ["U"], "cmm", "Commander Masters", "94"),
        new("Flusterstorm", "{U}", "Instant", "Counter target instant or sorcery spell unless its controller pays {1}.\nStorm (When you cast this spell, copy it for each spell cast before it this turn. You may choose new targets for the copies.)", null, null, ["Storm"], ["U"], "ima", "Iconic Masters", "55"),
        new("Force of Negation", "{1}{U}{U}", "Instant", "If it's not your turn, you may exile a blue card from your hand rather than pay this spell's mana cost.\nCounter target noncreature spell. If that spell is countered this way, exile it instead of putting it into its owner's graveyard.", null, null, [], ["U"], "2x2", "Double Masters 2022", "50"),
        new("Force of Will", "{3}{U}{U}", "Instant", "You may pay 1 life and exile a blue card from your hand rather than pay this spell's mana cost.\nCounter target spell.", null, null, [], ["U"], "dmr", "Dominaria Remastered", "50"),
        new("Into the Flood Maw", "{U}", "Instant", "Gift a tapped Fish (You may promise an opponent a gift as you cast this spell. If you do, they create a tapped 1/1 blue Fish creature token before its other effects.)\nReturn target creature an opponent controls to its owner's hand. If the gift was promised, instead return target nonland permanent an opponent controls to its owner's hand.", null, null, ["Gift"], ["U"], "blb", "Bloomburrow", "52"),
        new("Mana Drain", "{U}{U}", "Instant", "Counter target spell. At the beginning of your next main phase, add an amount of {C} equal to that spell's mana value.", null, null, [], ["U"], "2x2", "Double Masters 2022", "57"),
        new("Mental Misstep", "{U/P}", "Instant", "({U/P} can be paid with either {U} or 2 life.)\nCounter target spell with mana value 1.", null, null, [], ["U"], "nph", "New Phyrexia", "38"),
        new("Mindbreak Trap", "{2}{U}{U}", "Instant — Trap", "If an opponent cast three or more spells this turn, you may pay {0} rather than pay this spell's mana cost.\nExile any number of target spells.", null, null, [], ["U"], "zen", "Zendikar", "57"),
        new("Pact of Negation", "{0}", "Instant", "Counter target spell.\nAt the beginning of your next upkeep, pay {3}{U}{U}. If you don't, you lose the game.", null, null, [], ["U"], "tsr", "Time Spiral Remastered", "77"),
        new("Pongify", "{U}", "Instant", "Destroy target creature. It can't be regenerated. Its controller creates a 3/3 green Ape creature token.", null, null, [], ["U"], "tdc", "Tarkir: Dragonstorm Commander", "160"),
        new("Sink into Stupor // Soporific Springs", "{1}{U}{U}", "Instant // Land", "Return target spell or nonland permanent an opponent controls to its owner's hand.\n//\nAs this land enters, you may pay 3 life. If you don't, it enters tapped.\n{T}: Add {U}.", null, null, [], ["U"], "mh3", "Modern Horizons 3", "241"),
        new("Swan Song", "{U}", "Instant", "Counter target enchantment, instant, or sorcery spell. Its controller creates a 2/2 blue Bird creature token with flying.", null, null, [], ["U"], "eoc", "Edge of Eternities Commander", "46"),
        new("Veil of Summer", "{G}", "Instant", "Draw a card if an opponent has cast a blue or black spell this turn. Spells you control can't be countered this turn. You and permanents you control gain hexproof from blue and from black until end of turn. (You and they can't be the targets of blue or black spells or abilities your opponents control.)", null, null, [], ["G"], "m20", "Core Set 2020", "198"),
        new("Ancient Tomb", "", "Land", "{T}: Add {C}{C}. This land deals 2 damage to you.", null, null, [], [], "uma", "Ultimate Masters", "236"),
        new("Boseiju, Who Endures", "", "Legendary Land", "{T}: Add {G}.\nChannel — {1}{G}, Discard this card: Destroy target artifact, enchantment, or nonbasic land an opponent controls. That player may search their library for a land card with a basic land type, put it onto the battlefield, then shuffle. This ability costs {1} less to activate for each legendary creature you control.", null, null, ["Channel"], ["G"], "neo", "Kamigawa: Neon Dynasty", "266"),
        new("Breeding Pool", "", "Land — Forest Island", "({T}: Add {G} or {U}.)\nAs this land enters, you may pay 2 life. If you don't, it enters tapped.", null, null, [], ["G", "U"], "eoe", "Edge of Eternities", "251"),
        new("Cephalid Coliseum", "", "Land", "{T}: Add {U}. This land deals 1 damage to you.\nThreshold — {U}, {T}, Sacrifice this land: Target player draws three cards, then discards three cards. Activate only if there are seven or more cards in your graveyard.", null, null, ["Threshold"], ["U"], "tdc", "Tarkir: Dragonstorm Commander", "349"),
        new("City of Brass", "", "Land", "Whenever this land becomes tapped, it deals 1 damage to you.\n{T}: Add one mana of any color.", null, null, [], [], "tmc", "Teenage Mutant Ninja Turtles Eternal", "62"),
        new("Command Tower", "", "Land", "{T}: Add one mana of any color in your commander's color identity.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "233"),
        new("Emergence Zone", "", "Land", "{T}: Add {C}.\n{1}, {T}, Sacrifice this land: You may cast spells this turn as though they had flash.", null, null, [], [], "war", "War of the Spark", "245"),
        new("Exotic Orchard", "", "Land", "{T}: Add one mana of any color that a land an opponent controls could produce.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "241"),
        new("Flooded Strand", "", "Land", "{T}, Pay 1 life, Sacrifice this land: Search your library for a Plains or Island card, put it onto the battlefield, then shuffle.", null, null, [], [], "mh3", "Modern Horizons 3", "220"),
        new("Forest", "", "Basic Land — Forest", "({T}: Add {G}.)", null, null, [], ["G"], "hob", "The Hobbit", "198"),
        new("Gaea's Cradle", "", "Legendary Land", "{T}: Add {G} for each creature you control.", null, null, [], ["G"], "usg", "Urza's Saga", "321"),
        new("Gemstone Caverns", "", "Legendary Land", "If this card is in your opening hand and you're not the starting player, you may begin the game with Gemstone Caverns on the battlefield with a luck counter on it. If you do, exile a card from your hand.\n{T}: Add {C}. If Gemstone Caverns has a luck counter on it, instead add one mana of any color.", null, null, [], [], "tsr", "Time Spiral Remastered", "280"),
        new("Island", "", "Basic Land — Island", "({T}: Add {U}.)", null, null, [], ["U"], "hob", "The Hobbit", "195"),
        new("Mana Conference", "", "Land", "When Mana Conference enters the battlefield, each player secretly chooses a basic land type, then those choices are revealed. Mana Conference gains each basic land type that received at least one vote. (Basic land types are Plains, Island, Swamp, Mountain, and Forest.)", null, null, [], [], "unk", "Unknown Event", "UL01a"),
        new("Misty Rainforest", "", "Land", "{T}, Pay 1 life, Sacrifice this land: Search your library for a Forest or Island card, put it onto the battlefield, then shuffle.", null, null, [], [], "mh2", "Modern Horizons 2", "250"),
        new("Otawara, Soaring City", "", "Legendary Land", "{T}: Add {U}.\nChannel — {3}{U}, Discard this card: Return target artifact, creature, enchantment, or planeswalker to its owner's hand. This ability costs {1} less to activate for each legendary creature you control.", null, null, ["Channel"], ["U"], "neo", "Kamigawa: Neon Dynasty", "271"),
        new("Polluted Delta", "", "Land", "{T}, Pay 1 life, Sacrifice this land: Search your library for an Island or Swamp card, put it onto the battlefield, then shuffle.", null, null, [], [], "mh3", "Modern Horizons 3", "224"),
        new("Scalding Tarn", "", "Land", "{T}, Pay 1 life, Sacrifice this land: Search your library for an Island or Mountain card, put it onto the battlefield, then shuffle.", null, null, [], [], "mh2", "Modern Horizons 2", "254"),
        new("Tropical Island", "", "Land — Forest Island", "({T}: Add {G} or {U}.)", null, null, [], ["G", "U"], "vma", "Vintage Masters", "321"),
        new("Verdant Catacombs", "", "Land", "{T}, Pay 1 life, Sacrifice this land: Search your library for a Swamp or Forest card, put it onto the battlefield, then shuffle.", null, null, [], [], "mh2", "Modern Horizons 2", "260"),
        new("Waterlogged Grove", "", "Land", "{T}, Pay 1 life: Add {G} or {U}.\n{1}, {T}, Sacrifice this land: Draw a card.", null, null, [], ["G", "U"], "mh1", "Modern Horizons", "249"),
        new("Windswept Heath", "", "Land", "{T}, Pay 1 life, Sacrifice this land: Search your library for a Forest or Plains card, put it onto the battlefield, then shuffle.", null, null, [], [], "mh3", "Modern Horizons 3", "235"),
        new("Wooded Foothills", "", "Land", "{T}, Pay 1 life, Sacrifice this land: Search your library for a Mountain or Forest card, put it onto the battlefield, then shuffle.", null, null, [], [], "mh3", "Modern Horizons 3", "236"),
        new("Yavimaya Coast", "", "Land", "{T}: Add {C}.\n{T}: Add {G} or {U}. This land deals 1 damage to you.", null, null, [], ["G", "U"], "soc", "Secrets of Strixhaven Commander", "425"),
        new("Tezzeret the Seeker", "{3}{U}{U}", "Legendary Planeswalker — Tezzeret", "+1: Untap up to two target artifacts.\n−X: Search your library for an artifact card with mana value X or less, put it onto the battlefield, then shuffle.\n−5: Artifacts you control become artifact creatures with base power and toughness 5/5 until end of turn.", null, null, [], ["U"], "mm2", "Modern Masters 2015", "62"),
        new("Fabricate", "{2}{U}", "Sorcery", "Search your library for an artifact card, reveal it, put it into your hand, then shuffle.", null, null, [], ["U"], "hop", "Planechase", "9"),
        new("Finale of Devastation", "{X}{G}{G}", "Sorcery", "Search your library and/or graveyard for a creature card with mana value X or less and put it onto the battlefield. If you search your library this way, shuffle. If X is 10 or more, creatures you control get +X/+X and gain haste until end of turn.", null, null, [], ["G"], "cmm", "Commander Masters", "289"),
        new("Green Sun's Zenith", "{X}{G}", "Sorcery", "Search your library for a green creature card with mana value X or less, put it onto the battlefield, then shuffle. Shuffle Green Sun's Zenith into its owner's library.", null, null, [], ["G"], "2x2", "Double Masters 2022", "150"),
        new("Nature's Rhythm", "{X}{G}{G}", "Sorcery", "Search your library for a creature card with mana value X or less, put it onto the battlefield, then shuffle.\nHarmonize {X}{G}{G}{G}{G} (You may cast this card from your graveyard for its harmonize cost. You may tap a creature you control to reduce that cost by an amount of generic mana equal to its power. Then exile this spell.)", null, null, ["Harmonize"], ["G"], "tdm", "Tarkir: Dragonstorm", "150"),
        new("Transmute Artifact", "{U}{U}", "Sorcery", "Sacrifice an artifact. If you do, search your library for an artifact card. If that card's mana value is less than or equal to the sacrificed artifact's mana value, put it onto the battlefield. If it's greater, you may pay {X}, where X is the difference. If you do, put it onto the battlefield. If you don't, put it into its owner's graveyard. Then shuffle.", null, null, [], ["U"], "me4", "Masters Edition IV", "69"),
    ];

    private static string GetKinnanDeckSource() => """
Commander
1 Kinnan, Bonder Prodigy
1 Arcane Signet
1 Basalt Monolith
1 Chrome Mox
1 Fellwar Stone
1 Lotus Petal
1 Mana Vault
1 Mirage Mirror
1 Moonsilver Key
1 Mox Amber
1 Mox Diamond
1 Mox Opal
1 Simic Signet
1 Sol Ring
1 Springleaf Drum
1 Talisman of Curiosity
1 The One Ring
1 Treasure Vault
1 Invasion of Ikoria // Zilortha, Apex of Ikoria
1 Birds of Paradise
1 Bloom Tender
1 Clever Impersonator
1 Consecrated Sphinx
1 Delighted Halfling
1 Drift of Phantasms
1 Elvish Mystic
1 Elvish Spirit Guide
1 Endurance
1 Enduring Vitality
1 Faerie Mastermind
1 Flesh Duplicate
1 Fyndhorn Elves
1 High Fae Trickster
1 Hullbreaker Horror
1 Hydroelectric Specimen // Hydroelectric Laboratory
1 Llanowar Elves
1 Nezahal, Primal Tide
1 Nyxbloom Ancient
1 Phyrexian Metamorph
1 Seedborn Muse
1 Thrasios, Triton Hero
1 Tidespout Tyrant
1 Trophy Mage
1 Valley Floodcaller
1 Void Winnower
1 Amphibian Downpour
1 Copy Enchantment
1 Mirrormade
1 Mystic Remora
1 Rhystic Study
1 Sylvan Library
1 Chain of Vapor
1 Chord of Calling
1 Crop Rotation
1 Cyclonic Rift
1 Dispel
1 Dramatic Reversal
1 Fierce Guardianship
1 Flusterstorm
1 Force of Negation
1 Force of Will
1 Into the Flood Maw
1 Mana Drain
1 Mental Misstep
1 Mindbreak Trap
1 Pact of Negation
1 Pongify
1 Sink into Stupor // Soporific Springs
1 Swan Song
1 Veil of Summer
1 Ancient Tomb
1 Boseiju, Who Endures
1 Breeding Pool
1 Cephalid Coliseum
1 City of Brass
1 Command Tower
1 Emergence Zone
1 Exotic Orchard
1 Flooded Strand
1 Forest
1 Gaea's Cradle
1 Gemstone Caverns
1 Island
1 Mana Conference
1 Misty Rainforest
1 Otawara, Soaring City
1 Polluted Delta
1 Scalding Tarn
1 Tropical Island
1 Verdant Catacombs
1 Waterlogged Grove
1 Windswept Heath
1 Wooded Foothills
1 Yavimaya Coast
1 Tezzeret the Seeker
1 Fabricate
1 Finale of Devastation
1 Green Sun's Zenith
1 Nature's Rhythm
1 Transmute Artifact
""";

    private static IReadOnlyList<ScryfallCard> GetTalrandCards() =>
    [
        new("Talrand, Sky Summoner", "{2}{U}{U}", "Legendary Creature — Merfolk Wizard", "Whenever you cast an instant or sorcery spell, create a 2/2 blue Drake creature token with flying.", "2", "2", [], ["U"], "otc", "Outlaws of Thunder Junction Commander", "116"),
        new("Arcane Signet", "{2}", "Artifact", "{T}: Add one mana of any color in your commander's color identity.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "191"),
        new("Folio of Fancies", "{1}{U}", "Artifact — Book", "Players have no maximum hand size.\n{X}{X}, {T}: Each player draws X cards.\n{2}{U}, {T}: Each opponent mills cards equal to the number of cards in their hand.", null, null, ["Mill"], ["U"], "eld", "Throne of Eldraine", "46"),
        new("Midnight Clock", "{2}{U}", "Artifact", "{T}: Add {U}.\n{2}{U}: Put an hour counter on this artifact.\nAt the beginning of each upkeep, put an hour counter on this artifact.\nWhen the twelfth hour counter is put on this artifact, shuffle your hand and graveyard into your library, then draw seven cards. Exile this artifact.", null, null, [], ["U"], "otc", "Outlaws of Thunder Junction Commander", "100"),
        new("Primal Amulet // Primal Wellspring", "{4}", "Artifact // Land", "Instant and sorcery spells you cast cost {1} less to cast.\nWhenever you cast an instant or sorcery spell, put a charge counter on this artifact. Then if there are four or more charge counters on it, you may remove those counters and transform it.\n//\n(Transforms from Primal Amulet.)\n{T}: Add one mana of any color. When that mana is spent to cast an instant or sorcery spell, copy that spell and you may choose new targets for the copy.", null, null, ["Transform"], [], "xln", "Ixalan", "243"),
        new("Sol Ring", "{1}", "Artifact", "{T}: Add {C}{C}.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "211"),
        new("Witching Well", "{U}", "Artifact", "When this artifact enters, scry 2. (Look at the top two cards of your library, then put any number of them on the bottom and the rest on top in any order.)\n{3}{U}, Sacrifice this artifact: Draw two cards.", null, null, ["Scry"], ["U"], "cmm", "Commander Masters", "135"),
        new("Invasion of Arcavios // Invocation of the Founders", "{3}{U}{U}", "Battle — Siege // Enchantment", "(As a Siege enters, choose an opponent to protect it. You and others can attack it. When it's defeated, exile it, then cast it transformed.)\nWhen this Siege enters, search your library, graveyard, and/or outside the game for an instant or sorcery card you own, reveal it, and put it into your hand. If you search your library this way, shuffle.\n//\nWhenever you cast an instant or sorcery spell from your hand, you may copy that spell. You may choose new targets for the copy.", null, null, ["Transform"], ["U"], "mom", "March of the Machine", "61"),
        new("Archmage Emeritus", "{2}{U}{U}", "Creature — Human Wizard", "Magecraft — Whenever you cast or copy an instant or sorcery spell, draw a card.", "2", "2", ["Magecraft"], ["U"], "soc", "Secrets of Strixhaven Commander", "188"),
        new("Archmage of Runes", "{3}{U}{U}", "Creature — Giant Wizard", "Instant and sorcery spells you cast cost {1} less to cast.\nWhenever you cast an instant or sorcery spell, draw a card.", "3", "6", [], ["U"], "fdn", "Foundations", "30"),
        new("Curious Homunculus // Voracious Reader", "{1}{U}", "Creature — Homunculus // Creature — Eldrazi Homunculus", "{T}: Add {C}. Spend this mana only to cast an instant or sorcery spell.\nAt the beginning of your upkeep, if there are three or more instant and/or sorcery cards in your graveyard, transform this creature.\n//\nProwess (Whenever you cast a noncreature spell, this creature gets +1/+1 until end of turn.)\nInstant and sorcery spells you cast cost {1} less to cast.", null, null, ["Prowess", "Transform"], ["U"], "emn", "Eldritch Moon", "54"),
        new("Docent of Perfection // Final Iteration", "{3}{U}{U}", "Creature — Insect Horror // Creature — Eldrazi Insect", "Flying\nWhenever you cast an instant or sorcery spell, create a 1/1 blue Human Wizard creature token. Then if you control three or more Wizards, transform this creature.\n//\nFlying\nWizards you control get +2/+1 and have flying.\nWhenever you cast an instant or sorcery spell, create a 1/1 blue Human Wizard creature token.", null, null, ["Flying", "Transform"], ["U"], "inr", "Innistrad Remastered", "62"),
        new("Gandalf, Friend of the Shire", "{3}{U}", "Legendary Creature — Avatar Wizard", "Flash\nYou may cast sorcery spells as though they had flash.\nWhenever the Ring tempts you, if you chose a creature other than Gandalf as your Ring-bearer, draw a card.", "2", "4", ["Flash"], ["U"], "ltr", "The Lord of the Rings: Tales of Middle-earth", "50"),
        new("Karfell Harbinger", "{1}{U}", "Creature — Zombie Wizard", "{T}: Add {U}. Spend this mana only to foretell a card from your hand or cast an instant or sorcery spell.", "1", "3", [], ["U"], "khm", "Kaldheim", "65"),
        new("Murmuring Mystic", "{3}{U}", "Creature — Human Wizard", "Whenever you cast an instant or sorcery spell, create a 1/1 blue Bird Illusion creature token with flying.", "1", "5", [], ["U"], "otc", "Outlaws of Thunder Junction Commander", "102"),
        new("Prescient Chimera", "{3}{U}{U}", "Creature — Chimera", "Flying\nWhenever you cast an instant or sorcery spell, scry 1. (Look at the top card of your library. You may put that card on the bottom.)", "3", "4", ["Scry", "Flying"], ["U"], "jmp", "Jumpstart", "164"),
        new("Scholar of the Ages", "{5}{U}{U}", "Creature — Human Wizard", "When this creature enters, return up to two target instant and/or sorcery cards from your graveyard to your hand.", "3", "3", [], ["U"], "cmr", "Commander Legends", "93"),
        new("The Reality Chip", "{1}{U}", "Legendary Artifact Creature — Equipment Jellyfish", "You may look at the top card of your library any time.\nAs long as The Reality Chip is attached to a creature, you may play lands and cast spells from the top of your library.\nReconfigure {2}{U} ({2}{U}: Attach to target creature you control; or unattach from a creature. Reconfigure only as a sorcery. While attached, this isn't a creature.)", "0", "4", ["Reconfigure"], ["U"], "neo", "Kamigawa: Neon Dynasty", "74"),
        new("Tidal Barracuda", "{3}{U}", "Creature — Fish", "Any player may cast spells as though they had flash.\nYour opponents can't cast spells during your turn.", "3", "4", [], ["U"], "c20", "Commander 2020", "39"),
        new("Jace's Sanctum", "{3}{U}", "Enchantment", "Instant and sorcery spells you cast cost {1} less to cast.\nWhenever you cast an instant or sorcery spell, scry 1.", null, null, ["Scry"], ["U"], "c19", "Commander 2019", "88"),
        new("Leyline of Anticipation", "{2}{U}{U}", "Enchantment", "If this card is in your opening hand, you may begin the game with it on the battlefield.\nYou may cast spells as though they had flash.", null, null, [], ["U"], "clb", "Commander Legends: Battle for Baldur's Gate", "726"),
        new("Precognition Field", "{3}{U}", "Enchantment", "You may look at the top card of your library any time.\nYou may cast instant and sorcery spells from the top of your library.\n{3}: Exile the top card of your library.", null, null, [], ["U"], "dom", "Dominaria", "61"),
        new("Propaganda", "{2}{U}", "Enchantment", "Creatures can't attack you unless their controller pays {2} for each creature they control that's attacking you.", null, null, [], ["U"], "msc", "Marvel Super Heroes Commander", "151"),
        new("Robe of Mirrors", "{U}", "Enchantment — Aura", "Enchant creature (Target a creature as you cast this. This card enters attached to that creature.)\nEnchanted creature has shroud. (It can't be the target of spells or abilities.)", null, null, ["Enchant"], ["U"], "10e", "Tenth Edition", "101"),
        new("The Bath Song", "{3}{U}", "Enchantment — Saga", "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI, II — Draw two cards, then discard a card.\nIII — Shuffle any number of target cards from your graveyard into your library. Add {U}{U}.", null, null, [], ["U"], "ltr", "The Lord of the Rings: Tales of Middle-earth", "40"),
        new("The Mirari Conjecture", "{4}{U}", "Enchantment — Saga", "(As this Saga enters and after your draw step, add a lore counter. Sacrifice after III.)\nI — Return target instant card from your graveyard to your hand.\nII — Return target sorcery card from your graveyard to your hand.\nIII — Until end of turn, whenever you cast an instant or sorcery spell, copy it. You may choose new targets for the copy.", null, null, [], ["U"], "dom", "Dominaria", "57"),
        new("Aetherize", "{3}{U}", "Instant", "Return all attacking creatures to their owner's hand.", null, null, [], ["U"], "fdn", "Foundations", "151"),
        new("Anticipate", "{1}{U}", "Instant", "Look at the top three cards of your library. Put one of them into your hand and the rest on the bottom of your library in any order.", null, null, [], ["U"], "iko", "Ikoria: Lair of Behemoths", "40"),
        new("Behold the Multiverse", "{3}{U}", "Instant", "Scry 2, then draw two cards.\nForetell {1}{U} (During your turn, you may pay {2} and exile this card from your hand face down. Cast it on a later turn for its foretell cost.)", null, null, ["Foretell", "Scry"], ["U"], "khm", "Kaldheim", "46"),
        new("Brainstorm", "{U}", "Instant", "Draw three cards, then put two cards from your hand on top of your library in any order.", null, null, [], ["U"], "tle", "Avatar: The Last Airbender Eternal", "155"),
        new("Comparative Analysis", "{3}{U}", "Instant", "Surge {2}{U} (You may cast this spell for its surge cost if you or a teammate has cast another spell this turn.)\nTarget player draws two cards.", null, null, ["Surge"], ["U"], "ogw", "Oath of the Gatewatch", "51"),
        new("Counterspell", "{U}{U}", "Instant", "Counter target spell.", null, null, [], ["U"], "dsc", "Duskmourn: House of Horror Commander", "114"),
        new("Didn't Say Please", "{1}{U}{U}", "Instant", "Counter target spell. Its controller mills three cards.", null, null, ["Mill"], ["U"], "eld", "Throne of Eldraine", "42"),
        new("Dig Through Time", "{6}{U}{U}", "Instant", "Delve (Each card you exile from your graveyard while casting this spell pays for {1}.)\nLook at the top seven cards of your library. Put two of them into your hand and the rest on the bottom of your library in any order.", null, null, ["Delve"], ["U"], "soc", "Secrets of Strixhaven Commander", "195"),
        new("Disdainful Stroke", "{1}{U}", "Instant", "Counter target spell with mana value 4 or greater.", null, null, [], ["U"], "woe", "Wilds of Eldraine", "47"),
        new("Dispel", "{U}", "Instant", "Counter target instant spell.", null, null, [], ["U"], "bfz", "Battle for Zendikar", "76"),
        new("Dissolve", "{1}{U}{U}", "Instant", "Counter target spell. Scry 1. (Look at the top card of your library. You may put it on the bottom.)", null, null, ["Scry"], ["U"], "ima", "Iconic Masters", "51"),
        new("Essence Scatter", "{1}{U}", "Instant", "Counter target creature spell.", null, null, [], ["U"], "sos", "Secrets of Strixhaven", "47"),
        new("Everdream", "{1}{U}", "Instant", "Draw a card.\nSplice onto instant or sorcery {2}{U} (As you cast an instant or sorcery spell, you may reveal this card from your hand and pay its splice cost. If you do, add this card's effects to that spell.)", null, null, ["Splice"], ["U"], "mh1", "Modern Horizons", "47"),
        new("Fact or Fiction", "{3}{U}", "Instant", "Reveal the top five cards of your library. An opponent separates those cards into two piles. Put one pile into your hand and the other into your graveyard.", null, null, [], ["U"], "cmm", "Commander Masters", "91"),
        new("Into the Story", "{5}{U}{U}", "Instant", "This spell costs {3} less to cast if an opponent has seven or more cards in their graveyard.\nDraw four cards.", null, null, [], ["U"], "eld", "Throne of Eldraine", "50"),
        new("Mana Leak", "{1}{U}", "Instant", "Counter target spell unless its controller pays {3}.", null, null, [], ["U"], "2x2", "Double Masters 2022", "58"),
        new("Memory Lapse", "{1}{U}", "Instant", "Counter target spell. If that spell is countered this way, put it on top of its owner's library instead of into their graveyard.", null, null, [], ["U"], "ema", "Eternal Masters", "60"),
        new("Negate", "{1}{U}", "Instant", "Counter target noncreature spell.", null, null, [], ["U"], "tmt", "Teenage Mutant Ninja Turtles", "47"),
        new("Opt", "{U}", "Instant", "Scry 1. (Look at the top card of your library. You may put it on the bottom.)\nDraw a card.", null, null, ["Scry"], ["U"], "tdc", "Tarkir: Dragonstorm Commander", "158"),
        new("Overwhelming Denial", "{2}{U}{U}", "Instant", "Surge {U}{U} (You may cast this spell for its surge cost if you or a teammate has cast another spell this turn.)\nThis spell can't be countered.\nCounter target spell.", null, null, ["Surge"], ["U"], "ogw", "Oath of the Gatewatch", "61"),
        new("Saw It Coming", "{1}{U}{U}", "Instant", "Counter target spell.\nForetell {1}{U} (During your turn, you may pay {2} and exile this card from your hand face down. Cast it on a later turn for its foretell cost.)", null, null, ["Foretell"], ["U"], "khm", "Kaldheim", "76"),
        new("Supreme Will", "{2}{U}", "Instant", "Choose one —\n• Counter target spell unless its controller pays {3}.\n• Look at the top four cards of your library. Put one of them into your hand and the rest on the bottom of your library in any order.", null, null, [], ["U"], "cmr", "Commander Legends", "102"),
        new("Traumatic Visions", "{3}{U}{U}", "Instant", "Counter target spell.\nBasic landcycling {1}{U} ({1}{U}, Discard this card: Search your library for a basic land card, reveal it, put it into your hand, then shuffle.)", null, null, ["Landcycling", "Basic landcycling", "Typecycling", "Cycling"], ["U"], "c21", "Commander 2021", "132"),
        new("Void Shatter", "{1}{U}{U}", "Instant", "Devoid (This card has no color.)\nCounter target spell. If that spell is countered this way, exile it instead of putting it into its owner's graveyard.", null, null, ["Devoid"], ["U"], "ogw", "Oath of the Gatewatch", "49"),
        new("Whirlwind Denial", "{2}{U}", "Instant", "For each spell and ability your opponents control, counter it unless its controller pays {4}.", null, null, [], ["U"], "thb", "Theros Beyond Death", "81"),
        new("Wizard's Retort", "{1}{U}{U}", "Instant", "This spell costs {1} less to cast if you control a Wizard.\nCounter target spell.", null, null, [], ["U"], "jmp", "Jumpstart", "198"),
        new("Desert of the Mindful", "", "Land — Desert", "This land enters tapped.\n{T}: Add {U}.\nCycling {1}{U} ({1}{U}, Discard this card: Draw a card.)", null, null, ["Cycling"], ["U"], "c21", "Commander 2021", "287"),
        new("Emergence Zone", "", "Land", "{T}: Add {C}.\n{1}, {T}, Sacrifice this land: You may cast spells this turn as though they had flash.", null, null, [], [], "war", "War of the Spark", "245"),
        new("Island", "", "Basic Land — Island", "({T}: Add {U}.)", null, null, [], ["U"], "hob", "The Hobbit", "195"),
        new("Lonely Sandbar", "", "Land", "This land enters tapped.\n{T}: Add {U}.\nCycling {U} ({U}, Discard this card: Draw a card.)", null, null, ["Cycling"], ["U"], "eoc", "Edge of Eternities Commander", "166"),
        new("Memorial to Genius", "", "Land", "This land enters tapped.\n{T}: Add {U}.\n{4}{U}, {T}, Sacrifice this land: Draw two cards.", null, null, [], ["U"], "c21", "Commander 2021", "301"),
        new("Mystic Sanctuary", "", "Land — Island", "({T}: Add {U}.)\nThis land enters tapped unless you control three or more other Islands.\nWhen this land enters untapped, you may put target instant or sorcery card from your graveyard on top of your library.", null, null, [], ["U"], "soc", "Secrets of Strixhaven Commander", "388"),
        new("Temple of the False God", "", "Land", "{T}: Add {C}{C}. Activate only if you control five or more lands.", null, null, [], [], "soc", "Secrets of Strixhaven Commander", "416"),
        new("Narset, Parter of Veils", "{1}{U}{U}", "Legendary Planeswalker — Narset", "Each opponent can't draw more than one card each turn.\n−2: Look at the top four cards of your library. You may reveal a noncreature, nonland card from among them and put it into your hand. Put the rest on the bottom of your library in a random order.", null, null, [], ["U"], "cmm", "Commander Masters", "853"),
        new("Deep Analysis", "{3}{U}", "Sorcery", "Target player draws two cards.\nFlashback—{1}{U}, Pay 3 life. (You may cast this card from your graveyard for its flashback cost. Then exile it.)", null, null, ["Flashback"], ["U"], "msc", "Marvel Super Heroes Commander", "149"),
        new("Flood of Recollection", "{U}{U}", "Sorcery", "Return target instant or sorcery card from your graveyard to your hand. Exile Flood of Recollection.", null, null, [], ["U"], "cmr", "Commander Legends", "61"),
        new("Flood of Tears", "{4}{U}{U}", "Sorcery", "Return all nonland permanents to their owners' hands. If you return four or more nontoken permanents you control this way, you may put a permanent card from your hand onto the battlefield.", null, null, [], ["U"], "voc", "Crimson Vow Commander", "104"),
        new("Mind Spring", "{X}{U}{U}", "Sorcery", "Draw X cards.", null, null, [], ["U"], "blb", "Bloomburrow", "389"),
        new("Mnemonic Deluge", "{6}{U}{U}{U}", "Sorcery", "Exile target instant or sorcery card from a graveyard. Copy that card three times. You may cast the copies without paying their mana costs. Exile Mnemonic Deluge.", null, null, [], ["U"], "cmr", "Commander Legends", "82"),
        new("Preordain", "{U}", "Sorcery", "Scry 2, then draw a card. (To scry 2, look at the top two cards of your library, then put any number of them on the bottom and the rest on top in any order.)", null, null, ["Scry"], ["U"], "tdc", "Tarkir: Dragonstorm Commander", "161"),
        new("Pull from the Deep", "{2}{U}{U}", "Sorcery", "Return up to one target instant card and up to one target sorcery card from your graveyard to your hand. Exile Pull from the Deep.", null, null, [], ["U"], "jou", "Journey into Nyx", "47"),
        new("Rise from the Tides", "{5}{U}", "Sorcery", "Create a tapped 2/2 black Zombie creature token for each instant and sorcery card in your graveyard.", null, null, [], ["U"], "inr", "Innistrad Remastered", "82"),
        new("Scour All Possibilities", "{1}{U}", "Sorcery", "Scry 2, then draw a card.\nFlashback {4}{U} (You may cast this card from your graveyard for its flashback cost. Then exile it.)", null, null, ["Flashback", "Scry"], ["U"], "mh1", "Modern Horizons", "67"),
        new("Secrets of the Golden City", "{1}{U}{U}", "Sorcery", "Ascend (If you control ten or more permanents, you get the city's blessing for the rest of the game.)\nDraw two cards. If you have the city's blessing, draw three cards instead.", null, null, ["Ascend"], ["U"], "rix", "Rivals of Ixalan", "52"),
        new("Serum Visions", "{U}", "Sorcery", "Draw a card. Scry 2.", null, null, ["Scry"], ["U"], "otc", "Outlaws of Thunder Junction Commander", "112"),
        new("Sleep", "{2}{U}{U}", "Sorcery", "Tap all creatures target player controls. Those creatures don't untap during that player's next untap step.", null, null, [], ["U"], "m19", "Core Set 2019", "74"),
        new("Sleight of Hand", "{U}", "Sorcery", "Look at the top two cards of your library. Put one into your hand and the other on the bottom of your library.", null, null, [], ["U"], "woe", "Wilds of Eldraine", "67"),
        new("Solve the Equation", "{2}{U}", "Sorcery", "Search your library for an instant or sorcery card, reveal it, put it into your hand, then shuffle.", null, null, [], ["U"], "stx", "Strixhaven: School of Mages", "54"),
        new("Tales of the Ancestors", "{3}{U}", "Sorcery", "Each player with fewer cards in hand than the player with the most cards in hand draws cards equal to the difference.\nForetell {1}{U} (During your turn, you may pay {2} and exile this card from your hand face down. Cast it on a later turn for its foretell cost.)", null, null, ["Foretell"], ["U"], "khc", "Kaldheim Commander", "8"),
        new("Winged Words", "{2}{U}", "Sorcery", "This spell costs {1} less to cast if you control a creature with flying.\nDraw two cards.", null, null, [], ["U"], "jmp", "Jumpstart", "196"),
    ];

    private static string GetTalrandDeckSource() => """
Commander
1 Talrand, Sky Summoner
1 Arcane Signet
1 Folio of Fancies
1 Midnight Clock
1 Primal Amulet // Primal Wellspring
1 Sol Ring
1 Witching Well
1 Invasion of Arcavios // Invocation of the Founders
1 Archmage Emeritus
1 Archmage of Runes
1 Curious Homunculus // Voracious Reader
1 Docent of Perfection // Final Iteration
1 Gandalf, Friend of the Shire
1 Karfell Harbinger
1 Murmuring Mystic
1 Prescient Chimera
1 Scholar of the Ages
1 The Reality Chip
1 Tidal Barracuda
1 Jace's Sanctum
1 Leyline of Anticipation
1 Precognition Field
1 Propaganda
1 Robe of Mirrors
1 The Bath Song
1 The Mirari Conjecture
1 Aetherize
1 Anticipate
1 Behold the Multiverse
1 Brainstorm
1 Comparative Analysis
1 Counterspell
1 Didn't Say Please
1 Dig Through Time
1 Disdainful Stroke
1 Dispel
1 Dissolve
1 Essence Scatter
1 Everdream
1 Fact or Fiction
1 Into the Story
1 Mana Leak
1 Memory Lapse
1 Negate
1 Opt
1 Overwhelming Denial
1 Saw It Coming
1 Supreme Will
1 Traumatic Visions
1 Void Shatter
1 Whirlwind Denial
1 Wizard's Retort
1 Desert of the Mindful
1 Emergence Zone
25 Island
1 Lonely Sandbar
1 Memorial to Genius
1 Mystic Sanctuary
1 Temple of the False God
1 Narset, Parter of Veils
1 Deep Analysis
1 Flood of Recollection
1 Flood of Tears
1 Mind Spring
1 Mnemonic Deluge
1 Preordain
1 Pull from the Deep
1 Rise from the Tides
1 Scour All Possibilities
1 Secrets of the Golden City
1 Serum Visions
1 Sleep
1 Sleight of Hand
1 Solve the Equation
1 Tales of the Ancestors
1 Winged Words
""";

    private static IReadOnlyList<ScryfallCard> GetAesiCards() =>
    [
        new("Aesi, Tyrant of Gyre Strait", "{4}{G}{U}", "Legendary Creature — Serpent", "You may play an additional land on each of your turns.\nLandfall — Whenever a land you control enters, you may draw a card.", "5", "5", ["Landfall"], ["G", "U"], "dsc", "Duskmourn: House of Horror Commander", "210"),
        new("Seer's Sundial", "{4}", "Artifact", "Landfall — Whenever a land you control enters, you may pay {2}. If you do, draw a card.", null, null, ["Landfall"], [], "cmr", "Commander Legends", "470"),
        new("Simic Signet", "{2}", "Artifact", "{1}, {T}: Add {G}{U}.", null, null, [], ["G", "U"], "dsc", "Duskmourn: House of Horror Commander", "252"),
        new("Sol Ring", "{1}", "Artifact", "{T}: Add {C}{C}.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "211"),
        new("Swiftfoot Boots", "{2}", "Artifact — Equipment", "Equipped creature has hexproof and haste. (It can't be the target of spells or abilities your opponents control. It can attack and {T} no matter when it came under your control.)\nEquip {1} ({1}: Attach to target creature you control. Equip only as a sorcery.)", null, null, ["Equip"], [], "msc", "Marvel Super Heroes Commander", "216"),
        new("Acidic Slime", "{3}{G}{G}", "Creature — Ooze", "Deathtouch (Any amount of damage this deals to a creature is enough to destroy it.)\nWhen this creature enters, destroy target artifact, enchantment, or land.", "2", "2", ["Deathtouch"], ["G"], "cmm", "Commander Masters", "270"),
        new("Avenger of Zendikar", "{5}{G}{G}", "Creature — Elemental", "When this creature enters, create a 0/1 green Plant creature token for each land you control.\nLandfall — Whenever a land you control enters, you may put a +1/+1 counter on each Plant creature you control.", "5", "5", ["Landfall"], ["G"], "ecc", "Lorwyn Eclipsed Commander", "98"),
        new("Coiling Oracle", "{G}{U}", "Creature — Snake Elf Druid", "When this creature enters, reveal the top card of your library. If it's a land card, put it onto the battlefield. Otherwise, put that card into your hand.", "1", "1", [], ["G", "U"], "rvr", "Ravnica Remastered", "172"),
        new("Elder Deep-Fiend", "{8}", "Creature — Eldrazi Octopus", "Flash\nEmerge {5}{U}{U} (You may cast this spell by sacrificing a creature and paying the emerge cost reduced by that creature's mana value.)\nWhen you cast this spell, tap up to four target permanents.", "5", "6", ["Emerge", "Flash"], ["U"], "inr", "Innistrad Remastered", "4"),
        new("Eternal Witness", "{1}{G}{G}", "Creature — Human Shaman", "When this creature enters, you may return target card from your graveyard to your hand.", "2", "1", [], ["G"], "cmm", "Commander Masters", "286"),
        new("Fathom Mage", "{2}{G}{U}", "Creature — Human Wizard", "Evolve (Whenever a creature you control enters, if that creature has greater power or toughness than this creature, put a +1/+1 counter on this creature.)\nWhenever a +1/+1 counter is put on this creature, you may draw a card.", "1", "1", ["Evolve"], ["G", "U"], "ncc", "New Capenna Commander", "339"),
        new("Meloku the Clouded Mirror", "{4}{U}", "Legendary Creature — Moonfolk Wizard", "Flying\n{1}, Return a land you control to its owner's hand: Create a 1/1 blue Illusion creature token with flying.", "2", "4", ["Flying"], ["U"], "cmr", "Commander Legends", "399"),
        new("Meteor Golem", "{7}", "Artifact Creature — Golem", "When this creature enters, destroy target nonland permanent an opponent controls.", "3", "3", [], [], "fdn", "Foundations", "256"),
        new("Molimo, Maro-Sorcerer", "{4}{G}{G}{G}", "Legendary Creature — Elemental Sorcerer", "Trample (This creature can deal excess combat damage to the player or planeswalker it's attacking.)\nMolimo's power and toughness are each equal to the number of lands you control.", "*", "*", ["Trample"], ["G"], "cmm", "Commander Masters", "305"),
        new("Mulldrifter", "{4}{U}", "Creature — Elemental", "Flying\nWhen this creature enters, draw two cards.\nEvoke {2}{U} (You may cast this spell for its evoke cost. If you do, it's sacrificed when it enters.)", "2", "2", ["Flying", "Evoke"], ["U"], "ecc", "Lorwyn Eclipsed Commander", "67"),
        new("Murkfiend Liege", "{2}{G/U}{G/U}{G/U}", "Creature — Horror", "Other green creatures you control get +1/+1.\nOther blue creatures you control get +1/+1.\nUntap all green and/or blue creatures you control during each other player's untap step.", "4", "4", [], ["G", "U"], "2x2", "Double Masters 2022", "259"),
        new("Nezahal, Primal Tide", "{5}{U}{U}", "Legendary Creature — Elder Dinosaur", "This spell can't be countered.\nYou have no maximum hand size.\nWhenever an opponent casts a noncreature spell, draw a card.\nDiscard three cards: Exile Nezahal. Return it to the battlefield tapped under its owner's control at the beginning of the next end step.", "7", "7", [], ["U"], "cmr", "Commander Legends", "401"),
        new("Rampaging Baloths", "{4}{G}{G}", "Creature — Beast", "Trample\nLandfall — Whenever a land you control enters, create a 4/4 green Beast creature token.", "6", "6", ["Trample", "Landfall"], ["G"], "eoc", "Edge of Eternities Commander", "104"),
        new("Ramunap Excavator", "{2}{G}", "Creature — Snake Cleric", "You may play lands from your graveyard.", "2", "3", [], ["G"], "otc", "Outlaws of Thunder Junction Commander", "202"),
        new("Reclamation Sage", "{2}{G}", "Creature — Elf Shaman", "When this creature enters, you may destroy target artifact or enchantment.", "2", "1", [], ["G"], "fdn", "Foundations", "231"),
        new("Scourge of Fleets", "{5}{U}{U}", "Creature — Kraken", "When this creature enters, return each creature your opponents control with toughness X or less to their owner's hand, where X is the number of Islands you control.", "6", "6", [], ["U"], "cmr", "Commander Legends", "403"),
        new("Sharktocrab", "{2}{G}{U}", "Creature — Shark Octopus Crab", "{2}{G}{U}: Adapt 1. (If this creature has no +1/+1 counters on it, put a +1/+1 counter on it.)\nWhenever one or more +1/+1 counters are put on this creature, tap target creature an opponent controls. That creature doesn't untap during its controller's next untap step.", "4", "4", ["Adapt"], ["G", "U"], "rvr", "Ravnica Remastered", "222"),
        new("Shipbreaker Kraken", "{4}{U}{U}", "Creature — Kraken", "{6}{U}{U}: Monstrosity 4. (If this creature isn't monstrous, put four +1/+1 counters on it and it becomes monstrous.)\nWhen this creature becomes monstrous, tap up to four target creatures. Those creatures don't untap during their controllers' untap steps for as long as you control this creature.", "6", "6", ["Monstrosity"], ["U"], "cmr", "Commander Legends", "404"),
        new("Simic Sky Swallower", "{5}{G}{U}", "Creature — Leviathan", "Flying, trample\nShroud (This creature can't be the target of spells or abilities.)", "6", "6", ["Flying", "Shroud", "Trample"], ["G", "U"], "cmr", "Commander Legends", "452"),
        new("Slinn Voda, the Rising Deep", "{6}{U}{U}", "Legendary Creature — Leviathan", "Kicker {1}{U} (You may pay an additional {1}{U} as you cast this spell.)\nWhen Slinn Voda enters, if it was kicked, return all creatures to their owners' hands except for Merfolk, Krakens, Leviathans, Octopuses, and Serpents.", "8", "8", ["Kicker"], ["U"], "cmr", "Commander Legends", "405"),
        new("Sphinx of Uthuun", "{5}{U}{U}", "Creature — Sphinx", "Flying\nWhen this creature enters, reveal the top five cards of your library. An opponent separates those cards into two piles. Put one pile into your hand and the other into your graveyard.", "5", "6", ["Flying"], ["U"], "cmr", "Commander Legends", "406"),
        new("Sporemound", "{3}{G}{G}", "Creature — Fungus", "Landfall — Whenever a land you control enters, create a 1/1 green Saproling creature token.", "3", "3", ["Landfall"], ["G"], "cmr", "Commander Legends", "437"),
        new("Stormtide Leviathan", "{5}{U}{U}{U}", "Creature — Leviathan", "Islandwalk (This creature can't be blocked as long as defending player controls an Island.)\nAll lands are Islands in addition to their other types.\nCreatures without flying or islandwalk can't attack.", "8", "8", ["Landwalk", "Islandwalk"], ["U"], "cmr", "Commander Legends", "407"),
        new("Stumpsquall Hydra", "{X}{G}{G}{G}", "Creature — Hydra", "When this creature enters, distribute X +1/+1 counters among it and any number of commanders.", "1", "1", [], ["G"], "cmr", "Commander Legends", "367"),
        new("Terastodon", "{6}{G}{G}", "Creature — Elephant", "When this creature enters, you may destroy up to three target noncreature permanents. For each permanent put into a graveyard this way, its controller creates a 3/3 green Elephant creature token.", "9", "9", [], ["G"], "c21", "Commander 2021", "207"),
        new("Trench Behemoth", "{5}{U}{U}", "Creature — Kraken", "Return a land you control to its owner's hand: Untap this creature. It gains hexproof until end of turn.\nLandfall — Whenever a land you control enters, target creature an opponent controls attacks during its controller's next combat phase if able.", "7", "7", ["Landfall"], ["U"], "cmr", "Commander Legends", "366"),
        new("Tromokratis", "{5}{U}{U}", "Legendary Creature — Kraken", "Tromokratis has hexproof unless it's attacking or blocking.\nTromokratis can't be blocked unless all creatures defending player controls block it. (If any creature that player controls doesn't block this creature, it can't be blocked.)", "8", "8", [], ["U"], "cmm", "Commander Masters", "129"),
        new("Verdant Sun's Avatar", "{5}{G}{G}", "Creature — Dinosaur Avatar", "Whenever this creature or another creature you control enters, you gain life equal to that creature's toughness.", "5", "5", [], ["G"], "lcc", "The Lost Caverns of Ixalan Commander", "262"),
        new("Wickerbough Elder", "{3}{G}", "Creature — Treefolk Shaman", "This creature enters with a -1/-1 counter on it.\n{G}, Remove a -1/-1 counter from this creature: Destroy target artifact or enchantment.", "4", "4", [], ["G"], "ecc", "Lorwyn Eclipsed Commander", "118"),
        new("Yavimaya Elder", "{1}{G}{G}", "Creature — Human Druid", "When this creature dies, you may search your library for up to two basic land cards, reveal them, put them into your hand, then shuffle.\n{2}, Sacrifice this creature: Draw a card.", "2", "1", [], ["G"], "dsc", "Duskmourn: House of Horror Commander", "208"),
        new("Ior Ruin Expedition", "{1}{U}", "Enchantment", "Landfall — Whenever a land you control enters, you may put a quest counter on this enchantment.\nRemove three quest counters from this enchantment and sacrifice it: Draw two cards.", null, null, ["Landfall"], ["U"], "cmr", "Commander Legends", "398"),
        new("Khalni Heart Expedition", "{1}{G}", "Enchantment", "Landfall — Whenever a land you control enters, you may put a quest counter on this enchantment.\nRemove three quest counters from this enchantment and sacrifice it: Search your library for up to two basic land cards, put them onto the battlefield tapped, then shuffle.", null, null, ["Landfall"], ["G"], "cmm", "Commander Masters", "899"),
        new("Retreat to Kazandu", "{2}{G}", "Enchantment", "Landfall — Whenever a land you control enters, choose one —\n• Put a +1/+1 counter on target creature.\n• You gain 2 life.", null, null, ["Landfall"], ["G"], "cmr", "Commander Legends", "435"),
        new("Arcane Denial", "{1}{U}", "Instant", "Counter target spell. Its controller may draw up to two cards at the beginning of the next turn's upkeep.\nYou draw a card at the beginning of the next turn's upkeep.", null, null, [], ["U"], "msc", "Marvel Super Heroes Commander", "147"),
        new("Beast Within", "{2}{G}", "Instant", "Destroy target permanent. Its controller creates a 3/3 green Beast creature token.", null, null, [], ["G"], "soc", "Secrets of Strixhaven Commander", "263"),
        new("Counterspell", "{U}{U}", "Instant", "Counter target spell.", null, null, [], ["U"], "dsc", "Duskmourn: House of Horror Commander", "114"),
        new("Fact or Fiction", "{3}{U}", "Instant", "Reveal the top five cards of your library. An opponent separates those cards into two piles. Put one pile into your hand and the other into your graveyard.", null, null, [], ["U"], "cmm", "Commander Masters", "91"),
        new("Growth Spiral", "{G}{U}", "Instant", "Draw a card. You may put a land card from your hand onto the battlefield.", null, null, [], ["G", "U"], "dsc", "Duskmourn: House of Horror Commander", "88"),
        new("Into the Roil", "{1}{U}", "Instant", "Kicker {1}{U} (You may pay an additional {1}{U} as you cast this spell.)\nReturn target nonland permanent to its owner's hand. If this spell was kicked, draw a card.", null, null, ["Kicker"], ["U"], "fdn", "Foundations", "509"),
        new("Peel from Reality", "{1}{U}", "Instant", "Return target creature you control and target creature you don't control to their owners' hands.", null, null, [], ["U"], "cmr", "Commander Legends", "402"),
        new("Simic Charm", "{G}{U}", "Instant", "Choose one —\n• Target creature gets +3/+3 until end of turn.\n• Permanents you control gain hexproof until end of turn.\n• Return target creature to its owner's hand.", null, null, [], ["G", "U"], "cmr", "Commander Legends", "451"),
        new("Blighted Woodland", "", "Land", "{T}: Add {C}.\n{3}{G}, {T}, Sacrifice this land: Search your library for up to two basic land cards, put them onto the battlefield tapped, then shuffle.", null, null, [], ["G"], "clb", "Commander Legends: Battle for Baldur's Gate", "881"),
        new("Command Tower", "", "Land", "{T}: Add one mana of any color in your commander's color identity.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "233"),
        new("Coral Atoll", "", "Land", "This land enters tapped.\nWhen this land enters, sacrifice it unless you return an untapped Island you control to its owner's hand.\n{T}: Add {C}{U}.", null, null, [], ["U"], "cmr", "Commander Legends", "480"),
        new("Evolving Wilds", "", "Land", "{T}, Sacrifice this land: Search your library for a basic land card, put it onto the battlefield tapped, then shuffle.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "240"),
        new("Forest", "", "Basic Land — Forest", "({T}: Add {G}.)", null, null, [], ["G"], "hob", "The Hobbit", "198"),
        new("Island", "", "Basic Land — Island", "({T}: Add {U}.)", null, null, [], ["U"], "hob", "The Hobbit", "195"),
        new("Jungle Basin", "", "Land", "This land enters tapped.\nWhen this land enters, sacrifice it unless you return an untapped Forest you control to its owner's hand.\n{T}: Add {C}{G}.", null, null, [], ["G"], "cmr", "Commander Legends", "484"),
        new("Memorial to Genius", "", "Land", "This land enters tapped.\n{T}: Add {U}.\n{4}{U}, {T}, Sacrifice this land: Draw two cards.", null, null, [], ["U"], "c21", "Commander 2021", "301"),
        new("Reliquary Tower", "", "Land", "You have no maximum hand size.\n{T}: Add {C}.", null, null, [], [], "soc", "Secrets of Strixhaven Commander", "398"),
        new("Simic Growth Chamber", "", "Land", "This land enters tapped.\nWhen this land enters, return a land you control to its owner's hand.\n{T}: Add {G}{U}.", null, null, [], ["G", "U"], "dsc", "Duskmourn: House of Horror Commander", "298"),
        new("Simic Guildgate", "", "Land — Gate", "This land enters tapped.\n{T}: Add {G} or {U}.", null, null, [], ["G", "U"], "fdn", "Foundations", "695"),
        new("Terramorphic Expanse", "", "Land", "{T}, Sacrifice this land: Search your library for a basic land card, put it onto the battlefield tapped, then shuffle.", null, null, [], [], "msc", "Marvel Super Heroes Commander", "273"),
        new("Thornwood Falls", "", "Land", "This land enters tapped.\nWhen this land enters, you gain 1 life.\n{T}: Add {G} or {U}.", null, null, [], ["G", "U"], "tdm", "Tarkir: Dragonstorm", "269"),
        new("Vivid Creek", "", "Land", "This land enters tapped with two charge counters on it.\n{T}: Add {U}.\n{T}, Remove a charge counter from this land: Add one mana of any color.", null, null, [], ["U"], "ncc", "New Capenna Commander", "444"),
        new("Vivid Grove", "", "Land", "This land enters tapped with two charge counters on it.\n{T}: Add {G}.\n{T}, Remove a charge counter from this land: Add one mana of any color.", null, null, [], ["G"], "ncc", "New Capenna Commander", "445"),
        new("Woodland Stream", "", "Land", "This land enters tapped.\n{T}: Add {G} or {U}.", null, null, [], ["G", "U"], "cmr", "Commander Legends", "503"),
        new("Compulsive Research", "{2}{U}", "Sorcery", "Target player draws three cards. Then that player discards two cards unless they discard a land card.", null, null, [], ["U"], "tdc", "Tarkir: Dragonstorm Commander", "147"),
        new("Cultivate", "{2}{G}", "Sorcery", "Search your library for up to two basic land cards, reveal them, put one onto the battlefield tapped and the other into your hand, then shuffle.", null, null, [], ["G"], "msc", "Marvel Super Heroes Commander", "172"),
        new("Explore", "{1}{G}", "Sorcery", "You may play an additional land this turn.\nDraw a card.", null, null, [], ["G"], "tle", "Avatar: The Last Airbender Eternal", "259"),
        new("Harmonize", "{2}{G}{G}", "Sorcery", "Draw three cards.", null, null, [], ["G"], "tmc", "Teenage Mutant Ninja Turtles Eternal", "51"),
        new("Kodama's Reach", "{2}{G}", "Sorcery — Arcane", "Search your library for up to two basic land cards, reveal them, put one onto the battlefield tapped and the other into your hand, then shuffle.", null, null, [], ["G"], "ecc", "Lorwyn Eclipsed Commander", "113"),
        new("Rampant Growth", "{1}{G}", "Sorcery", "Search your library for a basic land card, put it onto the battlefield tapped, then shuffle.", null, null, [], ["G"], "tdc", "Tarkir: Dragonstorm Commander", "265"),
        new("Search for Tomorrow", "{2}{G}", "Sorcery", "Search your library for a basic land card, put it onto the battlefield, then shuffle.\nSuspend 2—{G} (Rather than cast this card from your hand, you may pay {G} and exile it with two time counters on it. At the beginning of your upkeep, remove a time counter. When the last is removed, you may cast it without paying its mana cost.)", null, null, ["Suspend"], ["G"], "dmc", "Dominaria United Commander", "137"),
        new("Spitting Image", "{4}{G/U}{G/U}", "Sorcery", "Create a token that's a copy of target creature.\nRetrace (You may cast this card from your graveyard by discarding a land card in addition to paying its other costs.)", null, null, ["Retrace"], ["G", "U"], "c21", "Commander 2021", "229"),
        new("Urban Evolution", "{3}{G}{U}", "Sorcery", "Draw three cards. You may play an additional land this turn.", null, null, [], ["G", "U"], "ncc", "New Capenna Commander", "355"),
        new("Whelming Wave", "{2}{U}{U}", "Sorcery", "Return all creatures to their owners' hands except for Krakens, Leviathans, Octopuses, and Serpents.", null, null, [], ["U"], "cmr", "Commander Legends", "409"),
    ];

    private static string GetAesiDeckSource() => """
Commander
1 Aesi, Tyrant of Gyre Strait
1 Seer's Sundial
1 Simic Signet
1 Sol Ring
1 Swiftfoot Boots
1 Acidic Slime
1 Avenger of Zendikar
1 Coiling Oracle
1 Elder Deep-Fiend
1 Eternal Witness
1 Fathom Mage
1 Meloku the Clouded Mirror
1 Meteor Golem
1 Molimo, Maro-Sorcerer
1 Mulldrifter
1 Murkfiend Liege
1 Nezahal, Primal Tide
1 Rampaging Baloths
1 Ramunap Excavator
1 Reclamation Sage
1 Scourge of Fleets
1 Sharktocrab
1 Shipbreaker Kraken
1 Simic Sky Swallower
1 Slinn Voda, the Rising Deep
1 Sphinx of Uthuun
1 Sporemound
1 Stormtide Leviathan
1 Stumpsquall Hydra
1 Terastodon
1 Trench Behemoth
1 Tromokratis
1 Verdant Sun's Avatar
1 Wickerbough Elder
1 Yavimaya Elder
1 Ior Ruin Expedition
1 Khalni Heart Expedition
1 Retreat to Kazandu
1 Arcane Denial
1 Beast Within
1 Counterspell
1 Fact or Fiction
1 Growth Spiral
1 Into the Roil
1 Peel from Reality
1 Simic Charm
1 Blighted Woodland
1 Command Tower
1 Coral Atoll
1 Evolving Wilds
15 Forest
15 Island
1 Jungle Basin
1 Memorial to Genius
1 Reliquary Tower
1 Simic Growth Chamber
1 Simic Guildgate
1 Terramorphic Expanse
1 Thornwood Falls
1 Vivid Creek
1 Vivid Grove
1 Woodland Stream
1 Compulsive Research
1 Cultivate
1 Explore
1 Harmonize
1 Kodama's Reach
1 Rampant Growth
1 Search for Tomorrow
1 Spitting Image
1 Urban Evolution
1 Whelming Wave
""";

    private sealed record DeckFixture(
        string Slug,
        string CommanderName,
        string TargetCommanderBracket,
        string DeckSource,
        IReadOnlyList<ScryfallCard> Cards,
        HashSet<string> Archetypes);

    private sealed class FakeContentKbRelevanceService : IContentKbRelevanceService
    {
        public IReadOnlyList<ContentKbExcerpt>? Result { get; init; }

        public Task<IReadOnlyList<ContentKbExcerpt>?> GetRelevantClipsAsync(string? commanderName, string? bracket, IReadOnlySet<string>? deckArchetypes = null, int maxRenderedChars = 4500, CancellationToken ct = default)
            => Task.FromResult(Result);

        public Task<IReadOnlyList<ContentKbExcerpt>?> GetMergedClipsAsync(ExpertSelection selection, string? commanderName, string? bracket, IReadOnlySet<string>? deckArchetypes = null, int maxRenderedChars = 4500, CancellationToken ct = default)
            => Task.FromResult(Result);

        public Task<IReadOnlyList<(DeckFlow.Core.Knowledge.ContentSiteIndexRow Row, double Score)>> ScoreAllAsync(string? commanderName, string? bracket, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<(DeckFlow.Core.Knowledge.ContentSiteIndexRow Row, double Score)>>(Array.Empty<(DeckFlow.Core.Knowledge.ContentSiteIndexRow Row, double Score)>());

        public Task<IReadOnlyDictionary<string, string>> ResolvePinTitlesAsync(IReadOnlyList<string> videoIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private sealed class FakeMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class FakeArchidektDeckImporter : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class FakeMechanicLookupService : IMechanicLookupService
    {
        public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
            => Task.FromResult(new MechanicLookupResult(mechanicName, true, mechanicName, "702.108", "Exact rules section", "702.108a Prowess is a triggered ability.", "A keyword ability.", "https://magic.wizards.com/en/rules", "https://media.wizards.com/test.txt"));
    }

    private sealed class FakeScryfallSetService : IScryfallSetService
    {
        public Task<IReadOnlyList<ScryfallSetOption>> GetSetsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ScryfallSetOption>>([new ScryfallSetOption("dsk", "Test Set", "2026-01-01")]);

        public Task<string> BuildSetPacketAsync(IReadOnlyList<string> setCodes, IReadOnlyList<string>? commanderColorIdentity = null, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class FakeCommanderBanListService : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(["Dockside Extortionist", "Mana Crypt"]);
    }

    private sealed class FakeCommanderSpellbookService : ICommanderSpellbookService
    {
        public Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken = default)
            => Task.FromResult<CommanderSpellbookResult?>(null);
    }
}
