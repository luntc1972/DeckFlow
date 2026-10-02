using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using Xunit;

namespace DeckFlow.Web.Tests;

// Why: RESEARCH Pitfall 1 requires both cache sides to bypass enriched packets; the norms latch is
// the sixth Snapshot call, so a mid-request flag flip must use that latched value at cache write.
public sealed partial class DeckAnalysisPacketServiceTests
{
    private const string CommanderCategoryNormsHeaderSentinel = "HARVESTED COMMANDER CATEGORY NORMS";

    [Fact]
    public async Task CommanderCategoryNormsCacheBypass_FlagOn_ReadSideKeyNull_BuildRendersBlock_NoReplayAfterFlipOff()
    {
        var (packetCache, flagCache, service, request) = ArrangeNormsCache();

        Assert.Null(await service.TryComputeCacheKeyAsync(request, CancellationToken.None));

        var onResult = await service.BuildAsync(request, CancellationToken.None);
        Assert.Contains(CommanderCategoryNormsHeaderSentinel, onResult.AnalysisPromptText, StringComparison.Ordinal);

        flagCache.Flags[DeckAnalysisPacketService.CommanderCategoryNormsFlag] = false;
        var offKey = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(offKey));
        AssertNotCached(packetCache, offKey!);

        var offResult = await service.BuildAsync(request, CancellationToken.None);
        Assert.DoesNotContain(CommanderCategoryNormsHeaderSentinel, offResult.AnalysisPromptText ?? string.Empty, StringComparison.Ordinal);
        Assert.True(packetCache.TryGet<DeckAnalysisPacketResult>(offKey!, out var freshCached));
        Assert.NotNull(freshCached);
    }

    [Fact]
    public async Task CommanderCategoryNormsCacheBypass_FlagOn_WriteSide_NothingCached()
    {
        var (packetCache, flagCache, service, request) = ArrangeNormsCache();

        await service.BuildAsync(request, CancellationToken.None);

        flagCache.Flags[DeckAnalysisPacketService.CommanderCategoryNormsFlag] = false;
        var probeKey = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(probeKey));
        AssertNotCached(packetCache, probeKey!);
    }

    [Fact]
    public async Task CommanderCategoryNormsCacheBypass_FlagFlipsOffMidRequest_LatchedValueSkipsCacheWrite()
    {
        var packetCache = new PacketSessionCache();
        var flagCache = new FlipAfterNSnapshotsFeatureFlagCache(DeckAnalysisPacketService.CommanderCategoryNormsFlag, trueCallCount: 6);
        var provider = new FakeCommanderCategoryNormsProvider(PacketByteIdentityFixtures.FixedCommanderCategoryNorms());
        var service = CreateServiceWithSharedCache(packetCache, flagCache, new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false)), provider);
        var request = CreateWinConMapCacheRequest();

        var result = await service.BuildAsync(request, CancellationToken.None);
        Assert.Contains(CommanderCategoryNormsHeaderSentinel, result.AnalysisPromptText, StringComparison.Ordinal);

        var probeService = CreateServiceWithSharedCache(packetCache, new FakeFeatureFlagCache(), new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false)));
        var probeKey = await probeService.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(probeKey));
        AssertNotCached(packetCache, probeKey!);
    }

    [Fact]
    public async Task CommanderCategoryNormsCacheBypass_FlagOff_Control_PacketCachedWithoutBlock()
    {
        var packetCache = new PacketSessionCache();
        var flagCache = new FakeFeatureFlagCache();
        var provider = new FakeCommanderCategoryNormsProvider(PacketByteIdentityFixtures.FixedCommanderCategoryNorms());
        var service = CreateServiceWithSharedCache(packetCache, flagCache, new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false)), provider);
        var request = CreateWinConMapCacheRequest();

        var key = await service.TryComputeCacheKeyAsync(request, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(key));
        var result = await service.BuildAsync(request, CancellationToken.None);

        Assert.DoesNotContain(CommanderCategoryNormsHeaderSentinel, result.AnalysisPromptText ?? string.Empty, StringComparison.Ordinal);
        Assert.True(packetCache.TryGet<DeckAnalysisPacketResult>(key!, out var cached));
        Assert.NotNull(cached);
        Assert.Empty(provider.RequestedKeys);
    }

    private static (PacketSessionCache PacketCache, FakeFeatureFlagCache FlagCache, DeckAnalysisPacketService Service, DeckAnalysisRequest Request) ArrangeNormsCache()
    {
        var packetCache = new PacketSessionCache();
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            [DeckAnalysisPacketService.CommanderCategoryNormsFlag] = true,
        });
        var provider = new FakeCommanderCategoryNormsProvider(PacketByteIdentityFixtures.FixedCommanderCategoryNorms());
        var service = CreateServiceWithSharedCache(packetCache, flagCache, new FakeMoxfieldDeckImporter(entries: CreateCompanionFixtureEntries(includeBackgroundCommander: false)), provider);
        return (packetCache, flagCache, service, CreateWinConMapCacheRequest());
    }

    private static void AssertNotCached(PacketSessionCache packetCache, string key)
    {
        Assert.False(packetCache.TryGet<DeckAnalysisPacketResult>(key, out var cached));
        Assert.Null(cached);
    }
}
