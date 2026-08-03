using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="DeckLookupController"/> card and mechanic lookup actions with faked service dependencies.
/// </summary>
public sealed class DeckLookupControllerTests
{
    [Fact]
    public async Task CardLookup_ReturnsValidationError_WhenCardListMissing()
    {
        var controller = new DeckLookupController(
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DownloadCardLookup(new CardLookupRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardLookupViewModel>(view.Model);
        Assert.Equal("A card list is required.", model.ErrorMessage);
    }

    [Fact]
    public async Task CardLookup_ReturnsValidationError_WhenLineCountExceedsCap()
    {
        var controller = CreateController(out var service);

        var result = await controller.DownloadCardLookup(new CardLookupRequest
        {
            CardList = BuildCardList(101)
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardLookupViewModel>(view.Model);
        Assert.Equal("Card Lookup accepts up to 100 non-empty lines per submission.", model.ErrorMessage);
        // The cap must short-circuit before Scryfall — that is the whole point of it.
        Assert.Equal(0, service.LookupCallCount);
    }

    [Fact]
    public async Task CardLookupJson_ReturnsValidationError_WhenLineCountExceedsCap()
    {
        var controller = CreateController(out _);

        var result = await controller.DownloadCardLookupJson(new CardLookupRequest
        {
            CardList = BuildCardList(101)
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardLookupViewModel>(view.Model);
        Assert.Equal("Card Lookup accepts up to 100 non-empty lines per submission.", model.ErrorMessage);
    }

    // Why: pins the boundary so the cap constant cannot drift without a test failing.
    // Mutating 100 to 99 or 101 in the controller breaks exactly one of these two.
    [Fact]
    public async Task CardLookup_AcceptsExactlyTheCap()
    {
        var controller = CreateController(out _);

        var result = await controller.DownloadCardLookup(new CardLookupRequest
        {
            CardList = BuildCardList(100)
        });

        Assert.IsType<FileContentResult>(result);
    }

    // Mirrors countNonEmptyLines() in card-lookup.ts: blank and whitespace-only
    // lines are not charged against the cap on either side of the wire.
    [Fact]
    public async Task CardLookup_IgnoresBlankLines_WhenApplyingCap()
    {
        var controller = CreateController(out _);
        var padded = string.Join("\n", Enumerable.Range(0, 100).Select(index => $"Card {index}\n   \n"));

        var result = await controller.DownloadCardLookup(new CardLookupRequest
        {
            CardList = padded
        });

        Assert.IsType<FileContentResult>(result);
    }

    private static string BuildCardList(int lineCount) =>
        string.Join("\n", Enumerable.Range(0, lineCount).Select(index => $"Card {index}"));

    private static DeckLookupController CreateController(out FakeCardLookupService service)
    {
        service = new FakeCardLookupService();
        return new DeckLookupController(
            service,
            new FakeMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task CardLookup_ReturnsUserFacingError_WhenScryfallFails()
    {
        var controller = new DeckLookupController(
            new ThrowingCardLookupService(new HttpRequestException("Scryfall search returned HTTP 503.", null, HttpStatusCode.ServiceUnavailable)),
            new FakeMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DownloadCardLookup(new CardLookupRequest
        {
            CardList = "Sol Ring"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardLookupViewModel>(view.Model);
        Assert.Equal("Scryfall returned HTTP 503. Try again shortly.", model.ErrorMessage);
    }

    [Fact]
    public async Task CardLookup_ReturnsValidationMessage_WhenTooManyLinesSubmitted()
    {
        var controller = new DeckLookupController(
            new ThrowingCardLookupService(new InvalidOperationException("Please verify 100 non-empty lines or fewer per submission.")),
            new FakeMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DownloadCardLookup(new CardLookupRequest
        {
            CardList = "Sol Ring"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CardLookupViewModel>(view.Model);
        Assert.Equal("Please verify 100 non-empty lines or fewer per submission.", model.ErrorMessage);
    }

    [Fact]
    public async Task DownloadCardLookup_ReturnsTextFile_WhenVerificationSucceeds()
    {
        var controller = new DeckLookupController(
            new StubSuccessfulCardLookupService(),
            new FakeMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DownloadCardLookup(new CardLookupRequest
        {
            CardList = "Sol Ring"
        });

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/plain; charset=utf-8", fileResult.ContentType);
        var text = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.Contains("Verified Cards", text);
        Assert.Contains("Sol Ring", text);
    }

    [Fact]
    public async Task SingleCardLookup_ReturnsMechanicRules_WhenCardHasDetectedMechanics()
    {
        var controller = new DeckLookupController(
            new StubSuccessfulSingleCardLookupService(),
            new StubSuccessfulMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Monastery Swiftspear");

        var json = Assert.IsType<JsonResult>(result);
        var payload = json.Value!;
        var cardName = payload.GetType().GetProperty("cardName")?.GetValue(payload) as string;
        var verifiedText = payload.GetType().GetProperty("verifiedText")?.GetValue(payload) as string;
        var mechanicRules = payload.GetType().GetProperty("mechanicRules")?.GetValue(payload) as System.Collections.IEnumerable;
        Assert.Equal("Monastery Swiftspear", cardName);
        Assert.Equal("Monastery Swiftspear", verifiedText);
        Assert.NotNull(mechanicRules);
        Assert.Single(mechanicRules!.Cast<object>());
    }

    [Fact]
    public async Task SingleCardLookup_ReturnsNotFound_WhenCardMissing()
    {
        var controller = new DeckLookupController(
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Missing Card");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var message = notFound.Value?.GetType().GetProperty("message")?.GetValue(notFound.Value) as string;
        Assert.Equal("Scryfall could not find \"Missing Card\".", message);
    }

    [Fact]
    public async Task SingleCardLookup_UsesResolvedCardName_WhenLookupFallsBackToAlternatePrintedName()
    {
        var controller = new DeckLookupController(
            new AlternateNameSingleCardLookupService(),
            new StubSuccessfulMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Pastor da Selva");

        var json = Assert.IsType<JsonResult>(result);
        var payload = json.Value!;
        var cardName = payload.GetType().GetProperty("cardName")?.GetValue(payload) as string;
        Assert.Equal("Ancient Greenwarden", cardName);
    }

    [Fact]
    public async Task SingleCardLookup_Continues_WhenOneMechanicLookupFails()
    {
        var controller = new DeckLookupController(
            new MultiMechanicSingleCardLookupService(),
            new PartiallyFailingMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Monastery Swiftspear");

        var json = Assert.IsType<JsonResult>(result);
        var payload = json.Value!;
        var mechanicRules = payload.GetType().GetProperty("mechanicRules")?.GetValue(payload) as System.Collections.IEnumerable;
        Assert.NotNull(mechanicRules);
        Assert.Single(mechanicRules!.Cast<object>());
    }

    [Fact]
    public async Task SingleCardLookup_ReturnsServiceUnavailable_WhenScryfallFails()
    {
        var controller = new DeckLookupController(
            new ThrowingCardLookupService(new HttpRequestException("Scryfall search returned HTTP 503.", null, HttpStatusCode.ServiceUnavailable)),
            new FakeMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.SingleCardLookup("Sol Ring");

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task MechanicLookup_ReturnsValidationError_WhenMechanicMissing()
    {
        var controller = new DeckLookupController(
            new FakeCardLookupService(),
            new FakeMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.MechanicLookup(new MechanicLookupRequest());

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MechanicLookupViewModel>(view.Model);
        Assert.Equal("A mechanic name is required.", model.ErrorMessage);
    }

    [Fact]
    public async Task MechanicLookup_ReturnsRules_WhenMechanicFound()
    {
        var controller = new DeckLookupController(
            new FakeCardLookupService(),
            new StubSuccessfulMechanicLookupService(),
            NullLogger<DeckLookupController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.MechanicLookup(new MechanicLookupRequest
        {
            MechanicName = "Prowess"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<MechanicLookupViewModel>(view.Model);
        Assert.Equal("Prowess", model.MechanicName);
        Assert.Equal("702.108", model.RuleReference);
        Assert.Contains("Prowess", model.RulesText);
    }
}
