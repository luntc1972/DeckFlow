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

    [Fact]
    public async Task ContentKbIndex_EntryStatusPublishStateAndTagsRenderAsTextBadges()
    {
        var entries = new[]
        {
            new KbEntryRow { Id = 1, Title = "Visible", Source = "Example Creator", IsVisible = true, IndexedUtc = DateTimeOffset.UtcNow, PublishState = PublishState.Published },
            new KbEntryRow { Id = 2, Title = "Hidden", Source = "Example Creator", IsVisible = false, IsHidden = true, IndexedUtc = DateTimeOffset.UtcNow, PublishState = PublishState.PushedHidden },
            new KbEntryRow { Id = 3, Title = "Never", Source = "Based Deck Department", IsVisible = false, IndexedUtc = DateTimeOffset.UtcNow, PublishState = PublishState.NeverPublished },
            new KbEntryRow { Id = 4, Title = "Local", Source = "Based Deck Department", IsVisible = false, IndexedUtc = DateTimeOffset.UtcNow, PublishState = PublishState.LocalNewer, Tags = new[] { "combo", "ramp" } },
        };
        var document = await DocumentAsync(Model(entries: entries));

        Assert.Equal(new[] { "Published", "Hidden", "Unpublished", "Unpublished" }, document.QuerySelectorAll("td[data-label='Status'] span.admin-badge").Select(x => x.TextContent.Trim()));
        Assert.NotNull(document.QuerySelector("td[data-label='Status'] .admin-badge--success[aria-label='Published']"));
        Assert.NotNull(document.QuerySelector("td[data-label='Status'] .admin-badge--neutral[aria-label='Hidden']"));
        Assert.Equal(2, document.QuerySelectorAll("td[data-label='Status'] .admin-badge--warning[aria-label='Unpublished']").Length);
        Assert.Equal(new[] { "Published", "Pushed-hidden", "Never published", "Local-newer" }, document.QuerySelectorAll("td[data-label='Publish State'] span.admin-badge").Select(x => x.TextContent.Trim()));
        Assert.NotNull(document.QuerySelector("td[data-label='Publish State'] .admin-badge--success"));
        Assert.NotNull(document.QuerySelector("td[data-label='Publish State'] .admin-badge--neutral"));
        Assert.NotNull(document.QuerySelector("td[data-label='Publish State'] .admin-badge--warning"));
        Assert.NotNull(document.QuerySelector("td[data-label='Publish State'] .admin-badge--info"));
        Assert.Equal(new[] { "combo", "ramp" }, document.QuerySelectorAll("span.admin-badge.admin-badge--neutral").Where(x => x.TextContent.Trim() is "combo" or "ramp").Select(x => x.TextContent.Trim()));
        Assert.Empty(document.QuerySelectorAll(".kb-status, .kb-tag"));
    }

    [Fact]
    public async Task ContentKbIndex_EntryActionsUseSharedButtonVariants()
    {
        var document = await DocumentAsync(Model());
        var table = document.QuerySelector("table#kb-entries-table")!;
        Assert.All(table.QuerySelectorAll("button").Where(x => x.TextContent.Trim() is "Publish" or "Unpublish" or "Evergreen: On" or "Evergreen: Off"), x => Assert.True(x.ClassList.Contains("admin-button") && x.ClassList.Contains("admin-button--secondary")));
        Assert.All(table.QuerySelectorAll("button").Where(x => x.TextContent.Trim() is "Hide" or "Delete"), x => Assert.True(x.ClassList.Contains("admin-button") && x.ClassList.Contains("admin-button--danger") && x.Closest("form[data-admin-confirm-twoclick]") is not null));
        Assert.NotNull(table.QuerySelector("button[data-confirm-label='Confirm hide'][aria-label=\"Hide 'Visible'\"]"));
        Assert.NotNull(table.QuerySelector("button[data-confirm-label='Confirm delete'][aria-label=\"Delete 'Visible' permanently\"]"));
        Assert.All(table.QuerySelectorAll("form.admin-action-form"), x =>
        {
            Assert.NotNull(x.QuerySelector("input[name='__RequestVerificationToken']"));
            Assert.NotNull(x.QuerySelector("input[name='entryId']"));
            Assert.NotNull(x.QuerySelector("input[name='visibilityFilter']"));
        });
        Assert.Empty(table.QuerySelectorAll("button.danger"));
    }

    [Fact]
    public async Task ContentKbIndex_EncodesEntryTitleSourceAndTags()
    {
        var entry = new KbEntryRow { Id = 1, Title = "<b>t</b>", Source = "<i>s</i>", IsVisible = false, IndexedUtc = DateTimeOffset.UtcNow, PublishState = PublishState.NeverPublished, Tags = new[] { "\"><u>x" } };
        var document = await DocumentAsync(Model(entries: new[] { entry }));
        Assert.Empty(document.QuerySelectorAll("b, i, u"));
        Assert.Equal("<b>t</b>", document.QuerySelector(".admin-kb-title")!.TextContent);
        Assert.Equal("<i>s</i>", document.QuerySelector(".admin-kb-source")!.TextContent);
        Assert.Equal("\"><u>x", document.QuerySelector("td[data-label='Tags'] span")!.TextContent);
    }

    [Fact]
    public async Task ContentKbIndex_StatusAndSourcesAreSharedCards()
    {
        var document = await DocumentAsync(Model());
        var status = document.QuerySelector("section.admin-card[aria-labelledby='kb-status-heading']");
        Assert.NotNull(status);
        Assert.Equal("Index Status", status.QuerySelector("h2#kb-status-heading.admin-card__title")!.TextContent.Trim());
        Assert.Contains("Index generated:", status.QuerySelector("p.admin-meta")!.TextContent);
        Assert.NotNull(status.QuerySelector("div.admin-card__actions form.admin-action-form button.admin-button--secondary"));
        var reload = status.QuerySelector("form[data-admin-confirm-reload]");
        Assert.NotNull(reload);
        Assert.Equal("Reload Index from Seed", reload.QuerySelector("button.admin-button--secondary")!.TextContent.Trim());
        Assert.NotNull(reload.QuerySelector("input[name='visibilityFilter']"));

        var sources = document.QuerySelector("section.admin-card[aria-labelledby='kb-bulk-heading']");
        Assert.NotNull(sources);
        Assert.Equal("Sources", sources.QuerySelector("h2#kb-bulk-heading.admin-card__title")!.TextContent.Trim());
        Assert.All(sources.QuerySelectorAll("button").Where(x => x.TextContent.Trim() == "Publish All"), x => Assert.True(x.ClassList.Contains("admin-button--secondary")));
        Assert.All(sources.QuerySelectorAll("button").Where(x => x.TextContent.Trim() == "Hide All"), x => Assert.True(x.ClassList.Contains("admin-button--danger") && x.Closest("form[data-admin-confirm-twoclick]") is not null && x.GetAttribute("data-confirm-label") == "Confirm Hide All"));
        Assert.Empty(document.QuerySelectorAll(".admin-harvest__panel, .admin-kb-status__actions, button.danger"));

        var stack = Assert.Single(document.QuerySelectorAll("div.admin-stack"));
        Assert.Equal(new[] { "kb-status-heading", "kb-bulk-heading", "kb-entries-heading" }, stack.Children.Select(x => x.GetAttribute("aria-labelledby")));
        Assert.Null(document.QuerySelector("p[data-admin-toast]")?.ParentElement?.Closest(".admin-stack"));
        Assert.Null(document.QuerySelector("dialog#admin-confirm-modal")!.Closest(".admin-stack"));
    }

    [Fact]
    public async Task ContentKbIndex_SuccessToastAndConfirmModalStay()
    {
        var document = await DocumentAsync(Model(successBanner: "Published 1 entry."));
        var toast = Assert.Single(document.QuerySelectorAll("p.admin-banner.admin-banner--success[role='status'][data-admin-toast]"));
        Assert.Equal("Published 1 entry.", toast.TextContent);
        var dialog = Assert.Single(document.QuerySelectorAll("dialog#admin-confirm-modal"));
        Assert.Single(dialog.QuerySelectorAll("[data-admin-modal-cancel]"));
        Assert.Single(dialog.QuerySelectorAll("[data-admin-modal-confirm]"));
    }

    private static async Task<AngleSharp.Html.Dom.IHtmlDocument> DocumentAsync(AdminContentKbViewModel model) =>
        new HtmlParser().ParseDocument(await RazorViewRenderer.RenderAsync(model, typeof(AdminContentKbController), "AdminContentKb", new TestRouter()));

    private static AdminContentKbViewModel Model(string visibilityFilter = "all", IReadOnlyList<KbEntryRow>? entries = null, int totalCount = 3, string? successBanner = null) => new()
    {
        VisibilityFilter = visibilityFilter,
        SuccessBanner = successBanner,
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
