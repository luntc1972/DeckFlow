using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.CutLab;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabController"/> covering the empty form and process error branches.</summary>
public sealed class CutLabControllerTests
{
    [Fact]
    public void Index_ReturnsViewWithCutLabTabActive()
    {
        var controller = CreateController(new FakeCutLabPageService());

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal(DeckPageTab.CutLab, model.ActiveTab);
        Assert.NotNull(model.Request);
    }

    [Fact]
    public async Task Process_HappyPath_ReturnsMappedView()
    {
        var service = new FakeCutLabPageService
        {
            Result = new CutLabProcessResult
            {
                State = new DeckFlow.Web.Models.CutLab.CutLabState(),
                SerializedStateJson = "{\"pool\":[]}",
                CardCount = 120,
                IsLegal = true,
                HasResult = true,
            },
        };
        var controller = CreateController(service);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
        };

        var result = await controller.Process(request);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("CutLab", view.ViewName);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal(1, service.CallCount);
        Assert.Same(request, service.LastRequest);
        Assert.True(model.HasResult);
        Assert.Equal(120, model.CardCount);
        Assert.Equal("{\"pool\":[]}", model.CutLabStateJson);
    }

    [Fact]
    public async Task Process_InvalidOperationException_ReturnsErrorView()
    {
        var controller = CreateController(new ThrowingCutLabPageService(new InvalidOperationException("Bad pool.")));

        var result = await controller.Process(new CutLabRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal("Bad pool.", model.ErrorMessage);
    }

    [Fact]
    public async Task Process_OperationCanceledException_ReturnsTimeoutError()
    {
        var controller = CreateController(new ThrowingCutLabPageService(new OperationCanceledException()));

        var result = await controller.Process(new CutLabRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CutLabViewModel>(view.Model);
        Assert.Equal("The request timed out. Try again.", model.ErrorMessage);
    }

    private static CutLabController CreateController(ICutLabPageService service) =>
        new(service, new FakeLogger<CutLabController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

    private sealed class FakeCutLabPageService : ICutLabPageService
    {
        public int CallCount { get; private set; }

        public CutLabRequest? LastRequest { get; private set; }

        public CutLabProcessResult Result { get; set; } = new();

        public Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class ThrowingCutLabPageService(Exception exception) : ICutLabPageService
    {
        public Task<CutLabProcessResult> ProcessAsync(CutLabRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<CutLabProcessResult>(exception);
    }
}
