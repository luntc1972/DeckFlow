using System.Reflection;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.CreatorStyleRubric;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Controller tests for the creator-style critique tool surface.
/// </summary>
public sealed class CreatorStyleControllerTests
{
    [Fact]
    public async Task Get_ReturnsPickerOptions_WhenProfilesExist()
    {
        var profileStore = new FakeCreatorStyleProfileStore(
            new CreatorStyleProfileSummary
            {
                Slug = "salubrious-snail",
                Platform = "youtube",
                MinDecks = 39,
                UpdatedUtc = DateTimeOffset.UtcNow,
            });
        var siteIndexStore = new FakeContentSiteIndexStore();
        siteIndexStore.Rows.Add(CreatePublishedRow(1, "Salubrious Snail"));
        siteIndexStore.Rows.Add(CreatePublishedRow(2, "Salubrious Snail"));
        siteIndexStore.Rows.Add(CreatePublishedRow(3, "Another Creator"));

        var controller = CreateController(
            packetService: new StubCreatorStylePacketService(CreatePacketResult()),
            profileStore: profileStore,
            siteIndexStore: siteIndexStore);

        var response = await controller.CreatorStyle();

        var view = Assert.IsType<ViewResult>(response);
        Assert.Equal("CreatorStyle", view.ViewName);
        var model = Assert.IsType<CreatorStyleViewModel>(view.Model);
        var option = Assert.Single(model.AvailableCreators);
        Assert.False(model.NoProfilesLoaded);
        Assert.Equal("salubrious-snail", option.Slug);
        Assert.Equal("Salubrious Snail — 39 decks · 2 videos", option.DisplayLabel);
        Assert.False(model.HasResult);
        Assert.Equal(1, profileStore.GetAllCallCount);
    }

    [Fact]
    public async Task Get_ReturnsNoProfilesLoaded_WhenStoreIsEmpty()
    {
        var controller = CreateController(
            packetService: new StubCreatorStylePacketService(CreatePacketResult()),
            profileStore: new FakeCreatorStyleProfileStore(),
            siteIndexStore: new FakeContentSiteIndexStore());

        var response = await controller.CreatorStyle();

        var view = Assert.IsType<ViewResult>(response);
        var model = Assert.IsType<CreatorStyleViewModel>(view.Model);
        Assert.True(model.NoProfilesLoaded);
        Assert.Empty(model.AvailableCreators);
    }

    [Fact]
    public async Task Post_UsesCachedResult_WhenCacheKeyHits()
    {
        const string cacheKey = "creator-style-cache-key";
        var cachedResult = CreatePacketResult(artifactText: "cached");
        var cache = new PacketSessionCache();
        cache.Set(cacheKey, cachedResult, PacketSizeEstimator.EstimateSizeBytes(cachedResult));
        var packetService = new StubCreatorStylePacketService(CreatePacketResult(artifactText: "fresh"))
        {
            CacheKey = cacheKey,
        };

        var controller = CreateController(
            packetService: packetService,
            profileStore: new FakeCreatorStyleProfileStore(CreateSummary()),
            siteIndexStore: new FakeContentSiteIndexStore(),
            packetCache: cache);

        var response = await controller.CreatorStyle(new CreatorStyleRequest
        {
            CreatorSlug = "salubrious-snail",
            DeckText = "1 Sol Ring",
        });

        var view = Assert.IsType<ViewResult>(response);
        var model = Assert.IsType<CreatorStyleViewModel>(view.Model);
        Assert.Equal("cached", model.Result?.ArtifactText);
        Assert.Equal(0, packetService.BuildCallCount);
        Assert.Equal(1, packetService.TryComputeCacheKeyCallCount);
    }

    [Fact]
    public async Task Post_ReturnsResult_WhenBuildSucceeds()
    {
        var packetService = new StubCreatorStylePacketService(CreatePacketResult());
        var controller = CreateController(
            packetService: packetService,
            profileStore: new FakeCreatorStyleProfileStore(CreateSummary()),
            siteIndexStore: new FakeContentSiteIndexStore());

        var response = await controller.CreatorStyle(new CreatorStyleRequest
        {
            CreatorSlug = "salubrious-snail",
            DeckText = "1 Sol Ring",
        });

        var view = Assert.IsType<ViewResult>(response);
        var model = Assert.IsType<CreatorStyleViewModel>(view.Model);
        Assert.True(model.HasResult);
        Assert.NotNull(model.Result);
        Assert.Equal(1, packetService.BuildCallCount);
    }

