using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;
using Xunit;
using System.Net;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ScryfallSetService"/> covering set listing, release-date ordering, and caching.
/// </summary>
public sealed class ScryfallSetServiceTests
{
    [Fact]
    public async Task GetSetsAsync_ReturnsSetsOrderedByReleaseDateDescending()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("old", "Old Set", "2024-01-01", "expansion", 250, Digital: false),
                        new ScryfallSet("new", "New Set", "2025-01-01", "expansion", 275, Digital: false),
                        new ScryfallSet("mid", "Mid Set", "2024-06-01", "expansion", 260, Digital: false)
                    ])
                }));

        var sets = await service.GetSetsAsync();

        Assert.Collection(
            sets,
            set => Assert.Equal("new", set.Code),
            set => Assert.Equal("mid", set.Code),
            set => Assert.Equal("old", set.Code));
    }

    [Fact]
    public async Task GetSetsAsync_ExcludesDigitalSets()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("ppr", "Paper Set", "2025-01-01", "expansion", 250, Digital: false),
                        new ScryfallSet("vow", "Digital Only Set", "2025-01-01", "expansion", 100, Digital: true)
                    ])
                }));

        var sets = await service.GetSetsAsync();

        Assert.Single(sets);
        Assert.Equal("ppr", sets[0].Code);
    }

    [Fact]
    public async Task BuildSetPacketAsync_FiltersCardsByCommanderColorIdentity()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("tst", "Test Set", "2025-01-01", "expansion", 3, Digital: false)
                    ])
                }),
            executeSearchAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSearchResponse>(new RestRequest("cards/search"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(
                    [
                        new ScryfallCard("Azorius Card", "{W}{U}", "Creature", "Flying", "2", "2", [], ["W", "U"], "tst", "Test Set", "1"),
                        new ScryfallCard("Colorless Card", "{3}", "Artifact", "{T}: Add {C}.", null, null, [], [], "tst", "Test Set", "2"),
                        new ScryfallCard("Rakdos Card", "{B}{R}", "Creature", "Menace", "3", "2", [], ["B", "R"], "tst", "Test Set", "3")
                    ],
                    false,
                    null)
                }));

        var packet = await service.BuildSetPacketAsync(["tst"], ["W", "U"]);

        Assert.Contains("Azorius Card", packet);
        Assert.Contains("Colorless Card", packet);
        Assert.DoesNotContain("Rakdos Card", packet);
    }

    [Fact]
    public async Task BuildSetPacketAsync_ExcludesLowSignalLandsAndAddsSelectionNotes()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("tst", "Test Set", "2025-01-01", "expansion", 4, Digital: false)
                    ])
                }),
            executeSearchAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSearchResponse>(new RestRequest("cards/search"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(
                    [
                        new ScryfallCard("Basic Plains", null, "Basic Land — Plains", "({T}: Add {W}.)", null, null, [], ["W"], "tst", "Test Set", "1"),
                        new ScryfallCard("Temple Campus", null, "Land", "This land enters tapped. {T}: Add {W}.", null, null, [], ["W"], "tst", "Test Set", "2"),
                        new ScryfallCard("Grave Lesson", "{1}{B}", "Sorcery", "Mill three cards, then return target creature card from your graveyard to your hand.", null, null, [], ["B"], "tst", "Test Set", "3"),
                        new ScryfallCard("Token Lecture", "{2}{G}", "Creature", "When this creature enters, create two 1/1 green tokens.", "3", "3", [], ["G"], "tst", "Test Set", "4")
                    ],
                    false,
                    null)
                }));

        var packet = await service.BuildSetPacketAsync(["tst"], ["B", "G", "W"]);

        Assert.Contains("selection_notes:", packet);
        Assert.Contains("candidate_cards_included:", packet);
        Assert.Contains("color_legal_cards_scanned: 4", packet);
        Assert.Contains("Grave Lesson", packet);
        Assert.Contains("Token Lecture", packet);
        Assert.DoesNotContain("Basic Plains", packet);
        Assert.DoesNotContain("Temple Campus", packet);
    }

    [Fact]
    public async Task GetSetsAsync_PopulatesSetTypeFromUpstream()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("cmm", "Commander Masters", "2025-01-01", "commander", 350, Digital: false),
                        new ScryfallSet("blb", "Bloomburrow", "2024-08-01", "expansion", 280, Digital: false)
                    ])
                }));

        var sets = await service.GetSetsAsync();

        var commander = Assert.Single(sets, set => set.Code == "cmm");
        Assert.Equal("commander", commander.SetType);
        var expansion = Assert.Single(sets, set => set.Code == "blb");
        Assert.Equal("expansion", expansion.SetType);
    }

    [Fact]
    public async Task BuildSetPacketAsync_AppendsNotReprintFilterForCommanderSets()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var capturedSearchResources = new List<string>();
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("cmm", "Commander Masters", "2025-01-01", "commander", 350, Digital: false)
                    ])
                }),
            executeSearchAsync: (request, _) =>
            {
                capturedSearchResources.Add(request.Resource ?? string.Empty);
                return Task.FromResult(
                    new RestResponse<ScryfallSearchResponse>(request)
                    {
                        StatusCode = HttpStatusCode.OK,
                        Data = new ScryfallSearchResponse(
                        [
                            new ScryfallCard("New Commander Card", "{2}{G}", "Creature", "When this creature enters, draw a card.", "3", "3", [], ["G"], "cmm", "Commander Masters", "1")
                        ],
                        false,
                        null)
                    });
            });

        await service.BuildSetPacketAsync(["cmm"]);

        var resource = Assert.Single(capturedSearchResources);
        Assert.Contains("not%3Areprint", resource);
    }

    [Fact]
    public async Task BuildSetPacketAsync_DoesNotAppendNotReprintForExpansionSets()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var capturedSearchResources = new List<string>();
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("blb", "Bloomburrow", "2024-08-01", "expansion", 280, Digital: false)
                    ])
                }),
            executeSearchAsync: (request, _) =>
            {
                capturedSearchResources.Add(request.Resource ?? string.Empty);
                return Task.FromResult(
                    new RestResponse<ScryfallSearchResponse>(request)
                    {
                        StatusCode = HttpStatusCode.OK,
                        Data = new ScryfallSearchResponse(
                        [
                            new ScryfallCard("Bloom Card", "{1}{G}", "Creature", "Draw a card.", "2", "2", [], ["G"], "blb", "Bloomburrow", "1")
                        ],
                        false,
                        null)
                    });
            });

        await service.BuildSetPacketAsync(["blb"]);

        var resource = Assert.Single(capturedSearchResources);
        Assert.DoesNotContain("not%3Areprint", resource);
    }

    [Fact]
    public async Task BuildSetPacketAsync_AddsReprintFilterNoteWhenFilterApplies()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("cmm", "Commander Masters", "2025-01-01", "commander", 350, Digital: false)
                    ])
                }),
            executeSearchAsync: (request, _) => Task.FromResult(
                new RestResponse<ScryfallSearchResponse>(request)
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(
                    [
                        new ScryfallCard("New Commander Card", "{2}{G}", "Creature", "When this creature enters, draw a card.", "3", "3", [], ["G"], "cmm", "Commander Masters", "1")
                    ],
                    false,
                    null)
                }));

        var packet = await service.BuildSetPacketAsync(["cmm"]);

        Assert.Contains("Commander/precon sets are filtered to first-print cards only (reprints excluded).", packet);
    }

    [Fact]
    public async Task BuildSetPacketAsync_OmitsReprintFilterNoteForExpansionOnly()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("blb", "Bloomburrow", "2024-08-01", "expansion", 280, Digital: false)
                    ])
                }),
            executeSearchAsync: (request, _) => Task.FromResult(
                new RestResponse<ScryfallSearchResponse>(request)
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(
                    [
                        new ScryfallCard("Bloom Card", "{1}{G}", "Creature", "Draw a card.", "2", "2", [], ["G"], "blb", "Bloomburrow", "1")
                    ],
                    false,
                    null)
                }));

        var packet = await service.BuildSetPacketAsync(["blb"]);

        Assert.DoesNotContain("first-print cards only", packet);
    }

    [Fact]
    public async Task BuildSetPacketAsync_AddsReprintFilterNoteWhenAnySetTriggersFilter()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("cmm", "Commander Masters", "2025-01-01", "commander", 350, Digital: false),
                        new ScryfallSet("blb", "Bloomburrow", "2024-08-01", "expansion", 280, Digital: false)
                    ])
                }),
            executeSearchAsync: (request, _) => Task.FromResult(
                new RestResponse<ScryfallSearchResponse>(request)
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(
                    [
                        new ScryfallCard("Test Card", "{1}{G}", "Creature", "Draw a card.", "2", "2", [], ["G"], (request.Resource ?? string.Empty).Contains("cmm", StringComparison.OrdinalIgnoreCase) ? "cmm" : "blb", "Test", "1")
                    ],
                    false,
                    null)
                }));

        var packet = await service.BuildSetPacketAsync(["cmm", "blb"]);

        Assert.Contains("Commander/precon sets are filtered to first-print cards only (reprints excluded).", packet);
    }

    [Fact]
    public async Task BuildSetPacketAsync_TransformCard_IncludedWithFrontFaceCostAndText()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("mar", "Marvel", "2025-01-01", "expansion", 2, Digital: false)
                    ])
                }),
            executeSearchAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSearchResponse>(new RestRequest("cards/search"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(
                    [
                        new ScryfallCard(
                            "Monica Rambeau // Photon, Living Light",
                            "",
                            "Legendary Creature — Hero // Legendary Creature — Hero",
                            null,
                            null,
                            null,
                            [],
                            ["W"],
                            "mar",
                            "Marvel",
                            "1",
                            CardFaces:
                            [
                                new ScryfallCardFace(
                                    "Monica Rambeau",
                                    "{2}{W}",
                                    "Legendary Creature — Hero",
                                    "Flying, prowess\nWhenever this attacks, put a +1/+1 counter on it.",
                                    "2",
                                    "2"),
                                new ScryfallCardFace(
                                    "Photon, Living Light",
                                    null,
                                    "Legendary Creature — Hero",
                                    "Flying\nWhenever you cast a noncreature spell, this deals 2 damage to any target.",
                                    "3",
                                    "3")
                            ]),
                        new ScryfallCard("Plain Soldier", "{1}{W}", "Creature — Soldier", "When this creature enters, draw a card.", "2", "2", [], ["W"], "mar", "Marvel", "2")
                    ],
                    false,
                    null)
                }));

        var packet = await service.BuildSetPacketAsync(["mar"], ["W"]);

        Assert.Contains("Monica Rambeau", packet);
        var transformLine = packet
            .Split('\n')
            .Single(line => line.StartsWith("Monica Rambeau", StringComparison.Ordinal));
        Assert.Contains("{2}{W}", transformLine);
        Assert.Contains("prowess", transformLine);
    }

    [Fact]
    public async Task BuildSetPacketAsync_TransformCard_RanksIntoCutOverScoreFloorFillers()
    {
        // The packet caps each set at the top 60 candidates by relevance score. Without
        // face-aware scoring a transform card scores only its type bonus minus the empty-cost
        // curve penalty (Creature +5, Legendary +1, no text signals, MV parsed as int.MaxValue
        // => -1 => ~5) and ties the vanilla fillers below, where the empty-cost tiebreak sorts
        // it dead last => excluded as the 61st card. With the fix it earns face text signals
        // and the front-face curve bonus, clearing the cut. This proves the original "cut from
        // top-60" failure is closed, not merely that face text renders.
        var cards = new List<ScryfallCard>
        {
            new ScryfallCard(
                "Monica Rambeau // Photon, Living Light",
                "",
                "Legendary Creature — Hero // Legendary Creature — Hero",
                null,
                null,
                null,
                [],
                ["W"],
                "mar",
                "Marvel",
                "1",
                CardFaces:
                [
                    new ScryfallCardFace(
                        "Monica Rambeau",
                        "{2}{W}",
                        "Legendary Creature — Hero",
                        "Flying, prowess\nWhenever this attacks, put a +1/+1 counter on it.",
                        "2",
                        "2"),
                    new ScryfallCardFace(
                        "Photon, Living Light",
                        null,
                        "Legendary Creature — Hero",
                        "Flying\nWhenever you cast a noncreature spell, this deals 2 damage to any target.",
                        "3",
                        "3")
                ])
        };

        // 60 single-face fillers, each scoring exactly +5 (Creature, MV6 => no curve bonus, no
        // text signals). Their non-empty {5}{W} cost sorts them ahead of an unfixed Monica on
        // the mana-value tiebreak, so without the fix she is the one card pushed past the cap.
        for (var i = 0; i < 60; i++)
        {
            cards.Add(new ScryfallCard(
                $"Filler {i:D2}",
                "{5}{W}",
                "Creature — Soldier",
                "A stalwart guardian.",
                "3",
                "3",
                [],
                ["W"],
                "mar",
                "Marvel",
                (10 + i).ToString()));
        }

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("mar", "Marvel", "2025-01-01", "expansion", 61, Digital: false)
                    ])
                }),
            executeSearchAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSearchResponse>(new RestRequest("cards/search"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(cards, false, null)
                }));

        var packet = await service.BuildSetPacketAsync(["mar"], ["W"]);

        // 61 candidates in, capped to 60 out, and Monica made the cut — so a filler was bumped.
        Assert.Contains("candidate_cards_included: 60", packet);
        Assert.Contains("Monica Rambeau", packet);
    }

    [Fact]
    public async Task BuildSetPacketAsync_CardWithParentTextButNoParentPowerToughness_DoesNotBorrowFacePowerToughness()
    {
        // Regression guard for the P/T fallback: it must only fire for genuine transform/MDFC
        // cards (parent oracle_text empty). A split/adventure-style card carries parent oracle
        // text but no parent P/T; borrowing a face's P/T here would drift its rendered line.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("tst", "Test Set", "2025-01-01", "expansion", 1, Digital: false)
                    ])
                }),
            executeSearchAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSearchResponse>(new RestRequest("cards/search"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(
                    [
                        new ScryfallCard(
                            "Split Spell",
                            "{1}{U}",
                            "Instant",
                            "Draw two cards.",
                            null,
                            null,
                            [],
                            ["U"],
                            "tst",
                            "Test Set",
                            "1",
                            CardFaces:
                            [
                                new ScryfallCardFace("Left Half", "{1}{U}", "Instant", "Draw two cards.", "5", "5"),
                                new ScryfallCardFace("Right Half", "{2}{U}", "Instant", "Counter target spell.", null, null)
                            ])
                    ],
                    false,
                    null)
                }));

        var packet = await service.BuildSetPacketAsync(["tst"], ["U"]);

        var cardLine = packet
            .Split('\n')
            .Single(line => line.StartsWith("Split Spell", StringComparison.Ordinal));
        Assert.Equal("Split Spell | {1}{U} | Instant | Draw two cards.", cardLine);
        Assert.DoesNotContain("5/5", cardLine);
    }

    [Fact]
    public async Task BuildSetPacketAsync_SingleFaceCard_LineUnchanged()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = TestServiceFactory.CreateScryfallSetService(
            cache,
            new FakeMechanicLookupService(),
            executeSetListAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSetListResponse>(new RestRequest("sets"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSetListResponse(
                    [
                        new ScryfallSet("tst", "Test Set", "2025-01-01", "expansion", 1, Digital: false)
                    ])
                }),
            executeSearchAsync: (_, _) => Task.FromResult(
                new RestResponse<ScryfallSearchResponse>(new RestRequest("cards/search"))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(
                    [
                        new ScryfallCard("Sage Scribe", "{1}{G}", "Creature — Elf", "Draw a card.", "2", "2", [], ["G"], "tst", "Test Set", "1")
                    ],
                    false,
                    null)
                }));

        var packet = await service.BuildSetPacketAsync(["tst"], ["G"]);

        var cardLine = packet
            .Split('\n')
            .Single(line => line.StartsWith("Sage Scribe", StringComparison.Ordinal));
        Assert.Equal("Sage Scribe | {1}{G} | Creature — Elf | Draw a card. 2/2", cardLine);
    }

    private sealed class FakeMechanicLookupService : IMechanicLookupService
    {
        public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
            => Task.FromResult(new MechanicLookupResult(mechanicName, false, null, null, null, null, null, "https://magic.wizards.com/en/rules", null));
    }
}
