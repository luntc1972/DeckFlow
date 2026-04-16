using DeckFlow.Core.Models;

namespace DeckFlow.Core.Integration;

public interface IMoxfieldDeckImporter
{
    Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default);
}

public interface IArchidektDeckImporter
{
    Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default);
}
