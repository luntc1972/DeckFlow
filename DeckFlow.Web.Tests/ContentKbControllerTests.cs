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
using Microsoft.Extensions.Logging;
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
    public async Task Detail_ReturnsNotFound_WhenRowVisibleButPending()
    {
        // Codex HIGH / D-04: a drifted visible-but-pending row must 404 at the by-id detail route,
        // not just be absent from the browse list.
        var (controller, store) = Build();
        store.Rows.Add(Row(20, artifactPath: "content-kb/edhrecast/pending.md", visible: true, approvalStatus: "pending"));

        var result = await controller.Detail(20);

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
    public async Task Detail_ReturnsNotFound_WhenFileMissingAndDirectPushGitBodyFlagOn()
    {
        // SYNC-07/D-01/D-11: under the flag, a missing /app body is a real 404, not the 200
        // "artifact unavailable" shell - a serving failure is an honest status (Codex LOW #6b).
        var (controller, store) = Build(new Dictionary<string, string?>(), out _, directPushGitBodyOn: true);
        store.Rows.Add(Row(40, artifactPath: "content-kb/edhrecast/missing-flagon.md", visible: true));

        var result = await controller.Detail(40);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Detail_ReturnsOk_ForPresentArtifact_WhenDirectPushGitBodyFlagOn()
    {
        // Happy-path render is unchanged by the flag; only the MissingFile branch is gated.
        var (controller, store) = Build(new Dictionary<string, string?>(), out var baseDir, directPushGitBodyOn: true);
        var rel = "content-kb/edhrecast/flagon-ok.md";
        WriteArtifact(baseDir, rel, "---\ntitle: Ok\n---\n# Body\n\nFlag-on present body.");
        store.Rows.Add(Row(41, artifactPath: rel, visible: true));

        var result = await controller.Detail(41);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbDetailViewModel>(view.Model);
        Assert.False(model.ArtifactUnavailable);
        Assert.Contains("Flag-on present body.", model.CleanBodyText);
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
    public async Task Detail_ServesBakedPromptSibling_WhenPresent()
    {
        var (controller, store) = Build(out var baseDir);
        var rel = "content-kb/edhrecast/baked.md";
        WriteArtifact(baseDir, rel, "---\ntitle: Ok\n---\n# Body\n\nNotes.");
        WriteArtifact(baseDir, "content-kb/edhrecast/baked.prompt.md", "BAKED-PROMPT-SENTINEL");
        store.Rows.Add(Row(7, artifactPath: rel, visible: true));

        var result = await controller.Detail(7);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbDetailViewModel>(view.Model);
        // The baked sibling is served verbatim as the copy payload; the page still renders the notes.
        Assert.Equal("BAKED-PROMPT-SENTINEL", model.CleanBodyText);
    }

    [Fact]
    public async Task Detail_ReconstructsPrompt_WhenNoSibling()
    {
        var (controller, store) = Build(out var baseDir);
        var rel = "content-kb/edhrecast/nosibling.md";
        WriteArtifact(baseDir, rel, "---\ntitle: Ok\n---\n## Summary\nReconstructed body.");
        store.Rows.Add(Row(8, artifactPath: rel, visible: true));

        var result = await controller.Detail(8);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbDetailViewModel>(view.Model);
        // No sibling → the framed prompt is reconstructed from the notes body.
        Assert.Contains("TASK:", model.CleanBodyText);
        Assert.Contains("Reconstructed body.", model.CleanBodyText);
    }

    [Fact]
    public async Task Detail_ServesBody_WhenPresentOnlyInOverlay()
    {
        var overlayRoot = Path.Combine(Path.GetTempPath(), "kbctl-overlay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(overlayRoot, "content-kb"));
        _tempDirs.Add(overlayRoot);

        var (controller, store) = Build(new Dictionary<string, string?> { ["MTG_DATA_DIR"] = overlayRoot });
        var rel = "content-kb/edhrecast/overlay.md";
        WriteArtifact(overlayRoot, rel, "# Overlay only\n\nVisible body.");
        store.Rows.Add(Row(6, artifactPath: rel, visible: true));

        var result = await controller.Detail(6);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbDetailViewModel>(view.Model);
        Assert.False(model.ArtifactUnavailable);
        Assert.Contains("Visible body.", model.CleanBodyText);
    }

    [Fact]
    public async Task Detail_MatchingBodyHash_RendersWithoutWarning()
    {
        var (controller, store, logger) = BuildWithLogger(out var baseDir);
        var rel = "content-kb/edhrecast/matching.md";
        const string raw = "---\ntitle: Ok\n---\n# Body\n\nMatching hash body.";
        WriteArtifact(baseDir, rel, raw);
        var expectedHash = DeckFlow.Core.Content.ContentSiteIndexContentSignature.ComputeBodySha256(raw);
        store.Rows.Add(Row(30, artifactPath: rel, visible: true, bodySha256: expectedHash));

        var result = await controller.Detail(30);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbDetailViewModel>(view.Model);
        Assert.False(model.ArtifactUnavailable);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Detail_MismatchedBodyHash_StillRendersAndLogsWarning()
    {
        var (controller, store, logger) = BuildWithLogger(out var baseDir);
        var rel = "content-kb/edhrecast/mismatch.md";
        WriteArtifact(baseDir, rel, "---\ntitle: Ok\n---\n# Body\n\nCurrent on-disk body.");
        store.Rows.Add(Row(31, artifactPath: rel, visible: true, bodySha256: "0000000000000000000000000000000000000000000000000000000000ff"));

        var result = await controller.Detail(31);

        // Fail-open (D-05): the mismatch still renders the body, it does not 404/blank it.
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbDetailViewModel>(view.Model);
        Assert.False(model.ArtifactUnavailable);
        Assert.Contains("Current on-disk body.", model.CleanBodyText);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("body hash mismatch", warning.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("31", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_NullStoredBodyHash_StillRendersAndLogsWarningWithNoneSentinel()
    {
        var (controller, store, logger) = BuildWithLogger(out var baseDir);
        var rel = "content-kb/edhrecast/nullhash.md";
        WriteArtifact(baseDir, rel, "---\ntitle: Ok\n---\n# Body\n\nLegacy pre-backfill body.");
        store.Rows.Add(Row(32, artifactPath: rel, visible: true, bodySha256: null));

        var result = await controller.Detail(32);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ContentKbDetailViewModel>(view.Model);
        Assert.False(model.ArtifactUnavailable);
        Assert.Contains("Legacy pre-backfill body.", model.CleanBodyText);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("(none)", warning.Message, StringComparison.Ordinal);
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

    private (ContentKbController Controller, FakeContentSiteIndexStore Store, FakeLogger<ContentKbController> Logger) BuildWithLogger(
        out string baseDir,
        bool directPushGitBodyOn = false)
    {
        baseDir = Path.Combine(Path.GetTempPath(), "kbctl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(baseDir, "content-kb"));
        _tempDirs.Add(baseDir);

        var config = new Dictionary<string, string?> { ["ContentKb:ContentBase"] = baseDir };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        // Why: sync.directpush-gitbody defaults OFF here (D-05) - these controller tests exercise
        // today's byte-identical git-then-overlay serving, not the flag-ON git-only path, unless
        // a test explicitly opts into flag-ON via directPushGitBodyOn. Both the resolver and the
        // controller share ONE flag cache instance so they observe the same flag state.
        var flagCache = new FakeFeatureFlagCache(
            new Dictionary<string, bool> { ["sync.directpush-gitbody"] = directPushGitBodyOn });
        var resolver = new ContentKbArtifactPathResolver(
            new StubWebHostEnvironment(baseDir),
            configuration,
            flagCache,
            NullLogger<ContentKbArtifactPathResolver>.Instance);
        var store = new FakeContentSiteIndexStore();
        var logger = new FakeLogger<ContentKbController>();
        var controller = new ContentKbController(store, resolver, flagCache, logger);
        return (controller, store, logger);
    }

    private (ContentKbController Controller, FakeContentSiteIndexStore Store) Build(out string baseDir)
        => Build(new Dictionary<string, string?>(), out baseDir);

    private (ContentKbController Controller, FakeContentSiteIndexStore Store) Build(Dictionary<string, string?> config)
        => Build(config, out _);

    private (ContentKbController Controller, FakeContentSiteIndexStore Store) Build(
        Dictionary<string, string?> config,
        out string baseDir,
        bool directPushGitBodyOn = false)
    {
        baseDir = Path.Combine(Path.GetTempPath(), "kbctl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(baseDir, "content-kb"));
        _tempDirs.Add(baseDir);

        config["ContentKb:ContentBase"] = baseDir;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
        // Why: sync.directpush-gitbody defaults OFF here (D-05) - see comment on the sibling
        // BuildWithLogger overload above; both resolver and controller share one flag cache.
        var flagCache = new FakeFeatureFlagCache(
            new Dictionary<string, bool> { ["sync.directpush-gitbody"] = directPushGitBodyOn });
        var resolver = new ContentKbArtifactPathResolver(
            new StubWebHostEnvironment(baseDir),
            configuration,
            flagCache,
            NullLogger<ContentKbArtifactPathResolver>.Instance);
        var store = new FakeContentSiteIndexStore();
        var controller = new ContentKbController(store, resolver, flagCache, NullLogger<ContentKbController>.Instance);
        return (controller, store);
    }

    private static void WriteArtifact(string baseDir, string relativePath, string content)
    {
        var full = Path.Combine(baseDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static ContentSiteIndexRow Row(
        long id,
        string artifactPath,
        bool visible,
        string approvalStatus = "approved",
        string? bodySha256 = null)
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
            ApprovalStatus = approvalStatus,
            BodySha256 = bodySha256,
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
