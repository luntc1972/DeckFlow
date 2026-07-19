using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services.CreatorStyle;

namespace DeckFlow.Web.Tests.Services.CreatorStyle;

internal sealed class FakeMoxfieldOwnerClient : IMoxfieldOwnerClient
{
    public Task<IReadOnlyList<MoxfieldDeckSummary>> ListDeckSummariesAsync(string username, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MoxfieldDeckSummary>>([]);
}

internal sealed class FakeMoxfieldDeckImporter : IMoxfieldDeckImporter
{
    public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<DeckEntry>());
}
