using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Integration;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="AdminYoutubeExportController"/>: the export POST must reject a
/// cross-origin request with 403 before any YouTube call, validate the channel input,
/// and return the plain-text listing as a download on success.
/// </summary>
public sealed class AdminYoutubeExportControllerTests
{
    [Fact]
    public async Task Export_CrossOrigin_Returns403_WithoutCallingYouTube()
    {
        var lister = new FakeLister([]);
        var controller = Build(lister, crossOrigin: true);

        var result = await controller.Export("@salubrioussnail", 10, default);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        Assert.Equal(0, lister.Calls);
    }

    [Fact]
    public async Task Export_BlankChannel_RendersErrorWithoutCallingYouTube()
    {
        var lister = new FakeLister([]);
        var controller = Build(lister, crossOrigin: false);

        var result = await controller.Export("   ", 10, default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminYoutubeExportViewModel>(view.Model);
        Assert.False(string.IsNullOrEmpty(model.ErrorMessage));
        Assert.Equal(0, lister.Calls);
    }

    [Fact]
    public async Task Export_SameOrigin_ReturnsTextFileWithViewsAndDates()
    {
        var lister = new FakeLister(
        [
            Video("vid-1", "A Lukewarm Defense of Sol Ring", 69_454, new DateTimeOffset(2026, 2, 25, 0, 0, 0, TimeSpan.Zero)),
            Video("vid-2", "Rhystic Study and the Dark Side of EDH", 94_498, new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero)),
        ]);
        var controller = Build(lister, crossOrigin: false);

        var result = await controller.Export("@salubrioussnail", 10, default);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/plain; charset=utf-8", file.ContentType);
        Assert.Equal("salubrioussnail-videos.txt", file.FileDownloadName);
        var text = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("Channel: @salubrioussnail", text, StringComparison.Ordinal);
        Assert.Contains("69,454", text, StringComparison.Ordinal);
        Assert.Contains("2026-02-25", text, StringComparison.Ordinal);
        Assert.Contains("https://youtu.be/vid-2", text, StringComparison.Ordinal);
        Assert.Contains("Total listed: 2", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_CsvFormat_ReturnsCsvFileWithHeader()
    {
        var lister = new FakeLister(
        [
            Video("vid-1", "A Lukewarm Defense of Sol Ring", 69_454, new DateTimeOffset(2026, 2, 25, 0, 0, 0, TimeSpan.Zero)),
        ]);
        var controller = Build(lister, crossOrigin: false);

        var result = await controller.Export("@salubrioussnail", 10, "csv", default);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal("salubrioussnail-videos.csv", file.FileDownloadName);
        var text = Encoding.UTF8.GetString(file.FileContents);
        Assert.StartsWith("video_id,title,views,uploaded_utc,url", text, StringComparison.Ordinal);
        Assert.Contains("69454", text, StringComparison.Ordinal);
        Assert.Contains("2026-02-25", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_WithDownloadToken_EchoesCompletionCookieOnFileResponse()
    {
        var lister = new FakeLister([Video("vid-1", "Title", 1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))]);
        var controller = Build(lister, crossOrigin: false);

        var result = await controller.Export("@chan", 10, null, "abc123DEF", default);

        Assert.IsType<FileContentResult>(result);
        var setCookie = controller.HttpContext.Response.Headers.SetCookie.ToString();
        Assert.Contains("yt-export-done=abc123DEF", setCookie, StringComparison.Ordinal);
        Assert.Contains("path=/Admin/YoutubeExport", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_WithMalformedDownloadToken_DoesNotSetCookie()
    {
        var lister = new FakeLister([Video("vid-1", "Title", 1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))]);
        var controller = Build(lister, crossOrigin: false);

        var result = await controller.Export("@chan", 10, null, "evil;Path=/;token", default);

        Assert.IsType<FileContentResult>(result);
        Assert.True(string.IsNullOrEmpty(controller.HttpContext.Response.Headers.SetCookie.ToString()));
    }

    [Fact]
    public async Task Export_NoVideos_RendersError()
    {
        var lister = new FakeLister([]);
        var controller = Build(lister, crossOrigin: false);

        var result = await controller.Export("@empty", 10, default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminYoutubeExportViewModel>(view.Model);
        Assert.Contains("No videos", model.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_ListerThrows_RendersErrorBanner()
    {
        var lister = new FakeLister([]) { Exception = new InvalidOperationException("channel not found") };
        var controller = Build(lister, crossOrigin: false);

        var result = await controller.Export("@broken", 10, default);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminYoutubeExportViewModel>(view.Model);
        Assert.Contains("channel not found", model.ErrorMessage, StringComparison.Ordinal);
    }

    private static AdminYoutubeExportController Build(FakeLister lister, bool crossOrigin)
    {
        var controller = new AdminYoutubeExportController(lister, NullLogger<AdminYoutubeExportController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("deckflow.test");
        httpContext.Request.Headers.Origin = crossOrigin ? "https://evil.test" : "https://deckflow.test";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static YouTubeChannelVideo Video(string id, string title, long views, DateTimeOffset published)
        => new()
        {
            VideoId = id,
            Url = "https://youtu.be/" + id,
            Title = title,
            Duration = TimeSpan.FromMinutes(20),
            PublishedUtc = published,
            ViewCount = views,
        };

    private sealed class FakeLister : IYouTubeChannelVideoLister
    {
        private readonly IReadOnlyList<YouTubeChannelVideo> _videos;

        public FakeLister(IReadOnlyList<YouTubeChannelVideo> videos)
        {
            _videos = videos;
        }

        public Exception? Exception { get; init; }

        public int Calls { get; private set; }

        public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(string channelUrl, int limit, int skip = 0, CancellationToken ct = default)
        {
            Calls++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(_videos);
        }

        public Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(IReadOnlyList<string> videoIds, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
