using DeckFlow.Core.Parsing;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckConvertControllerTests
{
    [Fact]
    public async Task Convert_ServiceThrowsDeckParseException_ReturnsViewWithErrorMessage()
    {
        const string message = "Unable to parse Archidekt line 5";
        var controller = CreateController(new ThrowingDeckConvertService(new DeckParseException(message)));
        var request = new DeckConvertRequest { InputSource = DeckInputSource.PasteText, DeckText = "1 Sol Ring" };

        var result = await controller.Convert(request);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("DeckConvert", view.ViewName);
        var model = Assert.IsType<DeckConvertViewModel>(view.Model);
        Assert.Equal(message, model.ErrorMessage);
        Assert.Null(model.ConvertedText);
    }

    [Fact]
    public async Task Convert_ServiceThrowsUnrelatedException_Propagates()
    {
        var exception = new NullReferenceException();
        var controller = CreateController(new ThrowingDeckConvertService(exception));
        var request = new DeckConvertRequest { InputSource = DeckInputSource.PasteText, DeckText = "1 Sol Ring" };

        await Assert.ThrowsAsync<NullReferenceException>(() => controller.Convert(request));
    }

    private static DeckConvertController CreateController(IDeckConvertService service)
    {
        var controller = new DeckConvertController(
            service,
            new StubCardSearchService(),
            NullLogger<DeckConvertController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }
}
