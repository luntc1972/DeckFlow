using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Pages;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

/// <summary>bUnit tests for the private-root Publish action.</summary>
public sealed class PublishPageTests : BunitContext
{
    private IRenderedComponent<Publish> RenderPublish(string? root)
    {
        var store = new FakeContentSiteIndexStore();
        var suppressionStore = new FakeCreatorSuppressionStore();
        Services.AddSingleton<IContentKbOrchestrator>(new FakeContentKbOrchestrator());
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

    [Fact]
    public void ExportToPrivateRoot_UnsetRoot_ShowsNamedOperatorError()
    {
        var cut = RenderPublish(null);

        cut.Find("button").Click();

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
}
