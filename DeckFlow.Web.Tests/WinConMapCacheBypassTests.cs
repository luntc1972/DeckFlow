using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Phase-73-class cache-replay regression for the Phase 80 win-condition/combo map (T-80-02-05):
/// proves a wincon-ON packet is never written to the shared <see cref="PacketSessionCache"/> and
/// that flipping the flag OFF never replays a stale ON packet, mirroring the exact
/// TryComputeCacheKeyAsync + <c>_packetCache.TryGet</c> sequence <c>DeckPacketController</c> uses.
/// Reuses the <see cref="DeckAnalysisPacketServiceTests"/> partial-class fakes (CreateCompanionFixtureEntries,
/// FakeMoxfieldDeckImporter, etc.) but constructs the service directly so a single
/// <see cref="PacketSessionCache"/> instance can be held and inspected across the ON and OFF calls.
/// </summary>
public sealed partial class DeckAnalysisPacketServiceTests
{
    private static DeckAnalysisPacketService CreateServiceWithSharedCache(
        PacketSessionCache packetCache,
        IFeatureFlagCache flagCache,
        IMoxfieldDeckImporter moxfieldDeckImporter)
        => new(
            new ScryfallCardResolver(
                new FakeScryfallRestClientFactory(new HttpClient { BaseAddress = new Uri("https://api.scryfall.com/") }),
                new FakeResiliencePipelineProvider(),
                executeCollectionAsyncOverride: (request, _) => Task.FromResult(CreateCollectionResponse(request)),
                executeSearchAsyncOverride: (request, _) => Task.FromResult(CreateSearchResponse(request)),
                executeNamedAsyncOverride: (request, _) => Task.FromResult(CreateNamedResponse(request))),
            new DeckEntryLoader(
                moxfieldDeckImporter,
                new FakeArchidektDeckImporter(),
                new MoxfieldParser(),
                new ArchidektParser()),
            new FakeMechanicLookupService(),
            new FakeCommanderBanListService(),
            new FakeScryfallSetService(),
            new FakeCommanderSpellbookService(),
            new FakeGameChangerCatalogService(EmptyGameChangerCatalog()),
            new AnalysisPromptVariantRegistry(new IAnalysisPromptVariant[]
            {
                new ChatGptAnalysisPromptVariant(),
                new ClaudeAnalysisPromptVariant(),
                new GeminiAnalysisPromptVariant(),
            }),
            new SetUpgradePromptVariantRegistry(new ISetUpgradePromptVariant[]
            {
                new ChatGptSetUpgradePromptVariant(),
                new ClaudeSetUpgradePromptVariant(),
                new GeminiSetUpgradePromptVariant(),
            }),
            packetCache,
            flagCache,
            NullLogger<DeckAnalysisPacketService>.Instance);

    private static DeckAnalysisRequest CreateWinConMapCacheRequest() => new()
    {
        DeckInputSource = DeckInputSource.PublicUrl,
        WorkflowStep = 2,
        DeckSource = "https://www.moxfield.com/decks/test-wincon-map-cache-bypass",
        TargetCommanderBracket = "Upgraded",
        TargetAiPlatform = "ChatGPT",
        SelectedAnalysisQuestions = ["strengths-weaknesses"],
    };

    /// <summary>
    /// Sentinel token proving the win-con block rendered; the header renders whenever the flag is
    /// ON regardless of live combo availability (WINCON-03 - data-unavailable still discloses).
    /// </summary>
    private const string WinConMapHeaderSentinel = "WIN CONDITION & COMBO MAP";

