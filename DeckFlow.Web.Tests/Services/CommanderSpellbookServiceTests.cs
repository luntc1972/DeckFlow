using DeckFlow.Core.Models;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Net.Http;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="CommanderSpellbookService"/> covering combo lookup, Moxfield fallback parsing, and API failure handling.
/// </summary>
public sealed class CommanderSpellbookServiceTests
{
    // Helper to build a minimal DeckEntry for "mainboard"
    private static DeckEntry MainboardEntry(string name) => new DeckEntry
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = 1,
        Board = "mainboard"
    };

    private static DeckEntry CommanderEntry(string name) => new DeckEntry
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = 1,
        Board = "commander"
    };

    private static CommanderSpellbookService BuildService(StubHttpMessageHandler stub, IMemoryCache? cache = null)
    {
        var factory = new FakeHttpClientFactory(new Dictionary<string, HttpMessageHandler>
        {
            ["commander-spellbook"] = stub
        });
        return TestServiceFactory.CreateCommanderSpellbookService(
            factory,
            cache ?? new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public async Task FindCombosAsync_SingleCombo_ReturnsCombo()
    {
        const string json = """
{
  "results": {
    "included": [
      {
        "uses": [{"card": {"name": "Thrasios, Triton Hero"}}, {"card": {"name": "Tymna the Weaver"}}],
        "produces": [{"feature": {"name": "Infinite mana"}}],
        "description": "Step 1: tap Thrasios."
      }
    ],
    "almostIncluded": []
  }
}
""";
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        var sut = BuildService(stub);
        var deck = new List<DeckEntry> { MainboardEntry("Thrasios, Triton Hero"), MainboardEntry("Tymna the Weaver") };

        var result = await sut.FindCombosAsync(deck, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.IncludedCombos);
        Assert.Contains("Infinite mana", result.IncludedCombos[0].Results);
        Assert.Contains("Thrasios, Triton Hero", result.IncludedCombos[0].CardNames);
    }

    [Fact]
    public async Task ParseVariants_PopularityAndManaValueNeeded_Parsed()
    {
        const string json = """
{
  "results": {
    "included": [
      {
        "uses": [{"card": {"name": "Thrasios, Triton Hero"}}, {"card": {"name": "Tymna the Weaver"}}],
        "produces": [{"feature": {"name": "Infinite mana"}}],
        "description": "Step 1: tap Thrasios.",
        "popularity": 5000,
        "manaValueNeeded": 3
      }
    ],
    "almostIncluded": []
  }
}
""";
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        var sut = BuildService(stub);
        var deck = new List<DeckEntry> { MainboardEntry("Thrasios, Triton Hero"), MainboardEntry("Tymna the Weaver") };

        var result = await sut.FindCombosAsync(deck, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.IncludedCombos);
        Assert.Equal(5000, result.IncludedCombos[0].Popularity);
        Assert.Equal(3, result.IncludedCombos[0].ManaValueNeeded);
    }

    [Fact]
    public async Task ParseVariants_MissingRankingFields_DefaultsToNull()
    {
        const string json = """
{
  "results": {
    "included": [
      {
        "uses": [{"card": {"name": "Thrasios, Triton Hero"}}, {"card": {"name": "Tymna the Weaver"}}],
        "produces": [{"feature": {"name": "Infinite mana"}}],
        "description": "Step 1: tap Thrasios."
      }
    ],
    "almostIncluded": []
  }
}
""";
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        var sut = BuildService(stub);
        var deck = new List<DeckEntry> { MainboardEntry("Thrasios, Triton Hero"), MainboardEntry("Tymna the Weaver") };

        var result = await sut.FindCombosAsync(deck, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.IncludedCombos);
        Assert.Null(result.IncludedCombos[0].Popularity);
        Assert.Null(result.IncludedCombos[0].ManaValueNeeded);
    }

    [Fact]
    public async Task ParseVariants_MalformedRankingFields_DegradeToNull()
    {
        // popularity is a string, manaValueNeeded is a decimal, and a second combo carries
        // an out-of-Int32-range integer. All must degrade to null without throwing or
        // dropping the combos (GetInt32 would have thrown on the decimal/out-of-range cases).
        const string json = """
{
  "results": {
    "included": [
      {
        "uses": [{"card": {"name": "Thrasios, Triton Hero"}}],
        "produces": [{"feature": {"name": "Infinite mana"}}],
        "description": "",
        "popularity": "abc",
        "manaValueNeeded": 3.5
      },
      {
        "uses": [{"card": {"name": "Tymna the Weaver"}}],
        "produces": [{"feature": {"name": "Draw engine"}}],
        "description": "",
        "popularity": 99999999999,
        "manaValueNeeded": 99999999999
      }
    ],
    "almostIncluded": []
  }
}
""";
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        var sut = BuildService(stub);
        var deck = new List<DeckEntry> { MainboardEntry("Thrasios, Triton Hero"), MainboardEntry("Tymna the Weaver") };

        var result = await sut.FindCombosAsync(deck, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.IncludedCombos.Count);
        Assert.All(result.IncludedCombos, combo => Assert.Null(combo.Popularity));
        Assert.All(result.IncludedCombos, combo => Assert.Null(combo.ManaValueNeeded));
    }

    [Fact]
    public async Task FindCombosAsync_MultiCombo_ParsesNestedArrays()
    {
        const string json = """
{
  "results": {
    "included": [
      {
        "uses": [{"card": {"name": "Thrasios, Triton Hero"}}],
        "produces": [{"feature": {"name": "Infinite mana"}}],
        "description": ""
      },
      {
        "uses": [{"card": {"name": "Tymna the Weaver"}}],
        "produces": [{"feature": {"name": "Draw engine"}}],
        "description": ""
      }
    ],
    "almostIncluded": []
  }
}
""";
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        var sut = BuildService(stub);
        var deck = new List<DeckEntry> { MainboardEntry("Thrasios, Triton Hero"), MainboardEntry("Tymna the Weaver") };

        var result = await sut.FindCombosAsync(deck, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.IncludedCombos.Count);
        Assert.Contains("Infinite mana", result.IncludedCombos[0].Results);
        Assert.Contains("Draw engine", result.IncludedCombos[1].Results);
    }

    [Fact]
    public async Task FindCombosAsync_HitsCache_OnSecondCall()
    {
        const string json = """
{
  "results": {
    "included": [
      {
        "uses": [{"card": {"name": "Thrasios, Triton Hero"}}],
        "produces": [{"feature": {"name": "Infinite mana"}}],
        "description": ""
      }
    ],
    "almostIncluded": []
  }
}
""";
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = BuildService(stub, cache);
        var deck = new List<DeckEntry> { MainboardEntry("Thrasios, Triton Hero") };

        var first = await sut.FindCombosAsync(deck, CancellationToken.None);
        var second = await sut.FindCombosAsync(deck, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        // Second call must be served from cache — handler invoked exactly once
        Assert.Single(stub.RecordedRequests);
        Assert.Single(first.IncludedCombos);
        Assert.Single(second.IncludedCombos);
    }

    [Fact]
    public async Task FindCombosAsync_ApiFailure_ReturnsNull()
    {
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var sut = BuildService(stub);
        var deck = new List<DeckEntry> { MainboardEntry("Thrasios, Triton Hero") };

        var result = await sut.FindCombosAsync(deck, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindCombosAsync_EmptyDeck_ReturnsNull()
    {
        // Service returns null early when main card list is empty (no mainboard entries)
        var stub = new StubHttpMessageHandler();
        var sut = BuildService(stub);
        var deck = new List<DeckEntry>();

        var result = await sut.FindCombosAsync(deck, CancellationToken.None);

        Assert.Null(result);
        // Handler should not be called — no HTTP request made
        Assert.Empty(stub.RecordedRequests);
    }

    [Fact]
    public async Task FindCombosAsync_CommanderEntry_IncludedInMainboard()
    {
        // Commander board entries are included in the main card list for combo lookup
        const string json = """
{
  "results": {
    "included": [
      {
        "uses": [{"card": {"name": "Thrasios, Triton Hero"}}],
        "produces": [{"feature": {"name": "Infinite mana"}}],
        "description": ""
      }
    ],
    "almostIncluded": []
  }
}
""";
        var stub = new StubHttpMessageHandler();
        stub.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        var sut = BuildService(stub);
        var deck = new List<DeckEntry> { CommanderEntry("Thrasios, Triton Hero") };

        var result = await sut.FindCombosAsync(deck, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.IncludedCombos);
    }
    private const string FrontFace = "Valki, God of Lies";
    private const string DoubleFaced = "Valki, God of Lies // Tibalt, Cosmic Impostor";
    private const string EmptyResponse = "{\"results\":{\"included\":[],\"almostIncluded\":[]}}";

    [Fact]
    public async Task FindCombosAsync_FrontFaceOnlyName_SendsCanonicalName()
    {
        var resolver = CanonicalResolver();
        var bodies = new List<string>();
        var service = BuildRecordingService(bodies, EmptyResponse, resolver);

        await service.FindCombosAsync([MainboardEntry(FrontFace), MainboardEntry("Sol Ring")]);

        var names = SentNames(Assert.Single(bodies), "main");
        Assert.Contains(DoubleFaced, names);
        Assert.DoesNotContain(FrontFace, names);
    }

    [Fact]
    public async Task FindCombosAsync_CommanderFrontFaceName_SendsCanonicalCommanderAndDedupesMain()
    {
        var bodies = new List<string>();
        var service = BuildRecordingService(bodies, EmptyResponse, CanonicalResolver());

        await service.FindCombosAsync([CommanderEntry(FrontFace), MainboardEntry(DoubleFaced), MainboardEntry("Sol Ring")]);

        var body = Assert.Single(bodies);
        Assert.Equal([DoubleFaced], SentNames(body, "commanders"));
        Assert.DoesNotContain(DoubleFaced, SentNames(body, "main"));
    }

    [Fact]
    public async Task FindCombosAsync_SingleFacedAndUnresolvableNames_SentUnchanged()
    {
        var resolver = new FakeCanonicalCardNameResolver();
        resolver.Map["Sol Ring"] = "Sol Ring";
        var bodies = new List<string>();
        var service = BuildRecordingService(bodies, EmptyResponse, resolver);

        await service.FindCombosAsync([MainboardEntry("Sol Ring"), MainboardEntry("Notacard Xyzzy")]);

        var requested = Assert.Single(resolver.RequestedNames);
        Assert.Equal(["Notacard Xyzzy", "Sol Ring"], requested.OrderBy(name => name, StringComparer.Ordinal).ToArray());
        Assert.Equal(["Notacard Xyzzy", "Sol Ring"], SentNames(Assert.Single(bodies), "main"));
    }

    [Fact]
    public async Task FindCombosAsync_ResolverThrows_SendsUnresolvedNamesAndReturnsCombos()
    {
        var resolver = new ThrowingCanonicalCardNameResolver();
        var bodies = new List<string>();
        const string response = """
            {"results":{"included":[{"uses":[{"card":{"name":"Sol Ring"}},{"card":{"name":"Valki, God of Lies"}}],"produces":[{"feature":{"name":"Infinite mana"}}]}],"almostIncluded":[]}}
            """;
        var service = BuildRecordingService(bodies, response, resolver);

        var result = await service.FindCombosAsync([MainboardEntry(FrontFace), MainboardEntry("Sol Ring")]);

        Assert.Equal(1, resolver.CallCount);
        Assert.NotNull(result);
        Assert.Single(result.IncludedCombos);
        Assert.Contains(FrontFace, SentNames(Assert.Single(bodies), "main"));
    }

    [Fact]
    public async Task FindCombosAsync_FrontFaceAndFullSpellings_ShareOneCachedLookup()
    {
        var bodies = new List<string>();
        var service = BuildRecordingService(bodies, EmptyResponse, CanonicalResolver());

        await service.FindCombosAsync([MainboardEntry(FrontFace)]);
        await service.FindCombosAsync([MainboardEntry(DoubleFaced)]);

        Assert.Single(bodies);
    }

    [Fact]
    public async Task FindCombosAsync_FailOpenResult_NotServedToLaterResolvedCall()
    {
        var bodies = new List<string>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var serviceA = BuildRecordingService(bodies, EmptyResponse, new ThrowingCanonicalCardNameResolver(), cache);
        var serviceB = BuildRecordingService(bodies, EmptyResponse, CanonicalResolver(), cache);
        var entries = new[] { MainboardEntry(FrontFace), MainboardEntry("Sol Ring") };

        await serviceA.FindCombosAsync(entries);
        await serviceB.FindCombosAsync(entries);

        Assert.Equal(2, bodies.Count);
        Assert.Contains(DoubleFaced, SentNames(bodies[1], "main"));
    }

    [Fact]
    public async Task FindCombosAsync_ResolverCallerCancellation_ReturnsNullWithoutWarningOrPost()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var resolver = new ThrowingCanonicalCardNameResolver(new OperationCanceledException(cancellation.Token));
        var logger = new FakeLogger<CommanderSpellbookService>();
        var postCalls = 0;
        var service = TestServiceFactory.CreateCommanderSpellbookService(
            new FakeHttpClientFactory(new Dictionary<string, HttpMessageHandler>()),
            new MemoryCache(new MemoryCacheOptions()),
            logger: logger,
            postJsonAsync: (_, _) =>
            {
                postCalls++;
                return Task.FromResult<string?>(EmptyResponse);
            },
            canonicalCardNameResolver: resolver);

        var result = await service.FindCombosAsync([MainboardEntry(FrontFace)], cancellation.Token);

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(0, postCalls);
        Assert.DoesNotContain(logger.Entries, entry =>
            entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning
            && entry.Message.Contains("Card-name canonicalization failed", StringComparison.Ordinal));
        Assert.Null(result);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("commanders")]
    public async Task FindCombosAsync_AliasNames_SendCanonicalNameExactlyOnce(string section)
    {
        var bodies = new List<string>();
        var service = BuildRecordingService(bodies, EmptyResponse, CanonicalResolver());
        DeckEntry[] entries = section == "main"
            ? [MainboardEntry(FrontFace), MainboardEntry(DoubleFaced)]
            : [CommanderEntry(FrontFace), CommanderEntry(DoubleFaced)];

        await service.FindCombosAsync(entries);

        var names = SentNames(Assert.Single(bodies), section);
        Assert.Single(names, name => string.Equals(name, DoubleFaced, StringComparison.OrdinalIgnoreCase));
    }

    private static FakeCanonicalCardNameResolver CanonicalResolver()
    {
        var resolver = new FakeCanonicalCardNameResolver();
        resolver.Map[FrontFace] = DoubleFaced;
        resolver.Map[DoubleFaced] = DoubleFaced;
        return resolver;
    }

    private static string[] SentNames(string body, string section)
    {
        using var document = System.Text.Json.JsonDocument.Parse(body);
        return document.RootElement.GetProperty(section).EnumerateArray()
            .Select(entry => entry.GetProperty("card").GetString()!).ToArray();
    }

    private static CommanderSpellbookService BuildRecordingService(
        List<string> bodies,
        string response,
        DeckFlow.Web.Services.Scryfall.ICanonicalCardNameResolver? canonicalCardNameResolver = null,
        IMemoryCache? cache = null)
    {
        return TestServiceFactory.CreateCommanderSpellbookService(
            new FakeHttpClientFactory(new Dictionary<string, HttpMessageHandler>()),
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            postJsonAsync: (body, _) =>
            {
                bodies.Add(body);
                return Task.FromResult<string?>(response);
            },
            canonicalCardNameResolver: canonicalCardNameResolver);
    }

    [Theory]
    [InlineData("Valki, God of Lies / Tibalt, Cosmic Impostor")]
    [InlineData("Valki, God of Lies // Tibalt, Cosmic Impostor")]
    public async Task FindCombosAsync_SlashName_SendsDoubleSlashInRequestBody(string name)
    {
        var bodies = new List<string>();
        var service = BuildRecordingService(bodies, "{\"results\":{\"included\":[],\"almostIncluded\":[]}}");
        await service.FindCombosAsync(new[] { MainboardEntry(name), CommanderEntry("Kenrith, the Returned King") });
        using var body = System.Text.Json.JsonDocument.Parse(Assert.Single(bodies));
        var main = body.RootElement.GetProperty("main").EnumerateArray().Select(card => card.GetProperty("card").GetString()).ToArray();
        Assert.Equal(new[] { "Valki, God of Lies // Tibalt, Cosmic Impostor" }, main);
    }

    [Fact]
    public async Task FindCombosAsync_SingleSlashCommander_SentAsDoubleSlashAndNotDuplicatedInMain()
    {
        var bodies = new List<string>();
        var service = BuildRecordingService(bodies, "{\"results\":{\"included\":[],\"almostIncluded\":[]}}");
        await service.FindCombosAsync(new[] { CommanderEntry("Esika, God of the Tree / The Prismatic Bridge") });
        using var body = System.Text.Json.JsonDocument.Parse(Assert.Single(bodies));
        Assert.Equal("Esika, God of the Tree // The Prismatic Bridge", body.RootElement.GetProperty("commanders")[0].GetProperty("card").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("commanders").GetArrayLength());
        Assert.Equal(0, body.RootElement.GetProperty("main").GetArrayLength());
    }

    [Fact]
    public async Task FindCombosAsync_SingleAndDoubleSlashSpellings_ShareOneCacheEntry()
    {
        var bodies = new List<string>();
        var service = BuildRecordingService(bodies, "{\"results\":{\"included\":[],\"almostIncluded\":[]}}");
        await service.FindCombosAsync(new[] { MainboardEntry("A / B") });
        await service.FindCombosAsync(new[] { MainboardEntry("A // B") });
        Assert.Single(bodies);
    }

    private const string NearComboResponse = """
        {"results":{"included":[],"almostIncluded":[{"uses":[{"card":{"name":"Valki, God of Lies // Tibalt, Cosmic Impostor"}},{"card":{"name":"Thassa's Oracle"}},{"card":{"name":"Demonic Consultation"}}],"produces":[{"feature":{"name":"Win the game"}}],"description":"Win."}]}}
        """;

    [Theory]
    [InlineData("Valki, God of Lies")]
    [InlineData("Valki, God of Lies / Tibalt, Cosmic Impostor")]
    [InlineData("Valki, God of Lies // Tibalt, Cosmic Impostor")]
    public async Task FindCombosAsync_AlmostIncluded_OwnedCardUnderAnotherSpelling_IsNotReportedMissing(string name)
    {
        var service = BuildRecordingService(new List<string>(), NearComboResponse);
        var result = await service.FindCombosAsync(new[] { MainboardEntry(name), MainboardEntry("Thassa's Oracle") });
        Assert.NotNull(result);
        var combo = Assert.Single(result.AlmostIncludedCombos);
        Assert.Equal("Demonic Consultation", combo.MissingCard);
        Assert.Equal(new[] { "Valki, God of Lies // Tibalt, Cosmic Impostor", "Thassa's Oracle" }, combo.CardsInDeck);
    }

    [Theory]
    [InlineData("Valki, God of Lies")]
    [InlineData("Valki, God of Lies / Tibalt, Cosmic Impostor")]
    [InlineData("Valki, God of Lies // Tibalt, Cosmic Impostor")]
    public async Task FindCombosAsync_AlmostIncluded_AllCardsOwnedUnderFrontFace_IsNotReportedAsMissingPiece(string name)
    {
        var service = BuildRecordingService(new List<string>(), NearComboResponse);
        var result = await service.FindCombosAsync(new[] { MainboardEntry(name), MainboardEntry("Thassa's Oracle"), MainboardEntry("Demonic Consultation") });
        Assert.NotNull(result);
        Assert.Empty(result.AlmostIncludedCombos);
    }

    [Fact]
    public async Task FindCombosAsync_AlmostIncluded_GenuinelyTwoMissing_StillDropped()
    {
        var service = BuildRecordingService(new List<string>(), NearComboResponse);
        var result = await service.FindCombosAsync(new[] { MainboardEntry("Thassa's Oracle") });
        Assert.NotNull(result);
        Assert.Empty(result.AlmostIncludedCombos);
    }
}
