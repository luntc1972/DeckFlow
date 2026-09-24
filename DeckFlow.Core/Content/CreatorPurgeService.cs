namespace DeckFlow.Core.Content;

/// <summary>Purges a resolved creator identity from all creator-scoped content stores.</summary>
public sealed class CreatorPurgeService
{
    private readonly IReadOnlyList<CreatorPurgeStep> _steps;

    /// <summary>Creates a purge service using the seven persistent creator stores.</summary>
    public CreatorPurgeService(
        IContentSiteIndexStore siteIndexStore,
        IContentVideoStore videoStore,
        ICreatorStyleStatedRuleStore statedRuleStore,
        ICreatorStyleProfileStore profileStore,
        ICreatorDeckCacheStore deckCacheStore,
        ICreatorSourceStore creatorSourceStore,
        ICreatorProfileSourceStore profileSourceStore)
    {
        ArgumentNullException.ThrowIfNull(siteIndexStore);
        ArgumentNullException.ThrowIfNull(videoStore);
        ArgumentNullException.ThrowIfNull(statedRuleStore);
        ArgumentNullException.ThrowIfNull(profileStore);
        ArgumentNullException.ThrowIfNull(deckCacheStore);
        ArgumentNullException.ThrowIfNull(creatorSourceStore);
        ArgumentNullException.ThrowIfNull(profileSourceStore);
        _steps = new CreatorPurgeStep[]
        {
            new("site-index", siteIndexStore.DeleteBySourceAsync),
            new("videos", videoStore.DeleteByCreatorAsync),
            new("stated-rules", statedRuleStore.DeleteByCreatorAsync),
            new("profile", profileStore.DeleteByCreatorAsync),
            new("deck-cache", deckCacheStore.DeleteByCreatorAsync),
            new("creator-sources", creatorSourceStore.DeleteByCreatorAsync),
            new("profile-source", profileSourceStore.DeleteByCreatorAsync),
        };
    }

    internal CreatorPurgeService(IReadOnlyList<CreatorPurgeStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _steps = steps;
    }

    /// <summary>Purges the supplied resolved creator identity.</summary>
    public async Task<CreatorPurgeResult> PurgeAsync(CreatorIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var outcomes = new List<CreatorPurgeStoreOutcome>(_steps.Count);
        foreach (var step in _steps)
        {
            try
            {
                var rowsDeleted = await step.DeleteAsync(identity, cancellationToken).ConfigureAwait(false);
                outcomes.Add(new CreatorPurgeStoreOutcome(step.StoreName, true, rowsDeleted, null));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                outcomes.Add(new CreatorPurgeStoreOutcome(step.StoreName, false, 0, exception.Message));
            }
        }

        var artifactFolders = identity.FolderSlugs.Select(folderSlug => $"content-kb/{folderSlug}").ToArray();
        return new CreatorPurgeResult(identity.CanonicalSlug, outcomes, artifactFolders, outcomes.All(outcome => outcome.Succeeded));
    }
}
