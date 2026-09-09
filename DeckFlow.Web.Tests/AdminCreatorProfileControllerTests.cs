using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services.CreatorStyle;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="AdminCreatorProfileController"/> covering form rendering, validation,
/// crawl/build orchestration, and action-boundary error handling (PTOOL-04).
/// </summary>
public sealed class AdminCreatorProfileControllerTests
{
    [Fact]
    public void Index_RendersEmptyForm()
    {
        var controller = Build();

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminCreatorProfileViewModel>(view.Model);
        Assert.Equal("archidekt", model.Platform);
        Assert.Null(model.Profile);
        Assert.Null(model.Report);
        Assert.Null(model.ErrorMessage);
    }

    private static AdminCreatorProfileController Build(
        FakeCreatorProfileSourceStore? store = null,
        Func<string, string, CancellationToken, Task<MeasuredStyleBuildResult>>? buildDetailedAsync = null,
        Func<DateTimeOffset>? nowUtc = null,
        string? origin = "https://deckflow.test")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("deckflow.test");
        if (!string.IsNullOrWhiteSpace(origin))
        {
            httpContext.Request.Headers.Origin = origin;
        }

        return new AdminCreatorProfileController(
            store ?? new FakeCreatorProfileSourceStore(),
            buildDetailedAsync ?? ((slug, platform, ct) => Task.FromResult(NewBuildResult(slug, platform))),
            nowUtc ?? (() => new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<AdminCreatorProfileController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private static CreatorStyleProfile NewProfile(string slug, string platform) =>
        new()
        {
            Slug = slug,
            Platform = platform,
            MinDecks = 7,
            InsufficientSample = false,
            MeasuredMetrics =
            [
                new MeasuredMetric
                {
                    Metric = "category_ratio:ramp",
                    Value = 12.3456,
                    NumDecks = 7,
                },
            ],
            UpdatedUtc = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
        };

    private static IReadOnlyList<CreatorDeckSample> NewSamples() =>
    [
        new CreatorDeckSample
        {
            DeckId = "deck-1",
            CardCount = 100,
            FolderName = "Brew Box",
            ConfidenceMarker = "ok",
            Entries =
            [
                NewEntry("Sol Ring", "mainboard"),
                NewEntry("Arcane Signet", "mainboard"),
                NewEntry("Atraxa, Praetors' Voice", "commander"),
            ],
        },
        new CreatorDeckSample
        {
            DeckId = "deck-2",
            CardCount = 100,
            FolderName = "League",
            ConfidenceMarker = "ok",
            Entries =
            [
                NewEntry("Sol Ring", "mainboard"),
                NewEntry("Arcane Signet", "mainboard"),
                NewEntry("Derevi, Empyrial Tactician", "commander"),
            ],
        },
    ];

    private static DeckEntry NewEntry(string name, string board) =>
        new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = 1,
            Board = board,
        };

    private static MeasuredStyleBuildResult NewBuildResult(string slug, string platform) =>
        new()
        {
            Profile = NewProfile(slug, platform),
            Samples = NewSamples(),
            CardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Sol Ring"] = ["Ramp"],
                ["Arcane Signet"] = ["Ramp"],
                ["Atraxa, Praetors' Voice"] = ["Commander"],
                ["Derevi, Empyrial Tactician"] = ["Commander"],
            },
            Baseline = new GlobalCategoryBaseline
            {
                TotalDecks = 10,
                DecksWithCategory = new Dictionary<string, int>(),
                DecksWithCategoryPair = new Dictionary<string, int>(),
            },
        };

    /// <summary>
    /// Stateful in-memory stand-in for <see cref="ICreatorProfileSourceStore"/> recording every
    /// upsert so facts can assert what the controller persisted.
    /// </summary>
    private sealed class FakeCreatorProfileSourceStore : ICreatorProfileSourceStore
    {
        private readonly CreatorProfileSource? _existing;

        public FakeCreatorProfileSourceStore(CreatorProfileSource? existing = null)
        {
            _existing = existing;
        }

        public List<CreatorProfileSource> Upserts { get; } = new();

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CreatorProfileSource?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult(_existing);

        public Task SetLastCrawledAsync(string slug, DateTimeOffset whenUtc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpsertAsync(CreatorProfileSource source, CancellationToken cancellationToken = default)
        {
            Upserts.Add(source);
            return Task.CompletedTask;
        }
    }
}
