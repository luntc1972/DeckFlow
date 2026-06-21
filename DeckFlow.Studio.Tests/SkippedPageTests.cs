using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Studio.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit behavioral tests for Skipped.razor (HSEL-03: list skipped videos + un-skip + empty state).
/// </summary>
public sealed class SkippedPageTests : BunitContext
{
    private IRenderedComponent<Skipped> RenderPage(FakeSkippedVideoStore store)
    {
        Services.AddSingleton<ISkippedVideoStore>(store);
        return Render<Skipped>();
    }

    [Fact]
    public void Skipped_NoSkipped_ShowsEmptyState()
    {
        var cut = RenderPage(new FakeSkippedVideoStore());

        cut.WaitForAssertion(() => Assert.Contains("No skipped videos", cut.Markup));
    }

    [Fact]
    public void Skipped_WithSkipped_RendersRows()
    {
        var store = new FakeSkippedVideoStore();
        store.Seed("vid-1");

        var cut = RenderPage(store);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("vid-1", cut.Markup);
            Assert.Contains("Un-skip", cut.Markup);
        });
    }

    [Fact]
    public void Skipped_Unskip_RemovesRowAndCallsStore()
    {
        var store = new FakeSkippedVideoStore();
        store.Seed("vid-1");
        var cut = RenderPage(store);

        cut.WaitForAssertion(() => Assert.Contains("vid-1", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button[aria-label='Un-skip vid-1']").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("vid-1", store.RemoveCalls);
            Assert.Contains("No skipped videos", cut.Markup);
        });
    }
}
