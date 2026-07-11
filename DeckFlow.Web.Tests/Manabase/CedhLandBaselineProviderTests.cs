using DeckFlow.Web.Services.Manabase;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeckFlow.Web.Tests.Manabase;

public sealed class CedhLandBaselineProviderTests
{
    private static string DataFilePath =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "DeckFlow.Web", "Data", "cedh-land-baseline", "latest.json"));

    private static IMemoryCache NewCache() =>
        new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public void TryGetBaseline_SingleCommanderMatch_BindsLatestJson()
    {
        var provider = new CedhLandBaselineProvider(DataFilePath, NewCache());

        bool found = provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out double mean, out int n, out double sd, out string? generated);

        Assert.True(found);
        Assert.Equal(25.8, mean, 1);
        Assert.Equal(327, n);
        Assert.Equal(0.9, sd, 1);
        Assert.Equal("2026-07", generated);
    }

    [Fact]
    public void TryGetBaseline_ExceptionCommander_Plagon_IsUsable()
    {
        // Plagon is under-played in size-tiered results; it was qualified via a 12-month commander
        // search and injected as an exception. Tripwire: a baseline refresh that drops it fails here.
        var provider = new CedhLandBaselineProvider(DataFilePath, NewCache());

        bool found = provider.TryGetBaseline(["Plagon, Lord of the Beach"], out double mean, out int n, out double sd, out _);

        Assert.True(found);
        Assert.True(n >= 10, "Plagon must stay at a usable sample size (N>=10).");
        Assert.Equal(26.3, mean, 1);
        Assert.True(sd > 0);
    }

    [Fact]
    public void TryGetBaseline_PartnerMatch_WorksInBothOrders()
    {
        var provider = new CedhLandBaselineProvider(DataFilePath, NewCache());

        bool reverseFound = provider.TryGetBaseline(
            ["Thrasios, Triton Hero", "Rograkh, Son of Rohgahh"], out double reverseMean, out int reverseN, out double reverseSd, out string? reverseGenerated);
        bool forwardFound = provider.TryGetBaseline(
            ["Rograkh, Son of Rohgahh", "Thrasios, Triton Hero"], out double forwardMean, out int forwardN, out double forwardSd, out string? forwardGenerated);

        Assert.True(reverseFound);
        Assert.True(forwardFound);
        Assert.Equal(27.3, reverseMean, 1);
        Assert.Equal(reverseMean, forwardMean, 1);
        Assert.Equal(241, reverseN);
        Assert.Equal(reverseN, forwardN);
        Assert.Equal(1.5, reverseSd, 1);
        Assert.Equal(reverseSd, forwardSd, 1);
        Assert.Equal("2026-07", reverseGenerated);
        Assert.Equal(reverseGenerated, forwardGenerated);
    }

    [Fact]
    public void TryGetBaseline_Miss_ReturnsFalse()
    {
        var provider = new CedhLandBaselineProvider(DataFilePath, NewCache());

        bool found = provider.TryGetBaseline(["Not A Real Commander"], out _, out _, out _, out string? generated);

        Assert.False(found);
        Assert.Equal("2026-07", generated);
    }

    [Fact]
    public void TryGetBaseline_MissingFile_FailsOpen()
    {
        var provider = new CedhLandBaselineProvider(
            Path.Combine(Path.GetTempPath(), $"missing-baseline-{Guid.NewGuid():N}.json"),
            NewCache());

        bool found = provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out _, out _, out _, out string? generated);

        Assert.False(found);
        Assert.Null(generated);
    }

    [Fact]
    public void TryGetBaseline_GarbageJson_FailsOpen()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"garbage-baseline-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tmp, "{ nope");
            var provider = new CedhLandBaselineProvider(tmp, NewCache());

            bool found = provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out _, out _, out _, out string? generated);

            Assert.False(found);
            Assert.Null(generated);
        }
        finally
        {
            if (File.Exists(tmp))
            {
                File.Delete(tmp);
            }
        }
    }

    [Fact]
    public void TryGetBaseline_SecondCall_UsesCachedSnapshot()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"baseline-copy-{Guid.NewGuid():N}.json");
        try
        {
            File.Copy(DataFilePath, tmp);
            var provider = new CedhLandBaselineProvider(tmp, NewCache());

            Assert.True(provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out double firstMean, out int firstN, out double firstSd, out string? firstGenerated));
            File.Delete(tmp);

            bool secondFound = provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out double secondMean, out int secondN, out double secondSd, out string? secondGenerated);

            Assert.True(secondFound);
            Assert.Equal(firstMean, secondMean, 1);
            Assert.Equal(firstN, secondN);
            Assert.Equal(firstSd, secondSd, 1);
            Assert.Equal(firstGenerated, secondGenerated);
        }
        finally
        {
            if (File.Exists(tmp))
            {
                File.Delete(tmp);
            }
        }
    }
}
