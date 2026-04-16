using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class ChatGptCedhMetaGapServiceTests
{
    [Fact]
    public async Task BuildAsync_ParsesSavedResponseWithoutReloadingDeck()
    {
        var service = CreateService(
            new FakeMoxfieldDeckImporter(),
            new FakeArchidektDeckImporter(),
            new FakeEdhTop16Client(),
            new FakeCommanderSpellbookService());

        var result = await service.BuildAsync(new ChatGptCedhMetaGapRequest
        {
            WorkflowStep = 3,
            MetaGapResponseJson = """
                ```json
                {
                  "meta_gap": {
                    "commander": "Tymna / Kraum",
                    "ref_deck_count": 3,
                    "meta_summary": "Play more stack interaction.",
                    "optimization_path": "Trim clunkier cards."
                  }
                }
                ```
                """
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Equal("Tymna / Kraum", result.AnalysisResponse!.MetaGap.Commander);
        Assert.Equal(3, result.AnalysisResponse.MetaGap.RefDeckCount);
        Assert.Equal("Play more stack interaction.", result.AnalysisResponse.MetaGap.MetaSummary);
        Assert.Equal("Trim clunkier cards.", result.AnalysisResponse.MetaGap.OptimizationPath);
        Assert.NotNull(result.SchemaJson);
        Assert.StartsWith("{", result.SchemaJson);
        Assert.Empty(result.FetchedEntries);
        Assert.Null(result.PromptText);
    }

    [Fact]
    public async Task BuildAsync_ParsesFencedResponseWithTrailingFenceNoise()
    {
        var service = CreateService(
            new FakeMoxfieldDeckImporter(),
            new FakeArchidektDeckImporter(),
            new FakeEdhTop16Client(),
            new FakeCommanderSpellbookService());

        var result = await service.BuildAsync(new ChatGptCedhMetaGapRequest
        {
            WorkflowStep = 3,
            MetaGapResponseJson = """
                ```json
                {
                  "meta_gap": {
                    "commander": "Tivit, Seller of Secrets",
                    "ref_deck_count": 4,
                    "meta_summary": "Closer to the midrange baseline than the turbo baseline.",
                    "optimization_path": "Raise free interaction density before adding extra win-more slots."
                  }
                }
                ```
                ```
                """
        });

        Assert.NotNull(result.AnalysisResponse);
        Assert.Equal("Tivit, Seller of Secrets", result.AnalysisResponse!.MetaGap.Commander);
        Assert.Equal(4, result.AnalysisResponse.MetaGap.RefDeckCount);
        Assert.Equal("Closer to the midrange baseline than the turbo baseline.", result.AnalysisResponse.MetaGap.MetaSummary);
    }

    [Fact]
    public async Task BuildAsync_GeneratesPromptFromDeckAndSortedReferenceEntries()
    {
        var importer = new FakeMoxfieldDeckImporter(new List<DeckEntry>
        {
            CreateDeckEntry("Kinnan, Bonder Prodigy", "commander"),
            CreateDeckEntry("Sol Ring"),
            CreateDeckEntry("Llanowar Elves")
        });

        var edhTop16Client = new FakeEdhTop16Client(
            new EdhTop16Entry
            {
                Standing = 2,
                PlayerName = "Later Pilot",
                TournamentName = "Modern Meta Cup",
                TournamentDate = new DateOnly(2026, 4, 10),
                MainDeck = new[]
                {
                    new EdhTop16Card { Name = "Mox Diamond", Type = "Artifact" },
                    new EdhTop16Card { Name = "Mana Crypt", Type = "Artifact" }
                }
            },
            new EdhTop16Entry
            {
                Standing = 1,
                PlayerName = "Earlier Pilot",
                TournamentName = "Open",
                TournamentDate = new DateOnly(2026, 3, 1),
                MainDeck = new[]
                {
                    new EdhTop16Card { Name = "Birds of Paradise", Type = "Creature" }
                }
            });

        var spellbookService = new FakeCommanderSpellbookService(
            new CommanderSpellbookResult(
                new[]
                {
                    new SpellbookCombo(
                        new[] { "Basalt Monolith", "Kinnan, Bonder Prodigy" },
                        new[] { "Infinite colorless mana" },
                        "Activate Basalt Monolith repeatedly.")
                },
                Array.Empty<SpellbookAlmostCombo>()),
            new CommanderSpellbookResult(
                new[]
                {
                    new SpellbookCombo(
                        new[] { "Mana Crypt", "Hullbreaker Horror" },
                        new[] { "Infinite mana" },
                        "Loop rocks with Horror.")
                },
                Array.Empty<SpellbookAlmostCombo>()),
            new CommanderSpellbookResult(
                Array.Empty<SpellbookCombo>(),
                new[]
                {
                    new SpellbookAlmostCombo(
                        "Devoted Druid",
                        new[] { "Vizier of Remedies" },
                        new[] { "Infinite green mana" },
                        "Add Devoted Druid to complete the loop.")
                }));

        var service = CreateService(importer, new FakeArchidektDeckImporter(), edhTop16Client, spellbookService);

        var result = await service.BuildAsync(new ChatGptCedhMetaGapRequest
        {
            WorkflowStep = 2,
            DeckSource = "https://www.moxfield.com/decks/test-list",
            SelectedReferenceIndexes = new List<int> { 0, 1 }
        });

        Assert.Equal("Kinnan, Bonder Prodigy", result.ResolvedCommanderName);
        Assert.Equal(new[] { "Later Pilot", "Earlier Pilot" }, result.FetchedEntries.Select(entry => entry.PlayerName));
        Assert.Contains("Commander: Kinnan, Bonder Prodigy", result.InputSummary);
        Assert.Contains("Fetched EDH Top 16 entries: 2", result.InputSummary);
        Assert.Contains("Title this chat: Kinnan, Bonder Prodigy | cEDH Meta Gap", result.PromptText);
        Assert.Contains("ROLE:", result.PromptText);
        Assert.Contains("EVIDENCE PRIORITY:", result.PromptText);
        Assert.Contains("RULES:", result.PromptText);
        Assert.Contains("INPUT DATA:", result.PromptText);
        Assert.Contains("ANALYSIS TASK:", result.PromptText);
        Assert.Contains("OUTPUT CONTRACT:", result.PromptText);
        Assert.Contains("Compare MY_DECK against 2 REF deck(s).", result.PromptText);
        Assert.Contains("Use the supplied decklists as the primary evidence.", result.PromptText);
        Assert.Contains("If Commander Spellbook evidence and deck-reading inference conflict, prefer the Commander Spellbook evidence.", result.PromptText);
        Assert.Contains("- Then return the JSON inside a fenced ```json code block (triple-backtick json) whose top-level object is meta_gap.", result.PromptText);
        Assert.Contains("- Fill every field in meta_gap.", result.PromptText);
        Assert.Contains("- Use empty strings, 0, 0.0, false, or [] when evidence is missing.", result.PromptText);
        Assert.Contains("1 Llanowar Elves", result.PromptText);
        Assert.Contains("1 Sol Ring", result.PromptText);
        Assert.Contains("Commander Spellbook combos for MY_DECK:", result.PromptText);
        Assert.Contains("Commander Spellbook combos for R1:", result.PromptText);
        Assert.Contains("Commander Spellbook combos for R2:", result.PromptText);
        Assert.Contains("Infinite colorless mana", result.PromptText);
        Assert.Contains("Infinite mana", result.PromptText);
        Assert.Contains("Infinite green mana", result.PromptText);
        Assert.DoesNotContain("How:", result.PromptText);
        Assert.Contains("R1 (Later Pilot, #2, Modern Meta Cup, 2026-04-10):", result.PromptText);
        Assert.Contains("\"meta_gap\"", result.SchemaJson);
        Assert.Equal("Kinnan, Bonder Prodigy", edhTop16Client.LastCommanderName);
        Assert.Equal(3, spellbookService.RecordedDecks.Count);
    }

    [Fact]
    public async Task BuildAsync_NormalizesAlternateFaceNamesToBaseCardNamesInPromptDecklists()
    {
        var importer = new FakeMoxfieldDeckImporter(new List<DeckEntry>
        {
            CreateDeckEntry("Kinnan, Bonder Prodigy", "commander"),
            CreateDeckEntry("Delver of Secrets // Insectile Aberration"),
            CreateDeckEntry("Fire / Ice")
        });

        var edhTop16Client = new FakeEdhTop16Client(
            new EdhTop16Entry
            {
                Standing = 1,
                PlayerName = "Pilot",
                TournamentDate = new DateOnly(2026, 4, 10),
                MainDeck = new[]
                {
                    new EdhTop16Card { Name = "Delver of Secrets // Insectile Aberration", Type = "Creature" },
                    new EdhTop16Card { Name = "Fire / Ice", Type = "Instant" }
                }
            });

        var service = CreateService(importer, new FakeArchidektDeckImporter(), edhTop16Client, new FakeCommanderSpellbookService());

        var result = await service.BuildAsync(new ChatGptCedhMetaGapRequest
        {
            WorkflowStep = 2,
            DeckSource = "https://www.moxfield.com/decks/test-list",
            SelectedReferenceIndexes = new List<int> { 0 }
        });

        Assert.Contains("1 Delver of Secrets", result.PromptText);
        Assert.Contains("1 Fire", result.PromptText);
        Assert.DoesNotContain("Insectile Aberration", result.PromptText);
        Assert.DoesNotContain("Fire / Ice", result.PromptText);
    }

    [Fact]
    public async Task BuildAsync_ResolvesAlternatePrintNamesToOracleNamesInPrompt()
    {
        var importer = new FakeMoxfieldDeckImporter(new List<DeckEntry>
        {
            CreateDeckEntry("Plagon, Lord of the Beach", "commander"),
            CreateDeckEntry("Unstable Harmonics")
        });

        var edhTop16Client = new FakeEdhTop16Client(
            new EdhTop16Entry
            {
                Standing = 1,
                PlayerName = "Pilot",
                TournamentDate = new DateOnly(2026, 4, 10),
                MainDeck = new[]
                {
                    new EdhTop16Card { Name = "Unstable Harmonics", Type = "Enchantment" }
                }
            });

        var scryfall = new FakeScryfallResolver();
        scryfall.CollectionNameMap["Unstable Harmonics"] = "Rhystic Study";

        var service = CreateService(
            importer,
            new FakeArchidektDeckImporter(),
            edhTop16Client,
            new FakeCommanderSpellbookService(),
            scryfall);

        var result = await service.BuildAsync(new ChatGptCedhMetaGapRequest
        {
            WorkflowStep = 2,
            DeckSource = "https://www.moxfield.com/decks/test-list",
            SelectedReferenceIndexes = new List<int> { 0 }
        });

        Assert.Contains("1 Rhystic Study", result.PromptText);
        Assert.DoesNotContain("1 Unstable Harmonics", result.PromptText);
    }

    [Fact]
    public async Task BuildAsync_ResolvesAlternatePrintNamesBeforeCommanderSpellbookLookup()
    {
        var importer = new FakeMoxfieldDeckImporter(new List<DeckEntry>
        {
            CreateDeckEntry("Plagon, Lord of the Beach", "commander"),
            CreateDeckEntry("Unstable Harmonics")
        });

        var edhTop16Client = new FakeEdhTop16Client(
            new EdhTop16Entry
            {
                Standing = 1,
                PlayerName = "Pilot",
                TournamentDate = new DateOnly(2026, 4, 10),
                MainDeck = new[]
                {
                    new EdhTop16Card { Name = "Unstable Harmonics", Type = "Enchantment" }
                }
            });

        var scryfall = new FakeScryfallResolver();
        scryfall.CollectionNameMap["Unstable Harmonics"] = "Rhystic Study";

        var spellbookService = new FakeCommanderSpellbookService();

        var service = CreateService(
            importer,
            new FakeArchidektDeckImporter(),
            edhTop16Client,
            spellbookService,
            scryfall);

        _ = await service.BuildAsync(new ChatGptCedhMetaGapRequest
        {
            WorkflowStep = 2,
            DeckSource = "https://www.moxfield.com/decks/test-list",
            SelectedReferenceIndexes = new List<int> { 0 }
        });

        Assert.Equal(2, spellbookService.RecordedDecks.Count);
        Assert.All(
            spellbookService.RecordedDecks,
            deck =>
            {
                Assert.Contains(deck, entry => string.Equals(entry.Name, "Rhystic Study", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(deck, entry => string.Equals(entry.Name, "Unstable Harmonics", StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public async Task BuildAsync_RejectsMoreThanFiveSelectedReferences()
    {
        var entries = Enumerable.Range(1, 4)
            .Select(index => new EdhTop16Entry
            {
                Standing = index,
                PlayerName = $"Pilot {index}",
                TournamentDate = new DateOnly(2026, 4, index),
                MainDeck = new[] { new EdhTop16Card { Name = $"Card {index}", Type = "Spell" } }
            })
            .ToArray();

        var service = CreateService(
            new FakeMoxfieldDeckImporter(new List<DeckEntry>
            {
                CreateDeckEntry("Kinnan, Bonder Prodigy", "commander"),
                CreateDeckEntry("Sol Ring")
            }),
            new FakeArchidektDeckImporter(),
            new FakeEdhTop16Client(entries),
            new FakeCommanderSpellbookService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildAsync(new ChatGptCedhMetaGapRequest
        {
            WorkflowStep = 2,
            DeckSource = "https://www.moxfield.com/decks/test-list",
            SelectedReferenceIndexes = new List<int> { 0, 1, 2, 3 }
        }));

        Assert.Equal("Select no more than 3 EDH Top 16 reference decks before generating the prompt.", exception.Message);
    }

    private static ChatGptCedhMetaGapService CreateService(
        IMoxfieldDeckImporter moxfieldDeckImporter,
        IArchidektDeckImporter archidektDeckImporter,
        IEdhTop16Client edhTop16Client,
        ICommanderSpellbookService? commanderSpellbookService = null,
        FakeScryfallResolver? scryfallResolver = null)
    {
        var resolver = scryfallResolver ?? new FakeScryfallResolver();
        return new(
            moxfieldDeckImporter,
            archidektDeckImporter,
            new MoxfieldParser(),
            new ArchidektParser(),
            edhTop16Client,
            commanderSpellbookService ?? new FakeCommanderSpellbookService(),
            executeCollectionAsync: resolver.ExecuteCollectionAsync,
            executeSearchAsync: resolver.ExecuteSearchAsync);
    }

    private static DeckEntry CreateDeckEntry(string name, string board = "mainboard")
        => new()
        {
            Name = name,
            NormalizedName = CardNormalizer.Normalize(name),
            Quantity = 1,
            Board = board
        };

    private sealed class FakeMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        private readonly List<DeckEntry> _entries;

        public FakeMoxfieldDeckImporter(List<DeckEntry>? entries = null)
        {
            _entries = entries ?? new List<DeckEntry>();
        }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(_entries);
    }

    private sealed class FakeArchidektDeckImporter : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class FakeEdhTop16Client : IEdhTop16Client
    {
        private readonly IReadOnlyList<EdhTop16Entry> _entries;

        public FakeEdhTop16Client(params EdhTop16Entry[] entries)
        {
            _entries = entries;
        }

        public string? LastCommanderName { get; private set; }

        public Task<IReadOnlyList<EdhTop16Entry>> SearchCommanderEntriesAsync(
            string commanderName,
            CedhMetaTimePeriod timePeriod,
            CedhMetaSortBy sortBy,
            int minEventSize,
            int? maxStanding,
            int count,
            CancellationToken cancellationToken = default)
        {
            LastCommanderName = commanderName;
            return Task.FromResult(_entries);
        }
    }

    private sealed class FakeCommanderSpellbookService : ICommanderSpellbookService
    {
        private readonly Queue<CommanderSpellbookResult?> _results;

        public FakeCommanderSpellbookService(params CommanderSpellbookResult?[] results)
        {
            _results = new Queue<CommanderSpellbookResult?>(results);
        }

        public List<IReadOnlyList<DeckEntry>> RecordedDecks { get; } = new();

        public Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken = default)
        {
            RecordedDecks.Add(entries.ToList());
            var result = _results.Count > 0 ? _results.Dequeue() : null;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeScryfallResolver
    {
        public Dictionary<string, string> CollectionNameMap { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
        {
            var identifiers = request.Parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, "application/json", StringComparison.OrdinalIgnoreCase))
                ?.Value?.ToString() ?? string.Empty;

            var names = CollectionNameMap.Keys
                .Concat(ExtractNames(identifiers))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cards = ExtractNames(identifiers)
                .Select(name => new ScryfallCard(
                    CollectionNameMap.TryGetValue(name, out var resolved) ? resolved : name,
                    null,
                    "Type",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null))
                .ToList();

            return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(cards, null)
            });
        }

        public Task<RestResponse<ScryfallSearchResponse>> ExecuteSearchAsync(RestRequest request, CancellationToken cancellationToken)
        {
            var rawQuery = request.Parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, "q", StringComparison.OrdinalIgnoreCase))
                ?.Value?.ToString() ?? string.Empty;
            var name = rawQuery.Trim().TrimStart('!').Trim('"');
            var resolvedName = CollectionNameMap.TryGetValue(name, out var resolved) ? resolved : name;

            return Task.FromResult(new RestResponse<ScryfallSearchResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallSearchResponse(new List<ScryfallCard>
                {
                    new(
                        resolvedName,
                        null,
                        "Type",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null)
                })
            });
        }

        private static IReadOnlyList<string> ExtractNames(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<string>();
            }

            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("identifiers", out var identifiers) || identifiers.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return identifiers.EnumerateArray()
                .Select(element => element.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }
    }
}
