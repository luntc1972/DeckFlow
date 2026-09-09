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

    // Why (phase 114): proves LoadCreatorStyleSeedAsync's file-read/deserialize path actually
    // executes against the real, committed seed file in the default environment — not merely
    // that startup succeeds and not via a stand-in loader like RecordingCreatorStyleSeedLoader
    // above. AddDeckFlowCreatorStyle no longer gates the seed loader behind an opt-in env var
    // (Program.BuildApp registers it unconditionally), so this fact resolves the *real*
    // CreatorStyleSeedLoader/ContentKbArtifactPathResolver pair against the repository's actual
    // content-kb directory and confirms the seed file is found and parsed (not skipped as
    // missing). The seed is still `[]` per Phase 112 D-11, so the row count stays 0 — the point
    // proven here is that the found-and-parsed branch runs, not the file-missing skip branch.
    [Fact]
    public async Task LoadCreatorStyleSeedAsync_WithRealLoaderAgainstRepoContentBase_ReadsRealSeedFileRatherThanSkippingAsMissing()
    {
        var repoRoot = GetRepoRoot();
        var resolver = new ContentKbArtifactPathResolver(
            new StubWebHostEnvironment(repoRoot),
            new ConfigurationBuilder().Build(),
            new FakeFeatureFlagCache(),
            NullLogger<ContentKbArtifactPathResolver>.Instance);

        var seedFilePath = resolver.ResolveArtifactFullPath(ContentKbPaths.CreatorStyleProfileSeedRelativePath);
        Assert.True(File.Exists(seedFilePath), $"Expected the committed creator-style seed file at {seedFilePath}.");

        var loader = new CreatorStyleSeedLoader(
            resolver,
            new NoOpCreatorStyleProfileStore(),
            new NoOpCreatorDeckCacheStore(),
            NullLogger<CreatorStyleSeedLoader>.Instance);
        await using var services = new ServiceCollection()
            .AddSingleton<ICreatorStyleSeedLoader>(loader)
            .BuildServiceProvider();

        var count = await Program.LoadCreatorStyleSeedAsync(services);

        Assert.Equal(0, count);
    }

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeckFlow.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the current test base directory.");
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public StubWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new NullFileProvider();
            WebRootPath = contentRootPath;
            WebRootFileProvider = new NullFileProvider();
        }

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }

        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = Environments.Production;
    }

    private sealed class NoOpCreatorStyleProfileStore : ICreatorStyleProfileStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult<CreatorStyleProfile?>(null);

        public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpCreatorDeckCacheStore : ICreatorDeckCacheStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetContentHashAsync(string creatorSlug, string deckId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CreatorDeckCacheEntry>>(Array.Empty<CreatorDeckCacheEntry>());

        public Task UpsertAsync(CreatorDeckCacheEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
