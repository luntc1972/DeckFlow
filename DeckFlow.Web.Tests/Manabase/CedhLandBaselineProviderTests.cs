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

        bool found = provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out double mean, out int n);

        Assert.True(found);
        Assert.Equal(25.8, mean, 1);
        Assert.Equal(327, n);
    }

    [Fact]
    public void TryGetBaseline_ExceptionCommander_Plagon_IsUsable()
    {
        // Plagon is under-played in size-tiered results; it was qualified via a 12-month commander
        // search and injected as an exception. Tripwire: a baseline refresh that drops it fails here.
        var provider = new CedhLandBaselineProvider(DataFilePath, NewCache());

        bool found = provider.TryGetBaseline(["Plagon, Lord of the Beach"], out double mean, out int n);

        Assert.True(found);
        Assert.True(n >= 10, "Plagon must stay at a usable sample size (N>=10).");
        Assert.Equal(26.3, mean, 1);
    }

    [Fact]
    public void TryGetBaseline_PartnerMatch_WorksInBothOrders()
    {
        var provider = new CedhLandBaselineProvider(DataFilePath, NewCache());

        bool reverseFound = provider.TryGetBaseline(
            ["Thrasios, Triton Hero", "Rograkh, Son of Rohgahh"], out double reverseMean, out int reverseN);
        bool forwardFound = provider.TryGetBaseline(
            ["Rograkh, Son of Rohgahh", "Thrasios, Triton Hero"], out double forwardMean, out int forwardN);

        Assert.True(reverseFound);
        Assert.True(forwardFound);
        Assert.Equal(27.3, reverseMean, 1);
        Assert.Equal(reverseMean, forwardMean, 1);
        Assert.Equal(241, reverseN);
        Assert.Equal(reverseN, forwardN);
    }

    [Fact]
    public void TryGetBaseline_Miss_ReturnsFalse()
    {
        var provider = new CedhLandBaselineProvider(DataFilePath, NewCache());

        bool found = provider.TryGetBaseline(["Not A Real Commander"], out _, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetBaseline_MissingFile_FailsOpen()
    {
        var provider = new CedhLandBaselineProvider(
            Path.Combine(Path.GetTempPath(), $"missing-baseline-{Guid.NewGuid():N}.json"),
            NewCache());

        bool found = provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out _, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetBaseline_GarbageJson_FailsOpen()
    {
        string tmp = Path.Combine(Path.GetTempPath(), $"garbage-baseline-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tmp, "{ nope");
            var provider = new CedhLandBaselineProvider(tmp, NewCache());

            bool found = provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out _, out _);

            Assert.False(found);
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

            Assert.True(provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out double firstMean, out int firstN));
            File.Delete(tmp);

            bool secondFound = provider.TryGetBaseline(["Kinnan, Bonder Prodigy"], out double secondMean, out int secondN);

            Assert.True(secondFound);
            Assert.Equal(firstMean, secondMean, 1);
            Assert.Equal(firstN, secondN);
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
