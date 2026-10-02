using DeckFlow.Web.Services.CommanderCategoryNorms;

namespace DeckFlow.Web.Tests;

/// <summary>Test provider that records requested harvest keys.</summary>
internal sealed class FakeCommanderCategoryNormsProvider : ICommanderCategoryNormsProvider
{
    private readonly CommanderCategoryNormsResult? _result;

    public FakeCommanderCategoryNormsProvider(CommanderCategoryNormsResult? result) => _result = result;

    public List<string> RequestedKeys { get; } = [];

    public Task<CommanderCategoryNormsResult?> GetNormsAsync(string harvestKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestedKeys.Add(harvestKey);
        return Task.FromResult(_result);
    }
}
