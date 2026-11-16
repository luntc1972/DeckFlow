using DeckFlow.Core.Bracket;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Services.Bracket;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeckFlow.Web.Tests.Bracket;

/// <summary>
/// Verifies that <see cref="GameChangerCatalogService"/> correctly loads
/// <c>bracket-data.json</c> into <see cref="IMemoryCache"/> and that the JSON binding
/// populates every field (53 Game Changers, 5 non-empty Tiers, EffectiveDate 2026-02-09).
/// Uses the internal test-seam constructor to avoid requiring a web host.
/// </summary>
public sealed class GameChangerCatalogServiceTests
{
    /// <summary>
    /// Absolute path to the canonical bracket-data.json in the repo. Navigated from the
    /// test assembly location (AppContext.BaseDirectory) back up to the solution root,
    /// then into DeckFlow.Web/Data/. Four ".." steps: net10.0 → Debug → bin → Tests → solution.
    /// </summary>
    private static string DataFilePath =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",   // net10.0 → Debug → bin → DeckFlow.Web.Tests → solution root
                "DeckFlow.Web", "Data", "bracket-data.json"));

    private static IMemoryCache NewCache() =>
        new MemoryCache(new MemoryCacheOptions());

    // Why: Shipped misspellings and front-face imports previously hid real Game Changers.
    [Theory]
    [InlineData("Braids, Cabal Minion", "Braids, Cabal Minion")]
    [InlineData("Narset, Parter of Veils", "Narset, Parter of Veils")]
    [InlineData("Tergrid, God of Fright", "Tergrid, God of Fright // Tergrid's Lantern")]
    [InlineData("Tergrid, God of Fright // Tergrid's Lantern", "Tergrid, God of Fright // Tergrid's Lantern")]
    public void Classify_ShippedCatalogRealCardName_DetectsCanonicalName(string deckName, string expectedName)
    {
        using var cache = NewCache();
        var catalog = new GameChangerCatalogService(DataFilePath, cache).GetCatalog();
        var entries = new[]
        {
            new DeckEntry { Name = deckName, NormalizedName = deckName.ToLowerInvariant(), Quantity = 1, Board = "mainboard" },
        };

        var result = BracketClassifier.Classify(entries, catalog, []);

        Assert.Equal(new[] { expectedName }, result.DetectedGameChangers, StringComparer.Ordinal);
        Assert.Equal(3, result.BracketNumber);
    }

    // Why: The shipped catalog must count Tergrid once across imported spellings.
    [Fact]
    public void Classify_ShippedCatalogTergridUnderTwoSpellings_CountsOnce()
    {
        const string frontName = "Tergrid, God of Fright";
        const string canonicalName = "Tergrid, God of Fright // Tergrid's Lantern";
        using var cache = NewCache();
        var catalog = new GameChangerCatalogService(DataFilePath, cache).GetCatalog();
        var entries = new[]
        {
            new DeckEntry { Name = frontName, NormalizedName = frontName.ToLowerInvariant(), Quantity = 1, Board = "commander" },
            new DeckEntry { Name = canonicalName, NormalizedName = canonicalName.ToLowerInvariant(), Quantity = 1, Board = "mainboard" },
        };

        var result = BracketClassifier.Classify(entries, catalog, []);

        Assert.Equal(new[] { canonicalName }, result.DetectedGameChangers, StringComparer.Ordinal);
    }

    // Why: Canonical spellings prevent misleading output and duplicate normalized entries.
    [Fact]
    public void GetCatalog_ShippedGameChangers_UseScryfallSpellings()
    {
        using var cache = NewCache();
        var catalog = new GameChangerCatalogService(DataFilePath, cache).GetCatalog();
        string[] canonicalNames = ["Braids, Cabal Minion", "Narset, Parter of Veils", "Tergrid, God of Fright // Tergrid's Lantern"];
        string[] misspellings = ["Braids Cabal Minion", "Narset Parter of Veils", "Tergrid God of Fright"];

        foreach (string name in canonicalNames)
        {
            Assert.Contains(name, catalog.GameChangers, StringComparer.Ordinal);
        }

        foreach (string name in misspellings)
        {
            Assert.DoesNotContain(name, catalog.GameChangers, StringComparer.Ordinal);
        }

        Assert.Equal(catalog.GameChangers.Count, catalog.GameChangers.Select(CardNormalizer.Normalize).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GetCatalog_ReturnsCorrectGameChangerCount()
    {
        var svc = new GameChangerCatalogService(DataFilePath, NewCache());
        var catalog = svc.GetCatalog();

        Assert.Equal(53, catalog.GameChangers.Count);
    }

    [Fact]
    public void GetCatalog_ReturnsCorrectEffectiveDate()
    {
        var svc = new GameChangerCatalogService(DataFilePath, NewCache());
        var catalog = svc.GetCatalog();

        Assert.Equal(new DateOnly(2026, 2, 9), catalog.EffectiveDate);
    }

    [Fact]
    public void GetCatalog_ReturnsExactlyFiveTiers()
    {
        var svc = new GameChangerCatalogService(DataFilePath, NewCache());
        var catalog = svc.GetCatalog();

        Assert.Equal(5, catalog.Tiers.Count);
    }

    [Fact]
    public void GetCatalog_AllTiersHaveNonEmptyNameLabelSummary()
    {
        // Proves the `tiers` → `Tiers` JSON binding populated every field,
        // not just that the load didn't throw. If the JSON key were wrong
        // (e.g. "bracketTiers") the collection would be empty, not 5-element.
        var svc = new GameChangerCatalogService(DataFilePath, NewCache());
        var catalog = svc.GetCatalog();

        Assert.All(catalog.Tiers, tier =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tier.Name),
                $"Tier {tier.Number}: Name is empty");
            Assert.False(string.IsNullOrWhiteSpace(tier.Label),
                $"Tier {tier.Number}: Label is empty");
            Assert.False(string.IsNullOrWhiteSpace(tier.Summary),
                $"Tier {tier.Number}: Summary is empty");
        });
    }

    [Fact]
    public void GetCatalog_SecondCall_ReturnsCachedInstance()
    {
        var cache = NewCache();
        var svc = new GameChangerCatalogService(DataFilePath, cache);

        var first = svc.GetCatalog();
        var second = svc.GetCatalog();

        Assert.Same(first, second);
    }

    [Fact]
    public void GetCatalog_MissingFile_ThrowsInvalidOperationException()
    {
        var svc = new GameChangerCatalogService(
            Path.Combine(Path.GetTempPath(), "does-not-exist.json"),
            NewCache());

        var ex = Assert.Throws<FileNotFoundException>(() => svc.GetCatalog());
        Assert.NotNull(ex);
    }

    [Fact]
    public void GetCatalog_GarbageJson_ThrowsException()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bad-bracket-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tmp, "{ not valid json {{{{");
            var svc = new GameChangerCatalogService(tmp, NewCache());

            Assert.ThrowsAny<Exception>(() => svc.GetCatalog());
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
