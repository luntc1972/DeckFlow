using DeckFlow.Studio;
using Microsoft.Extensions.Configuration;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="LoopbackBindGuard"/> loopback-only host classification
/// and config-URL gathering. Covers H2 behaviour (Studio must not bind non-loopback).
/// </summary>
public sealed class LoopbackBindGuardTests
{
    // ── IsLoopbackBindUrl — loopback-true cases ────────────────────────────────

    [Theory]
    [InlineData("http://localhost:5271")]
    [InlineData("http://127.0.0.1:5271")]
    [InlineData("http://127.0.0.2")]
    [InlineData("https://[::1]:5271")]
    [InlineData("http://::1")]
    public void IsLoopbackBindUrl_LoopbackAddress_ReturnsTrue(string url)
    {
        Assert.True(LoopbackBindGuard.IsLoopbackBindUrl(url),
            $"Expected loopback-true for '{url}'");
    }

    // ── IsLoopbackBindUrl — non-loopback-false cases ───────────────────────────

    [Theory]
    [InlineData("http://0.0.0.0:5271")]
    [InlineData("http://+:5271")]
    [InlineData("http://*:5271")]
    [InlineData("http://[::]:5271")]
    [InlineData("http://192.168.1.50:5271")]
    [InlineData("http://studio.example.com")]
    public void IsLoopbackBindUrl_NonLoopbackAddress_ReturnsFalse(string url)
    {
        Assert.False(LoopbackBindGuard.IsLoopbackBindUrl(url),
            $"Expected loopback-false for '{url}'");
    }

    // ── FindNonLoopbackBindings ────────────────────────────────────────────────

    [Fact]
    public void FindNonLoopbackBindings_MixedList_ReturnsOnlyOffending()
    {
        var urls = new[]
        {
            "http://localhost:5271",
            "http://0.0.0.0:5271",
            "http://127.0.0.1:5000",
            "http://+:8080",
        };

        var offending = LoopbackBindGuard.FindNonLoopbackBindings(urls);

        Assert.Equal(2, offending.Count);
        Assert.Contains("http://0.0.0.0:5271", offending);
        Assert.Contains("http://+:8080", offending);
    }

    // ── GatherConfiguredBindUrls ───────────────────────────────────────────────

    [Fact]
    public void GatherConfiguredBindUrls_KestrelEndpointUrl_GathersLocalhostUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kestrel:Endpoints:Http:Url"] = "http://localhost:5271",
            })
            .Build();

        var urls = LoopbackBindGuard.GatherConfiguredBindUrls(config);

        Assert.Contains("http://localhost:5271", urls);
    }

    [Fact]
    public void GatherConfiguredBindUrls_UrlsEnvVar_GathersWildcardUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["urls"] = "http://0.0.0.0:8080",
            })
            .Build();

        var urls = LoopbackBindGuard.GatherConfiguredBindUrls(config);

        // Guard would reject this — but GatherConfiguredBindUrls must surface it.
        Assert.Contains("http://0.0.0.0:8080", urls);
    }

    [Fact]
    public void GatherConfiguredBindUrls_NoConfig_FallsBackToDefault()
    {
        var config = new ConfigurationBuilder().Build();

        var urls = LoopbackBindGuard.GatherConfiguredBindUrls(config);

        Assert.Single(urls);
        Assert.Equal("http://localhost:5271", urls[0]);
    }
}