    [Fact]
    public async Task Post_WhenUpstreamFails_ReturnsFriendlyError()
    {
        var packetService = new ThrowingCreatorStylePacketService(
            new HttpRequestException("Scryfall returned HTTP 503.", null, System.Net.HttpStatusCode.ServiceUnavailable));
        var controller = CreateController(
            packetService: packetService,
            profileStore: new FakeCreatorStyleProfileStore(CreateSummary()),
            siteIndexStore: new FakeContentSiteIndexStore());

        var response = await controller.CreatorStyle(new CreatorStyleRequest
        {
            CreatorSlug = "salubrious-snail",
            DeckText = "1 Sol Ring",
        });

        var view = Assert.IsType<ViewResult>(response);
        var model = Assert.IsType<CreatorStyleViewModel>(view.Model);
        Assert.Equal("Scryfall returned HTTP 503. Try again shortly.", model.ErrorMessage);
    }

    [Fact]
    public async Task Post_WhenRequestIsCanceled_ReturnsTimeoutMessage()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var controller = CreateController(
            packetService: new CancelAwareCreatorStylePacketService(),
            profileStore: new FakeCreatorStyleProfileStore(CreateSummary()),
            siteIndexStore: new FakeContentSiteIndexStore());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestAborted = cts.Token,
            },
        };

        var response = await controller.CreatorStyle(new CreatorStyleRequest
        {
            CreatorSlug = "salubrious-snail",
            DeckText = "1 Sol Ring",
        });

        var view = Assert.IsType<ViewResult>(response);
        var model = Assert.IsType<CreatorStyleViewModel>(view.Model);
        Assert.Equal("The deck took too long to load. Try again in a moment.", model.ErrorMessage);
    }

    [Fact]
    public async Task Post_ProfileUnavailable_SurfacesNoticeWithoutPacketBlock()
    {
        const string notice = "The creator style profile sample is insufficient for artifact generation.";
        var controller = CreateController(
            packetService: new StubCreatorStylePacketService(CreatePacketResult(profileUnavailable: true, notice: notice)),
            profileStore: new FakeCreatorStyleProfileStore(CreateSummary(insufficientSample: true)),
            siteIndexStore: new FakeContentSiteIndexStore());

        var response = await controller.CreatorStyle(new CreatorStyleRequest
        {
            CreatorSlug = "salubrious-snail",
            DeckText = "1 Sol Ring",
        });

        var view = Assert.IsType<ViewResult>(response);
        var model = Assert.IsType<CreatorStyleViewModel>(view.Model);
        Assert.NotNull(model.Result);
        Assert.True(model.Result!.ProfileUnavailable);
        Assert.False(model.HasResult);
        Assert.Equal(notice, model.Result.Notice);
    }

    [Fact]
    public void BothActions_UseFeatureFlagGate_AndPostUsesCsrfProtection()
    {
        var getMethod = typeof(CreatorStyleController).GetMethod(nameof(CreatorStyleController.CreatorStyle), Type.EmptyTypes);
        var postMethod = typeof(CreatorStyleController).GetMethod(nameof(CreatorStyleController.CreatorStyle), [typeof(CreatorStyleRequest)]);

        var getGate = Assert.IsType<FeatureFlagGateAttribute>(getMethod?.GetCustomAttribute(typeof(FeatureFlagGateAttribute)));
        var postGate = Assert.IsType<FeatureFlagGateAttribute>(postMethod?.GetCustomAttribute(typeof(FeatureFlagGateAttribute)));
        Assert.Equal("tool.creator-style.enabled", getGate.Key);
        Assert.Equal("tool.creator-style.enabled", postGate.Key);
        Assert.NotNull(postMethod?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    private static CreatorStyleController CreateController(
        ICreatorStylePacketService packetService,
        ICreatorStyleProfileStore profileStore,
        IContentSiteIndexStore siteIndexStore,
        PacketSessionCache? packetCache = null)
    {
        var controller = new CreatorStyleController(
            packetService,
            profileStore,
            siteIndexStore,
            packetCache ?? new PacketSessionCache(),
            NullLogger<CreatorStyleController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return controller;
    }

    private static CreatorStyleProfileSummary CreateSummary(bool insufficientSample = false) => new()
    {
        Slug = "salubrious-snail",
        Platform = "youtube",
        MinDecks = 39,
        InsufficientSample = insufficientSample,
        UpdatedUtc = DateTimeOffset.UtcNow,
    };

    private static CreatorStylePacketResult CreatePacketResult(
        string artifactText = "packet",
        bool profileUnavailable = false,
        string? notice = null) => new()
        {
            ArtifactText = artifactText,
            RubricScores = new RubricScoreResult
            {
                CreatorSlug = "salubrious-snail",
                MetricScores =
            [
                new RubricMetricScore
                {
                    Metric = "category_ratio:ramp",
                    TargetValue = 12,
                    SubmittedValue = 10,
                    Delta = -2,
                    Weight = 1,
                    Verdict = "under",
                    Confidence = "high",
                },
            ],
            },
            Exemplars =
        [
            new CreatorStyleExemplarDeck
            {
                DeckId = "deck-1",
                ConfidenceMarker = "high",
                CardNames = ["Sol Ring"],
            },
        ],
            ValidatedWhitelist = ["Sol Ring"],
            ValidatedComboCards = ["Dockside Extortionist"],
            GroundingDegraded = false,
            Notice = notice,
            ProfileUnavailable = profileUnavailable,
        };

    private static ContentSiteIndexRow CreatePublishedRow(long id, string source) => new()
    {
        Id = id,
        Source = source,
        Title = $"Title {id}",
        VideoUrl = $"https://example.com/{id}",
        ArtifactPath = $"content-kb/{id}.md",
        IndexedUtc = DateTimeOffset.UtcNow,
        IsVisible = true,
        ApprovalStatus = "approved",
        ArchetypeTags = Array.Empty<string>(),
        BracketTags = Array.Empty<string>(),
        CardCategoryTags = Array.Empty<string>(),
        YoutubeVideoId = $"video-{id}",
    };

    private sealed class StubCreatorStylePacketService : ICreatorStylePacketService
    {
        private readonly CreatorStylePacketResult _result;

        public StubCreatorStylePacketService(CreatorStylePacketResult result)
        {
            _result = result;
        }

        public int BuildCallCount { get; private set; }

        public int TryComputeCacheKeyCallCount { get; private set; }

        public string? CacheKey { get; init; }

        public Task<string?> TryComputeCacheKeyAsync(CreatorStyleRequest request, CancellationToken cancellationToken)
        {
            TryComputeCacheKeyCallCount++;
            return Task.FromResult(CacheKey);
        }

        public Task<CreatorStylePacketResult> BuildAsync(CreatorStyleRequest request, CancellationToken cancellationToken = default)
        {
            BuildCallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingCreatorStylePacketService : ICreatorStylePacketService
    {
        private readonly Exception _exception;

        public ThrowingCreatorStylePacketService(Exception exception)
        {
            _exception = exception;
        }

        public Task<string?> TryComputeCacheKeyAsync(CreatorStyleRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);

        public Task<CreatorStylePacketResult> BuildAsync(CreatorStyleRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<CreatorStylePacketResult>(_exception);
    }

    private sealed class CancelAwareCreatorStylePacketService : ICreatorStylePacketService
    {
        public Task<string?> TryComputeCacheKeyAsync(CreatorStyleRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);

        public Task<CreatorStylePacketResult> BuildAsync(CreatorStyleRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreatePacketResult());
        }
    }

    private sealed class FakeCreatorStyleProfileStore : ICreatorStyleProfileStore
    {
        private readonly IReadOnlyList<CreatorStyleProfileSummary> _summaries;

        public FakeCreatorStyleProfileStore(params CreatorStyleProfileSummary[] summaries)
        {
            _summaries = summaries;
        }

        public int GetAllCallCount { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertAsync(DeckFlow.Core.Knowledge.CreatorStyleProfile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<DeckFlow.Core.Knowledge.CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult<DeckFlow.Core.Knowledge.CreatorStyleProfile?>(null);

        public Task<IReadOnlyList<CreatorStyleProfileSummary>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            GetAllCallCount++;
            return Task.FromResult(_summaries);
        }
    }
}
