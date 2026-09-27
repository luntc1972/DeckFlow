using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Pages;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Tests;

/// <summary>bUnit tests for the private-root Publish action.</summary>
public sealed class PublishPageTests : BunitContext
{
    private IRenderedComponent<Publish> RenderPublish(
        string? root,
        IReadOnlyList<ContentSiteIndexRow>? rows = null,
        FakeContentKbOrchestrator? orchestrator = null,
        CapturingLoggerProvider? loggerProvider = null,
        IReadOnlyList<string>? suppressedCreators = null)
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.AddRange(rows ?? Array.Empty<ContentSiteIndexRow>());
        var suppressionStore = new FakeCreatorSuppressionStore();
        foreach (var creator in suppressedCreators ?? Array.Empty<string>())
        {
            suppressionStore.Suppressed.Add(creator);
        }
        Services.AddLogging(logging =>
        {
            if (loggerProvider is not null)
            {
                logging.AddProvider(loggerProvider);
            }
        });
        Services.AddSingleton<IContentKbOrchestrator>(orchestrator ?? new FakeContentKbOrchestrator());
        Services.AddSingleton<IContentSiteIndexStore>(store);
        Services.AddSingleton<ICreatorSuppressionStore>(suppressionStore);
        Services.AddSingleton<ICreatorIdentityResolver>(new FakeCreatorIdentityResolver());
        Services.AddSingleton(new ContentKbOrchestratorOptions { ArtifactRoot = Path.Combine(Path.GetTempPath(), "content-kb") });
        Services.AddSingleton<PublishStateDeriver>();
        Services.AddSingleton<CreatorSuppressionRowFilter>();
        Services.AddSingleton<IPrivateKbRootProvider>(new StudioPrivateKbRootProvider(null, root));
        Services.AddScoped<PublishCoordinator>();
        return Render<Publish>();
    }

    private static ContentSiteIndexRow MakeApprovedRow(long id, string videoId, bool pushed = false, bool visible = false)
        => new()
        {
            Id = id,
            Source = "test-channel",
            Title = $"Video {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = DateTimeOffset.UtcNow,
            ApprovalStatus = "approved",
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
            PushedToProdUtc = pushed ? DateTimeOffset.UtcNow : null,
            IsVisible = visible,
        };

    [Fact]
    public void ExportToPrivateRoot_UnsetRoot_ShowsNamedOperatorError()
    {
        var cut = RenderPublish(null, new[] { MakeApprovedRow(1, "first") });

        cut.WaitForAssertion(() => Assert.False(cut.Find("button.btn-primary").HasAttribute("disabled")));
        cut.Find("button.btn-primary").Click();

        cut.WaitForAssertion(() => Assert.Contains("DECKFLOW_KB_ROOT", cut.Markup));
    }

    [Fact]
    public void PublishPage_RendersOnlyPrivateRootExportAction()
    {
        var cut = RenderPublish(null);

        Assert.Contains("Export to private KB root", cut.Markup);
        Assert.DoesNotContain("Commit", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("branch", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublishPage_ZeroApprovedRows_DisablesExport()
    {
        var cut = RenderPublish(null);

        cut.WaitForAssertion(() => Assert.True(cut.Find("button.btn-primary").HasAttribute("disabled")));
    }

    [Fact]
    public void PublishPage_ApprovedRows_RendersCountAndPublishStateSummary()
    {
        var rows = new[]
        {
            MakeApprovedRow(1, "first"),
            MakeApprovedRow(2, "second", pushed: true, visible: true),
        };
        var cut = RenderPublish(null, rows);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("<strong>2</strong>", cut.Markup);
            Assert.Contains("entries", cut.Markup);
            Assert.Contains("approved and ready to export", cut.Markup);
            Assert.Contains("Never published", cut.Markup);
            Assert.Contains("Published", cut.Markup);
        });
    }

    [Fact]
    public void PublishPage_SuppressedCreator_ExcludesItFromApprovedCount()
    {
        var rows = new[]
        {
            MakeApprovedRow(1, "blocked") with { Source = "blocked-creator" },
            MakeApprovedRow(2, "control") with { Source = "allowed-creator" },
        };
        var cut = RenderPublish(null, rows, suppressedCreators: new[] { "blocked-creator" });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("<strong>1</strong>", cut.Markup);
            Assert.Contains("entry", cut.Markup);
            Assert.Contains("approved and ready to export", cut.Markup);
        });
    }

    [Fact]
    public void ExportToPrivateRoot_UnexpectedException_ShowsSanitizedErrorAndLogsException()
    {
        var root = Path.Combine(Path.GetTempPath(), "deckflow-private-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var loggerProvider = new CapturingLoggerProvider();
            var exception = new InvalidOperationException("connection string password=hunter2");
            var cut = RenderPublish(root, new[] { MakeApprovedRow(1, "first") }, new FakeContentKbOrchestrator { ThrowOnExport = exception }, loggerProvider);

            cut.WaitForAssertion(() => Assert.False(cut.Find("button.btn-primary").HasAttribute("disabled")));
            cut.Find("button.btn-primary").Click();

            cut.WaitForAssertion(() =>
            {
                Assert.Contains("Export failed — check the Studio logs and retry.", cut.Markup);
                Assert.DoesNotContain("password=hunter2", cut.Markup);
                Assert.Contains(loggerProvider.Logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception == exception);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
