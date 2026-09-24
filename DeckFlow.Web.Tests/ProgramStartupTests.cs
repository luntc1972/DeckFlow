using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services;
using DeckFlow.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Startup-specific tests for the web composition root.
/// </summary>
[Collection("AdminEnvSerial")]
public sealed class ProgramStartupTests
{
    [Fact]
    public async Task AwaitStartupSeedTasksAsync_WhenBothSeedTasksFault_LogsEachFailureBeforeRethrow()
    {
        var logger = new FakeLogger<Program>();
        var contentException = new InvalidOperationException("Malformed content seed.");
        var creatorException = new InvalidOperationException("Malformed creator-style seed.");

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            Program.AwaitStartupSeedTasksAsync(
                Task.FromException(contentException),
                Task.FromException(creatorException),
                logger));

        Assert.Same(contentException, exception);
        Assert.Collection(
            logger.Entries,
            entry =>
            {
                Assert.Equal(LogLevel.Error, entry.Level);
                Assert.Contains("contentKbSeedTask", entry.Message, StringComparison.Ordinal);
            },
            entry =>
            {
                Assert.Equal(LogLevel.Error, entry.Level);
                Assert.Contains("creatorStyleSeedTask", entry.Message, StringComparison.Ordinal);
            });
    }

    // Why (IN-01): CompositionRoot_WithCreatorStyleDisabled_DoesNotRegisterSeedLoader was deleted.
    // It re-implemented the gate inside its own body (`if (Program.IsCreatorStyleEnabled()) ...`),
    // so with EnvScope.Clear guaranteeing the gate false, the `if` never ran and Assert.Null was
    // trivially true — deleting the real gate from Program.cs would not fail it. It exercised the
    // test's own copy of the logic, not Program.Main's real registration.
    //
    // Why (phase 114): IsCreatorStyleEnabled_ParsesEnvironmentVariable was deleted along with
    // Program.IsCreatorStyleEnabled() itself — the DECKFLOW_CREATOR_STYLE_ENABLED opt-in gate it
    // parsed was retired once AdminCreatorStyleController gave the creator-style engine a real
    // HTTP entry point (Program.BuildApp now registers AddDeckFlowCreatorStyle unconditionally).

    private sealed class RecordingCreatorStyleSeedLoader : ICreatorStyleSeedLoader
    {
        public int CallCount { get; private set; }

        public Task<int> LoadIfPresentAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(3);
        }
    }

    [Fact]
    public async Task LoadCreatorStyleSeedAsync_WhenLoaderIsNotRegistered_ReturnsZeroWithoutThrowing()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();

        Assert.Equal(0, await Program.LoadCreatorStyleSeedAsync(services));
    }

    [Fact]
    public async Task LoadCreatorStyleSeedAsync_WhenLoaderIsRegistered_InvokesLoaderAndReturnsItsCount()
    {
        var loader = new RecordingCreatorStyleSeedLoader();
        await using var services = new ServiceCollection()
            .AddSingleton<ICreatorStyleSeedLoader>(loader)
            .BuildServiceProvider();

        var result = await Program.LoadCreatorStyleSeedAsync(services);

        Assert.Equal(1, loader.CallCount);
        Assert.Equal(3, result);
    }

    // Why (phase 114): proves the real loader takes the found-and-parsed branch for synthetic files,
    // rather than silently skipping missing files.
    [Fact]
    public async Task LoadCreatorStyleSeedAsync_WithRealLoaderAgainstTempContentBase_ReadsSyntheticSeedFiles()
    {
        var tempContentBase = Path.Combine(Path.GetTempPath(), "program-startup-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempContentBase);
        WriteSeed(tempContentBase, ContentKbPaths.CreatorStyleProfileSeedRelativePath, "[{\"slug\":\"test-creator\",\"platform\":\"test\",\"minDecks\":1,\"insufficientSample\":false,\"statedRules\":[],\"measuredMetrics\":[],\"fusedTargets\":[],\"updatedUtc\":\"2026-07-18T00:00:00Z\"}]");
        WriteSeed(tempContentBase, ContentKbPaths.CreatorDeckCacheSeedRelativePath, "[{\"creatorSlug\":\"test-creator\",\"deckId\":\"test-deck\",\"contentHash\":\"hash\",\"folderId\":1,\"folderName\":\"Test\",\"size\":1,\"confidenceMarker\":\"exact\",\"entries\":[{\"name\":\"Sol Ring\",\"normalizedName\":\"sol ring\",\"quantity\":1,\"board\":\"mainboard\"}],\"cachedUtc\":\"2026-07-18T00:00:00Z\"}]");
        try
        {
            var resolver = new ContentKbArtifactPathResolver(
                new StubWebHostEnvironment(tempContentBase),
                new ConfigurationBuilder().Build(),
                new FakeFeatureFlagCache(),
                NullLogger<ContentKbArtifactPathResolver>.Instance);

            var loader = new CreatorStyleSeedLoader(
                resolver,
                new NoOpCreatorStyleProfileStore(),
                new NoOpCreatorDeckCacheStore(),
                new FakeCreatorIdentityResolver(),
                new FakeCreatorSuppressionStore(),
                NullLogger<CreatorStyleSeedLoader>.Instance);
            await using var services = new ServiceCollection()
                .AddSingleton<ICreatorStyleSeedLoader>(loader)
                .BuildServiceProvider();

            var count = await Program.LoadCreatorStyleSeedAsync(services);

            Assert.Equal(2, count);
        }
        finally
        {
            Directory.Delete(tempContentBase, recursive: true);
        }
    }

    private static void WriteSeed(string baseDir, string relativePath, string json)
    {
        var path = Path.Combine(baseDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private sealed class StubWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = contentRootPath;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = Environments.Production;
    }

    private sealed class NoOpCreatorStyleProfileStore : ICreatorStyleProfileStore
    {
        public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult<CreatorStyleProfile?>(null);

        public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpCreatorDeckCacheStore : ICreatorDeckCacheStore
    {
        public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetContentHashAsync(string creatorSlug, string deckId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>([]);

        public Task UpsertAsync(CreatorDeckCacheEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
