using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DeckFlow.Web.Services.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Polly.Timeout;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Startup smoke test for the named Polly v8 resilience pipelines. A misspelled or removed
/// pipeline name would otherwise surface only at runtime on the first upstream call
/// (<see cref="KeyNotFoundException"/>); this fails fast at test time instead.
///
/// The provider is built once in a static field and reused for convenience; registration is
/// per-collection (TryAddSingleton) so this is not order-dependent.
/// </summary>
public sealed class ResiliencePipelineFactoryTests
{
    private static readonly ResiliencePipelineProvider<string> Provider = BuildProvider();

    private static ResiliencePipelineProvider<string> BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDeckFlowResiliencePipelines();
        return services.BuildServiceProvider()
            .GetRequiredService<ResiliencePipelineProvider<string>>();
    }

    [Theory]
    [InlineData("banlist")]
    [InlineData("spellbook")]
    [InlineData("tagger")]
    [InlineData("tagger-post")]
    [InlineData("scryfall")]
    public void EveryNamedPipeline_Resolves(string name)
    {
        var pipeline = Provider.GetPipeline<RestResponse>(name);
        Assert.NotNull(pipeline);
    }

    [Fact]
    public void UnregisteredPipeline_FailsFast()
    {
        // Documents real behavior: an unknown key throws rather than returning a silent no-op,
        // so a typo'd pipeline name surfaces loudly on first use.
        Assert.Throws<KeyNotFoundException>(
            () => Provider.GetPipeline<RestResponse>("does-not-exist"));
    }

    [Fact]
    public void ScryfallBudget_MatchesIndependentProductionContract()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), ResiliencePipelineFactory.ScryfallTotalTimeout);
        Assert.Equal(2, ResiliencePipelineFactory.ScryfallMaxRetryAttempts);
    }

    [Fact]
    public void EdhrecBudget_CoversAllAttemptsWithPerAttemptTimeout()
    {
        const int maxRetryAttempts = 2;

        Assert.True(
            ResiliencePipelineFactory.EdhrecTotalTimeout >=
            (maxRetryAttempts + 1) * ResiliencePipelineFactory.EdhrecAttemptTimeout);
    }

    [Fact]
    public async Task ScryfallTimeout_UsesTotalBudgetAcrossRetries()
    {
        var builder = new ResiliencePipelineBuilder<RestResponse>();
        ResiliencePipelineFactory.BuildScryfall(builder, TimeSpan.FromMilliseconds(200));
        var pipeline = builder.Build();

        await Assert.ThrowsAsync<TimeoutRejectedException>(
            async () => await pipeline.ExecuteAsync(
                async cancellationToken =>
                {
                    await Task.Delay(150, cancellationToken);
                    return new RestResponse { StatusCode = HttpStatusCode.InternalServerError };
                }));
    }
}
