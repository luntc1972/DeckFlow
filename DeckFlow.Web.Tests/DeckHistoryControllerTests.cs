using System.Text;
using DeckFlow.Core.History;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="DeckHistoryController"/> covering the empty form, request validation,
/// service hand-off, and JSON download behavior.
/// </summary>
public sealed class DeckHistoryControllerTests
{
    [Fact]
    public void Index_ReturnsViewWithDeckHistoryTabActive()
    {
        var controller = CreateController(new FakeDeckHistoryPageService());

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("DeckHistory", view.ViewName);
        var model = Assert.IsType<DeckHistoryViewModel>(view.Model);
        Assert.Equal(DeckPageTab.DeckHistory, model.ActiveTab);
        Assert.NotNull(model.Request);
    }

    [Fact]
    public async Task Process_OversizedFile_ReturnsErrorAndDoesNotCallService()
    {
        var service = new FakeDeckHistoryPageService();
        var controller = CreateController(service);
        var request = new DeckHistoryRequest { DeckName = "Atraxa" };
        var file = CreateFormFile(new byte[DeckHistorySerializer.MaxUploadBytes + 1], "history.json");

        var result = await controller.Process(file, request);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckHistoryViewModel>(view.Model);
        Assert.Equal("History file is too large (limit 1 MB).", model.ErrorMessage);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Process_NonJsonFile_ReturnsExtensionErrorAndDoesNotCallService()
    {
        var service = new FakeDeckHistoryPageService();
        var controller = CreateController(service);
        var request = new DeckHistoryRequest { DeckName = "Atraxa" };
        var file = CreateFormFile(Encoding.UTF8.GetBytes("{}"), "history.txt");

        var result = await controller.Process(file, request);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckHistoryViewModel>(view.Model);
        Assert.Equal("Only .json files produced by Download are accepted.", model.ErrorMessage);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task Process_HappyPath_PassesUploadedJsonToServiceAndReturnsMappedView()
    {
        var service = new FakeDeckHistoryPageService
        {
            Result = new DeckHistoryProcessResult
            {
                File = BuildHistoryFile(),
                SerializedJson = BuildHistoryJson(),
                Appended = true,
                PromptText = "Explain how this deck evolved.",
                Warnings = ["Started a new history - version 1 saved."],
            },
        };
        var controller = CreateController(service);
        var request = new DeckHistoryRequest
        {
            DeckName = "Atraxa Midrange",
            Notes = "Initial import",
            Label = "v1",
            TargetAiPlatform = "ChatGPT",
        };
        var file = CreateFormFile(Encoding.UTF8.GetBytes(BuildHistoryJson()), "history.json");

        var result = await controller.Process(file, request);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckHistoryViewModel>(view.Model);
        Assert.Equal(1, service.CallCount);
        Assert.Same(request, service.LastRequest);
        Assert.Equal(BuildHistoryJson(), service.LastUploadedJson);
        Assert.True(model.HasResult);
        Assert.Equal("Explain how this deck evolved.", model.PromptText);
        Assert.Equal(BuildHistoryJson(), model.HistoryJson);
        Assert.Contains("Started a new history - version 1 saved.", model.Warnings);
    }

    [Fact]
    public void Download_ValidHistoryJson_ReturnsJsonFileAndHeader()
    {
        var controller = CreateController(new FakeDeckHistoryPageService());
        var request = new DeckHistoryRequest
        {
            HistoryJson = BuildHistoryJson(),
        };

        var result = controller.Download(request);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json; charset=utf-8", file.ContentType);
        Assert.NotNull(file.FileDownloadName);
        Assert.Matches(@"^deck-history-[a-z0-9-]+-\d{8}\.json$", file.FileDownloadName);
        Assert.EndsWith($"{DateTime.UtcNow:yyyyMMdd}.json", file.FileDownloadName, StringComparison.Ordinal);
        Assert.Equal(file.FileDownloadName, controller.Response.Headers["X-DeckFlow-Filename"].ToString());
        Assert.Equal(BuildHistoryJson(), Encoding.UTF8.GetString(file.FileContents));
    }

    [Fact]
    public void Download_BlankHistoryJson_ReturnsErrorView()
    {
        var controller = CreateController(new FakeDeckHistoryPageService());

        var result = controller.Download(new DeckHistoryRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DeckHistoryViewModel>(view.Model);
        Assert.Equal("Nothing to download yet — import a deck or upload a history file first.", model.ErrorMessage);
    }

    private static DeckHistoryController CreateController(FakeDeckHistoryPageService service) =>
        new(service, new FakeLogger<DeckHistoryController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

    private static IFormFile CreateFormFile(byte[] bytes, string fileName)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "historyFile", fileName);
    }

    private static string BuildHistoryJson() => DeckHistorySerializer.Serialize(BuildHistoryFile());

    private static DeckHistoryFile BuildHistoryFile() => new()
    {
        DeckName = "Atraxa Midrange",
        Versions =
        [
            new DeckSnapshot
            {
                Id = 1,
                Date = DateTimeOffset.Parse("2026-07-16T12:00:00Z"),
                Label = "v1",
                Notes = "Initial import",
                Commander = ["Atraxa, Praetors' Voice"],
                Cards =
                [
                    new SnapshotCard { Name = "Arcane Signet", Qty = 1 },
                    new SnapshotCard { Name = "Sol Ring", Qty = 1 },
                    new SnapshotCard { Name = "Plains", Qty = 97 },
                ],
                Delta = new SnapshotDelta(),
            },
        ],
    };

    private sealed class FakeDeckHistoryPageService : IDeckHistoryPageService
    {
        public int CallCount { get; private set; }

        public DeckHistoryRequest? LastRequest { get; private set; }

        public string? LastUploadedJson { get; private set; }

        public DeckHistoryProcessResult Result { get; set; } = new();

        public Task<DeckHistoryProcessResult> ProcessAsync(
            DeckHistoryRequest request,
            string? uploadedHistoryJson,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            LastUploadedJson = uploadedHistoryJson;
            return Task.FromResult(Result);
        }
    }
}
