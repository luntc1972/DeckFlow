using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Test fake for <see cref="IProdStoreFactory"/>. Returns a pre-seeded
/// <see cref="IContentSiteIndexStore"/> representing prod rows, ignoring the connection
/// string so bUnit tests never touch a live Postgres connection.
/// </summary>
internal sealed class FakeProdStoreFactory : IProdStoreFactory
{
    private readonly IContentSiteIndexStore _prodStore;
    private readonly ICreatorSuppressionStore _suppressionStore = new CreatorSuppressionStore(RelationalDatabaseConnection.FromSqlitePath(Path.Combine(Path.GetTempPath(), $"fake-prod-suppression-{Guid.NewGuid():N}.db")));

    public FakeProdStoreFactory(IContentSiteIndexStore prodStore)
    {
        ArgumentNullException.ThrowIfNull(prodStore);
        _prodStore = prodStore;
    }

    /// <summary>Returns the pre-configured fake prod store; ignores the connection string.</summary>
    public IContentSiteIndexStore Create(string connectionString) => _prodStore;
    public ICreatorSuppressionStore CreateSuppression(string connectionString) => _suppressionStore;
}
