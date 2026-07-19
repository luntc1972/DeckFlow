using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DeckFlow.Web.Tests.Services.CreatorStyle;

namespace DeckFlow.Web.Tests.Services.CreatorStyle;

/// <summary>
/// Guards the creator-style DI graph required by Development ValidateOnBuild.
/// </summary>
public sealed class CreatorStyleDiRegistrationTests
{
    [Fact]
    public void ServiceCollection_ValidateOnBuild_ResolvesCreatorStyleScopedServicesWithinScope()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "deckflow-98-05-di", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<ICreatorDeckCacheStore>(_ =>
                new CreatorDeckCacheStore(RelationalDatabaseConnection.FromSqlitePath(Path.Combine(tempDirectory, "creator-deck-cache.db"))));
            services.AddSingleton<ICreatorProfileSourceStore>(_ =>
                new CreatorProfileSourceStore(RelationalDatabaseConnection.FromSqlitePath(Path.Combine(tempDirectory, "creator-deck-cache.db"))));
            services.AddSingleton(_ =>
                new CategoryKnowledgeRepository(RelationalDatabaseConnection.FromSqlitePath(Path.Combine(tempDirectory, "category-knowledge.db"))));
            services.AddSingleton<IArchidektOwnerClient, FakeArchidektOwnerClient>();
            services.AddSingleton<IArchidektDeckImporter, FakeArchidektDeckImporter>();
            services.AddSingleton<IMoxfieldOwnerClient, FakeMoxfieldOwnerClient>();
            services.AddSingleton<IMoxfieldDeckImporter, FakeMoxfieldDeckImporter>();
            services.AddSingleton<IScryfallTaggerLookupService, FakeScryfallTaggerLookupService>();
            services.AddSingleton<ICommanderSpellbookService, FakeCommanderSpellbookService>();
            services.AddSingleton<IScryfallCardResolver, FakeScryfallCardResolver>();
            services.AddSingleton<ICreatorStyleProfileStore, FakeCreatorStyleProfileStore>();
            services.AddSingleton<ICardGroundingGuard, FakeCardGroundingGuard>();
            services.AddSingleton<IDeckEntryLoader, FakeDeckEntryLoader>();
            services.AddMemoryCache();
            services.AddSingleton<PacketSessionCache>();
            services.AddSingleton<CreatorWhitelistPoolBuilder>();
            services.AddScoped<CreatorProfileDeckCrawler>();
            services.AddScoped<CreatorDeckCategoryResolver>();
            services.AddScoped<MeasuredStyleProfileBuilder>();
            services.AddScoped<ISubmittedDeckStatsBuilder>(sp =>
                new SubmittedDeckStatsBuilder(
                    sp.GetRequiredService<IDeckEntryLoader>(),
                    sp.GetRequiredService<CategoryKnowledgeRepository>(),
                    sp.GetRequiredService<ICommanderSpellbookService>(),
                    sp.GetRequiredService<IScryfallCardResolver>(),
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<SubmittedDeckStatsBuilder>>()));
            services.AddScoped<ICreatorStylePacketService>(sp =>
                new CreatorStylePacketService(
                    sp.GetRequiredService<ICreatorStyleProfileStore>(),
                    sp.GetRequiredService<ISubmittedDeckStatsBuilder>(),
                    sp.GetRequiredService<CreatorWhitelistPoolBuilder>(),
                    sp.GetRequiredService<ICardGroundingGuard>(),
                    sp.GetRequiredService<ICreatorDeckCacheStore>(),
                    sp.GetRequiredService<PacketSessionCache>(),
                    sp.GetService<IFeatureFlagCache>(),
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<CreatorStylePacketService>>()));

            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            using IServiceScope scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreatorProfileDeckCrawler>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreatorDeckCategoryResolver>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<MeasuredStyleProfileBuilder>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICreatorStylePacketService>());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private sealed class FakeArchidektOwnerClient : IArchidektOwnerClient
    {
        public Task<IReadOnlyList<ArchidektDeckSummary>> ListDeckSummariesAsync(string ownerUsername, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ArchidektDeckSummary>>([]);

        public Task<string?> ResolveUsernameAsync(string usernameOrUrl, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(usernameOrUrl);
    }

    private sealed class FakeArchidektDeckImporter : IArchidektDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());
    }

    private sealed class FakeScryfallTaggerLookupService : IScryfallTaggerLookupService
    {
        public Task<IReadOnlyList<string>> LookupOracleTagsAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeCommanderSpellbookService : ICommanderSpellbookService
    {
        public Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken = default)
            => Task.FromResult<CommanderSpellbookResult?>(null);
    }

    private sealed class FakeCardGroundingGuard : ICardGroundingGuard
    {
        public Task<CardGroundingVerdict> TryValidateAsync(
            string candidateName,
            CardGroundingDeckContext deckContext,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CardGroundingVerdict
            {
                Accepted = true,
                CanonicalName = candidateName,
                RejectReason = CardGroundingRejectReason.None,
            });

        public Task<CardGroundingBatchResult> ValidateAllAsync(
            IReadOnlyList<string> candidateNames,
            CardGroundingDeckContext deckContext,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CardGroundingBatchResult
            {
                Verdicts = candidateNames
                    .Select(candidateName => new CardGroundingVerdict
                    {
                        Accepted = true,
                        CanonicalName = candidateName,
                        RejectReason = CardGroundingRejectReason.None,
                    })
                    .ToArray(),
                HasUpstreamFailure = false,
            });
    }

    private sealed class FakeDeckEntryLoader : IDeckEntryLoader
    {
        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<DeckEntry>());

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeckSourceLoadResult(new List<DeckEntry>(), null));

        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100)
        {
        }
    }

    private sealed class FakeScryfallCardResolver : IScryfallCardResolver
    {
        public Task<RestSharp.RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestSharp.RestRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<RestSharp.RestResponse<ScryfallCard>> ExecuteNamedFuzzyAsync(string cardName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult<ScryfallCard?>(null);

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult<ScryfallCard?>(null);

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult<ScryfallCard?>(null);
    }

    private sealed class FakeCreatorStyleProfileStore : ICreatorStyleProfileStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<CreatorStyleProfileSummary>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CreatorStyleProfileSummary>>(Array.Empty<CreatorStyleProfileSummary>());

        public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult<CreatorStyleProfile?>(null);

        public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
