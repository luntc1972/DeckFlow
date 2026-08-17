using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Controllers.Api;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="SuggestionsApiController"/> covering card and deck suggestion endpoints,
/// input validation, and service error handling.
/// </summary>
public sealed class SuggestionsApiControllerTests
{
    [Theory]
    [InlineData(nameof(SuggestionsApiController.PostCardSuggestionAsync), "tool.categories.enabled")]
    [InlineData(nameof(SuggestionsApiController.PostCommanderSuggestionAsync), "tool.commander-categories.enabled")]
    [InlineData(nameof(SuggestionsApiController.PostMechanicLookupAsync), "tool.mechanic-lookup.enabled")]
    public void Tool_backing_actions_use_expected_feature_flag_gates(string actionName, string expectedFlagKey)
    {
        var method = typeof(SuggestionsApiController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        var gate = method!.GetCustomAttribute<FeatureFlagGateAttribute>();
        Assert.NotNull(gate);
        Assert.Equal(expectedFlagKey, gate!.Key);
    }

    [Fact]
    public async Task PostCardSuggestionAsync_ReturnsBadRequest_WhenCardNameMissing()
    {
        var controller = CreateController(
            new FakeCategorySuggestionService(CategorySuggestionResult.Empty("")),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCardSuggestionAsync(new CategorySuggestionRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task PostCardSuggestionAsync_ReturnsStructuredResponse()
    {
        var result = new CategorySuggestionResult(
            "Guardian Project",
            Array.Empty<string>(),
            new[] { "Draw", "Ramp" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            new Dictionary<string, int>(StringComparer.Ordinal),
            new CardDeckTotals(3, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["mainboard"] = 3 }),
            new[] { "cached store" },
            false);

        var controller = CreateController(
            new FakeCategorySuggestionService(result),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCardSuggestionAsync(new CategorySuggestionRequest
        {
            CardName = "Guardian Project"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<CategorySuggestionApiResponse>(ok.Value);
        Assert.Equal("Guardian Project", payload.CardName);
        Assert.True(payload.HasInferredCategories);
    }

    [Fact]
    public async Task PostCardSuggestionAsync_ReturnsWeightedCategoriesInDisplayOrderWithUnavailableCountsNull()
    {
        var result = new CategorySuggestionResult(
            "Guardian Project",
            ["Card Draw", "Ramp"],
            ["Draw", "Protection"],
            ["Protection"],
            ["Protection", "Tutor"],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["draw"] = 70,
                ["protection"] = 120,
                ["ramp"] = 30
            },
            new CardDeckTotals(60, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["mainboard"] = 60
            }),
            ["reference deck", "cached store", "EDHREC", "Scryfall Tagger"],
            false);

        var controller = CreateController(
            new FakeCategorySuggestionService(result),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCardSuggestionAsync(new CategorySuggestionRequest
        {
            CardName = "Guardian Project"
        }, CancellationToken.None);

        var payload = Assert.IsType<CategorySuggestionApiResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Collection(
            payload.WeightedCategories,
            row =>
            {
                Assert.Equal("Protection", row.Category);
                Assert.Equal(120, row.DeckCount);
                Assert.Equal(100, row.Percent);
                Assert.Equal(3, row.SourceCount);
                Assert.Equal(4, row.SourceTotal);
            },
            row =>
            {
                Assert.Equal("Draw", row.Category);
                Assert.Equal(70, row.DeckCount);
                Assert.Equal(100, row.Percent);
            },
            row =>
            {
                Assert.Equal("Ramp", row.Category);
                Assert.Equal(30, row.DeckCount);
                Assert.Equal(50, row.Percent);
            },
            row =>
            {
                Assert.Equal("Tutor", row.Category);
                Assert.Null(row.DeckCount);
                Assert.Null(row.Percent);
                Assert.Equal(1, row.SourceCount);
                Assert.Equal(4, row.SourceTotal);
            });
    }

    [Fact]
    public async Task PostCardSuggestionAsync_MergedCopyTextFollowsWeightedTableOrder()
    {
        var result = new CategorySuggestionResult(
            "Guardian Project",
            Array.Empty<string>(),
            Array.Empty<string>(),
            ["Ramp"],
            ["Protection", "Draw"],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["draw"] = 6,
                ["ramp"] = 30
            },
            new CardDeckTotals(60, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["mainboard"] = 60
            }),
            ["EDHREC", "Scryfall Tagger"],
            false);

        var controller = CreateController(
            new FakeCategorySuggestionService(result),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCardSuggestionAsync(new CategorySuggestionRequest
        {
            CardName = "Guardian Project"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<CategorySuggestionApiResponse>(ok.Value);
        var mergedCategoriesText = Assert.IsType<string>(payload.MergedCategoriesText);
        Assert.Equal("- Ramp" + Environment.NewLine + "- Draw" + Environment.NewLine + "- Protection", mergedCategoriesText);
        Assert.Equal(
            payload.WeightedCategories.Select(row => row.Category),
            mergedCategoriesText.Split(Environment.NewLine).Select(line => line[2..]));
    }

    [Fact]
    public async Task PostCardSuggestionAsync_ReturnsTaggerFields()
    {
        var result = new CategorySuggestionResult(
            "Esper Sentinel",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "Protection", "Value" },
            new Dictionary<string, int>(StringComparer.Ordinal),
            CardDeckTotals.Empty,
            new[] { "Scryfall Tagger" },
            false);

        var controller = CreateController(
            new FakeCategorySuggestionService(result),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCardSuggestionAsync(new CategorySuggestionRequest
        {
            CardName = "Esper Sentinel",
            Mode = CategorySuggestionMode.ScryfallTagger
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<CategorySuggestionApiResponse>(ok.Value);
        Assert.True(payload.HasTaggerCategories);
        Assert.Contains("Protection", payload.TaggerCategoriesText);
        Assert.Equal("These are community-curated functional tags from Scryfall Tagger.", payload.TaggerSuggestionContextText);
        Assert.Equal("Source used: Scryfall Tagger", payload.SuggestionSourceSummary);
    }

    [Fact]
    public async Task PostCommanderSuggestionAsync_ReturnsBadRequest_WhenCommanderMissing()
    {
        var controller = CreateController(
            new FakeCategorySuggestionService(CategorySuggestionResult.Empty("")),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCommanderSuggestionAsync(new CommanderCategoryRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task PostCommanderSuggestionAsync_ReturnsStructuredResponse()
    {
        var result = new CommanderCategoryResult(
            "Bello",
            new[] { new CategoryKnowledgeRow("Ramp", "Birds of Paradise", 2) },
            new[] { new CommanderCategorySummary("Ramp", 2, 2, 0.5d) },
            8,
            new CardDeckTotals(4, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["commander"] = 4 }));

        var controller = CreateController(
            new FakeCategorySuggestionService(CategorySuggestionResult.Empty("")),
            new FakeCommanderCategoryService(result),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCommanderSuggestionAsync(new CommanderCategoryRequest
        {
            CommanderName = "Bello"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<CommanderCategoryApiResponse>(ok.Value);
        Assert.Equal("Bello", payload.CommanderName);
        Assert.Equal(1, payload.CategoryCount);
    }

    [Fact]
    public async Task PostCommanderSuggestionAsync_ReturnsCachedDataMessage_WhenNoResults()
    {
        var controller = CreateController(
            new FakeCategorySuggestionService(CategorySuggestionResult.Empty("")),
            new FakeCommanderCategoryService(EmptyCommanderResult() with { CommanderName = "Bello" }),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCommanderSuggestionAsync(new CommanderCategoryRequest
        {
            CommanderName = "Bello"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<CommanderCategoryApiResponse>(ok.Value);
        Assert.Equal("No commander categories for Bello have been observed in the cached data yet.", payload.NoResultsMessage);
    }

    [Fact]
    public async Task PostCardSuggestionAsync_ReturnsSiteSpecificMessage_WhenUpstreamRequestFails()
    {
        var controller = CreateController(
            new ThrowingCategorySuggestionService(new HttpRequestException("EDHREC returned HTTP 503.", null, System.Net.HttpStatusCode.ServiceUnavailable)),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCardSuggestionAsync(new CategorySuggestionRequest
        {
            CardName = "Sol Ring"
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        var message = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value) as string;
        Assert.Equal("EDHREC returned HTTP 503. Try again shortly.", message);
    }

    [Fact]
    public async Task PostCardSuggestionAsync_ReturnsServiceUnavailable_WhenDatabaseLookupFails()
    {
        var controller = CreateController(
            new ThrowingCategorySuggestionService(new TestDbException("read timed out")),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostCardSuggestionAsync(new CategorySuggestionRequest
        {
            CardName = "Sol Ring"
        }, CancellationToken.None);

        var serviceUnavailable = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, serviceUnavailable.StatusCode);
        var message = serviceUnavailable.Value?.GetType().GetProperty("Message")?.GetValue(serviceUnavailable.Value) as string;
        Assert.Equal("Category lookup is temporarily unavailable, please try again.", message);
    }

    [Fact]
    public async Task PostMechanicLookupAsync_ReturnsStructuredResponse()
    {
        var controller = CreateController(
            new FakeCategorySuggestionService(CategorySuggestionResult.Empty("")),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(new MechanicLookupResult(
                "Prowess",
                true,
                "Prowess",
                "702.108",
                "Exact rules section",
                "702.108. Prowess",
                "A keyword ability that causes a creature to get +1/+1 whenever its controller casts a noncreature spell.",
                "https://magic.wizards.com/en/rules",
                "https://media.wizards.com/2026/downloads/MagicCompRules%2020260227.txt")),
            NullLogger<SuggestionsApiController>.Instance);

        var response = await controller.PostMechanicLookupAsync(new MechanicLookupRequest
        {
            MechanicName = "Prowess"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<MechanicLookupApiResponse>(ok.Value);
        Assert.True(payload.Found);
        Assert.Equal("Prowess", payload.MechanicName);
        Assert.Equal("702.108", payload.RuleReference);
    }

    [Fact]
    public async Task PostCardSuggestionAsync_ReturnsForbidden_WhenOriginIsCrossSite()
    {
        var controller = new SuggestionsApiController(
            new FakeCategorySuggestionService(CategorySuggestionResult.Empty("")),
            new FakeCommanderCategoryService(EmptyCommanderResult()),
            new FakeMechanicLookupService(MechanicLookupResult.NotFound("", "https://magic.wizards.com/en/rules", null)),
            NullLogger<SuggestionsApiController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("deckflow.test");
        controller.Request.Headers.Origin = "https://evil.test";

        var response = await controller.PostCardSuggestionAsync(new CategorySuggestionRequest
        {
            CardName = "Sol Ring"
        }, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private static SuggestionsApiController CreateController(
        ICategorySuggestionService categorySuggestionService,
        ICommanderCategoryService commanderCategoryService,
        IMechanicLookupService mechanicLookupService,
        ILogger<SuggestionsApiController> logger)
    {
        var controller = new SuggestionsApiController(categorySuggestionService, commanderCategoryService, mechanicLookupService, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("deckflow.test");
        controller.Request.Headers.Origin = "https://deckflow.test";
        return controller;
    }

    private static CommanderCategoryResult EmptyCommanderResult()
        => new("", Array.Empty<CategoryKnowledgeRow>(), Array.Empty<CommanderCategorySummary>(), 0, CardDeckTotals.Empty);

    private sealed class FakeCategorySuggestionService : ICategorySuggestionService
    {
        private readonly CategorySuggestionResult _result;

        public FakeCategorySuggestionService(CategorySuggestionResult result)
        {
            _result = result;
        }

        public Task<CategorySuggestionResult> SuggestAsync(CategorySuggestionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class FakeCommanderCategoryService : ICommanderCategoryService
    {
        private readonly CommanderCategoryResult _result;

        public FakeCommanderCategoryService(CommanderCategoryResult result)
        {
            _result = result;
        }

        public Task<CommanderCategoryResult> LookupAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class ThrowingCategorySuggestionService : ICategorySuggestionService
    {
        private readonly Exception _exception;

        public ThrowingCategorySuggestionService(Exception exception)
        {
            _exception = exception;
        }

        public Task<CategorySuggestionResult> SuggestAsync(CategorySuggestionRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<CategorySuggestionResult>(_exception);
    }

    private sealed class TestDbException : DbException
    {
        public TestDbException(string message)
            : base(message)
        {
        }
    }

    private sealed class FakeMechanicLookupService : IMechanicLookupService
    {
        private readonly MechanicLookupResult _result;

        public FakeMechanicLookupService(MechanicLookupResult result)
        {
            _result = result;
        }

        public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
            => Task.FromResult(_result with { Query = mechanicName });
    }
}
