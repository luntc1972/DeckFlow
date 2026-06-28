using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="ContentKbOrchestratorSmokeService"/> (L3) — the read-only startup probe
/// that confirms the Content KB maintenance slice is reachable and reports the blocked-row count.
/// </summary>
public sealed class ContentKbOrchestratorSmokeServiceTests
{
    private static BlockedVideoListResult.BlockedVideoListItem Blocked(string id)
        => new()
        {
            YoutubeVideoId = id,
            BlockedUtc = DateTimeOffset.UnixEpoch,
        };

    [Fact]
    public async Task ProbeAsync_ReturnsBlockedRowCountFromOrchestrator()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedBlockedResult = new BlockedVideoListResult
            {
                Items = new[] { Blocked("a"), Blocked("b"), Blocked("c") },
            },
        };
        var smoke = new ContentKbOrchestratorSmokeService(orchestrator);

        var count = await smoke.ProbeAsync();

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ProbeAsync_NoBlockedRows_ReturnsZero()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedBlockedResult = new BlockedVideoListResult(),
        };
        var smoke = new ContentKbOrchestratorSmokeService(orchestrator);

        var count = await smoke.ProbeAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public void Constructor_NullOrchestrator_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ContentKbOrchestratorSmokeService(null!));
}
