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

    private static DeckAnalysisRequest CreateAnalysisRequest()
    {
        return new DeckAnalysisRequest
        {
            WorkflowStep = 2,
            DeckSource = """
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
            TargetCommanderBracket = "Upgraded",
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

    private static ScryfallCard? FindDefaultCard(string query)
    {
        var normalizedQuery = query.Trim().ToUpperInvariant();
        return GetDefaultTestCards().FirstOrDefault(card =>
            normalizedQuery.Contains(card.Name.ToUpperInvariant(), StringComparison.Ordinal));
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
