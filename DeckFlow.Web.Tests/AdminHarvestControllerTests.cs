using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="AdminHarvestController"/> covering harvested-commander paging, same-origin guards, and render paths.
/// </summary>
[Collection("ArchidektThrottleSerial")]
public sealed class AdminHarvestControllerTests
{
    [Fact]
    public async Task Commanders_ClampsHugePageToDeckTotalPages()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 999999);

        var view = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsType<CommandersGridViewModel>(view.Model);
        Assert.Equal(model.DeckTotalPages, model.DeckPage);
        Assert.Equal(1, model.DeckPage);
    }

    [Fact]
    public async Task Commanders_ClampsZeroPageToOne()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 0);

        var view = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsType<CommandersGridViewModel>(view.Model);
        Assert.Equal(1, model.DeckPage);
    }

    [Fact]
    public async Task Commanders_PassesClampedPageToPagedCommanderStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 125);
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 999999);

        var view = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsType<CommandersGridViewModel>(view.Model);
        Assert.Equal(model.DeckTotalPages, store.LastPagedCommanderPage);
        Assert.Equal(AdminHarvestViewModel.DefaultDeckPageSize, store.LastPagedCommanderPageSize);
        Assert.NotEqual(999999, store.LastPagedCommanderPage);
    }

    [Fact]
    public async Task Commanders_Search_PassesNormalizedSearchTermToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        await controller.Commanders(page: 1, search: "tef");

        Assert.Equal("tef", store.LastCommanderGridQuery?.SearchTerm);
    }

    [Fact]
    public async Task Commanders_Search_BlankTermPassesNullToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        await controller.Commanders(page: 1, search: "   ");

        Assert.Null(store.LastCommanderGridQuery?.SearchTerm);
    }

    [Fact]
    public async Task Commanders_Search_TruncatesTermBeforePassingToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        await controller.Commanders(page: 1, search: new string('a', 101));

        Assert.Equal(100, store.LastCommanderGridQuery?.SearchTerm?.Length);
    }

    [Fact]
    public async Task Commanders_Search_ClampsPageUsingFilteredCount()
    {
        var store = NewStore(distinctProcessedCommanderCount: 500);
        store.FilteredCommanderCount = 3;
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 2, search: "tef");

        var view = Assert.IsType<PartialViewResult>(result);
        Assert.Equal(1, Assert.IsType<CommandersGridViewModel>(view.Model).DeckPage);
    }

    [Fact]
    public async Task Commanders_Search_PassesSortToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 3);
        var controller = Build(store, crossOrigin: false);

        await controller.Commanders(page: 1, search: "tef", sortBy: "name", sortDir: "asc");

        Assert.Equal(CommanderSortColumn.Name, store.LastCommanderGridQuery?.SortBy);
        Assert.False(store.LastCommanderGridQuery?.Descending);
    }

    [Fact]
    public async Task Index_DoesNotQueryCommanderStoreDirectly()
    {
        var store = NewStore(distinctProcessedCommanderCount: 125);
        var controller = Build(store);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<AdminHarvestViewModel>(view.Model);
        Assert.Equal(0, store.LastPagedCommanderPage);
        Assert.Equal(0, store.GetDistinctProcessedCommanderCountCalls);
    }

    [Fact]
    public async Task Commanders_SameOrigin_ReturnsPartialView()
    {
        var store = NewStore(distinctProcessedCommanderCount: 125);
        var controller = Build(store, crossOrigin: false);

        var result = await controller.Commanders(page: 1);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_CommandersGrid", partial.ViewName);
        var model = Assert.IsType<CommandersGridViewModel>(partial.Model);
        Assert.Equal(1, model.DeckPage);
    }

    [Fact]
    public async Task Commanders_CrossOrigin_Returns403()
    {
        var store = NewStore(distinctProcessedCommanderCount: 125);
        var controller = Build(store, crossOrigin: true);

        var result = await controller.Commanders(page: 1);

        AssertForbidden(result);
    }

    [Fact]
    public async Task CommandersGrid_EmptyModel_RendersEmptyStateWithoutTable()
    {
        var model = new CommandersGridViewModel
        {
            DeckPage = 1,
            DeckPageSize = AdminHarvestViewModel.DefaultDeckPageSize,
            DeckTotalCount = 0,
        };

        var html = await RenderPartialViewAsync("_CommandersGrid", model);

        Assert.Contains("class=\"admin-empty\"", html, StringComparison.Ordinal);
        Assert.Contains("No harvested commanders yet.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table class=\"admin-table\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandersGrid_EmptySearch_RendersSearchSpecificEncodedMessage()
    {
        var model = new CommandersGridViewModel
        {
            Query = CommanderGridQuery.FromRequest("A < B", null, null),
            DeckPage = 1,
            DeckPageSize = AdminHarvestViewModel.DefaultDeckPageSize,
            DeckTotalCount = 0,
        };

        var html = await RenderPartialViewAsync("_CommandersGrid", model);

        Assert.Contains("No commanders start with &quot;A &lt; B&quot;.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No harvested commanders yet.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandersGrid_MultiPageModel_RendersNumberedPaginationWithCurrentPageStrong()
    {
        var model = new CommandersGridViewModel
        {
            HarvestedCommanders = new[]
            {
                new HarvestedCommanderRow("Commander One", 3, "2026-01-01T00:00:00.0000000Z"),
            },
            DeckPage = 2,
            DeckPageSize = AdminHarvestViewModel.DefaultDeckPageSize,
            DeckTotalCount = 250,
        };

        var html = await RenderPartialViewAsync("_CommandersGrid", model);

        Assert.Contains("data-page=\"1\"", html, StringComparison.Ordinal);
        Assert.Contains("data-page=\"3\"", html, StringComparison.Ordinal);
        Assert.Contains("<strong aria-current=\"page\">2</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-page=\"2\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandersGrid_Disclosure_RendersOneFullWidthRowPerCommander()
    {
        var model = new CommandersGridViewModel { HarvestedCommanders = [new HarvestedCommanderRow("One", 1, "2026-01-01T00:00:00.0000000Z"), new HarvestedCommanderRow("Two", 2, "2026-01-02T00:00:00.0000000Z")] };

        var html = await RenderPartialViewAsync("_CommandersGrid", model);

        Assert.Equal(2, Regex.Matches(html, "data-commander-details").Count);
        Assert.Equal(2, Regex.Matches(html, "<tr class=\"admin-harvest__category-row\">").Count);
        Assert.Equal(2, Regex.Matches(html, "<td colspan=\"4\">").Count);
    }

    [Fact]
    public async Task CommandersGrid_Disclosure_HtmlEncodesCommanderNameAttribute()
    {
        var model = new CommandersGridViewModel { HarvestedCommanders = [new HarvestedCommanderRow("A \" < B", 1, "2026-01-01T00:00:00.0000000Z")] };

        var html = await RenderPartialViewAsync("_CommandersGrid", model);

        Assert.Contains("data-commander-details=\"A &quot; &lt; B\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_ErrorMessage_RendersErrorColumn()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun(errorMessage: "upstream failed") });

        Assert.Contains("upstream failed", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_Started_RendersMountainTimeWithUtcTitle()
    {
        var startedUtc = DateTimeOffset.Parse("2026-10-01T18:21:54Z");
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun(startedUtc: startedUtc) });

        Assert.Contains("title=\"2026-10-01 18:21:54Z\">2026-10-01 12:21:54 MDT", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_NotStarted_RendersRequestedTimeInMountainTime()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun() });

        Assert.Contains("title=\"2026-01-01 00:00:00Z\">2025-12-31 17:00:00 MST", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_Runs_RendersOneTableWithEightColumnHeadings()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun() });

        Assert.Equal(1, html.Split("<table", StringSplitOptions.None).Length - 1);
        Assert.Equal(8, html.Split("<th scope=\"col\">", StringSplitOptions.None).Length - 1);
        Assert.Equal(
            ["Started", "Kind", "Trigger", "State", "Decks", "Update counters", "Duration", "Error"],
            Regex.Matches(html, "<th scope=\"col\">([^<]+)</th>").Select(match => match.Groups[1].Value));
    }

    [Fact]
    public async Task HarvestRunLog_EmptyList_RendersNamedEmptyStateWithoutTable()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", Array.Empty<HarvestRunRow>());

        Assert.Contains("id=\"harvest-run-log-heading\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"harvest-run-log-empty\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"admin-empty\"", html, StringComparison.Ordinal);
        Assert.Contains("No runs recorded yet.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HarvestIndex_StatsAvailability_RendersSameRunLogColumns(bool includesStats)
    {
        var runs = new[] { CreateHarvestRun() };
        var model = CreateHarvestViewModel(runs, includesStats);

        var html = await RenderPartialViewAsync("Index", model);

        Assert.Equal(8, html.Split("<th scope=\"col\">", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task HarvestRunLog_ShortError_RendersPlainEncodedText()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun(errorMessage: "brief failure") });

        Assert.Contains("brief failure", html, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-harvest__run-error", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_LongError_RendersDisclosureWithSummaryAndBody()
    {
        var error = new string('x', 81);
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun(errorMessage: error) });

        Assert.Contains("admin-harvest__run-error", html, StringComparison.Ordinal);
        Assert.Contains(new string('x', 80) + "&#x2026;", html, StringComparison.Ordinal);
        Assert.Contains(error, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_NullError_RendersEmDashWithoutDisclosure()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun() });

        Assert.Contains("&#x2014;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-harvest__run-error", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_HtmlError_EncodesSummaryAndBody()
    {
        var error = "<tag attr=\"quoted\">" + new string('x', 80);
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun(errorMessage: error) });

        Assert.Equal(2, html.Split("&lt;tag attr=&quot;quoted&quot;&gt;", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(error, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestIndex_ImportPanel_RendersProtectedSubmitUrlForm()
    {
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), includesStats: false));
        var importPanel = html[html.IndexOf("id=\"harvest-import-panel\"", StringComparison.Ordinal)..];

        Assert.Contains("<form method=\"post\"", importPanel, StringComparison.Ordinal);
        Assert.Contains("SubmitUrl", importPanel, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", importPanel, StringComparison.Ordinal);
        Assert.Contains("id=\"url\" type=\"url\" name=\"url\"", importPanel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunNow_InvalidDuration_SetsDangerBannerTone()
    {
        var jobService = new StubArchidektCacheJobService();
        var controller = Build(NewStore(0), jobService: jobService);
        await controller.RunNow("bulk", 1, CancellationToken.None);
        Assert.Equal("Invalid duration.", controller.TempData["AdminHarvestBanner"]);
        Assert.Equal("danger", controller.TempData["AdminHarvestBannerTone"]);
        Assert.Empty(jobService.Requests);
    }

    [Fact]
    public async Task RunNow_AllowedDuration_EnqueuesManualBulkRun()
    {
        var jobService = new StubArchidektCacheJobService();
        var controller = Build(NewStore(0), jobService: jobService);

        await controller.RunNow("bulk", 900, CancellationToken.None);

        Assert.Equal([(HarvestRunKind.Bulk, TimeSpan.FromSeconds(900), HarvestTriggerSource.Manual)], jobService.Requests);
        Assert.Equal("Run queued (cap 15 min).", controller.TempData["AdminHarvestBanner"]);
    }

    [Fact]
    public async Task RunNow_UpdateOptionFromRenderedForm_EnqueuesManualUpdateRunEndToEnd()
    {
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), includesStats: false));
        var formStart = html.IndexOf("<form", StringComparison.Ordinal);
        var formEnd = html.IndexOf("</form>", formStart, StringComparison.Ordinal) + "</form>".Length;
        var form = html[formStart..formEnd];
        var updateOption = Regex.Match(form, "<option[^>]*value=\"([^\"]+)\"[^>]*>Update</option>");
        var jobService = new StubArchidektCacheJobService();
        var controller = Build(NewStore(0), jobService: jobService);

        Assert.True(updateOption.Success);
        Assert.Equal((int)ArchidektCacheJobService.UpdateRunDuration.TotalMinutes, AdminHarvestViewModel.UpdateRunCapMinutes);
        await controller.RunNow(updateOption.Groups[1].Value, 0, CancellationToken.None);

        Assert.Equal([(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Manual)], jobService.Requests);
        Assert.Equal($"Update run queued (cap {AdminHarvestViewModel.UpdateRunCapMinutes} min).", controller.TempData["AdminHarvestBanner"]);
        Assert.Equal("success", controller.TempData["AdminHarvestBannerTone"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(900)]
    [InlineData(3600)]
    [InlineData(7200)]
    [InlineData(-5)]
    public async Task RunNow_UpdateKind_IgnoresPostedDuration(int durationSeconds)
    {
        var jobService = new StubArchidektCacheJobService();
        await Build(NewStore(0), jobService: jobService).RunNow("update", durationSeconds, CancellationToken.None);
        Assert.Equal([(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Manual)], jobService.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("url")]
    [InlineData("Bulk")]
    [InlineData("UPDATE")]
    [InlineData("1")]
    [InlineData(" bulk")]
    [InlineData("update ")]
    public async Task RunNow_InvalidKind_RejectsWithoutEnqueue(string? kind)
    {
        var jobService = new StubArchidektCacheJobService();
        var controller = Build(NewStore(0), jobService: jobService);
        await controller.RunNow(kind, 900, CancellationToken.None);
        Assert.Empty(jobService.Requests);
        Assert.Equal("Invalid run kind.", controller.TempData["AdminHarvestBanner"]);
        Assert.Equal("danger", controller.TempData["AdminHarvestBannerTone"]);
    }

    [Fact]
    public async Task RunNow_InvalidKindAndInvalidDuration_ReportsKindFirst()
    {
        var jobService = new StubArchidektCacheJobService();
        var controller = Build(NewStore(0), jobService: jobService);
        await controller.RunNow("url", 1, CancellationToken.None);
        Assert.Empty(jobService.Requests);
        Assert.Equal("Invalid run kind.", controller.TempData["AdminHarvestBanner"]);
        Assert.Equal("danger", controller.TempData["AdminHarvestBannerTone"]);
    }

    [Theory]
    [InlineData("bulk")]
    [InlineData("update")]
    public async Task RunNow_ActiveRun_ReportsNothingQueued(string kind)
    {
        var jobService = new StubArchidektCacheJobService { StartedNewJob = false };
        var controller = Build(NewStore(0), jobService: jobService);
        await controller.RunNow(kind, 900, CancellationToken.None);
        Assert.Single(jobService.Requests);
        Assert.Equal("A harvest run is already active. No new run was queued.", controller.TempData["AdminHarvestBanner"]);
        Assert.Equal("danger", controller.TempData["AdminHarvestBannerTone"]);
    }

    [Fact]
    public async Task HarvestIndex_RunNowForm_PostsKindSelectorWithAllowedKinds()
    {
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), includesStats: false));
        var formStart = html.IndexOf("<form", StringComparison.Ordinal);
        var formEnd = html.IndexOf("</form>", formStart, StringComparison.Ordinal) + "</form>".Length;
        var form = html[formStart..formEnd];
        Assert.Contains("id=\"runKind\" name=\"kind\"", form, StringComparison.Ordinal);
        var kindSelect = Regex.Match(form, "<select id=\"runKind\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
        Assert.Equal(2, Regex.Matches(kindSelect.Groups[1].Value, "<option").Count);
        Assert.Contains("value=\"bulk\"", form, StringComparison.Ordinal);
        Assert.Contains("value=\"update\"", form, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"url\"", form, StringComparison.Ordinal);
        Assert.Contains("name=\"durationSeconds\"", form, StringComparison.Ordinal);
        Assert.Contains($"Update runs stop after {AdminHarvestViewModel.UpdateRunCapMinutes} minutes.", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestRunLog_UpdateRun_RendersTriggerAndCounters()
    {
        var run = CreateHarvestRun() with { Kind = HarvestRunKind.Update, TriggerSource = HarvestTriggerSource.Manual, PagesPolled = 3, RefreshesRequeued = 2, RefreshesDrained = 1, NewIdsSeen = 4 };
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { run });
        Assert.Contains("<td>Manual</td>", html, StringComparison.Ordinal);
        Assert.Contains("Pages 3, requeued 2, drained 1, new IDs 4", html, StringComparison.Ordinal);
        Assert.Equal(1, html.Split("&#x2014;", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task HarvestRunLog_LegacyRun_RendersDashForTriggerAndCounters()
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun() });
        Assert.Equal(3, html.Split("&#x2014;", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task HarvestRunLog_PartialCounters_RendersDash()
    {
        var run = CreateHarvestRun() with { TriggerSource = HarvestTriggerSource.Scheduled, PagesPolled = 3 };
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { run });
        Assert.DoesNotContain("Pages 3", html, StringComparison.Ordinal);
        Assert.Contains("<td>Scheduled</td>", html, StringComparison.Ordinal);
        Assert.Equal(2, html.Split("&#x2014;", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task PauseSchedule_Pause_SetsSuccessBannerTone()
    {
        var controller = Build(NewStore(0));
        await controller.PauseSchedule(true, CancellationToken.None);
        Assert.Equal("Schedule paused.", controller.TempData["AdminHarvestBanner"]);
        Assert.Equal("success", controller.TempData["AdminHarvestBannerTone"]);
    }

    [Fact]
    public async Task Index_ReadsBannerToneIntoViewModel()
    {
        var controller = Build(NewStore(0));
        controller.TempData["AdminHarvestBanner"] = "Banner";
        controller.TempData["AdminHarvestBannerTone"] = "danger";
        var result = await controller.Index();
        var model = Assert.IsType<AdminHarvestViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Banner", model.LastBanner);
        Assert.True(model.LastBannerIsError);
    }

    [Theory]
    [InlineData(true, "admin-banner--danger", "alert")]
    [InlineData(false, "admin-banner--success", "status")]
    public async Task HarvestIndex_Banner_RendersToneAndRole(bool isError, string modifier, string role)
    {
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), false) with { LastBanner = "Banner <b>x</b>", LastBannerIsError = isError });
        Assert.Contains($"{modifier}\" role=\"{role}\"", html, StringComparison.Ordinal);
        Assert.Contains("Banner &lt;b&gt;x&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>x</b>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestHealthStrip_RendersFourStatTiles()
    {
        var html = await RenderPartialViewAsync("_HarvestHealthStrip", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), true).Stats!);
        Assert.Equal(4, html.Split("admin-stat-tile\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("health-database-size", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestHealthStrip_BacklogFlag_IsWarningAlertBadge()
    {
        var stats = CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), true).Stats! with { Health = new HarvestHealthSignals(true, HarvestBacklogReason.AboveFloor, 100, 3, 0, false) };
        var html = await RenderPartialViewAsync("_HarvestHealthStrip", stats);
        Assert.Contains("admin-badge--warning", html, StringComparison.Ordinal);
        Assert.Contains("admin-badge--alert", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HarvestRunState.Queued, "admin-badge--neutral")]
    [InlineData(HarvestRunState.Running, "admin-badge--info")]
    [InlineData(HarvestRunState.Stopping, "admin-badge--warning")]
    [InlineData(HarvestRunState.Succeeded, "admin-badge--success")]
    [InlineData(HarvestRunState.Interrupted, "admin-badge--neutral")]
    [InlineData(HarvestRunState.Failed, "admin-badge--danger")]
    [InlineData(HarvestRunState.Cancelled, "admin-badge--warning")]
    public async Task HarvestRunLog_State_RendersTextBadge(HarvestRunState state, string modifier)
    {
        var html = await RenderPartialViewAsync("_HarvestRunLog", new[] { CreateHarvestRun() with { State = state } });
        Assert.Contains(modifier, html, StringComparison.Ordinal);
        Assert.Contains($">{state}</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestIndex_UsesSharedTabsCardsAndButtons()
    {
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(new[] { CreateHarvestRun() }, true));
        Assert.Contains("admin-tabs", html, StringComparison.Ordinal);
        Assert.Contains("admin-stack", html, StringComparison.Ordinal);
        Assert.Contains("admin-button--primary", html, StringComparison.Ordinal);
        Assert.Contains("admin-filter", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestIndex_StatsUnavailable_RendersEmptyState()
    {
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), false));
        Assert.Contains("class=\"admin-empty\">Stats unavailable.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestIndex_Stats_RendersPerKindLastAndNextRuns()
    {
        var stats = new HarvestStatsPayload(0, 0, 0, 0, 0, Array.Empty<HarvestRunRow>(), null, new(2026, 9, 23, 6, 0, 0, TimeSpan.Zero), new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero), new(2026, 9, 23, 11, 45, 0, TimeSpan.Zero), new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero), new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false));
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), true, new(15, false, DateTimeOffset.UtcNow), new(4, false, DateTimeOffset.UtcNow), stats));

        Assert.Contains("Last scheduled bulk run: 2026-09-23 06:00 UTC", html, StringComparison.Ordinal);
        Assert.Contains("Next scheduled bulk run: 2026-09-23 10:00 UTC", html, StringComparison.Ordinal);
        Assert.Contains("Last scheduled update run: 2026-09-23 11:45 UTC", html, StringComparison.Ordinal);
        Assert.Contains("Next scheduled update run: 2026-09-23 12:00 UTC", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Last successful run:", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HarvestRunKind.Bulk, false, false, "Off")]
    [InlineData(HarvestRunKind.Bulk, true, true, "Paused")]
    [InlineData(HarvestRunKind.Bulk, true, false, "Pending")]
    [InlineData(HarvestRunKind.Update, false, false, "Off")]
    [InlineData(HarvestRunKind.Update, true, true, "Paused")]
    public async Task HarvestIndex_Stats_NoNextRun_RendersLiveScheduleState(HarvestRunKind kind, bool enabled, bool paused, string expected)
    {
        var stats = new HarvestStatsPayload(0, 0, 0, 0, 0, Array.Empty<HarvestRunRow>(), null, null, kind == HarvestRunKind.Bulk && (paused || !enabled) ? new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero) : null, null, kind == HarvestRunKind.Update ? new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero) : null, new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false));
        var bulk = kind == HarvestRunKind.Bulk ? new HarvestScheduleSnapshot(enabled ? 4 : null, paused, DateTimeOffset.UtcNow) : new HarvestScheduleSnapshot(4, false, DateTimeOffset.UtcNow);
        var update = kind == HarvestRunKind.Update ? new HarvestUpdateScheduleSnapshot(enabled ? 15 : null, paused, DateTimeOffset.UtcNow) : new HarvestUpdateScheduleSnapshot(15, false, DateTimeOffset.UtcNow);
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), true, update, bulk, stats));

        Assert.Contains($"Next scheduled {kind.ToString().ToLowerInvariant()} run: {expected}", html, StringComparison.Ordinal);
    }

    private static HarvestRunRow CreateHarvestRun(string? errorMessage = null, DateTimeOffset? startedUtc = null)
        => new(Guid.NewGuid(), HarvestRunKind.Bulk, HarvestRunState.Failed, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), startedUtc, null, 900, 2, 0, null, null, errorMessage, null, null, null, null, null, null);

    private static AdminHarvestViewModel CreateHarvestViewModel(IReadOnlyList<HarvestRunRow> runs, bool includesStats, HarvestUpdateScheduleSnapshot? updateSchedule = null, HarvestScheduleSnapshot? schedule = null, HarvestStatsPayload? stats = null)
        => new()
        {
            Schedule = schedule ?? new HarvestScheduleSnapshot(null, false, DateTimeOffset.Parse("2026-01-01T00:00:00Z")),
            UpdateSchedule = updateSchedule ?? DefaultUpdateSchedule,
            RecentRuns = runs,
            Stats = includesStats
                ? stats ?? new HarvestStatsPayload(0, 0, 0, 0, 0, runs, null, null, null, null, null, new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false))
                : null,
        };

    [Fact]
    public void Commanders_Sort_NextDirectionUsesAscendingForInactiveNameColumn()
        => Assert.Equal("asc", CommanderGridQuery.Default.NextDirectionToken(CommanderSortColumn.Name));

    [Theory]
    [InlineData(true, "asc")]
    [InlineData(false, "desc")]
    public void Commanders_Sort_NextDirectionReversesActiveColumn(bool descending, string expected)
    {
        var query = CommanderGridQuery.FromRequest(null, "name", descending ? "desc" : "asc");
        Assert.Equal(expected, query.NextDirectionToken(CommanderSortColumn.Name));
    }

    [Fact]
    public void Commanders_Sort_IsActiveMatchesSelectedColumn()
    {
        var query = CommanderGridQuery.FromRequest(null, "last_processed", "asc");
        Assert.True(query.IsActive(CommanderSortColumn.LastProcessed));
        Assert.False(query.IsActive(CommanderSortColumn.Name));
    }

    [Fact]
    public async Task Commanders_Sort_ControllerPassesNameAscendingQuery()
    {
        var store = NewStore(3);
        var result = await Build(store, crossOrigin: false).Commanders(sortBy: "name", sortDir: "asc");
        Assert.IsType<PartialViewResult>(result);
        Assert.Equal("name", store.LastCommanderGridQuery!.SortByToken);
        Assert.Equal("asc", store.LastCommanderGridQuery.SortDirToken);
    }

    [Fact]
    public async Task Commanders_Sort_ControllerPassesLastProcessedDescendingQuery()
    {
        var store = NewStore(3);
        await Build(store, crossOrigin: false).Commanders(sortBy: "last_processed", sortDir: "desc");
        Assert.Equal("last_processed", store.LastCommanderGridQuery!.SortByToken);
    }

    [Fact]
    public async Task CommandersGrid_Sort_DefaultRendersActiveDeckCountAndAriaSort()
    {
        var html = await RenderPartialViewAsync("_CommandersGrid", CreateSortGridModel(CommanderGridQuery.Default));
        AssertHeaderSort(html, "deck_count", "descending", active: true);
        AssertHeaderSort(html, "name", "none");
        AssertHeaderSort(html, "last_processed", "none");
    }

    [Fact]
    public async Task CommandersGrid_Sort_NameAscendingRendersAriaSortAndNextDirection()
    {
        var html = await RenderPartialViewAsync("_CommandersGrid", CreateSortGridModel(CommanderGridQuery.FromRequest("Te", "name", "asc")));
        AssertHeaderNextDirection(html, "name", "desc");
        AssertHeaderSort(html, "name", "ascending", active: true);
        AssertHeaderSort(html, "deck_count", "none");
        AssertHeaderSort(html, "last_processed", "none");
    }

    [Fact]
    public async Task CommandersGrid_Sort_PaginationPreservesQueryState()
    {
        var html = await RenderPartialViewAsync("_CommandersGrid", CreateSortGridModel(CommanderGridQuery.FromRequest("Te", "last_processed", "desc")));
        Assert.Contains("data-search=\"Te\"", html, StringComparison.Ordinal);
        Assert.Contains("data-sort-by=\"last_processed\"", html, StringComparison.Ordinal);
        Assert.Contains("data-sort-dir=\"desc\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("deck_count", "desc", "asc", "asc", "desc")]
    [InlineData("name", "asc", "desc", "desc", "desc")]
    public async Task CommandersGrid_Sort_RendersNextDirectionPerHeader(string sortBy, string sortDir, string expectedName, string expectedDeckCount, string expectedLastProcessed)
    {
        var html = await RenderPartialViewAsync("_CommandersGrid", CreateSortGridModel(CommanderGridQuery.FromRequest(null, sortBy, sortDir)));

        AssertHeaderNextDirection(html, "name", expectedName);
        AssertHeaderNextDirection(html, "deck_count", expectedDeckCount);
        AssertHeaderNextDirection(html, "last_processed", expectedLastProcessed);
    }

    private static void AssertHeaderSort(string html, string column, string expectedAriaSort, bool active = false)
    {
        var match = FindSortHeader(html, column);
        Assert.Contains($"aria-sort=\"{expectedAriaSort}\"", match.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("@if", html, StringComparison.Ordinal);
        if (active)
        {
            Assert.Single(Regex.Matches(match.Value, "<span class=\"admin-table__sort-indicator\" aria-hidden=\"true\">").Cast<Match>());
        }
        else
        {
            Assert.DoesNotContain("admin-table__sort-indicator", match.Value, StringComparison.Ordinal);
        }
    }

    private static Match FindSortHeader(string html, string column)
    {
        var match = Regex.Match(html, $"<th(?:(?!</th>).)*data-sort-column=\\\"{Regex.Escape(column)}\\\"(?:(?!</th>).)*</th>", RegexOptions.Singleline);
        Assert.True(match.Success, $"Missing sort header for {column}.");
        return match;
    }

    private static void AssertHeaderNextDirection(string html, string column, string expected)
        => Assert.Contains($"data-sort-next-dir=\"{expected}\"", FindSortHeader(html, column).Value, StringComparison.Ordinal);

    private static CommandersGridViewModel CreateSortGridModel(CommanderGridQuery query)
        => new()
        {
            Query = query,
            DeckPage = 1,
            DeckPageSize = 1,
            DeckTotalCount = 2,
            HarvestedCommanders = new[] { new HarvestedCommanderRow("Commander One", 1, null) },
        };

    private static FakeCategoryKnowledgeStore NewStore(int distinctProcessedCommanderCount)
        => new()
        {
            DistinctProcessedCommanderCount = distinctProcessedCommanderCount,
            PagedCommandersResult = new[]
            {
                new HarvestedCommanderRow("Commander One", 3, "2026-01-01T00:00:00.0000000Z"),
                new HarvestedCommanderRow("Commander Two", 2, "2026-01-02T00:00:00.0000000Z"),
                new HarvestedCommanderRow("Commander Three", 1, "2026-01-03T00:00:00.0000000Z"),
            },
        };

    [Fact]
    public async Task ExportCommanders_SameOriginPost_ReturnsCsvFile()
    {
        var file = await ExportFileAsync(NewStore(0));
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.True(file.FileContents.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
    }

    [Fact]
    public async Task ExportCommanders_ReturnsTimestampedCsvFileName()
    {
        var file = await ExportFileAsync(NewStore(0));

        Assert.Matches(@"^harvested-commanders-\d{8}-\d{6}\.csv$", file.FileDownloadName);
    }

    [Fact]
    public async Task ExportCommanders_CrossOrigin_ReturnsForbiddenWithoutStoreRead()
    {
        var store = NewStore(0);
        var result = await Build(store, crossOrigin: true).ExportCommanders(cancellationToken: CancellationToken.None);

        AssertForbidden(result);
        Assert.Equal(0, store.GetAllFilteredProcessedCommandersCalls);
    }

    [Fact]
    public async Task ExportCommanders_RequestParameters_PassesNormalizedGridQueryToStore()
    {
        var store = NewStore(0);
        await Build(store).ExportCommanders(search: "tef", sortBy: "last_processed", sortDir: "asc", cancellationToken: CancellationToken.None);

        Assert.Equal(CommanderGridQuery.FromRequest("tef", "last_processed", "asc"), store.LastCommanderGridQuery);
    }

    [Fact]
    public async Task ExportCommanders_NoMatches_ReturnsHeaderOnly()
    {
        var store = NewStore(0);
        store.AllFilteredCommandersResult = Array.Empty<HarvestedCommanderRow>();
        var file = await ExportFileAsync(store);

        Assert.Equal("rank,commander,decks_categorized,last_processed_utc\n", Encoding.UTF8.GetString(file.FileContents[Encoding.UTF8.GetPreamble().Length..]));
    }

    [Fact]
    public async Task ExportCommanders_RequestsOneMoreThanMaximumRows()
    {
        var store = NewStore(0);
        await Build(store).ExportCommanders(cancellationToken: CancellationToken.None);

        Assert.Equal(AdminHarvestController.MaxCommanderExportRows + 1, store.LastExportMaxRows);
    }

    [Fact]
    public async Task ExportCommanders_ExactlyAtCap_IsComplete()
    {
        var store = NewStore(0);
        store.AllFilteredCommandersResult = ExportRows(AdminHarvestController.MaxCommanderExportRows);
        var file = await ExportFileAsync(store);

        Assert.Equal(AdminHarvestController.MaxCommanderExportRows + 1, CsvLineCount(file));
        Assert.DoesNotContain("truncated", file.FileDownloadName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportCommanders_OverCap_TruncatesRowsAndMarksFileName()
    {
        var store = NewStore(0);
        store.AllFilteredCommandersResult = ExportRows(AdminHarvestController.MaxCommanderExportRows + 1);
        var file = await ExportFileAsync(store);

        Assert.Equal(AdminHarvestController.MaxCommanderExportRows + 1, CsvLineCount(file));
        Assert.Contains("truncated-first-" + AdminHarvestController.MaxCommanderExportRows, file.FileDownloadName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportCommanders_HasPostRouteAndValidateAntiForgeryTokenAttributes()
    {
        var method = typeof(AdminHarvestController).GetMethod(nameof(AdminHarvestController.ExportCommanders))!;

        Assert.Equal("commanders/export", Assert.IsType<HttpPostAttribute>(method.GetCustomAttributes(typeof(HttpPostAttribute), false).Single()).Template);
        Assert.Single(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), false));
    }

    [Fact]
    public async Task ExportCommanders_UsesStoreResultForCsvRows()
    {
        var store = NewStore(0);
        store.AllFilteredCommandersResult = new[] { new HarvestedCommanderRow("Tef", 7, null) };
        var file = await ExportFileAsync(store);

        Assert.Contains("1,\"Tef\",7,", Encoding.UTF8.GetString(file.FileContents), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportCommanders_PerformsOneUnpagedStoreRead()
    {
        var store = NewStore(0);
        await Build(store).ExportCommanders(cancellationToken: CancellationToken.None);

        Assert.Equal(1, store.GetAllFilteredProcessedCommandersCalls);
    }

    private static IReadOnlyList<HarvestedCommanderRow> ExportRows(int count)
        => Enumerable.Range(1, count).Select(index => new HarvestedCommanderRow($"Commander {index}", index, null)).ToArray();

    private static int CsvLineCount(FileContentResult file)
        => Encoding.UTF8.GetString(file.FileContents[Encoding.UTF8.GetPreamble().Length..]).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

    private static void AssertForbidden(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }

    [Fact]
    public async Task SubmitUrl_RecordsUrlRunWithManualTrigger()
    {
        var runStore = new StubHarvestRunStore();
        var controller = Build(NewStore(distinctProcessedCommanderCount: 0), runStore: runStore);
        await controller.SubmitUrl("https://archidekt.com/decks/123", CancellationToken.None);
        Assert.Single(runStore.Inserted);
        Assert.Equal((HarvestRunKind.Url, HarvestTriggerSource.Manual), runStore.Inserted[0]);
    }

    [Fact]
    public async Task SubmitUrl_MetadataBearingImport_PassesMetadataToStore()
    {
        var store = NewStore(distinctProcessedCommanderCount: 0);
        var metadata = new ArchidektDeckMetadata(3, 1, true, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"), DateTimeOffset.Parse("2026-01-03T00:00:00Z"));
        var importer = new StubArchidektDeckImporter { Metadata = metadata };
        var controller = Build(store, importer: importer);

        await controller.SubmitUrl("https://archidekt.com/decks/123", CancellationToken.None);

        Assert.Equal("123", store.LastUrlDeckId);
        Assert.Same(metadata, store.LastUrlMetadata);
    }

    [Fact]
    public async Task SubmitUrl_CommanderBoard_RecordsCommanderAndBanner()
    {
        var store = NewStore(distinctProcessedCommanderCount: 0);
        var importer = new StubArchidektDeckImporter
        {
            Entries = new List<DeckEntry>
            {
                new() { Name = "Kenrith, the Returned King", NormalizedName = "Kenrith, the Returned King", Board = "commander", Quantity = 1 },
                new() { Name = "Sol Ring", NormalizedName = "Sol Ring", Board = "mainboard", Quantity = 1 },
            },
        };
        var controller = Build(store, importer: importer);

        await controller.SubmitUrl("https://archidekt.com/decks/123", CancellationToken.None);

        Assert.Equal("Kenrith, the Returned King", store.LastUrlCommanderName);
        Assert.Equal("Harvested Kenrith, the Returned King: 2 new observations.", controller.TempData["AdminHarvestBanner"]);
    }

    private static async Task<FileContentResult> ExportFileAsync(ICategoryKnowledgeStore store)
        => Assert.IsType<FileContentResult>(await Build(store).ExportCommanders(cancellationToken: CancellationToken.None));

    [Theory]
    [InlineData(15, false)]
    [InlineData(30, true)]
    [InlineData(60, false)]
    [InlineData(120, true)]
    [InlineData(null, false)]
    public async Task SaveUpdateSchedule_AllowedInterval_SavesReloadsAndConfirms(int? intervalMinutes, bool paused)
    {
        var updateStore = new StubHarvestUpdateScheduleStore();
        var updateCache = new StubHarvestUpdateScheduleCache();
        var controller = Build(NewStore(0), updateScheduleStore: updateStore, updateScheduleCache: updateCache);
        await controller.SaveUpdateSchedule(intervalMinutes, paused, CancellationToken.None);
        Assert.Equal([(intervalMinutes, paused)], updateStore.Saves);
        Assert.Equal(1, updateCache.ReloadCount);
        Assert.Equal("Update schedule updated.", controller.TempData["AdminHarvestBanner"]);
        Assert.Equal("success", controller.TempData["AdminHarvestBannerTone"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(240)]
    [InlineData(-15)]
    [InlineData(2)]
    public async Task SaveUpdateSchedule_IntervalOutsideAllowList_RejectsWithoutWrite(int intervalMinutes)
    {
        var updateStore = new StubHarvestUpdateScheduleStore();
        var updateCache = new StubHarvestUpdateScheduleCache();
        var controller = Build(NewStore(0), updateScheduleStore: updateStore, updateScheduleCache: updateCache);
        await controller.SaveUpdateSchedule(intervalMinutes, false, CancellationToken.None);
        Assert.Empty(updateStore.Saves); Assert.Equal(0, updateCache.ReloadCount);
        Assert.Equal("Invalid update interval.", controller.TempData["AdminHarvestBanner"]);
        Assert.Equal("danger", controller.TempData["AdminHarvestBannerTone"]);
    }

    [Fact]
    public async Task SaveUpdateSchedule_UnparseableInput_RejectsWithoutWrite()
    {
        var updateStore = new StubHarvestUpdateScheduleStore(); var updateCache = new StubHarvestUpdateScheduleCache();
        var controller = Build(NewStore(0), updateScheduleStore: updateStore, updateScheduleCache: updateCache);
        controller.ModelState.AddModelError("intervalMinutes", "invalid");
        await controller.SaveUpdateSchedule(null, false, CancellationToken.None);
        Assert.Empty(updateStore.Saves); Assert.Equal(0, updateCache.ReloadCount);
        Assert.Equal("Invalid update interval.", controller.TempData["AdminHarvestBanner"]);
    }

    [Theory]
    [InlineData(30, false, true, "Update schedule paused.")]
    [InlineData(60, true, false, "Update schedule resumed.")]
    public async Task PauseUpdateSchedule_PreservesIntervalAndSetsPaused(int interval, bool currentPaused, bool paused, string message)
    {
        var updateStore = new StubHarvestUpdateScheduleStore();
        var updateCache = new StubHarvestUpdateScheduleCache { Current = new(interval, currentPaused, DateTimeOffset.MinValue) };
        var controller = Build(NewStore(0), updateScheduleStore: updateStore, updateScheduleCache: updateCache);
        await controller.PauseUpdateSchedule(paused, CancellationToken.None);
        Assert.Equal([(interval, paused)], updateStore.Saves); Assert.Equal(1, updateCache.ReloadCount);
        Assert.Equal(message, controller.TempData["AdminHarvestBanner"]);
    }

    [Fact]
    public async Task PauseUpdateSchedule_DoubleSubmit_WritesSameAbsoluteState()
    {
        var updateStore = new StubHarvestUpdateScheduleStore(); var updateCache = new StubHarvestUpdateScheduleCache { Current = new(15, false, DateTimeOffset.MinValue) };
        var controller = Build(NewStore(0), updateScheduleStore: updateStore, updateScheduleCache: updateCache);
        await controller.PauseUpdateSchedule(true, CancellationToken.None); await controller.PauseUpdateSchedule(true, CancellationToken.None);
        Assert.Equal([(15, true), (15, true)], updateStore.Saves);
    }

    [Fact]
    public async Task PauseUpdateSchedule_UnparseableInput_RejectsWithoutWrite()
    {
        var updateStore = new StubHarvestUpdateScheduleStore(); var updateCache = new StubHarvestUpdateScheduleCache();
        var controller = Build(NewStore(0), updateScheduleStore: updateStore, updateScheduleCache: updateCache);
        controller.ModelState.AddModelError("paused", "invalid"); await controller.PauseUpdateSchedule(false, CancellationToken.None);
        Assert.Empty(updateStore.Saves); Assert.Equal(0, updateCache.ReloadCount);
        Assert.Equal("Invalid update schedule request.", controller.TempData["AdminHarvestBanner"]);
    }

    [Theory]
    [InlineData(nameof(AdminHarvestController.RunNow), "run")]
    [InlineData(nameof(AdminHarvestController.SaveUpdateSchedule), "update-schedule")]
    [InlineData(nameof(AdminHarvestController.PauseUpdateSchedule), "update-pause")]
    public void AdminHarvestStatePosts_RequirePostRouteAndAntiforgery(string methodName, string route)
    {
        var method = typeof(AdminHarvestController).GetMethod(methodName)!;
        Assert.Equal(route, Assert.Single(method.GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>()).Template);
        Assert.Single(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), false));
    }

    [Fact]
    public async Task UpdateSchedulePosts_NeverWriteBulkSchedule()
    {
        var bulkStore = new StubHarvestScheduleStore(); var bulkCache = new StubHarvestScheduleCache();
        var controller = Build(NewStore(0), scheduleStore: bulkStore, scheduleCache: bulkCache);
        await controller.SaveUpdateSchedule(30, false, CancellationToken.None);
        await controller.PauseUpdateSchedule(true, CancellationToken.None);
        Assert.Equal(0, bulkStore.SaveCount); Assert.Equal(0, bulkCache.ReloadCount);
    }

    [Fact]
    public async Task BulkSchedulePosts_NeverWriteUpdateSchedule()
    {
        var updateStore = new StubHarvestUpdateScheduleStore(); var updateCache = new StubHarvestUpdateScheduleCache();
        var controller = Build(NewStore(0), updateScheduleStore: updateStore, updateScheduleCache: updateCache);
        await controller.SaveSchedule(4, false, CancellationToken.None);
        await controller.PauseSchedule(true, CancellationToken.None);
        Assert.Empty(updateStore.Saves); Assert.Equal(0, updateCache.ReloadCount);
    }

    [Fact]
    public async Task HarvestIndex_UpdateScheduleCard_RendersProtectedSaveAndPauseForms()
    {
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), false));
        var start = html.IndexOf("id=\"harvest-update-schedule\"", StringComparison.Ordinal);
        var card = html[start..(html.IndexOf("</section>", start, StringComparison.Ordinal) + "</section>".Length)];
        Assert.Contains("Update Schedule", card, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(card, "<form method=\"post\"").Count);
        Assert.Contains("SaveUpdateSchedule", card, StringComparison.Ordinal); Assert.Contains("PauseUpdateSchedule", card, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(card, "name=\"__RequestVerificationToken\"").Count);
        Assert.Contains("id=\"updateIntervalMinutes\" name=\"intervalMinutes\"", card, StringComparison.Ordinal);
        foreach (var interval in new[] { 15, 30, 60, 120 }) Assert.Contains($"value=\"{interval}\"", card, StringComparison.Ordinal);
        Assert.Contains("sample", card, StringComparison.Ordinal); Assert.Contains("research estimate", card, StringComparison.Ordinal);
        Assert.True(html.IndexOf("id=\"harvest-bulk-schedule\"", StringComparison.Ordinal) < start);
        Assert.Contains("Bulk Schedule", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, false, "Current interval: Off", "Paused: No", "Pause")]
    [InlineData(30, true, "Current interval: Every 30 minutes", "Paused: Yes", "Resume")]
    public async Task HarvestIndex_UpdateScheduleCard_RendersSnapshotState(int? interval, bool paused, string current, string pause, string button)
    {
        var html = await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), false, new(interval, paused, DateTimeOffset.MinValue)));
        var start = html.IndexOf("id=\"harvest-update-schedule\"", StringComparison.Ordinal);
        var card = html[start..(html.IndexOf("</section>", start, StringComparison.Ordinal) + "</section>".Length)];
        Assert.Contains(current, card, StringComparison.Ordinal); Assert.Contains(pause, card, StringComparison.Ordinal); Assert.Contains($">{button}</button>", card, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_ReadsUpdateScheduleSnapshotIntoViewModel()
    {
        var snapshot = new HarvestUpdateScheduleSnapshot(60, true, DateTimeOffset.MinValue);
        var controller = Build(NewStore(0), updateScheduleCache: new StubHarvestUpdateScheduleCache { Current = snapshot });
        var result = Assert.IsType<ViewResult>(await controller.Index());
        Assert.Equal(snapshot, Assert.IsType<AdminHarvestViewModel>(result.Model).UpdateSchedule);
    }

    [Fact]
    public async Task SaveRate_AllowedRate_PersistsAppliesLimiterAndRendersSelected()
    {
        ArchidektThrottle.ResetForTests();
        var path = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid() + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            var throttle = new HarvestThrottleStore(path);
            var controller = Build(NewStore(0), throttleStore: throttle);
            Assert.IsType<RedirectToActionResult>(await controller.SaveRate(10, CancellationToken.None));
            Assert.Equal(10, (await throttle.GetAsync()).MaxRequestsPerMinute);
            Assert.Equal(10, ArchidektThrottle.CurrentRatePerMinute);
            var model = Assert.IsType<AdminHarvestViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);
            Assert.Equal(10, model.RatePerMinute); Assert.Null(model.RateLimitedUtc);
            Assert.Equal("Archidekt rate set to 10 requests per minute.", model.LastBanner);
            var document = await new HtmlParser().ParseDocumentAsync(await RenderPartialViewAsync("Index", model));
            Assert.Equal("10", document.QuerySelector("#harvest-rate-card #ratePerMinute option[selected]")?.GetAttribute("value"));
            Assert.Null(document.QuerySelector("#harvest-rate-limited-banner"));
        }
        finally { ArchidektThrottle.ResetForTests(); DeleteThrottleDatabase(path); }
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(15)]
    [InlineData(21)]
    [InlineData(100)]
    public async Task SaveRate_RateOutsideAllowList_RejectedWithoutWrite(int rate)
    {
        ArchidektThrottle.ResetForTests();
        try
        {
            var throttle = new StubHarvestThrottleStore();
            var controller = Build(NewStore(0), throttleStore: throttle);
            Assert.IsType<RedirectToActionResult>(await controller.SaveRate(rate, CancellationToken.None));
            Assert.Equal("Invalid rate.", controller.TempData["AdminHarvestBanner"]);
            Assert.Equal("danger", controller.TempData["AdminHarvestBannerTone"]);
            Assert.Empty(throttle.SavedRates);
            Assert.Equal(ArchidektThrottle.MaxRatePerMinute, ArchidektThrottle.CurrentRatePerMinute);
        }
        finally { ArchidektThrottle.ResetForTests(); }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task SaveRate_AllowListedRate_SavesThatRate(int rate)
    {
        var throttle = new StubHarvestThrottleStore();
        var controller = Build(NewStore(0), throttleStore: throttle);
        await controller.SaveRate(rate, CancellationToken.None);
        Assert.Equal([rate], throttle.SavedRates); Assert.Equal($"Archidekt rate set to {rate} requests per minute.", controller.TempData["AdminHarvestBanner"]);
    }

    [Fact]
    public void AllowedRatesPerMinute_IsFiveTenTwentyWithinCodeCeiling()
    {
        Assert.Equal([5, 10, 20], AdminHarvestViewModel.AllowedRatesPerMinute); Assert.All(AdminHarvestViewModel.AllowedRatesPerMinute, rate => Assert.InRange(rate, 1, ArchidektThrottle.MaxRatePerMinute));
    }

    [Fact]
    public async Task SaveRate_SameRateTwice_IsIdempotent()
    {
        ArchidektThrottle.ResetForTests(); var path = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid() + ".db"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try { var throttle = new HarvestThrottleStore(path); var controller = Build(NewStore(0), throttleStore: throttle); await controller.SaveRate(10, CancellationToken.None); await controller.SaveRate(10, CancellationToken.None); Assert.Equal(10, (await throttle.GetAsync()).MaxRequestsPerMinute); Assert.Equal(10, ArchidektThrottle.CurrentRatePerMinute); }
        finally { ArchidektThrottle.ResetForTests(); DeleteThrottleDatabase(path); }
    }

    [Fact]
    public async Task SaveRate_CrossOrigin_Returns403WithoutWrite()
    {
        ArchidektThrottle.ResetForTests();
        try { var throttle = new StubHarvestThrottleStore(); var controller = Build(NewStore(0), crossOrigin: true, throttleStore: throttle); AssertForbidden(await controller.SaveRate(0, CancellationToken.None)); Assert.Null(controller.TempData["AdminHarvestBanner"]); Assert.Empty(throttle.SavedRates); Assert.Equal(ArchidektThrottle.MaxRatePerMinute, ArchidektThrottle.CurrentRatePerMinute); }
        finally { ArchidektThrottle.ResetForTests(); }
    }

    [Fact]
    public void SaveRate_HasPostRouteAndAntiforgery()
    {
        var method = typeof(AdminHarvestController).GetMethod(nameof(AdminHarvestController.SaveRate))!;
        Assert.Equal("rate", Assert.IsType<HttpPostAttribute>(method.GetCustomAttributes(typeof(HttpPostAttribute), false).Single()).Template); Assert.Single(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), false));
    }

    [Fact]
    public async Task Index_MapsThrottleSnapshotIntoViewModel()
    {
        var marker = DateTimeOffset.UtcNow; var controller = Build(NewStore(0), throttleStore: new StubHarvestThrottleStore { Snapshot = new HarvestThrottleSnapshot(5, marker, marker) });
        var model = Assert.IsType<AdminHarvestViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);
        Assert.Equal(5, model.RatePerMinute); Assert.Equal(marker, model.RateLimitedUtc);
    }

    [Fact]
    public async Task HarvestIndex_RateCard_RendersAllowListFormWithToken()
    {
        var document = await new HtmlParser().ParseDocumentAsync(await RenderPartialViewAsync("Index", CreateHarvestViewModel(Array.Empty<HarvestRunRow>(), false) with { RatePerMinute = 20 })); var card = document.QuerySelector("#harvest-rate-card")!;
        Assert.Equal(["5", "10", "20"], card.QuerySelectorAll("option").Select(option => option.GetAttribute("value")!).ToArray()); Assert.Equal("20", card.QuerySelector("option[selected]")?.GetAttribute("value")); Assert.EndsWith("Admin/Harvest/SaveRate", card.QuerySelector("form")?.GetAttribute("action")); Assert.NotNull(card.QuerySelector("input[name=__RequestVerificationToken]")); Assert.Contains("admin-button", card.QuerySelector("button")?.ClassName); Assert.Contains("Code ceiling: 20 requests per minute.", card.TextContent);
    }

    private static AdminHarvestController Build(ICategoryKnowledgeStore store, bool crossOrigin = false, IArchidektDeckImporter? importer = null, ICommanderCategoryService? commanderCategoryService = null, IHarvestRunStore? runStore = null, IArchidektCacheJobService? jobService = null, IHarvestScheduleStore? scheduleStore = null, IHarvestScheduleCache? scheduleCache = null, IHarvestUpdateScheduleStore? updateScheduleStore = null, IHarvestUpdateScheduleCache? updateScheduleCache = null, IHarvestThrottleStore? throttleStore = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("deckflow.test");
        httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";

        return new AdminHarvestController(
            jobService ?? new StubArchidektCacheJobService(),
            runStore ?? new StubHarvestRunStore(),
            scheduleStore ?? new StubHarvestScheduleStore(),
            scheduleCache ?? new StubHarvestScheduleCache(),
            updateScheduleStore ?? new StubHarvestUpdateScheduleStore(),
            updateScheduleCache ?? new StubHarvestUpdateScheduleCache(),
            throttleStore ?? new StubHarvestThrottleStore(),
            new StubHarvestStatsAggregator(),
            importer ?? new StubArchidektDeckImporter(),
            store,
            commanderCategoryService ?? new CommanderCategoryService(store),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AdminHarvestController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new StubTempDataProvider()),
        };
    }

    private sealed class StubHarvestThrottleStore : IHarvestThrottleStore
    {
        public HarvestThrottleSnapshot Snapshot { get; set; } = new(20, null, DateTimeOffset.MinValue);
        public List<int> SavedRates { get; } = [];
        public bool ResumeResult { get; set; }
        public int ResumeCalls { get; private set; }
        public int MarkCalls { get; private set; }
        public Action? OnResume { get; set; }
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<HarvestThrottleSnapshot> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
        public Task SaveRateAsync(int ratePerMinute, DateTimeOffset now, CancellationToken cancellationToken = default) { SavedRates.Add(ratePerMinute); Snapshot = Snapshot with { MaxRequestsPerMinute = ratePerMinute, UpdatedUtc = now }; return Task.CompletedTask; }
        public Task MarkRateLimitedAsync(DateTimeOffset now, CancellationToken cancellationToken = default) { MarkCalls++; return Task.CompletedTask; }
        public Task<bool> ResumeAfterRateLimitAsync(DateTimeOffset now, CancellationToken cancellationToken = default) { ResumeCalls++; OnResume?.Invoke(); return Task.FromResult(ResumeResult); }
    }

    private static void DeleteThrottleDatabase(string path)
    {
        if (!File.Exists(path)) return;
        SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
        GC.Collect(); GC.WaitForPendingFinalizers(); File.Delete(path);
    }

    [Fact]
    public async Task HarvestHealthStrip_RendersValuesAndUnknownDatabaseSize()
    {
        var known = new HarvestStatsPayload(1234, 0, 56, 78, 0, Array.Empty<HarvestRunRow>(), 2048, null, null, null, null, new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false));
        var unknown = known with { DatabaseSizeBytes = null };

        var knownHtml = await RenderPartialViewAsync("_HarvestHealthStrip", known);
        var unknownHtml = await RenderPartialViewAsync("_HarvestHealthStrip", unknown);

        Assert.Contains("health-processed-decks", knownHtml, StringComparison.Ordinal);
        Assert.Contains("1,234", knownHtml, StringComparison.Ordinal);
        Assert.Contains("health-queued-decks", knownHtml, StringComparison.Ordinal);
        Assert.Contains("56", knownHtml, StringComparison.Ordinal);
        Assert.Contains("health-distinct-commanders", knownHtml, StringComparison.Ordinal);
        Assert.Contains("78", knownHtml, StringComparison.Ordinal);
        Assert.Contains("health-database-size", knownHtml, StringComparison.Ordinal);
        Assert.Contains("2 KB", knownHtml, StringComparison.Ordinal);
        Assert.Contains("&#x2014;", unknownHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HarvestBacklogReason.AboveFloor, "Backlog exceeds floor", "configured floor 100")]
    [InlineData(HarvestBacklogReason.Growing, "Backlog growing", "3 consecutive runs")]
    [InlineData(HarvestBacklogReason.AboveFloorAndGrowing, "Backlog exceeds floor and growing", "grew for 3 consecutive runs")]
    public async Task HarvestHealthStrip_FlaggedReason_RendersReasonAndThreshold(
        HarvestBacklogReason reason,
        string expectedText,
        string expectedTitle)
    {
        var payload = new HarvestStatsPayload(0, 0, 0, 0, 0, Array.Empty<HarvestRunRow>(), null, null, null, null, null, new HarvestHealthSignals(true, reason, 100, 3, 0, false));

        var html = await RenderPartialViewAsync("_HarvestHealthStrip", payload);

        Assert.Contains("health-backlog-flag", html, StringComparison.Ordinal);
        Assert.Contains(expectedText, html, StringComparison.Ordinal);
        Assert.Contains(expectedTitle, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestHealthStrip_NoneReason_DoesNotRenderBacklogBadge()
    {
        var payload = new HarvestStatsPayload(0, 0, 0, 0, 0, Array.Empty<HarvestRunRow>(), null, null, null, null, null, new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false));

        var html = await RenderPartialViewAsync("_HarvestHealthStrip", payload);

        Assert.DoesNotContain("health-backlog-flag", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, false, 3, "Discovering new decks", false)]
    [InlineData(2, false, 3, "Zero-discovery streak: 2", false)]
    [InlineData(3, true, 3, "Zero-discovery streak: 3&#x2B;", true)]
    [InlineData(2, false, 2, "Zero-discovery streak: 2", true)]
    public async Task HarvestZeroDiscovery_RendersExpectedStreakState(
        int streak,
        bool capped,
        int threshold,
        string expectedText,
        bool expectsWarning)
    {
        var signals = new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, threshold, streak, capped);

        var html = await RenderPartialViewAsync("_HarvestZeroDiscovery", signals);

        Assert.Contains("harvest-zero-discovery", html, StringComparison.Ordinal);
        Assert.Contains(expectsWarning ? "Zero-discovery streak:" : expectedText, html, StringComparison.Ordinal);
        Assert.Equal(expectsWarning, html.Contains("admin-badge--warning", StringComparison.Ordinal));
        if (expectsWarning)
        {
            var badgeContent = Regex.Escape(streak.ToString(CultureInfo.InvariantCulture))
                + (capped ? Regex.Escape("&#x2B;") : string.Empty);
            Assert.Matches(@"admin-badge--alert\"">\s*" + badgeContent + @"\s*</span>", html);
        }
    }

    private static async Task<string> RenderPartialViewAsync(string viewName, object model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(serviceProvider => serviceProvider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(serviceProvider => serviceProvider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddControllersWithViews().AddApplicationPart(typeof(AdminHarvestController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
        };

        var routeData = new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "AdminHarvest" }));
        routeData.Routers.Add(new TestRouter());
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, viewName, isMainPage: false);
        Assert.True(viewResult.Success, $"View '{viewName}' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View!,
            viewData,
            new TempDataDictionary(httpContext, new StubTempDataProvider()),
            writer,
            new HtmlHelperOptions());

        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment()
    {
        var contentRoot = AppContext.BaseDirectory;
        var fileProvider = new NullFileProvider();
        return new TestWebHostEnvironment
        {
            ApplicationName = typeof(AdminHarvestController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = contentRoot,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = contentRoot,
            WebRootFileProvider = fileProvider,
        };
    }

    private sealed class TestRouter : IRouter
    {
        public VirtualPathData? GetVirtualPath(VirtualPathContext context)
        {
            var action = context.Values["action"]?.ToString();
            return action is null ? null : new VirtualPathData(this, $"Admin/Harvest/{action}");
        }

        public Task RouteAsync(RouteContext context) => Task.CompletedTask;
    }

    private sealed class StubArchidektCacheJobService : IArchidektCacheJobService
    {
        public List<(HarvestRunKind Kind, TimeSpan Duration, HarvestTriggerSource Trigger)> Requests { get; } = [];

        public bool StartedNewJob { get; set; } = true;

        public Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(HarvestRunKind kind, TimeSpan duration, HarvestTriggerSource trigger, CancellationToken cancellationToken = default)
        {
            Requests.Add((kind, duration, trigger));
            var job = new ArchidektCacheJobStatus(Guid.NewGuid(), ArchidektCacheJobState.Queued, (int)duration.TotalSeconds, DateTimeOffset.UtcNow, null, null, 0, 0, null);
            return Task.FromResult(new ArchidektCacheJobEnqueueResult(job, StartedNewJob));
        }

        public ArchidektCacheJobStatus? GetJob(Guid jobId) => null;

        public ArchidektCacheJobStatus? GetActiveJob() => null;

        public Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class StubHarvestRunStore : IHarvestRunStore
    {
        public List<(HarvestRunKind Kind, HarvestTriggerSource? TriggerSource)> Inserted { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid> InsertQueuedAsync(HarvestRunKind kind, int durationSeconds, string? url, DateTimeOffset now, HarvestTriggerSource? triggerSource, CancellationToken cancellationToken = default)
        {
            Inserted.Add((kind, triggerSource));
            return Task.FromResult(Guid.NewGuid());
        }

        public Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int? decksProcessed, int? additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateProgressAsync(Guid id, int decksProcessed, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SetSweepCountsAsync(Guid id, int decksEnqueued, int decksDrained, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetUpdateCountsAsync(Guid id, int pagesPolled, int refreshesRequeued, int refreshesDrained, int newIdsSeen, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<HarvestRunRow?>(null);

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentHealthSignalRunsAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestRunRow>>(Array.Empty<HarvestRunRow>());

        public Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("0");

        public Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(null);

        public Task<HarvestFailureStreak> GetFailureStreakSinceLastSuccessAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HarvestFailureStreak(0, null, null));

        public Task<HarvestFailureStreak> GetFailureStreakSinceLastSuccessAsync(HarvestRunKind kind, CancellationToken cancellationToken = default)
            => Task.FromResult(new HarvestFailureStreak(0, null, null));

        public Task<DateTimeOffset?> GetLastScheduledSuccessUtcAsync(HarvestRunKind kind, CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(null);

        public Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0L);
    }

    private sealed class StubHarvestScheduleStore : IHarvestScheduleStore
    {
        public int SaveCount { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HarvestScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultSchedule);

        public Task SaveAsync(int? intervalHours, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubHarvestScheduleCache : IHarvestScheduleCache
    {
        public int ReloadCount { get; private set; }

        public HarvestScheduleSnapshot Snapshot() => DefaultSchedule;

        public Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            ReloadCount++;
            return Task.CompletedTask;
        }
        public void ForcePausedSnapshot() { /* Why: these tests never drive a rate-limit trip. */ }
    }

    private sealed class StubHarvestUpdateScheduleStore : IHarvestUpdateScheduleStore
    {
        public List<(int? IntervalMinutes, bool Paused)> Saves { get; } = new();

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<HarvestUpdateScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(DefaultUpdateSchedule);
        public Task SaveAsync(int? intervalMinutes, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            Saves.Add((intervalMinutes, paused));
            return Task.CompletedTask;
        }
    }

    private sealed class StubHarvestUpdateScheduleCache : IHarvestUpdateScheduleCache
    {
        public HarvestUpdateScheduleSnapshot Current { get; set; } = DefaultUpdateSchedule;
        public int ReloadCount { get; private set; }
        public HarvestUpdateScheduleSnapshot Snapshot() => Current;
        public Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            ReloadCount++;
            return Task.CompletedTask;
        }
        public void ForcePausedSnapshot() { /* Why: these tests never drive a rate-limit trip. */ }
    }

    private sealed class StubHarvestStatsAggregator : IHarvestStatsAggregator
    {
        public Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HarvestStatsPayload(
                0,
                0,
                0,
                0,
                0,
                Array.Empty<HarvestRunRow>(),
                null,
                null,
                null,
                null,
                null,
                new HarvestHealthSignals(false, HarvestBacklogReason.None, 100, 3, 0, false)));

        public void Invalidate()
        {
        }
    }

    private sealed class StubArchidektDeckImporter : IArchidektDeckImporter
    {
        public List<DeckEntry> Entries { get; set; } = new();

        public ArchidektDeckMetadata? Metadata { get; set; }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries);

        public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => new(await ImportAsync(urlOrDeckId, cancellationToken), Metadata);
    }

    [Fact]
    public async Task CommanderCategories_SameOrigin_ReturnsTypedPartial()
    {
        var result = await Build(NewStore(0)).CommanderCategories("Krenko, Mob Boss");
        var view = Assert.IsType<PartialViewResult>(result);
        var model = Assert.IsType<CommanderCategoryBreakdownViewModel>(view.Model);
        Assert.Equal("_CommanderCategoryBreakdown", view.ViewName);
        Assert.Equal("Krenko, Mob Boss", model.CommanderName);
    }

    [Fact]
    public async Task CommanderCategories_CrossOrigin_Returns403WithoutLookup()
    {
        var service = new CountingCommanderCategoryService();
        var result = await Build(NewStore(0), crossOrigin: true, commanderCategoryService: service).CommanderCategories("Krenko");
        AssertForbidden(result);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task CommanderCategories_SkipsProcessedDeckCount()
    {
        var service = new CountingCommanderCategoryService();
        await Build(NewStore(0), commanderCategoryService: service).CommanderCategories("Krenko");
        Assert.False(service.IncludeProcessedDeckCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CommanderCategories_BlankName_Returns400WithoutLookup(string? name)
    {
        var service = new CountingCommanderCategoryService();
        var result = await Build(NewStore(0), commanderCategoryService: service).CommanderCategories(name);
        Assert.IsAssignableFrom<BadRequestResult>(result);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task CommanderCategories_UsesExistingAggregationFiltersAndCollapsesCategories()
    {
        var store = NewStore(0);
        store.CommanderDeckCount = 40;
        store.CategoryRowsResult = [new CategoryKnowledgeRow("Ramp", "Bird", 4), new CategoryKnowledgeRow("Ramp", "Elf", 2), new CategoryKnowledgeRow("Junk", "Thing", 1)];
        store.Memberships.AddRange([new CategoryDeckMembership("Ramp", "Bird", 1), new CategoryDeckMembership("Ramp", "Elf", 2), new CategoryDeckMembership("Ramp", "Other", 3), new CategoryDeckMembership("Junk", "Thing", 4)]);
        var model = Assert.IsType<CommanderCategoryBreakdownViewModel>(Assert.IsType<PartialViewResult>(await Build(store).CommanderCategories("Krenko")).Model);
        Assert.Single(model.Summaries);
        Assert.Equal("Ramp", model.Summaries[0].Category);
    }

    [Fact]
    public async Task CommanderCategories_EmptyObservations_ReturnsPartial()
    {
        var result = await Build(NewStore(0)).CommanderCategories("Nobody");
        Assert.Empty(Assert.IsType<CommanderCategoryBreakdownViewModel>(Assert.IsType<PartialViewResult>(result).Model).Summaries);
    }

    [Fact]
    public async Task CommanderCategories_ProjectsCommanderDeckCount()
    {
        var store = NewStore(0);
        store.CommanderDeckCount = 40;
        var model = Assert.IsType<CommanderCategoryBreakdownViewModel>(Assert.IsType<PartialViewResult>(await Build(store).CommanderCategories("Krenko")).Model);
        Assert.Equal(40, model.CommanderDeckCount);
    }

    [Fact]
    public async Task CommanderCategories_PartialRendersRowsHeadersAndRoundedShares()
    {
        var html = await RenderPartialViewAsync("_CommanderCategoryBreakdown", new CommanderCategoryBreakdownViewModel { CommanderName = "Krenko", CommanderDeckCount = 40, Summaries = [new CommanderCategorySummary("Ramp", 1, 5, .126), new CommanderCategorySummary("Card Draw", 1, 20, .5)] });
        Assert.Contains("Category", html, StringComparison.Ordinal); Assert.Contains("% of decks", html, StringComparison.Ordinal); Assert.Contains("Decks", html, StringComparison.Ordinal);
        var body = Regex.Match(html, "<tbody>([\\s\\S]*?)</tbody>").Groups[1].Value;
        Assert.Equal(2, Regex.Matches(body, "<tr>").Count);
        Assert.Matches("<td>Ramp</td>[\\s\\S]*?<td>13%</td>[\\s\\S]*?<td>5</td>", body);
        Assert.Matches("<td>Card Draw</td>[\\s\\S]*?<td>50%</td>[\\s\\S]*?<td>20</td>", body);
    }

    [Fact]
    public async Task CommanderCategories_PartialRendersDeckCountBeforeTable()
    {
        var html = await RenderPartialViewAsync("_CommanderCategoryBreakdown", new CommanderCategoryBreakdownViewModel { CommanderDeckCount = 40, Summaries = [new CommanderCategorySummary("Ramp", 1, 5, .1)] });
        Assert.True(html.IndexOf("40", StringComparison.Ordinal) < html.IndexOf("<table", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CommanderCategories_PartialShareMatchesPublicPageUnderInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            var html = await RenderPartialViewAsync("_CommanderCategoryBreakdown", new CommanderCategoryBreakdownViewModel { CommanderDeckCount = 40, Summaries = [new CommanderCategorySummary("Ramp", 1, 5, .125)] });
            Assert.Contains("<td>12%</td>", html, StringComparison.Ordinal);
            Assert.DoesNotContain(" %</td>", html, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    [Fact]
    public async Task CommanderCategories_PartialEmptyStateHasNoTable()
    {
        var html = await RenderPartialViewAsync("_CommanderCategoryBreakdown", new CommanderCategoryBreakdownViewModel());
        Assert.Contains("class=\"admin-empty\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommanderCategories_PartialEscapesCommanderName()
    {
        var html = await RenderPartialViewAsync("_CommanderCategoryBreakdown", new CommanderCategoryBreakdownViewModel { CommanderName = "A < B" });
        Assert.Contains("A &lt; B", html, StringComparison.Ordinal);
        Assert.DoesNotContain("A < B", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommanderCategories_PreservesDoubleFacedName()
    {
        const string name = "Esika, God of the Tree // The Prismatic Bridge";
        var model = Assert.IsType<CommanderCategoryBreakdownViewModel>(Assert.IsType<PartialViewResult>(await Build(NewStore(0)).CommanderCategories(name)).Model);
        Assert.Equal(name, model.CommanderName);
    }

    [Fact]
    public void CommanderCategories_UsesQueryNameRouteContract()
    {
        var method = typeof(AdminHarvestController).GetMethod(nameof(AdminHarvestController.CommanderCategories))!;
        Assert.Equal("commander-categories", method.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>().Single().Template);
        Assert.IsType<FromQueryAttribute>(method.GetParameters()[0].GetCustomAttributes(typeof(FromQueryAttribute), false).Single());
    }

    private sealed class CountingCommanderCategoryService : ICommanderCategoryService
    {
        public int CallCount { get; private set; }
        public bool IncludeProcessedDeckCount { get; private set; }
        public Task<CommanderCategoryResult> LookupAsync(string commanderName, CancellationToken cancellationToken = default, bool includeProcessedDeckCount = true)
        {
            CallCount++;
            IncludeProcessedDeckCount = includeProcessedDeckCount;
            return Task.FromResult(new CommanderCategoryResult(
                commanderName,
                Array.Empty<CategoryKnowledgeRow>(),
                Array.Empty<CommanderCategorySummary>(),
                0,
                new CardDeckTotals(0, new Dictionary<string, int>())));
        }
    }

    private static HarvestScheduleSnapshot DefaultSchedule
        => new(null, Paused: false, DateTimeOffset.MinValue);

    private static HarvestUpdateScheduleSnapshot DefaultUpdateSchedule
        => new(null, Paused: false, DateTimeOffset.MinValue);

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }
}
