using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentKbDeployedBodyController"/>: the D-09 (REVISED) deploy-confirm
/// surface. BasicAuth enforcement itself is NOT unit-testable here - it is applied by the
/// `/Admin`-prefix <c>UseWhen</c> branch in Program.cs (integration-level middleware), which this
/// attribute-routed controller inherits automatically with no controller-level auth attribute.
/// These tests cover the action's own read-only, is_visible-independent, git-/app-only contract.
/// </summary>
public sealed class ContentKbDeployedBodyControllerTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    [Fact]
    public async Task GetDeployedBodyHash_ArtifactPresent_Returns200WithComputedHash()
    {
        var contentRoot = CreateTempWithContentKb();
        var raw = "---\ntitle: t\n---\n# Body\n";
        WriteFile(Path.Combine(contentRoot, "content-kb", "edhrecast", "present.md"), raw);
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, youtubeVideoId: "present", artifactPath: "content-kb/edhrecast/present.md", visible: true));
        var controller = Build(store, contentRoot);

        var result = await controller.GetDeployedBodyHash(ContentSourceType.Youtube, "present", default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var expectedHash = ContentSiteIndexContentSignature.ComputeBodySha256(raw);
        Assert.Equal(expectedHash, GetBodySha256(ok.Value));
    }

    [Fact]
    public async Task GetDeployedBodyHash_ArtifactMissing_Returns404()
    {
        var contentRoot = CreateTempWithContentKb();
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, youtubeVideoId: "missing", artifactPath: "content-kb/edhrecast/missing.md", visible: true));
        var controller = Build(store, contentRoot);

        var result = await controller.GetDeployedBodyHash(ContentSourceType.Youtube, "missing", default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetDeployedBodyHash_HiddenRowWithPresentArtifact_StillReturns200WithHash()
    {
        // D-09 REVISED: is_visible-independent - a not-yet-visible DirectPush'd row must still confirm.
        var contentRoot = CreateTempWithContentKb();
        var raw = "# Hidden but present";
        WriteFile(Path.Combine(contentRoot, "content-kb", "edhrecast", "hidden.md"), raw);
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Row(1, youtubeVideoId: "hidden", artifactPath: "content-kb/edhrecast/hidden.md", visible: false));
        var controller = Build(store, contentRoot);

        var result = await controller.GetDeployedBodyHash(ContentSourceType.Youtube, "hidden", default);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(ContentSiteIndexContentSignature.ComputeBodySha256(raw), GetBodySha256(ok.Value));
    }

    [Fact]
    public async Task GetDeployedBodyHash_UnknownNaturalKey_Returns404()
    {
        var contentRoot = CreateTempWithContentKb();
        var store = new FakeContentSiteIndexStore();
        var controller = Build(store, contentRoot);

        var result = await controller.GetDeployedBodyHash(ContentSourceType.Youtube, "no-such-key", default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(null, "value")]
    [InlineData("", "value")]
    [InlineData(" ", "value")]
    [InlineData(ContentSourceType.Youtube, null)]
    [InlineData(ContentSourceType.Youtube, "")]
    [InlineData(ContentSourceType.Youtube, " ")]
    public async Task GetDeployedBodyHash_BlankParam_Returns400(string? naturalKeyType, string? naturalKeyValue)
    {
        var contentRoot = CreateTempWithContentKb();
        var store = new FakeContentSiteIndexStore();
        var controller = Build(store, contentRoot);

        var result = await controller.GetDeployedBodyHash(naturalKeyType, naturalKeyValue, default);

        Assert.IsType<BadRequestResult>(result);
    }

    private static string? GetBodySha256(object? value)
        => value?.GetType().GetProperty("bodySha256")?.GetValue(value) as string;

    private static ContentSiteIndexRow Row(
        long id,
        string youtubeVideoId,
        string artifactPath,
        bool visible)
        => new()
        {
            Id = id,
            Source = "EDHRECast",
            Title = "Title " + id,
            VideoUrl = "https://youtu.be/" + youtubeVideoId,
            ArtifactPath = artifactPath,
            IndexedUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = youtubeVideoId,
            IsVisible = visible,
        };

    private ContentKbDeployedBodyController Build(FakeContentSiteIndexStore store, string contentRoot)
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = new StubWebHostEnvironment(contentRoot);
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool>());
        var resolver = new ContentKbArtifactPathResolver(
            environment,
            configuration,
            flagCache,
            NullLogger<ContentKbArtifactPathResolver>.Instance);
        return new ContentKbDeployedBodyController(store, resolver, NullLogger<ContentKbDeployedBodyController>.Instance);
    }

    private string CreateTempWithContentKb()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kbdbh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "content-kb"));
        _tempDirs.Add(dir);
        return dir;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
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
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
