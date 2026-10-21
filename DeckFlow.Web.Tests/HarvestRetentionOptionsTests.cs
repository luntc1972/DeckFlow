using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class HarvestRetentionOptionsTests
{
    [Fact]
    public void Defaults_MatchOq6()
    {
        var options = new HarvestRetentionOptions();

        Assert.Equal(100, options.PerCommanderCap);
        Assert.Equal(25, options.BatchSize);
        Assert.Equal(500, options.PauseMilliseconds);
        Assert.Equal(1_073_741_824L, options.MinFreeDiskBytes);
        Assert.Null(options.MaxAgeDays);
        Assert.Equal(500, options.PostHarvestMaxDecks);
    }

    [Fact]
    public void AddTo_BindsSectionValues()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["HarvestRetention:PerCommanderCap"] = "101",
            ["HarvestRetention:BatchSize"] = "26",
            ["HarvestRetention:PauseMilliseconds"] = "750",
            ["HarvestRetention:MinFreeDiskBytes"] = "1024",
            ["HarvestRetention:MaxAgeDays"] = "30",
            ["HarvestRetention:PostHarvestMaxDecks"] = "42"
        });

        var options = provider.GetRequiredService<IOptions<HarvestRetentionOptions>>().Value;

        Assert.Equal(101, options.PerCommanderCap);
        Assert.Equal(26, options.BatchSize);
        Assert.Equal(750, options.PauseMilliseconds);
        Assert.Equal(1024, options.MinFreeDiskBytes);
        Assert.Equal(30, options.MaxAgeDays);
        Assert.Equal(42, options.PostHarvestMaxDecks);
    }

    [Fact]
    public void AddTo_InvalidBatchSize_ThrowsOnValue()
    {
        using var provider = CreateProvider(new Dictionary<string, string?> { ["HarvestRetention:BatchSize"] = "0" });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<HarvestRetentionOptions>>().Value);
    }

    [Fact]
    public async Task AddTo_InvalidBatchSize_HostStartFails()
    {
        using var host = new HostBuilder()
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["HarvestRetention:BatchSize"] = "0" }))
            .ConfigureServices((context, services) => HarvestRetentionOptions.AddTo(services, context.Configuration))
            .Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public void AddTo_MaxAgeDaysAboveTimeSpanLimit_ThrowsOnValue()
    {
        using var invalidProvider = CreateProvider(new Dictionary<string, string?> { ["HarvestRetention:MaxAgeDays"] = "10675200" });
        Assert.Throws<OptionsValidationException>(() => invalidProvider.GetRequiredService<IOptions<HarvestRetentionOptions>>().Value);

        using var validProvider = CreateProvider(new Dictionary<string, string?> { ["HarvestRetention:MaxAgeDays"] = "10675199" });
        var options = validProvider.GetRequiredService<IOptions<HarvestRetentionOptions>>().Value;
        Assert.Equal(TimeSpan.FromDays(10675199), options.ToPruningOptions().MaxAge);
    }

    [Fact]
    public void ToPruningOptions_MapsEveryField()
    {
        var options = new HarvestRetentionOptions
        {
            PerCommanderCap = 101,
            BatchSize = 26,
            PauseMilliseconds = 750,
            MinFreeDiskBytes = 1024,
            MaxAgeDays = 30,
            PostHarvestMaxDecks = 42
        };

        var pruning = options.ToPruningOptions();

        Assert.Equal(101, pruning.PerCommanderCap);
        Assert.Equal(26, pruning.BatchSize);
        Assert.Equal(TimeSpan.FromMilliseconds(750), pruning.Pause);
        Assert.Equal(1024, pruning.MinFreeDiskBytes);
        Assert.Equal(TimeSpan.FromDays(30), pruning.MaxAge);
        options.MaxAgeDays = null;
        Assert.Null(options.ToPruningOptions().MaxAge);
    }

    private static ServiceProvider CreateProvider(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        HarvestRetentionOptions.AddTo(services, configuration);
        return services.BuildServiceProvider();
    }
}
