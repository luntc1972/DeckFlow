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
