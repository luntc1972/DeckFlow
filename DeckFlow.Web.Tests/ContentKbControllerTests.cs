using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentKbController"/> — focused on the security-relevant Detail
/// guards (T-22-08 hidden-entry 404, T-22-09 content-kb/ subtree confinement / D-22F) and the
/// published-only browse projection.
/// </summary>
public sealed class ContentKbControllerTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    [Fact]
    public async Task Detail_ReturnsNotFound_WhenRowMissing()
    {
        var (controller, _) = Build();

        var result = await controller.Detail(999_999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Detail_ReturnsNotFound_WhenRowHidden()
    {
        var (controller, store) = Build();
        store.Rows.Add(Row(1, artifactPath: "content-kb/edhrecast/a.md", visible: false));

        var result = await controller.Detail(1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Detail_ReturnsNotFound_WhenArtifactPathNotUnderContentKbPrefix()
    {
        var (controller, store) = Build();
        // A visible row whose artifact_path escapes the content-kb/ prefix (e.g. a config file).
        store.Rows.Add(Row(2, artifactPath: "appsettings.json", visible: true));

        var result = await controller.Detail(2);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Detail_ReturnsNotFound_WhenResolvedPathEscapesSubtree()
    {
        var (controller, store) = Build();
        // Starts with content-kb/ (passes the prefix key) but resolves OUTSIDE {base}/content-kb.
        store.Rows.Add(Row(3, artifactPath: "content-kb/../escape.md", visible: true));

        var result = await controller.Detail(3);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Detail_ReturnsUnavailableView_WhenFileMissing()
    {
        var (controller, store) = Build();
        store.Rows.Add(Row(4, artifactPath: "content-kb/edhrecast/missing.md", visible: true));

        var result = await controller.Detail(4);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbDetailViewModel>(view.Model);
        Assert.True(model.ArtifactUnavailable);
    }

    [Fact]
    public async Task Detail_RendersBodyWithoutFrontmatter_OnHappyPath()
    {
        var (controller, store) = Build(out var baseDir);
        var rel = "content-kb/edhrecast/ok.md";
        WriteArtifact(baseDir, rel, "---\ntitle: Ok\n---\n# Body\n\nPaste me.");
        store.Rows.Add(Row(5, artifactPath: rel, visible: true));

        var result = await controller.Detail(5);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbDetailViewModel>(view.Model);
        Assert.False(model.ArtifactUnavailable);
        Assert.DoesNotContain("---", model.CleanBodyText);
        Assert.DoesNotContain("title:", model.CleanBodyText);
        Assert.Contains("Paste me.", model.CleanBodyText);
    }

    [Fact]
    public async Task Index_ProjectsPublishedRowsOnly()
    {
        var (controller, store) = Build();
        store.Rows.Add(Row(10, artifactPath: "content-kb/a/1.md", visible: true));
        store.Rows.Add(Row(11, artifactPath: "content-kb/a/2.md", visible: false));

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbBrowseViewModel>(view.Model);
        Assert.Single(model.Entries);
        Assert.Equal(10, model.Entries[0].Id);
    }

    private (ContentKbController Controller, FakeContentSiteIndexStore Store) Build()
        => Build(out _);

    private (ContentKbController Controller, FakeContentSiteIndexStore Store) Build(out string baseDir)
    {
        baseDir = Path.Combine(Path.GetTempPath(), "kbctl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(baseDir, "content-kb"));
        _tempDirs.Add(baseDir);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ContentKb:ContentBase"] = baseDir })
            .Build();
        var resolver = new ContentKbArtifactPathResolver(
            new StubWebHostEnvironment(baseDir),
            configuration,
            NullLogger<ContentKbArtifactPathResolver>.Instance);
        var store = new FakeContentSiteIndexStore();
        var controller = new ContentKbController(store, resolver, NullLogger<ContentKbController>.Instance);
        return (controller, store);
    }

    private static void WriteArtifact(string baseDir, string relativePath, string content)
    {
        var full = Path.Combine(baseDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static ContentSiteIndexRow Row(long id, string artifactPath, bool visible)
        => new()
        {
            Id = id,
            Source = "EDHRECast",
            Title = "Title " + id,
            VideoUrl = "https://youtu.be/x" + id,
            ArtifactPath = artifactPath,
            IndexedUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ArchetypeTags = new[] { "ramp" },
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = "x" + id,
            IsVisible = visible,
        };

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
