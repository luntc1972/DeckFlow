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

    [Fact]
    public async Task Run_BlankSlug_AddsModelError_AndSkipsPipeline()
    {
        var store = new FakeCreatorProfileSourceStore();
        var calls = new List<string>();
        var controller = Build(store, buildDetailedAsync: (slug, platform, ct) =>
        {
            calls.Add("build");
            return Task.FromResult(NewBuildResult(slug, platform));
        });

        var result = await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "   ",
            Username = "user",
            Platform = "archidekt",
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(AdminCreatorProfileInputModel.Slug)]!.Errors, e => !string.IsNullOrEmpty(e.ErrorMessage));
        Assert.Empty(store.Upserts);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Run_BlankUsername_AddsModelError_AndSkipsPipeline()
    {
        var store = new FakeCreatorProfileSourceStore();
        var calls = new List<string>();
        var controller = Build(store, buildDetailedAsync: (slug, platform, ct) =>
        {
            calls.Add("build");
            return Task.FromResult(NewBuildResult(slug, platform));
        });

        var result = await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "   ",
            Platform = "archidekt",
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(AdminCreatorProfileInputModel.Username)]!.Errors, e => !string.IsNullOrEmpty(e.ErrorMessage));
        Assert.Empty(store.Upserts);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Run_InvalidPlatform_AddsModelError_AndSkipsPipeline()
    {
        var store = new FakeCreatorProfileSourceStore();
        var calls = new List<string>();
        var controller = Build(store, buildDetailedAsync: (slug, platform, ct) =>
        {
            calls.Add("build");
            return Task.FromResult(NewBuildResult(slug, platform));
        });

        var result = await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "user",
            Platform = "tappedout",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminCreatorProfileViewModel>(view.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[nameof(AdminCreatorProfileInputModel.Platform)]!.Errors, e => e.ErrorMessage.Contains("archidekt|moxfield", StringComparison.Ordinal));
        Assert.Empty(store.Upserts);
        Assert.Empty(calls);
        Assert.Equal("tappedout", model.Platform);
    }

    [Fact]
    public async Task Run_CrossOriginRequest_ReturnsForbidden_AndSkipsPipeline()
    {
        var store = new FakeCreatorProfileSourceStore();
        var calls = new List<string>();
        var controller = Build(
            store,
            buildDetailedAsync: (slug, platform, ct) =>
            {
                calls.Add("build");
                return Task.FromResult(NewBuildResult(slug, platform));
            },
            origin: "https://evil.test");

        var result = await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "user",
            Platform = "archidekt",
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal(SameOriginRequestValidator.GetForbiddenMessage(), objectResult.Value);
        Assert.Empty(store.Upserts);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Run_HappyPath_UpsertsCarriesForwardWeightsBuildsAndReports()
    {
        var existing = new CreatorProfileSource
        {
            Slug = "new-slug",
            Platform = "moxfield",
            ProfileUsername = "CreatorUser",
            ProfileUrl = "https://moxfield.example/creatoruser",
            FolderWeights = new Dictionary<int, double> { [7] = 0.5 },
            WeightsUncurated = false,
            LastCrawledUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        };
        var store = new FakeCreatorProfileSourceStore(existing);
        var callOrder = new List<string>();
        var profile = NewProfile("new-slug", "moxfield");
        var controller = Build(
            store,
            buildDetailedAsync: (slug, platform, ct) =>
            {
                callOrder.Add("build");
                return Task.FromResult(NewBuildResult(slug, platform, profile));
            },
            nowUtc: () => new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));

        var result = await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "  New-Slug  ",
            Username = "CreatorUser",
            Platform = "moxfield",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminCreatorProfileViewModel>(view.Model);

        Assert.Single(store.Upserts);
        Assert.Equal("new-slug", store.Upserts[0].Slug);
        Assert.Equal("moxfield", store.Upserts[0].Platform);
        Assert.Equal("CreatorUser", store.Upserts[0].ProfileUsername);
        Assert.Equal(existing.FolderWeights, store.Upserts[0].FolderWeights);
        Assert.False(store.Upserts[0].WeightsUncurated);
        Assert.Equal(existing.ProfileUrl, store.Upserts[0].ProfileUrl);
        Assert.Equal(["build"], callOrder);
        Assert.Same(profile, model.Profile);
        Assert.NotNull(model.Report);
        Assert.Equal(2, model.Report!.DeckCount);
    }

    [Fact]
    public async Task Run_UnchangedIdentity_PreservesLastCrawled()
    {
        var existing = new CreatorProfileSource
        {
            Slug = "slug",
            Platform = "archidekt",
            ProfileUsername = "same-user",
            ProfileUrl = "https://archidekt.example/same-user",
            FolderWeights = new Dictionary<int, double>(),
            WeightsUncurated = true,
            LastCrawledUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        };
        var store = new FakeCreatorProfileSourceStore(existing);
        var controller = Build(
            store,
            buildDetailedAsync: (slug, platform, ct) => Task.FromResult(NewBuildResult(slug, platform)));

        await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "same-user",
            Platform = "archidekt",
        });

        Assert.Equal(existing.LastCrawledUtc, store.Upserts[0].LastCrawledUtc);
    }

    [Fact]
    public async Task Run_UsernameChange_ClearsLastCrawled()
    {
        var existing = new CreatorProfileSource
        {
            Slug = "slug",
            Platform = "archidekt",
            ProfileUsername = "old-user",
            FolderWeights = new Dictionary<int, double>(),
            WeightsUncurated = true,
            LastCrawledUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        };
        var store = new FakeCreatorProfileSourceStore(existing);
        var controller = Build(
            store,
            buildDetailedAsync: (slug, platform, ct) => Task.FromResult(NewBuildResult(slug, platform)));

        await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "new-user",
            Platform = "archidekt",
        });

        Assert.Null(store.Upserts[0].LastCrawledUtc);
    }

    [Fact]
    public async Task Run_PlatformChange_ClearsLastCrawled()
    {
        var existing = new CreatorProfileSource
        {
            Slug = "slug",
            Platform = "archidekt",
            ProfileUsername = "same-user",
            FolderWeights = new Dictionary<int, double>(),
            WeightsUncurated = true,
            LastCrawledUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        };
        var store = new FakeCreatorProfileSourceStore(existing);
        var controller = Build(
            store,
            buildDetailedAsync: (slug, platform, ct) => Task.FromResult(NewBuildResult(slug, platform)));

        await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "same-user",
            Platform = "moxfield",
        });

        Assert.Null(store.Upserts[0].LastCrawledUtc);
    }

    [Fact]
    public async Task Run_ForceRefresh_ClearsLastCrawledRegardlessOfIdentity()
    {
        var existing = new CreatorProfileSource
        {
            Slug = "slug",
            Platform = "archidekt",
            ProfileUsername = "same-user",
            FolderWeights = new Dictionary<int, double>(),
            WeightsUncurated = true,
            LastCrawledUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        };
        var store = new FakeCreatorProfileSourceStore(existing);
        var controller = Build(
            store,
            buildDetailedAsync: (slug, platform, ct) => Task.FromResult(NewBuildResult(slug, platform)));

        await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "same-user",
            Platform = "archidekt",
            ForceRefresh = true,
        });

        Assert.Null(store.Upserts[0].LastCrawledUtc);
    }

    [Fact]
    public async Task Run_BuildCanceled_ReRendersWithTimeoutMessage_AndNoReport()
    {
        var store = new FakeCreatorProfileSourceStore();
        var controller = Build(
            store,
            buildDetailedAsync: (slug, platform, ct) => Task.FromException<MeasuredStyleBuildResult>(new OperationCanceledException()));

        var result = await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "user",
            Platform = "archidekt",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminCreatorProfileViewModel>(view.Model);
        Assert.NotNull(model.ErrorMessage);
        Assert.Contains("timed out", model.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(model.Report);
        Assert.Null(model.Profile);
    }

    [Fact]
    public async Task Run_BuildThrowsGeneralException_ReRendersWithFailureMessage_AndNoReport()
    {
        var store = new FakeCreatorProfileSourceStore();
        var controller = Build(
            store,
            buildDetailedAsync: (slug, platform, ct) => Task.FromException<MeasuredStyleBuildResult>(new InvalidOperationException("boom")));

        var result = await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "user",
            Platform = "archidekt",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminCreatorProfileViewModel>(view.Model);
        Assert.NotNull(model.ErrorMessage);
        Assert.DoesNotContain("timed out", model.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(model.Report);
        Assert.Null(model.Profile);
    }

    [Fact]
    public async Task Run_BuildReturnsZeroSamples_RendersReportWithZeroDeckCount_DoesNotThrow()
    {
        var store = new FakeCreatorProfileSourceStore();
        var emptyResult = new MeasuredStyleBuildResult
        {
            Profile = NewProfile("slug", "archidekt"),
            Samples = [],
            CardCategories = new Dictionary<string, IReadOnlyList<string>>(),
            Baseline = new GlobalCategoryBaseline
            {
                TotalDecks = 0,
                DecksWithCategory = new Dictionary<string, int>(),
                DecksWithCategoryPair = new Dictionary<string, int>(),
            },
        };
        var controller = Build(
            store,
            buildDetailedAsync: (slug, platform, ct) => Task.FromResult(emptyResult));

        var result = await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "user",
            Platform = "archidekt",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminCreatorProfileViewModel>(view.Model);
        Assert.NotNull(model.Report);
        Assert.Equal(0, model.Report!.DeckCount);
        Assert.Null(model.ErrorMessage);
    }

    [Fact]
    public async Task Run_BuildDelegateBlocksPastShortTestTimeout_ReRendersWithTimeoutMessage()
    {
        var store = new FakeCreatorProfileSourceStore();
        var controller = Build(
            store,
            buildDetailedAsync: async (slug, platform, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return NewBuildResult(slug, platform);
            },
            runTimeout: TimeSpan.FromMilliseconds(50));

        var result = await controller.Run(new AdminCreatorProfileInputModel
        {
            Slug = "slug",
            Username = "user",
            Platform = "archidekt",
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminCreatorProfileViewModel>(view.Model);
        Assert.NotNull(model.ErrorMessage);
        Assert.Contains("timed out", model.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_SuppressedCreator_ReturnsRefusalWithoutBuildOrUpsert()
    {
        var store = new FakeCreatorProfileSourceStore();
        var suppressionStore = new FakeCreatorSuppressionStore();
        suppressionStore.Suppressed.Add("suppressed");
        var calls = new List<string>();
        var controller = Build(store, (slug, platform, ct) => { calls.Add("build"); return Task.FromResult(NewBuildResult(slug, platform)); }, suppressionStore: suppressionStore);

        var result = await controller.Run(new AdminCreatorProfileInputModel { Slug = "suppressed", Username = "user", Platform = "archidekt" });

        var model = Assert.IsType<AdminCreatorProfileViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("This creator is suppressed and cannot be rebuilt.", model.ErrorMessage);
        Assert.Empty(calls);
        Assert.Empty(store.Upserts);

        var control = await controller.Run(new AdminCreatorProfileInputModel { Slug = "control", Username = "user", Platform = "archidekt" });
        Assert.IsType<ViewResult>(control);
        Assert.Single(calls);
        Assert.Single(store.Upserts);
    }

    [Fact]
    public async Task Run_LinkedSuppressedCreator_ReturnsRefusalWhileUnrelatedControlBuilds()
    {
        var store = new FakeCreatorProfileSourceStore();
        ICreatorSuppressionGate gate = CreatorSuppressionGateFixture.LinkedRepresentation("requested-slug", "linked-display");
        var calls = new List<string>();
        var controller = Build(
            store,
            (slug, platform, ct) => { calls.Add(slug); return Task.FromResult(NewBuildResult(slug, platform)); },
            suppressionGate: gate);

        Assert.True(await gate.IsSuppressedAsync("requested-slug"));

        var suppressed = await controller.Run(new AdminCreatorProfileInputModel { Slug = "requested-slug", Username = "user", Platform = "archidekt" });
        var model = Assert.IsType<AdminCreatorProfileViewModel>(Assert.IsType<ViewResult>(suppressed).Model);
        Assert.Equal("This creator is suppressed and cannot be rebuilt.", model.ErrorMessage);

        var control = await controller.Run(new AdminCreatorProfileInputModel { Slug = "control", Username = "user", Platform = "archidekt" });
        Assert.IsType<ViewResult>(control);
        Assert.Equal(["control"], calls);
        Assert.Single(store.Upserts);
    }

    [Fact]
    public async Task Run_SuppressedCreator_HidesStoredProfileData()
    {
        var existing = new CreatorProfileSource { Slug = "suppressed", Platform = "archidekt", ProfileUsername = "stored-user", ProfileUrl = "https://example.test/private", FolderWeights = new Dictionary<int, double>(), UpdatedUtc = DateTimeOffset.UtcNow };
        var store = new FakeCreatorProfileSourceStore(existing);
        var suppressionStore = new FakeCreatorSuppressionStore();
        suppressionStore.Suppressed.Add("suppressed");
        var controller = Build(store, suppressionStore: suppressionStore);

        var result = await controller.Run(new AdminCreatorProfileInputModel { Slug = "suppressed", Username = "submitted-user", Platform = "archidekt" });

        var model = Assert.IsType<AdminCreatorProfileViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("This creator is suppressed and cannot be rebuilt.", model.ErrorMessage);
        Assert.Null(model.Profile);
        Assert.Null(model.Report);
        Assert.Equal("submitted-user", model.Username);
        Assert.Empty(store.Upserts);

        var control = new CreatorProfileSource { Slug = "control", Platform = "archidekt", ProfileUsername = "control-user", ProfileUrl = "https://example.test/control", FolderWeights = new Dictionary<int, double>(), UpdatedUtc = DateTimeOffset.UtcNow };
        var controlStore = new FakeCreatorProfileSourceStore(control);
        var controlResult = await Build(controlStore, suppressionStore: new FakeCreatorSuppressionStore()).Run(new AdminCreatorProfileInputModel { Slug = "control", Username = "control-user", Platform = "archidekt" });
        var controlModel = Assert.IsType<AdminCreatorProfileViewModel>(Assert.IsType<ViewResult>(controlResult).Model);
        Assert.Equal("control", controlModel.Profile?.Slug);
        Assert.Equal("archidekt", controlModel.Profile?.Platform);
    }

    [Fact]
    public async Task Run_SuppressionReadFailure_PropagatesWithoutBuildOrUpsert()
    {
        var readableStore = new FakeCreatorProfileSourceStore();
        var readableCalls = new List<string>();
        var readableController = Build(readableStore, (slug, platform, ct) => { readableCalls.Add("build"); return Task.FromResult(NewBuildResult(slug, platform)); }, suppressionStore: new FakeCreatorSuppressionStore());
        Assert.IsType<ViewResult>(await readableController.Run(new AdminCreatorProfileInputModel { Slug = "control", Username = "user", Platform = "archidekt" }));
        Assert.Single(readableCalls);
        Assert.Single(readableStore.Upserts);

        var store = new FakeCreatorProfileSourceStore();
        var suppressionStore = new FakeCreatorSuppressionStore { ReadException = new InvalidOperationException("suppression unavailable") };
        var calls = new List<string>();
        var controller = Build(store, (slug, platform, ct) => { calls.Add("build"); return Task.FromResult(NewBuildResult(slug, platform)); }, suppressionStore: suppressionStore);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Run(new AdminCreatorProfileInputModel { Slug = "control", Username = "user", Platform = "archidekt" }));

        Assert.Equal("suppression unavailable", exception.Message);
        Assert.Empty(calls);
        Assert.Empty(store.Upserts);
    }

    [Fact]
    public async Task Run_SuppressionReadFailure_HidesStoredProfileData()
    {
        var readableStore = new FakeCreatorProfileSourceStore();
        var readableController = Build(readableStore, suppressionStore: new FakeCreatorSuppressionStore());
        Assert.IsType<ViewResult>(await readableController.Run(new AdminCreatorProfileInputModel { Slug = "control", Username = "user", Platform = "archidekt" }));
        Assert.Single(readableStore.Upserts);

        var stored = new CreatorProfileSource { Slug = "suppressed", Platform = "archidekt", ProfileUsername = "stored-user", ProfileUrl = "https://example.test/private", FolderWeights = new Dictionary<int, double>(), UpdatedUtc = DateTimeOffset.UtcNow };
        var store = new FakeCreatorProfileSourceStore(stored);
        var calls = new List<string>();
        var controller = Build(store, (slug, platform, ct) => { calls.Add("build"); return Task.FromResult(NewBuildResult(slug, platform)); }, suppressionStore: new FakeCreatorSuppressionStore { ReadException = new InvalidOperationException("suppression unavailable") });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Run(new AdminCreatorProfileInputModel { Slug = "suppressed", Username = "user", Platform = "archidekt" }));

        Assert.Equal("suppression unavailable", exception.Message);
        Assert.Empty(calls);
        Assert.Empty(store.Upserts);
    }

    private static AdminCreatorProfileController Build(
        FakeCreatorProfileSourceStore? store = null,
        Func<string, string, CancellationToken, Task<MeasuredStyleBuildResult>>? buildDetailedAsync = null,
        Func<DateTimeOffset>? nowUtc = null,
        string? origin = "https://deckflow.test",
        TimeSpan? runTimeout = null,
        ICreatorSuppressionStore? suppressionStore = null,
        ICreatorSuppressionGate? suppressionGate = null)
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
            NullLogger<AdminCreatorProfileController>.Instance,
            runTimeout,
            suppressionGate ?? (suppressionStore is null ? null : new FakeCreatorSuppressionGate(suppressionStore)))
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

    private static MeasuredStyleBuildResult NewBuildResult(string slug, string platform, CreatorStyleProfile? profile = null) =>
        new()
        {
            Profile = profile ?? NewProfile(slug, platform),
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
    private sealed class FakeCreatorProfileSourceStore(CreatorProfileSource? existing = null) : ICreatorProfileSourceStore
    {
        public Task<int> DeleteByCreatorAsync(CreatorIdentity identity, CancellationToken cancellationToken = default) => Task.FromResult(0);
        private readonly CreatorProfileSource? _existing = existing;

        public List<CreatorProfileSource> Upserts { get; } = [];

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
