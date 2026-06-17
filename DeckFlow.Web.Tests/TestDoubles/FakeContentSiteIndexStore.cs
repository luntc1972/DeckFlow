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

    /// <summary>Rows passed to <see cref="UpsertContentColumnsOnlyAsync"/>.</summary>
    public List<ContentSiteIndexRow> ContentColumnsOnlyUpserts { get; } = new();

    /// <summary>Ids passed to <see cref="DeleteByIdAsync"/>.</summary>
    public List<long> DeletedIds { get; } = new();

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        PlainUpserts.Add(row);
        Rows.Add(ApplyInvariant(row));
        return Task.CompletedTask;
    }

    public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        PreservingUpserts.Add(row);
        Rows.Add(ApplyInvariant(row));
        return Task.CompletedTask;
    }

    public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        ContentColumnsOnlyUpserts.Add(row);
        Rows.Add(ApplyInvariant(row));
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

    public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Rows.Where(r => r.ApprovalStatus == "approved").ToList());

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
                Rows[i] = ApplyInvariant(Rows[i] with { IsVisible = visible, IsHidden = false });
                count++;
            }
        }

        return Task.FromResult(count);
    }

    public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
    {
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Id == id)
            {
                Rows[i] = ApplyInvariant(Rows[i] with
                {
                    IsHidden = hidden,
                    IsVisible = hidden ? false : Rows[i].IsVisible
                });
                count++;
            }
        }

        return Task.FromResult(count);
    }

    public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        DeletedIds.Add(id);
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
                Rows[i] = ApplyInvariant(Rows[i] with { IsVisible = visible, IsHidden = false });
                count++;
            }
        }

        return Task.FromResult(count);
    }

    public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
    {
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Source == source)
            {
                Rows[i] = ApplyInvariant(Rows[i] with
                {
                    IsHidden = hidden,
                    IsVisible = hidden ? false : Rows[i].IsVisible
                });
                count++;
            }
        }

        return Task.FromResult(count);
    }

    public Task<int> SetApprovalStatusAsync(
        string naturalKeyType,
        string naturalKeyValue,
        string status,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ApplyApprovalStatus(naturalKeyType, naturalKeyValue, status));

    public Task<int> SetApprovalStatusAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        string status,
        CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var (type, value) in keys)
        {
            count += ApplyApprovalStatus(type, value, status);
        }

        return Task.FromResult(count);
    }

    private int ApplyApprovalStatus(string naturalKeyType, string naturalKeyValue, string status)
    {
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            var matches =
                (naturalKeyType == ContentSourceType.Youtube && Rows[i].YoutubeVideoId == naturalKeyValue)
                || (naturalKeyType == ContentSourceType.Podcast && Rows[i].RssGuid == naturalKeyValue);
            if (matches)
            {
                Rows[i] = Rows[i] with { ApprovalStatus = status };
                count++;
            }
        }

        return count;
    }

    private static ContentSiteIndexRow ApplyInvariant(ContentSiteIndexRow row)
        => row.IsVisible ? row with { IsHidden = false } : row;
}
