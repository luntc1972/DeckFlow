using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Services;

/// <summary>Keeps Studio's local suppression replica consistent with the production authority.</summary>
public sealed class CreatorSuppressionSyncCoordinator
{
    private readonly ICreatorSuppressionStore _localStore;
    private readonly IProdStoreFactory _prodStoreFactory;
    private readonly IStudioProdConnectionSource _prodConnection;

    /// <summary>Creates the sync coordinator.</summary>
    public CreatorSuppressionSyncCoordinator(ICreatorSuppressionStore localStore, IProdStoreFactory prodStoreFactory, IStudioProdConnectionSource prodConnection)
    {
        _localStore = localStore ?? throw new ArgumentNullException(nameof(localStore));
        _prodStoreFactory = prodStoreFactory ?? throw new ArgumentNullException(nameof(prodStoreFactory));
        _prodConnection = prodConnection ?? throw new ArgumentNullException(nameof(prodConnection));
    }

    /// <summary>Re-pulls only when production's revision differs; read failures propagate and block callers.</summary>
    public async Task EnsureCurrentAsync(CancellationToken cancellationToken = default)
    {
        var production = _prodStoreFactory.CreateSuppression(_prodConnection.ConnectionString);
        if (await _localStore.IsStaleComparedToAsync(production, cancellationToken).ConfigureAwait(false))
        {
            await _localStore.ApplySnapshotAsync(await production.ReadSnapshotAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        }
    }
}
