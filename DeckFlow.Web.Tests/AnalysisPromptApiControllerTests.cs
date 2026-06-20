using System.Net;
using DeckFlow.Web.Controllers.Api;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers the development-only analysis-prompt API controller: environment gating, same-origin
/// enforcement, input validation, request mapping (WorkflowStep + default questions), and the
/// upstream-exception to 400 mapping.
/// </summary>
public sealed class AnalysisPromptApiControllerTests
{
    [Fact]
    public async Task PostAsync_ReturnsNotFound_OutsideDevelopment()
    {
        var service = new FakePacketService();
        var controller = CreateController(service, environment: Environments.Production);

        var result = await controller.PostAsync(new AnalysisPromptApiRequest { DeckText = "1 Sol Ring" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Null(service.LastRequest); // the service is never reached in Production
    }

    [Fact]
    public async Task PostAsync_ReturnsForbidden_WhenCrossOrigin()
    {
        var service = new FakePacketService();
        var controller = CreateController(service);
        controller.Request.Host = new HostString("deckflow.test");
        controller.Request.Headers.Origin = "https://evil.test";

        var result = await controller.PostAsync(new AnalysisPromptApiRequest { DeckText = "1 Sol Ring" }, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        Assert.Null(service.LastRequest);
    }

    [Fact]
    public async Task PostAsync_ReturnsBadRequest_WhenNoDeckInput()
    {
        var service = new FakePacketService();
        var controller = CreateController(service);

        var result = await controller.PostAsync(new AnalysisPromptApiRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(service.LastRequest);
    }

    [Fact]
    public async Task PostAsync_ReturnsPrompt_AndForcesWorkflowStepTwoWithDefaultQuestions()
    {
        var service = new FakePacketService(BuildResult("PROMPT TEXT"));
        var controller = CreateController(service);

        var result = await controller.PostAsync(new AnalysisPromptApiRequest { DeckText = "1 Sol Ring" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AnalysisPromptApiResponse>(ok.Value);
        Assert.Equal("PROMPT TEXT", response.AnalysisPromptText);
        Assert.Equal("PROMPT TEXT".Length, response.PromptCharacterCount);

        Assert.NotNull(service.LastRequest);
        Assert.Equal(2, service.LastRequest!.WorkflowStep);
        Assert.Equal(DeckInputSource.PasteText, service.LastRequest.DeckInputSource);
        Assert.NotEmpty(service.LastRequest.SelectedAnalysisQuestions); // default set applied
        Assert.Contains("bracket-assessment", service.LastRequest.SelectedAnalysisQuestions);
    }

    [Fact]
    public async Task PostAsync_UsesPublicUrlSource_AndSuppliedQuestions_WhenProvided()
    {
        var service = new FakePacketService(BuildResult("X"));
        var controller = CreateController(service);

        await controller.PostAsync(
            new AnalysisPromptApiRequest
            {
                DeckUrl = "https://moxfield.com/decks/abc",
                SelectedAnalysisQuestions = ["consistency"],
            },
            CancellationToken.None);

        Assert.NotNull(service.LastRequest);
        Assert.Equal(DeckInputSource.PublicUrl, service.LastRequest!.DeckInputSource);
        Assert.Equal(["consistency"], service.LastRequest.SelectedAnalysisQuestions);
    }

    [Fact]
    public async Task PostAsync_MapsValidationFailure_ToBadRequest()
    {
        var service = new FakePacketService(new InvalidOperationException("bad bracket"));
        var controller = CreateController(service);

        var result = await controller.PostAsync(new AnalysisPromptApiRequest { DeckText = "1 Sol Ring" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task PostAsync_MapsUpstreamFailure_ToBadRequest()
    {
        var service = new FakePacketService(new HttpRequestException("scryfall down", null, HttpStatusCode.ServiceUnavailable));
        var controller = CreateController(service);

        var result = await controller.PostAsync(new AnalysisPromptApiRequest { DeckText = "1 Sol Ring" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static AnalysisPromptApiController CreateController(
        IDeckAnalysisPacketService service,
        string environment = "Development")
        => new(service, new StubWebHostEnvironment { EnvironmentName = environment }, NullLogger<AnalysisPromptApiController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

    private static DeckAnalysisPacketResult BuildResult(string promptText)
        => new(
            InputSummary: "input",
            SuggestedChatTitle: "Title",
            DeckProfileSchemaJson: "{}",
            ReferenceText: "ref",
            AnalysisPromptText: promptText,
            SetUpgradePromptText: null,
            RequestContextText: null,
            TimingSummary: null);

    private sealed class FakePacketService : IDeckAnalysisPacketService
    {
        private readonly DeckAnalysisPacketResult? _result;
        private readonly Exception? _throw;

        public FakePacketService(DeckAnalysisPacketResult? result = null) => _result = result;

        public FakePacketService(Exception toThrow) => _throw = toThrow;

        public DeckAnalysisRequest? LastRequest { get; private set; }

        public Task<DeckAnalysisPacketResult> BuildAsync(DeckAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (_throw is not null)
            {
                throw _throw;
            }

            return Task.FromResult(_result ?? BuildResult("default"));
        }

        public Task<string?> TryComputeCacheKeyAsync(DeckAnalysisRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
