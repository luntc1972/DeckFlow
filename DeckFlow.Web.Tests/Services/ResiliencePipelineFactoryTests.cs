using System.Collections.Generic;
using DeckFlow.Web.Services.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Startup smoke test for the named Polly v8 resilience pipelines. A misspelled or removed
/// pipeline name would otherwise surface only at runtime on the first upstream call
/// (<see cref="KeyNotFoundException"/>); this fails fast at test time instead.
///
/// The provider is built ONCE in a static field and reused: AddDeckFlowResiliencePipelines
/// registers its provider into the DI container behind a process-global guard, so only the
/// first ServiceCollection in the process receives the registration. A single shared build
/// keeps these tests order-independent. (The guard is a known factory fragility — see the
/// codebase CONCERNS map.)
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
}
