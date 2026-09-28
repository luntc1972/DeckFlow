using AngleSharp.Html.Parser;
using DeckFlow.Core.Content;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Pins D-06 and D-07 shared Content KB entries markup.</summary>
public sealed class AdminContentKbViewRenderTests
{
    [Fact]
    public async Task ContentKbIndex_VisibilityChipsAreSharedFilterChipLinks()
    {
        var document = await DocumentAsync(Model(visibilityFilter: "unpublished"));
        var chips = document.QuerySelectorAll("nav.admin-filter-chips[data-kb-visibility][aria-label='Visibility filter'] a.admin-filter-chips__chip");
        Assert.Equal(new[] { "All", "Published", "Unpublished", "Hidden" }, chips.Select(x => x.TextContent.Trim()));
        Assert.Equal(new[] { "all", "published", "unpublished", "hidden" }, chips.Select(x => x.GetAttribute("href")!.Split("visibilityFilter=")[1]));
        Assert.Single(chips, x => x.ClassList.Contains("is-active"));
        Assert.Equal("true", chips.Single(x => x.ClassList.Contains("is-active")).GetAttribute("aria-current"));
        Assert.Empty(document.QuerySelectorAll("[role='tablist'], .admin-kb-toggle"));
    }

    [Fact]
    public async Task ContentKbIndex_EntriesFilterUsesSharedFilterComponents()
    {
        var document = await DocumentAsync(Model());
        Assert.NotNull(document.QuerySelector("section.admin-card[aria-labelledby='kb-entries-heading'] .admin-section-header h2#kb-entries-heading.admin-card__title"));
        Assert.Equal("Filter entries", document.QuerySelector("div.admin-filter .admin-field.admin-filter-search label[for='kb-filter-search']")!.TextContent.Trim());
        Assert.NotNull(document.QuerySelector("input#kb-filter-search[aria-controls='kb-entries-table']"));
        Assert.Equal("Creator", document.QuerySelector("div.admin-filter .admin-field label[for='kb-creator-filter']")!.TextContent.Trim());
        Assert.Equal(new[] { "All creators", "Example Creator", "Based Deck Department" }, document.QuerySelectorAll("#kb-creator-filter option").Select(x => x.TextContent.Trim()));
        Assert.NotNull(document.QuerySelector("#kb-filter-count.admin-filter__count[role='status'][aria-live='polite']"));
        Assert.NotNull(document.QuerySelector("tr#kb-filter-empty.admin-filter__empty-row.hidden td[colspan='6']"));
        Assert.NotNull(document.QuerySelector(".admin-table-scroll[tabindex='0'] table#kb-entries-table"));
        Assert.Empty(document.QuerySelectorAll(".kb-filter, .kb-filter__count, .kb-filter__empty-row"));
        var chips = document.QuerySelector("[data-kb-visibility]")!;
        var filter = document.QuerySelector("#kb-filter-search")!;
        var table = document.QuerySelector("#kb-entries-table")!;
        var elements = document.QuerySelectorAll("*").ToList();
        Assert.True(elements.IndexOf(chips) < elements.IndexOf(filter));
        Assert.True(elements.IndexOf(filter) < elements.IndexOf(table));
    }

    [Theory]
    [InlineData(0, "No entries loaded. Use 'Reload Index from Seed' to populate the index.")]
    [InlineData(5, "No entries match the current filter.")]
    public async Task ContentKbIndex_NoEntries_RendersSharedEmptyState(int totalCount, string expected)
    {
        var document = await DocumentAsync(Model(entries: Array.Empty<KbEntryRow>(), totalCount: totalCount));
        Assert.Equal(expected, document.QuerySelector("p.admin-empty")!.TextContent.Trim());
        Assert.Null(document.QuerySelector("#kb-filter-search, .admin-kb-empty"));
    }

    private static async Task<AngleSharp.Html.Dom.IHtmlDocument> DocumentAsync(AdminContentKbViewModel model) =>
        new HtmlParser().ParseDocument(await RazorViewRenderer.RenderAsync(model, typeof(AdminContentKbController), "AdminContentKb", new TestRouter()));

    private static AdminContentKbViewModel Model(string visibilityFilter = "all", IReadOnlyList<KbEntryRow>? entries = null, int totalCount = 3) => new()
    {
        VisibilityFilter = visibilityFilter,
        Status = new KbIndexStatus { TotalCount = totalCount, PublishedCount = 1, UnpublishedCount = 1, HiddenCount = 1, SourceCount = 2, FlagEnabled = true, IndexGeneratedUtc = DateTimeOffset.UtcNow },
        Sources = new[] { new KbSourceGroup("Example Creator", 2), new KbSourceGroup("Based Deck Department", 1) },
        Entries = entries ?? new[]
        {
            new KbEntryRow { Id = 1, Title = "Visible", Source = "Example Creator", IsVisible = true, IndexedUtc = DateTimeOffset.UtcNow, PublishState = PublishState.Published },
            new KbEntryRow { Id = 2, Title = "Hidden", Source = "Example Creator", IsVisible = false, IsHidden = true, IndexedUtc = DateTimeOffset.UtcNow, PublishState = PublishState.PushedHidden },
            new KbEntryRow { Id = 3, Title = "Unpublished", Source = "Based Deck Department", IsVisible = false, IndexedUtc = DateTimeOffset.UtcNow, PublishState = PublishState.LocalNewer, Tags = new[] { "combo", "ramp" } },
        },
    };

    private sealed class TestRouter : IRouter
    {
        public Task RouteAsync(RouteContext context) => Task.CompletedTask;

        public VirtualPathData? GetVirtualPath(VirtualPathContext context)
        {
            var path = $"Admin/ContentKb/{context.Values["action"]}";
            return context.Values.TryGetValue("visibilityFilter", out var value)
                ? new VirtualPathData(this, $"{path}?visibilityFilter={value}")
                : new VirtualPathData(this, path);
        }
    }
}