    [Fact]
    public async Task WinConMapCacheBypass_FlagOn_BuildAsyncRendersBlock_AndCacheBypassed_NoReplayAfterFlagFlipsOff()
    {
        var packetCache = new PacketSessionCache();
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["analysis.wincon-map"] = true,
        });
        var importer = new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false));
        var service = CreateServiceWithSharedCache(packetCache, flagCache, importer);
        var request = CreateWinConMapCacheRequest();

        // (1) Flag ON: the block renders AND the read-side cache-key computation returns null (no
        // key at all), so the controller-level replay guard (cacheKey is not null && TryGet(...))
        // can never even attempt a lookup while the flag is ON.
        var keyWhileOn = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.Null(keyWhileOn);

        var onResult = await service.BuildAsync(request, CancellationToken.None);
        Assert.Contains(WinConMapHeaderSentinel, onResult.AnalysisPromptText, StringComparison.Ordinal);

        // (2) Flip the flag OFF and recompute the cache key the SAME way the controller does. If the
        // ON BuildAsync call above had (incorrectly) written to the cache under this now-OFF key, this
        // TryGet would return a stale ON packet here -- proving the Phase-73 cache-replay class did not
        // reopen for the win-con map.
        flagCache.Flags["analysis.wincon-map"] = false;
        var keyWhileOff = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(keyWhileOff));
        var hitAfterFlip = packetCache.TryGet<DeckAnalysisPacketResult>(keyWhileOff!, out var staleCached);
        Assert.False(hitAfterFlip, "No wincon-ON packet should have been cached under the flag-OFF key.");
        Assert.Null(staleCached);

        // (3) A second BuildAsync on the SAME request, now with the flag OFF, must not surface the
        // win-con sentinel -- proving the flag-OFF path rebuilds clean rather than replaying a stale
        // ON packet from anywhere (cache or otherwise).
        var offResult = await service.BuildAsync(request, CancellationToken.None);
        Assert.DoesNotContain(WinConMapHeaderSentinel, offResult.AnalysisPromptText ?? string.Empty, StringComparison.Ordinal);

        // This second (flag-OFF) BuildAsync call legitimately writes an OFF packet under keyWhileOff --
        // that write is expected normal behavior, not a replay of the earlier ON packet.
        var hitAfterOffBuild = packetCache.TryGet<DeckAnalysisPacketResult>(keyWhileOff!, out var freshCached);
        Assert.True(hitAfterOffBuild);
        Assert.NotNull(freshCached);
        Assert.DoesNotContain(WinConMapHeaderSentinel, freshCached!.AnalysisPromptText ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regression (Task 2 must preserve the Phase-73 bypass): command-zone-awareness ON still makes
    /// <see cref="DeckAnalysisPacketService.TryComputeCacheKeyAsync"/> return null after
    /// <c>ShouldBypassPacketCache()</c> generalized the predicate to also cover the win-con map.
    /// </summary>
    [Fact]
    public async Task WinConMapCacheBypass_CommandZoneAwareness_StillBypassesCacheAfterGeneralization()
    {
        var packetCache = new PacketSessionCache();
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["analysis.command-zone-awareness"] = true,
        });
        var importer = new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false));
        var service = CreateServiceWithSharedCache(packetCache, flagCache, importer);
        var request = CreateWinConMapCacheRequest();

        var keyWhileOn = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.Null(keyWhileOn);

        flagCache.Flags["analysis.command-zone-awareness"] = false;
        var keyWhileOff = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(keyWhileOff));
    }

    /// <summary>
    /// Test double for the Phase 80 code-review fix (Codex LOW/MED finding #1). Returns TRUE for
    /// the configured flag key on only the first <c>trueCallCount</c> calls to
    /// <see cref="Snapshot"/>, then FALSE on every subsequent call -- simulating the flag flipping
    /// OFF partway through a single <see cref="DeckAnalysisPacketService.BuildAsync"/> request.
    /// </summary>
    private sealed class FlipAfterNSnapshotsFeatureFlagCache : IFeatureFlagCache
    {
        private readonly string _flagKey;
        private readonly int _trueCallCount;
        private int _callCount;

        public FlipAfterNSnapshotsFeatureFlagCache(string flagKey, int trueCallCount)
        {
            _flagKey = flagKey;
            _trueCallCount = trueCallCount;
        }

        public bool IsEnabled(string key) => Snapshot().TryGetValue(key, out var enabled) && enabled;

        public IReadOnlyDictionary<string, bool> Snapshot()
        {
            _callCount++;
            return new Dictionary<string, bool> { [_flagKey] = _callCount <= _trueCallCount };
        }

        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Phase 80 code-review fix (Codex LOW/MED finding #1): proves the write-side cache decision in
    /// <see cref="DeckAnalysisPacketService.BuildAsync"/> uses the BUILD-TIME LATCHED
    /// <c>winConMapEnabled</c> local rather than a fresh flag-cache re-read. <see cref="FlipAfterNSnapshotsFeatureFlagCache"/>
    /// returns the wincon-map flag as ON only for the first two <see cref="IFeatureFlagCache.Snapshot"/>
    /// calls (the command-zone-awareness latch consumes the first call, the win-con-map latch consumes
    /// the second) and OFF for every call after that -- simulating the flag flipping OFF mid-request,
    /// after enrichment already observed it as ON. Before this fix, the write-side guard called
    /// <c>ShouldBypassPacketCache()</c> again at the end of the method, which would re-invoke
    /// <see cref="IFeatureFlagCache.Snapshot"/> and observe the (by-then) OFF value, incorrectly caching
    /// the enriched packet under a key with no flag/win-con inputs baked in. The fix removes that late
    /// re-read entirely, so the packet built with the win-con block present is correctly never cached.
    /// </summary>
    [Fact]
    public async Task WinConMapCacheBypass_FlagFlipsOffMidRequest_WriteSideSkipsCacheBasedOnLatchedValue()
    {
        var packetCache = new PacketSessionCache();
        var sequencedFlagCache = new FlipAfterNSnapshotsFeatureFlagCache("analysis.wincon-map", trueCallCount: 2);
        var importer = new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false));
        var service = CreateServiceWithSharedCache(packetCache, sequencedFlagCache, importer);
        var request = CreateWinConMapCacheRequest();

        var buildResult = await service.BuildAsync(request, CancellationToken.None);

        // The win-con block rendered -- proving the build-time latch observed the flag as ON (the
        // second Snapshot() call), even though later Snapshot() calls in the same request return OFF.
        Assert.Contains(WinConMapHeaderSentinel, buildResult.AnalysisPromptText, StringComparison.Ordinal);

        // Compute the SAME deterministic cache key via an independent flag-OFF service instance
        // sharing the same PacketSessionCache. Cache-key computation is a pure function of the
        // (pre-Scryfall) deck entries and commander name -- it never depends on flag state -- so
        // this key is identical to whatever key BuildAsync would have written under, above.
        var probeFlagCache = new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.wincon-map"] = false });
        var probeService = CreateServiceWithSharedCache(packetCache, probeFlagCache, new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false)));
        var cacheKey = await probeService.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(cacheKey));

        var hit = packetCache.TryGet<DeckAnalysisPacketResult>(cacheKey!, out var cached);
        Assert.False(hit, "The enriched (flag-ON-at-build-time) packet must never be written to the cache, regardless of later Snapshot() reads.");
        Assert.Null(cached);
    }

    /// <summary>
    /// Sentinel token proving the multi-axis-score block rendered; the header always renders while the
    /// flag is ON (mirrors <see cref="WinConMapHeaderSentinel"/> above).
    /// </summary>
    private const string ScoreBlockHeaderSentinel = "DECK SCORE";

    /// <summary>
    /// Sentinel token proving the interaction-audit block rendered.
    /// </summary>
    private const string InteractionAuditHeaderSentinel = "INTERACTION AUDIT";

    /// <summary>
    /// Follow-up hardening regression (closes the open replay gap noted in the Phase 80 SUMMARY):
    /// before this fix, <c>DeckAnalysisPacketService.ShouldBypassPacketCache()</c> and the
    /// write-side <c>bypassCacheWrite</c> gate only covered command-zone-awareness and the win-con
    /// map, NOT the multi-axis-score flag — a score-ON packet was written to the shared
    /// <see cref="PacketSessionCache"/> and could be replayed unchanged after the flag flipped OFF.
    /// Mirrors <see cref="WinConMapCacheBypass_FlagOn_BuildAsyncRendersBlock_AndCacheBypassed_NoReplayAfterFlagFlipsOff"/>.
    /// </summary>
    [Fact]
    public async Task ScoreCacheBypass_FlagOn_BuildAsyncRendersBlock_AndCacheBypassed_NoReplayAfterFlagFlipsOff()
    {
        var packetCache = new PacketSessionCache();
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["analysis.multi-axis-score"] = true,
        });
        var importer = new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false));
        var service = CreateServiceWithSharedCache(packetCache, flagCache, importer);
        var request = CreateWinConMapCacheRequest();

        // (1) Flag ON: the block renders AND the read-side cache-key computation returns null.
        var keyWhileOn = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.Null(keyWhileOn);

        var onResult = await service.BuildAsync(request, CancellationToken.None);
        Assert.Contains(ScoreBlockHeaderSentinel, onResult.AnalysisPromptText, StringComparison.Ordinal);

        // (2) Flip the flag OFF and recompute the cache key the SAME way the controller does. If the
        // ON BuildAsync call above had (incorrectly) written to the cache under this now-OFF key, this
        // TryGet would return a stale ON packet here.
        flagCache.Flags["analysis.multi-axis-score"] = false;
        var keyWhileOff = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(keyWhileOff));
        var hitAfterFlip = packetCache.TryGet<DeckAnalysisPacketResult>(keyWhileOff!, out var staleCached);
        Assert.False(hitAfterFlip, "No score-ON packet should have been cached under the flag-OFF key.");
        Assert.Null(staleCached);

        // (3) A second BuildAsync on the SAME request, now with the flag OFF, must not surface the
        // score sentinel -- proving the flag-OFF path rebuilds clean rather than replaying a stale
        // ON packet from anywhere (cache or otherwise).
        var offResult = await service.BuildAsync(request, CancellationToken.None);
        Assert.DoesNotContain(ScoreBlockHeaderSentinel, offResult.AnalysisPromptText ?? string.Empty, StringComparison.Ordinal);

        var hitAfterOffBuild = packetCache.TryGet<DeckAnalysisPacketResult>(keyWhileOff!, out var freshCached);
        Assert.True(hitAfterOffBuild);
        Assert.NotNull(freshCached);
        Assert.DoesNotContain(ScoreBlockHeaderSentinel, freshCached!.AnalysisPromptText ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Follow-up hardening regression, same gap as <see cref="ScoreCacheBypass_FlagOn_BuildAsyncRendersBlock_AndCacheBypassed_NoReplayAfterFlagFlipsOff"/>
    /// but for the interaction-audit flag: an interaction-ON packet must never be written to (or
    /// replayed from) the shared <see cref="PacketSessionCache"/>.
    /// </summary>
    [Fact]
    public async Task InteractionAuditCacheBypass_FlagOn_BuildAsyncRendersBlock_AndCacheBypassed_NoReplayAfterFlagFlipsOff()
    {
        var packetCache = new PacketSessionCache();
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["analysis.interaction-audit"] = true,
        });
        var importer = new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false));
        var service = CreateServiceWithSharedCache(packetCache, flagCache, importer);
        var request = CreateWinConMapCacheRequest();

        var keyWhileOn = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.Null(keyWhileOn);

        var onResult = await service.BuildAsync(request, CancellationToken.None);
        Assert.Contains(InteractionAuditHeaderSentinel, onResult.AnalysisPromptText, StringComparison.Ordinal);

        flagCache.Flags["analysis.interaction-audit"] = false;
        var keyWhileOff = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(keyWhileOff));
        var hitAfterFlip = packetCache.TryGet<DeckAnalysisPacketResult>(keyWhileOff!, out var staleCached);
        Assert.False(hitAfterFlip, "No interaction-audit-ON packet should have been cached under the flag-OFF key.");
        Assert.Null(staleCached);

        var offResult = await service.BuildAsync(request, CancellationToken.None);
        Assert.DoesNotContain(InteractionAuditHeaderSentinel, offResult.AnalysisPromptText ?? string.Empty, StringComparison.Ordinal);

        var hitAfterOffBuild = packetCache.TryGet<DeckAnalysisPacketResult>(keyWhileOff!, out var freshCached);
        Assert.True(hitAfterOffBuild);
        Assert.NotNull(freshCached);
        Assert.DoesNotContain(InteractionAuditHeaderSentinel, freshCached!.AnalysisPromptText ?? string.Empty, StringComparison.Ordinal);
    }
}
