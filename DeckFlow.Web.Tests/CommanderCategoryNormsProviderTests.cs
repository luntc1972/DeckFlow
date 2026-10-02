using System.Diagnostics;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CommanderCategoryNorms;
using DeckFlow.Web.Tests.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Internal;
using Xunit;
using Xunit.Abstractions;

namespace DeckFlow.Web.Tests;

public sealed class CommanderCategoryNormsProviderTests
{
    private readonly ITestOutputHelper _output;

    public CommanderCategoryNormsProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }
    [Fact]
    public async Task EndToEnd_FlagOn_RealServiceOverStore_ChatGptPromptCarriesBlock()
    {
        var store = CreateStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CommanderCategoryNormsProvider(new CommanderCategoryService(store), store, cache, NullLogger<CommanderCategoryNormsProvider>.Instance);
        var service = PacketByteIdentityFixtures.CreateAnalysisService(
            new PacketByteIdentityFixtures.StaticMoxfieldDeckImporter(PacketByteIdentityFixtures.BaselineEntries()),
            PacketByteIdentityFixtures.WithSingleFlagOn(DeckAnalysisPacketService.CommanderCategoryNormsFlag),
            normsProvider: provider);

        var prompt = (await service.BuildAsync(PacketByteIdentityFixtures.BaselineAnalysisRequest())).AnalysisPromptText;

        Assert.Contains("HARVESTED COMMANDER CATEGORY NORMS - 12 decks (LOW confidence)", prompt);
        Assert.Contains("- Ramp - in 83% of 12 decks", prompt);
        Assert.Contains("- Draw - in 50% of 12 decks", prompt);
    }

    [Fact]
    public async Task RealService_AboveFloor_ReturnsNormsInServiceOrder()
    {
        var store = CreateStore();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CommanderCategoryNormsProvider(new CommanderCategoryService(store), store, cache, NullLogger<CommanderCategoryNormsProvider>.Instance);

        var result = await provider.GetNormsAsync("Kraum, Ludevic's Opus");

        Assert.NotNull(result);
        Assert.Equal("Kraum, Ludevic's Opus", result.HarvestKey);
        Assert.Equal(12, result.DeckCount);
        Assert.Equal(["Ramp", "Draw"], result.Categories.Select(category => category.Category));
    }

    [Fact]
    public async Task CacheHit_SecondCall_NoStoreOrServiceCall()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = RecordingCommanderCategoryService.Success();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache);

        var first = await provider.GetNormsAsync("Cache Hit");
        var second = await provider.GetNormsAsync("Cache Hit");

        Assert.Equal(first, second);
        Assert.Equal(1, service.LookupCalls);
        Assert.Equal(1, store.GetCommanderDeckCountCalls);
    }

    [Fact]
    public async Task CacheKey_IsCaseInsensitive()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = RecordingCommanderCategoryService.Success();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache);

        await provider.GetNormsAsync("Kraum, Ludevic's Opus");
        await provider.GetNormsAsync("kraum, ludevic's opus");

        Assert.Equal(1, service.LookupCalls);
    }

    [Fact]
    public async Task BelowFloor_CountFirst_SkipsLookupAndCaches()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 9 };
        var service = RecordingCommanderCategoryService.Success();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache);

        Assert.Null(await provider.GetNormsAsync("Below Floor"));
        Assert.Null(await provider.GetNormsAsync("Below Floor"));
        Assert.Equal(0, service.LookupCalls);
        Assert.Equal(1, store.GetCommanderDeckCountCalls);
    }

    [Fact]
    public async Task FloorRecheck_LookupTotalBelowFloor_ReturnsNull()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 10 };
        var service = new RecordingCommanderCategoryService((key, token, includeCount) => Task.FromResult(Result(key, 9)));
        using var cache = new MemoryCache(new MemoryCacheOptions());

        Assert.Null(await CreateProvider(service, store, cache).GetNormsAsync("Floor Recheck"));
    }

    [Fact]
    public async Task Lookup_PassesIncludeProcessedDeckCountFalse()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = RecordingCommanderCategoryService.Success();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await CreateProvider(service, store, cache).GetNormsAsync("Include Flag");

        Assert.False(service.LastIncludeProcessedDeckCount);
    }

    [Fact]
    public async Task Timeout_ReturnsNull_LogsWarning_NegativeCaches()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = new RecordingCommanderCategoryService(async (key, token, includeCount) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Result(key);
        });
        var logger = new FakeLogger<CommanderCategoryNormsProvider>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache, logger, TimeSpan.FromMilliseconds(50));

        Assert.Null(await provider.GetNormsAsync("Timeout").WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Null(await provider.GetNormsAsync("Timeout"));
        Assert.Equal(1, service.LookupCalls);
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Exception_ReturnsNull_LogsWarning_NegativeCaches()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = new RecordingCommanderCategoryService((key, token, includeCount) => throw new InvalidOperationException("boom"));
        var logger = new FakeLogger<CommanderCategoryNormsProvider>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache, logger);

        Assert.Null(await provider.GetNormsAsync("Exception"));
        Assert.Null(await provider.GetNormsAsync("Exception"));
        Assert.Equal(1, service.LookupCalls);
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task CallerCancellation_Propagates_NotCached()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = new RecordingCommanderCategoryService(async (key, token, includeCount) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Result(key);
        });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache, timeout: TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetNormsAsync("Caller Cancel", cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5)));
        service.Behavior = (key, token, includeCount) => Task.FromResult(Result(key));
        Assert.NotNull(await provider.GetNormsAsync("Caller Cancel"));
        Assert.Equal(2, service.LookupCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Kraum\nX")]
    public async Task KeyGuard_InvalidKey_SkipsStoreAndService(string key)
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = RecordingCommanderCategoryService.Success();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        Assert.Null(await CreateProvider(service, store, cache).GetNormsAsync(key));
        Assert.Equal(0, service.LookupCalls);
        Assert.Equal(0, store.GetCommanderDeckCountCalls);
    }

    [Fact]
    public async Task SingleFlight_ConcurrentSameKey_OneLookup()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var (service, gate) = GatedService();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var first = CreateProvider(service, store, cache).GetNormsAsync("Single Flight");
        var second = CreateProvider(service, store, cache).GetNormsAsync("Single Flight");

        gate.SetResult();

        Assert.NotNull(await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(await second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, service.LookupCalls);
    }

    [Fact]
    public async Task Cap_TwentySummaries_KeepsFirstFifteenInOrder()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var summaries = Enumerable.Range(1, 20).Select(index => new CommanderCategorySummary($"Category {index}", 1, 12, 1)).ToList();
        var service = new RecordingCommanderCategoryService((key, token, includeCount) => Task.FromResult(Result(key, 12, summaries)));
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var result = await CreateProvider(service, store, cache).GetNormsAsync("Cap");

        Assert.NotNull(result);
        Assert.Equal(15, result.Categories.Count);
        Assert.Equal(summaries.Take(15), result.Categories);
    }

    [Fact]
    public async Task DoubleCheckHit_ReleasesStripe_LaterMissOnSameStripeAcquires()
    {
        var firstKey = "Stripe A";
        var secondKey = FindSameStripeKey(firstKey);
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var (service, gate) = GatedService();
        var logger = new FakeLogger<CommanderCategoryNormsProvider>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache, logger, TimeSpan.FromSeconds(1));
        var first = provider.GetNormsAsync(firstKey);
        var second = provider.GetNormsAsync(firstKey);

        gate.SetResult();
        Assert.NotNull(await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(await second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, service.LookupCalls);

        service.Behavior = (key, token, includeCount) => Task.FromResult(Result(key));
        Assert.NotNull(await provider.GetNormsAsync(secondKey).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, service.LookupCalls);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task CancelledToken_OnCacheHit_Throws()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = RecordingCommanderCategoryService.Success();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache);
        await provider.GetNormsAsync("Cached Cancel");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetNormsAsync("Cached Cancel", cancellation.Token));
        Assert.Equal(1, service.LookupCalls);
    }

    [Fact]
    public async Task CancelledWhileWaitingOnStripe_Throws_NotCached()
    {
        var firstKey = "Waiting A";
        var secondKey = FindSameStripeKey(firstKey);
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var (service, gate) = GatedService();
        var logger = new FakeLogger<CommanderCategoryNormsProvider>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache, logger, TimeSpan.FromSeconds(1));
        var first = provider.GetNormsAsync(firstKey);
        using var cancellation = new CancellationTokenSource();
        var second = provider.GetNormsAsync(secondKey, cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
        gate.SetResult();
        Assert.NotNull(await first.WaitAsync(TimeSpan.FromSeconds(5)));

        service.Behavior = (key, token, includeCount) => Task.FromResult(Result(key));
        Assert.NotNull(await provider.GetNormsAsync(secondKey));
        Assert.Equal(2, service.LookupCalls);
    }

    [Fact]
    public async Task SuccessEntry_ExpiresAfterNormsCacheDuration_Reloads()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var cache = new MemoryCache(new MemoryCacheOptions { Clock = new TimeProviderSystemClock(time) });
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = RecordingCommanderCategoryService.Success();
        var provider = CreateProvider(service, store, cache);

        await provider.GetNormsAsync("Success Expiry");
        time.Advance(TimeSpan.FromMinutes(29));
        await provider.GetNormsAsync("Success Expiry");
        Assert.Equal(1, service.LookupCalls);
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.NotNull(await provider.GetNormsAsync("Success Expiry"));
        Assert.Equal(2, service.LookupCalls);
    }

    [Fact]
    public async Task FailureEntry_ExpiresAfterFailureCacheDuration_Recovers()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var cache = new MemoryCache(new MemoryCacheOptions { Clock = new TimeProviderSystemClock(time) });
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        var service = new RecordingCommanderCategoryService((key, token, includeCount) => throw new InvalidOperationException("first"));
        var provider = CreateProvider(service, store, cache);

        Assert.Null(await provider.GetNormsAsync("Failure Expiry"));
        service.Behavior = (key, token, includeCount) => Task.FromResult(Result(key));
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await provider.GetNormsAsync("Failure Expiry"));
        Assert.Equal(1, service.LookupCalls);
        time.Advance(TimeSpan.FromMinutes(2));
        Assert.NotNull(await provider.GetNormsAsync("Failure Expiry"));
        Assert.Equal(2, service.LookupCalls);
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(50000)]
    [InlineData(200000)]
    public async Task ColdLookup_MembershipScale_ReportsAllocationAndLatency(int rows)
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 500, CategoryRowsResult = CategoryRows() };
        var labels = CategoryLabels();
        var materializeStart = GC.GetAllocatedBytesForCurrentThread();
        var materializeTimer = Stopwatch.StartNew();
        var memberships = new List<CategoryDeckMembership>(rows);
        for (var index = 0; index < rows; index++)
        {
            var category = new string(labels[index % labels.Length].ToCharArray());
            var cardName = new string($"Card {index}".ToCharArray());
            memberships.Add(new CategoryDeckMembership(category, cardName, (index % 500) + 1));
        }

        materializeTimer.Stop();
        var materializeBytes = GC.GetAllocatedBytesForCurrentThread() - materializeStart;
        var repositoryStart = GC.GetAllocatedBytesForCurrentThread();
        var repositoryTimer = Stopwatch.StartNew();
        var filtered = CardCategoryRepository.FilterGenericMembershipWithFallback(memberships.ToList());
        repositoryTimer.Stop();
        var repositoryBytes = GC.GetAllocatedBytesForCurrentThread() - repositoryStart;
        Assert.NotEmpty(filtered);
        store.Memberships.AddRange(filtered);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CommanderCategoryNormsProvider(new CommanderCategoryService(store), store, cache, NullLogger<CommanderCategoryNormsProvider>.Instance);
        var lookupStart = GC.GetAllocatedBytesForCurrentThread();
        var lookupTimer = Stopwatch.StartNew();
        var task = provider.GetNormsAsync($"Scale {rows}");
        Assert.True(task.IsCompleted);
        var result = await task;
        lookupTimer.Stop();
        var lookupBytes = GC.GetAllocatedBytesForCurrentThread() - lookupStart;

        Assert.NotNull(result);
        Assert.Equal(15, result.Categories.Count);
        _output.WriteLine($"NORMS-MEASURE rows={rows} materializeBytes={materializeBytes} repositoryBytes={repositoryBytes} repositoryMs={repositoryTimer.ElapsedMilliseconds} lookupBytes={lookupBytes} lookupMs={lookupTimer.ElapsedMilliseconds} materializeMs={materializeTimer.ElapsedMilliseconds}");
    }

    [Fact]
    public async Task CacheGrowth_DistinctKeys_ReportsRetainedBytesPerEntry()
    {
        var store = new FakeCategoryKnowledgeStore { CommanderDeckCount = 500 };
        var service = new RecordingCommanderCategoryService((key, token, includeCount) => Task.FromResult(Result(key, 500, Summaries(20))));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = CreateProvider(service, store, cache);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        var before = GC.GetTotalMemory(true);

        for (var index = 0; index < 1000; index++)
        {
            await provider.GetNormsAsync(index.ToString("D4") + new string('x', 196));
        }

        var after = GC.GetTotalMemory(true);
        var retainedBytesPerEntry = Math.Max(0, after - before) / 1000;
        Assert.Equal(1000, service.LookupCalls);
        await provider.GetNormsAsync("0000" + new string('x', 196));
        Assert.Equal(1000, service.LookupCalls);
        _output.WriteLine($"CACHE-MEASURE entries=1000 retainedBytesPerEntry={retainedBytesPerEntry}");
    }

    private static CommanderCategoryNormsProvider CreateProvider(
        ICommanderCategoryService service,
        FakeCategoryKnowledgeStore? store = null,
        IMemoryCache? cache = null,
        ILogger<CommanderCategoryNormsProvider>? logger = null,
        TimeSpan? timeout = null)
    {
        store ??= new FakeCategoryKnowledgeStore { CommanderDeckCount = 12 };
        cache ??= new MemoryCache(new MemoryCacheOptions());
        logger ??= NullLogger<CommanderCategoryNormsProvider>.Instance;
        return timeout.HasValue
            ? new CommanderCategoryNormsProvider(service, store, cache, logger, timeout.Value)
            : new CommanderCategoryNormsProvider(service, store, cache, logger);
    }

    private static CommanderCategoryResult Result(
        string key,
        int deckCount = 12,
        IReadOnlyList<CommanderCategorySummary>? summaries = null)
        => new(key, [], summaries ?? Summaries(2), 0, new CardDeckTotals(deckCount, new Dictionary<string, int>()));

    private static IReadOnlyList<CommanderCategorySummary> Summaries(int count)
        => Enumerable.Range(1, count).Select(index => new CommanderCategorySummary($"Summary {index}", 1, 1, 0.5)).ToList();

    private static string FindSameStripeKey(string firstKey)
    {
        var stripe = CommanderCategoryNormsProvider.StripeIndexFor(CommanderCategoryNormsProvider.CacheKeyFor(firstKey));
        for (var index = 0; index < 10000; index++)
        {
            var candidate = $"{firstKey} collision {index}";
            if (CommanderCategoryNormsProvider.StripeIndexFor(CommanderCategoryNormsProvider.CacheKeyFor(candidate)) == stripe)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to find a colliding stripe key.");
    }

    private static (RecordingCommanderCategoryService Service, TaskCompletionSource Gate) GatedService()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return (new RecordingCommanderCategoryService(async (key, token, includeCount) =>
        {
            await gate.Task.WaitAsync(token);
            return Result(key);
        }), gate);
    }

    private static string[] CategoryLabels() =>
    [
        "Alpha", "Beta", "Gamma", "Delta", "Epsilon",
        "Zeta", "Eta", "Theta", "Iota", "Kappa",
        "Lambda", "Mu", "Nu", "Xi", "Omicron",
        "Pi", "Rho", "Sigma", "Tau", "Upsilon",
    ];

    private static IReadOnlyList<CategoryKnowledgeRow> CategoryRows()
        => CategoryLabels().Select(label => new CategoryKnowledgeRow(label, $"{label} Card", 1)).ToList();

    private sealed class RecordingCommanderCategoryService : ICommanderCategoryService
    {
        private int _lookupCalls;

        public RecordingCommanderCategoryService(Func<string, CancellationToken, bool, Task<CommanderCategoryResult>> behavior)
        {
            Behavior = behavior;
        }

        public Func<string, CancellationToken, bool, Task<CommanderCategoryResult>> Behavior { get; set; }

        public int LookupCalls => _lookupCalls;

        public bool? LastIncludeProcessedDeckCount { get; private set; }

        public static RecordingCommanderCategoryService Success()
            => new((key, token, includeCount) => Task.FromResult(Result(key)));

        public Task<CommanderCategoryResult> LookupAsync(
            string commanderName,
            CancellationToken cancellationToken = default,
            bool includeProcessedDeckCount = true)
        {
            Interlocked.Increment(ref _lookupCalls);
            LastIncludeProcessedDeckCount = includeProcessedDeckCount;
            return Behavior(commanderName, cancellationToken, includeProcessedDeckCount);
        }
    }

    private static FakeCategoryKnowledgeStore CreateStore()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            CommanderDeckCount = 12,
            CategoryRowsResult =
            [
                new CategoryKnowledgeRow("Ramp", "Sol Ring", 10),
                new CategoryKnowledgeRow("Card Draw", "Rhystic Study", 6),
            ],
        };
        store.Memberships.AddRange(Enumerable.Range(1, 10).Select(deckId => new CategoryDeckMembership("Ramp", "Sol Ring", deckId)));
        store.Memberships.AddRange(Enumerable.Range(1, 6).Select(deckId => new CategoryDeckMembership("Card Draw", "Rhystic Study", deckId)));
        return store;
    }

}
