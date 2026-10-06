using System.Net;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Controllers.Api;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies rate-limit failures from interactive Archidekt imports receive the existing upstream response.
/// </summary>
// Why (D-10): interactive imports share the limiter without exemption; its HttpRequestException base must not become a 500.
[Collection("ArchidektThrottleSerial")]
public sealed class ArchidektRateLimitInteractivePathTests : IDisposable
{
    public ArchidektRateLimitInteractivePathTests()
    {
        ArchidektThrottle.ResetForTests();
    }

    [Fact]
    public async Task DeckSyncCompare_ArchidektLimiterTrip_ReturnsUpstreamMessageNot500()
    {
        var stub = new StubHttpMessageHandler();
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
        stub.Enqueue(response);
        using var httpClient = new HttpClient(stub) { BaseAddress = new Uri("https://archidekt.com/") };
        using var restClient = new RestClient(httpClient);
        var importer = new CapturingArchidektDeckImporter(new ArchidektApiDeckImporter(restClient));
        var loader = new DeckEntryLoader(new UnusedMoxfieldDeckImporter(), importer, new MoxfieldParser(), new ArchidektParser());
        var controller = new DeckSyncApiController(new DeckSyncService(loader), NullLogger<DeckSyncApiController>.Instance);
        SetSameOriginHeaders(controller);
        var request = new DeckSyncApiRequest
        {
            Direction = SyncDirection.MoxfieldToArchidekt,
            MoxfieldInputSource = DeckInputSource.PasteText,
            MoxfieldText = "1 Sol Ring",
            ArchidektInputSource = DeckInputSource.PublicUrl,
            ArchidektUrl = "https://archidekt.com/decks/123/test",
        };

        var result = await controller.PostDiffAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var message = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value) as string;
        var captured = Assert.IsType<ArchidektRateLimitedException>(importer.Captured);
        Assert.Equal(UpstreamErrorMessageBuilder.BuildDeckSyncMessage(request.ToDeckDiffRequest(), captured), message);
        Assert.Equal("Archidekt returned HTTP 429. Try again shortly.", message);
        Assert.Equal(1, stub.CallCount);
    }

    public void Dispose() => ArchidektThrottle.ResetForTests();

    private static void SetSameOriginHeaders(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("deckflow.test");
        controller.Request.Headers.Origin = "https://deckflow.test";
    }

    private sealed class CapturingArchidektDeckImporter(IArchidektDeckImporter inner) : IArchidektDeckImporter
    {
        public Exception? Captured { get; private set; }

        public async Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await inner.ImportAsync(urlOrDeckId, cancellationToken);
            }
            catch (Exception exception)
            {
                Captured = exception;
                throw;
            }
        }

        public Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => inner.ImportWithMetadataAsync(urlOrDeckId, cancellationToken);
    }

    private sealed class UnusedMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            Assert.Fail("Paste-text input must not import from Moxfield.");
            return Task.FromResult(new List<DeckEntry>());
        }
    }
}
