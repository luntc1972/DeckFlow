using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Test fake for <see cref="IContentKbSeedLoader"/>. Records how many times the reload was
/// invoked and returns a configurable row count.
/// </summary>
internal sealed class FakeContentKbSeedLoader : IContentKbSeedLoader
{
    private readonly int _rowCount;

    public FakeContentKbSeedLoader(int rowCount = 0)
    {
        _rowCount = rowCount;
    }

    /// <summary>Number of times <see cref="LoadIfPresentAsync"/> was called.</summary>
    public int LoadCallCount { get; private set; }

    public Task<int> LoadIfPresentAsync(CancellationToken cancellationToken = default)
    {
        LoadCallCount++;
        return Task.FromResult(_rowCount);
    }
}
