using System.Reflection;
using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class SuppressionWiringTests
{
    [Fact]
    public void ContentKbStores_UseRegisteredSuppressionStoreAndResolver()
    {
        using var provider = CreateProvider();
        var resolver = provider.GetRequiredService<ICreatorIdentityResolver>();

        AssertStore(provider.GetRequiredService<IContentSourceStore>(), resolver, true, true);
        AssertStore(provider.GetRequiredService<IContentVideoStore>(), resolver, true, false);
        AssertStore(provider.GetRequiredService<IContentSiteIndexStore>(), resolver, false, true);
        AssertStore(provider.GetRequiredService<ICreatorStyleStatedRuleStore>(), resolver, true, true);
    }

    [Fact]
    public void ContentKbStoreResolution_DoesNotCreateResolverCycle()
    {
        using var provider = CreateProvider();

        _ = provider.GetRequiredService<IContentSourceStore>();
        _ = provider.GetRequiredService<IContentVideoStore>();
        _ = provider.GetRequiredService<IContentSiteIndexStore>();
        _ = provider.GetRequiredService<ICreatorStyleStatedRuleStore>();
        _ = provider.GetRequiredService<ICreatorIdentityResolver>();
    }

    [Fact]
    public void CreatorStyleStores_UseRegisteredSuppressionStoreAndResolverWithoutCycle()
    {
        using var provider = CreateCreatorStyleProvider();
        var resolver = provider.GetRequiredService<ICreatorIdentityResolver>();

        AssertStore(provider.GetRequiredService<ICreatorDeckCacheStore>(), resolver, true, true);
        AssertStore(provider.GetRequiredService<ICreatorStyleProfileStore>(), resolver, true, true);
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddDeckFlowContentKbStores(RelationalDatabaseConnection.FromSqlitePath(Path.GetTempFileName()))
            .BuildServiceProvider();

    private static ServiceProvider CreateCreatorStyleProvider()
    {
        var contentBase = Path.Combine(Path.GetTempPath(), "suppression-wiring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentBase);
        return new ServiceCollection()
            .AddDeckFlowContentKbStores(RelationalDatabaseConnection.FromSqlitePath(Path.GetTempFileName()))
            .AddDeckFlowCreatorStyle(new TestEnvironment(contentBase))
            .BuildServiceProvider();
    }

    private static void AssertStore(object store, ICreatorIdentityResolver resolver, bool hasSuppressionStore, bool hasFilter)
    {
        if (hasSuppressionStore)
        {
            Assert.NotNull(store.GetType().GetField("_suppressionStore", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(store));
        }
        if (!hasFilter)
        {
            return;
        }

        var filter = Assert.IsAssignableFrom<ICreatorSuppressionFilter>(store.GetType().GetField("_suppressionFilter", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(store));
        var lazy = Assert.IsType<Lazy<ICreatorIdentityResolver>>(filter.GetType().GetField("_resolver", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(filter));
        Assert.Same(resolver, lazy.Value);
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
