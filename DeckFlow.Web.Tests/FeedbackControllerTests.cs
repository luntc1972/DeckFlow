using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class FeedbackControllerTests
{
    [Fact]
    public void Index_Get_ReturnsView()
    {
        var controller = BuildController(out _);
        var result = controller.Index();
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Index_Post_HoneypotFilled_RedirectsSuccess_WithoutStoring()
    {
        var controller = BuildController(out var store);
        var submission = new FeedbackSubmission
        {
            Type = FeedbackType.Bug,
            Message = "Serious problem here please read.",
            Website = "http://bot.example",
        };

        var result = await controller.Index(submission, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(0, store.AddCalls);
        Assert.True(controller.TempData.ContainsKey("FeedbackSuccess"));
    }

    [Fact]
    public async Task Index_Post_InvalidModel_ReturnsView_WithoutStoring()
    {
        var controller = BuildController(out var store);
        controller.ModelState.AddModelError("Message", "too short");
        var submission = new FeedbackSubmission { Type = FeedbackType.Bug, Message = "short" };

        var result = await controller.Index(submission, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public async Task Index_Post_Valid_CallsStore_AndSetsTempData()
    {
        var controller = BuildController(out var store);
        controller.HttpContext.Request.Headers["Referer"] = "https://decksync.test/deck/99";
        controller.HttpContext.Request.Headers["User-Agent"] = "MyAgent/1.0";
        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.33");

        var submission = new FeedbackSubmission
        {
            Type = FeedbackType.Suggestion,
            Message = "A real suggestion with enough chars.",
            Email = "u@example.com",
        };

        var result = await controller.Index(submission, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, store.AddCalls);
        Assert.Equal(FeedbackType.Suggestion, store.LastSubmission!.Type);
        Assert.Equal("https://decksync.test/deck/99", store.LastContext!.PageUrl);
        Assert.Equal("MyAgent/1.0", store.LastContext!.UserAgent);
        Assert.Equal("192.0.2.33", store.LastContext!.Ip);
        Assert.Equal("test-version", store.LastContext!.AppVersion);
        Assert.True(controller.TempData.ContainsKey("FeedbackSuccess"));
    }

    private static FeedbackController BuildController(out FakeFeedbackStore store)
    {
        store = new FakeFeedbackStore();
        var httpContext = new DefaultHttpContext();
        var controller = new FeedbackController(store, new FakeVersionService("test-version"))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider()),
        };
        return controller;
    }

    private sealed class FakeFeedbackStore : IFeedbackStore
    {
        public int AddCalls { get; private set; }
        public FeedbackSubmission? LastSubmission { get; private set; }
        public FeedbackRequestContext? LastContext { get; private set; }

        public Task<long> AddAsync(FeedbackSubmission submission, FeedbackRequestContext context, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            LastSubmission = submission;
            LastContext = context;
            return Task.FromResult(42L);
        }

        public Task<FeedbackItem?> GetAsync(long id, CancellationToken ct = default) => Task.FromResult<FeedbackItem?>(null);
        public Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FeedbackItem>>(Array.Empty<FeedbackItem>());
        public Task<int> CountAsync(FeedbackStatus? status, FeedbackType? type, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyDictionary<FeedbackStatus, int>> CountsByStatusAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<FeedbackStatus, int>>(new Dictionary<FeedbackStatus, int>());
        public Task UpdateStatusAsync(long id, FeedbackStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(long id, CancellationToken ct = default) => Task.CompletedTask;
        public string HashIp(string? ip) => ip ?? string.Empty;
    }

    private sealed class FakeVersionService : IVersionService
    {
        private readonly string _version;
        public FakeVersionService(string v) { _version = v; }
        public string GetVersion() => _version;
    }

    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
