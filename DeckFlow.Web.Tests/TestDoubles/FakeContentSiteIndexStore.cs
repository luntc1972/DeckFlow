using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Tests;

/// <summary>
/// In-memory test fake for <see cref="IContentSiteIndexStore"/>. Backs the public/admin
/// Content KB controller and seed-loader tests. Records which upsert method was used so the
/// curation-preserving contract (Pitfall 1) can be asserted.
/// </summary>
internal sealed class FakeContentSiteIndexStore : IContentSiteIndexStore
{
    /// <summary>All rows held by the fake (mutable for test setup).</summary>
    public List<ContentSiteIndexRow> Rows { get; } = new();

    /// <summary>Rows passed to <see cref="UpsertRowPreservingVisibilityAsync"/>.</summary>
    public List<ContentSiteIndexRow> PreservingUpserts { get; } = new();

    /// <summary>Rows passed to <see cref="UpsertRowAsync"/> (should stay empty for the seed path).</summary>
    public List<ContentSiteIndexRow> PlainUpserts { get; } = new();

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        PlainUpserts.Add(row);
        Rows.Add(row);
        return Task.CompletedTask;
    }

    public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        PreservingUpserts.Add(row);
        Rows.Add(row);
        return Task.CompletedTask;
    }

    public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(
        string naturalKeyType,
        string naturalKeyValue,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Rows.FirstOrDefault(r =>
            (naturalKeyType == ContentSourceType.Youtube && r.YoutubeVideoId == naturalKeyValue)
            || (naturalKeyType == ContentSourceType.Podcast && r.RssGuid == naturalKeyValue)));

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Rows.Where(r => r.IsVisible).ToList());

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Rows.ToList());

    public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));

    public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
    {
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Id == id)
            {
                Rows[i] = Rows[i] with { IsVisible = visible };
                count++;
            }
        }

        return Task.FromResult(count);
    }

    public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var removed = Rows.RemoveAll(row => row.Id == id);
        return Task.FromResult(removed);
    }

    /// <summary>Sets evergreen flag for a single site-index row.</summary>
    public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
    {
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Id == id)
            {
                Rows[i] = Rows[i] with { IsEvergreen = evergreen };
                count++;
            }
        }

        return Task.FromResult(count);
    }

    public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
    {
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Source == source)
            {
                Rows[i] = Rows[i] with { IsVisible = visible };
                count++;
            }
        }

        return Task.FromResult(count);
    }
}
