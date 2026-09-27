using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Pins the public-image cold start when Content KB bodies only exist in the private data overlay.
/// </summary>
public sealed class PrivateRootColdStartTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    [Fact]
    public async Task ColdStart_AbsentGitContentKb_UsesOverlayBodyAndSkipsSeedLoad()
    {
        var contentBase = CreateTempDir();
        var overlay = CreateTempDir();
        var bodyPath = Path.Combine(overlay, "content-kb", "synthetic", "entry.md");
        Directory.CreateDirectory(Path.GetDirectoryName(bodyPath)!);
        await File.WriteAllTextAsync(bodyPath, "# Overlay body");
        var resolver = BuildResolver(contentBase, overlay);
        var loader = new ContentKbSeedLoader(
            resolver,
            new FakeContentSiteIndexStore(),
            new FakeCreatorIdentityResolver(),
            new FakeCreatorSuppressionStore(),
            NullLogger<ContentKbSeedLoader>.Instance);

        var resolution = resolver.TryResolveExistingArtifact("content-kb/synthetic/entry.md", out var resolvedPath);
        var seedLoadCount = await loader.LoadIfPresentAsync();
        var seed = SeedIndexFileReader.Read(resolver.SeedFilePath);

        Assert.Equal(ContentKbArtifactResolution.Resolved, resolution);
        Assert.Equal(bodyPath, resolvedPath);
        Assert.Equal(0, seedLoadCount);
        Assert.False(seed.SeedAvailable);
    }

    [Fact]
    public async Task ColdStart_OverlayPresent_BackfillsNullBodyHash_AndMissingOverlayWritesNothing()
    {
        var contentBase = CreateTempDir();
        var overlay = CreateTempDir();
        var artifactPath = "content-kb/synthetic/entry.md";
        var bodyPath = Path.Combine(overlay, "content-kb", "synthetic", "entry.md");
        Directory.CreateDirectory(Path.GetDirectoryName(bodyPath)!);
        await File.WriteAllTextAsync(bodyPath, "# Overlay body");
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(CreateRow(1, artifactPath));
        store.Rows.Add(CreateRow(2, "content-kb/synthetic/missing.md"));
        var resolver = new ContentKbArtifactBodyResolver(BuildResolver(contentBase, overlay));
        var backfill = new ContentBodyHashBackfill(store, resolver, new FakeLogger<ContentBodyHashBackfill>());

        await backfill.RunAsync();

        Assert.Equal([1], store.BodySha256BackfilledIds);
        Assert.NotNull(store.Rows.Single(row => row.Id == 1).BodySha256);
        Assert.Null(store.Rows.Single(row => row.Id == 2).BodySha256);
    }

    private ContentKbArtifactPathResolver BuildResolver(string contentBase, string overlay)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MTG_DATA_DIR"] = overlay })
            .Build();
        var flags = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            [ContentKbFeatureFlagKeys.DirectPushGitBody] = false,
        });
        return new ContentKbArtifactPathResolver(
            new StubWebHostEnvironment(contentBase),
            configuration,
            flags,
            NullLogger<ContentKbArtifactPathResolver>.Instance);
    }

    private string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "private-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private static ContentSiteIndexRow CreateRow(long id, string artifactPath) => new()
    {
        Id = id,
        Source = "Synthetic",
        Title = "Synthetic entry",
        VideoUrl = "https://example.test/entry",
        ArtifactPath = artifactPath,
        IndexedUtc = DateTimeOffset.UtcNow,
        ArchetypeTags = [],
        BracketTags = [],
        CardCategoryTags = [],
    };

    public void Dispose()
    {
        foreach (var directory in _tempDirs)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class StubWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
