using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// In-memory test fake for <see cref="IContentSiteIndexStore"/>.
/// Adapted from DeckFlow.Web.Tests version; includes both SetApprovalStatusAsync overloads
/// and tracks call arguments for assertion.
/// </summary>
internal sealed class FakeContentSiteIndexStore : IContentSiteIndexStore
{
    public List<ContentSiteIndexRow> Rows { get; } = new();

    // Approval-status call tracking
    public List<(string Type, string Value, string Status)> SingleApprovalCalls { get; } = new();
    public List<(IReadOnlyList<(string Type, string Value)> Keys, string Status)> BatchApprovalCalls { get; } = new();

    // Upsert-method call tracking — lets SC3 (D-08) assert ONLY UpsertContentColumnsOnlyAsync
    // was invoked on the prod store (never the two full-row upserts).
    public List<string> UpsertMethodCalls { get; } = new();

    public List<(IReadOnlyList<(string Type, string Value)> Keys, DateTimeOffset PushedUtc)> StampCalls { get; } = new();

    public List<(IReadOnlyList<(string Type, string Value)> Keys, bool Visible)> VisibilityKeyCalls { get; } = new();

    // Reconcile Apply routes its destructive soft-hide through HideSeedManagedAsync (ownership-scoped),
    // NOT the ownership-agnostic SetVisibilityAsync — this list lets Apply tests assert the safe path.
    public List<IReadOnlyList<(string Type, string Value)>> HideSeedManagedKeyCalls { get; } = new();

    // D-10 awaiting-confirm marker call tracking (90-05) — the interface declares these as throwing
    // default interface methods, so this fake real-implements + tracks them for assertion.
    public List<(IReadOnlyList<(string Type, string Value)> Keys, DateTimeOffset WhenUtc)> SetAwaitingConfirmCalls { get; } = new();

    public List<IReadOnlyList<(string Type, string Value)>> ClearAwaitingConfirmCalls { get; } = new();

    // Batch-upsert call tracking
    public List<IReadOnlyList<ContentSiteIndexRow>> BatchUpsertCalls { get; } = new();

    // ── Fault-injection hooks (47-03) ─────────────────────────────────────────
    // Natural keys (YoutubeVideoId ?? RssGuid) that should throw from the content-columns-only
    // upsert; drives the per-row partial-failure + HIGH-2 secret-leak tests.
    public HashSet<string> KeysToFailOnUpsert { get; } = new();

    // Message used when an upsert is forced to fail. The HIGH-2 secret test sets this to a
    // sentinel-bearing connection string so the page's catch path can be proven NOT to surface it.
    public string UpsertFailureMessage { get; set; } = "Simulated prod upsert failure";

    // If set, GetAllRowsAsync throws with this message. The HIGH-2 diff-read test sets the
    // sentinel-bearing string to prove the diff catch never surfaces ex.Message.
    public string? ReadFailureMessage { get; set; }

    // Schema call tracking — lets H3 tests assert the read-only diff path never issues DDL.
    public int EnsureSchemaCallCount { get; private set; }

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        EnsureSchemaCallCount++;
        return Task.CompletedTask;
    }

    public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        // MEDIUM-4: full-row upsert is forbidden on the prod store — fail loudly if ever called
        // so an accidental clobber of is_visible / is_evergreen breaks the test, not just an
        // absent assertion.
        UpsertMethodCalls.Add("UpsertRowAsync");
        throw new InvalidOperationException("full-row upsert is forbidden on the prod store");
    }

    public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        // MEDIUM-4: full-row upsert is forbidden on the prod store (see UpsertRowAsync).
        UpsertMethodCalls.Add("UpsertRowPreservingVisibilityAsync");
        throw new InvalidOperationException("full-row upsert is forbidden on the prod store");
    }

    public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        UpsertMethodCalls.Add("UpsertContentColumnsOnlyAsync");

        var key = row.YoutubeVideoId ?? row.RssGuid ?? string.Empty;
        if (KeysToFailOnUpsert.Contains(key))
        {
            throw new InvalidOperationException(UpsertFailureMessage);
        }

        Rows.Add(row);
        return Task.CompletedTask;
    }

    public Task UpsertContentColumnsOnlyBatchAsync(
        IReadOnlyList<ContentSiteIndexRow> rows,
        CancellationToken cancellationToken = default)
    {
        UpsertMethodCalls.Add("UpsertContentColumnsOnlyBatchAsync");
        BatchUpsertCalls.Add(rows);

        // Why: true all-or-nothing in-memory — scan ALL rows for a fail key before adding ANY
        // to Rows, mirroring the transactional rollback semantics of the real implementation.
        // A partial-add followed by an exception would leave Rows in an inconsistent state.
        foreach (var row in rows)
        {
            var key = row.YoutubeVideoId ?? row.RssGuid ?? string.Empty;
            if (KeysToFailOnUpsert.Contains(key))
            {
                // Throw without adding any row — no partial state.
                throw new ContentSiteIndexBatchUpsertException(
                    row.Title,
                    row.YoutubeVideoId is not null ? ContentSourceType.Youtube : ContentSourceType.Podcast,
                    key,
                    $"Simulated batch rollback at row '{row.Title}'",
                    new InvalidOperationException(UpsertFailureMessage));
            }
        }

        // All rows passed — add all.
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

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
    {
        if (ReadFailureMessage is not null)
        {
            throw new InvalidOperationException(ReadFailureMessage);
        }

        return Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Rows.ToList());
    }

    public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));

    public Task<ContentSiteIndexRow?> GetPublishedByIdAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(Rows.FirstOrDefault(r => r.Id == id && r.IsVisible && r.ApprovalStatus == "approved"));

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

    public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
    {
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Id == id)
            {
                Rows[i] = Rows[i] with { IsHidden = hidden };
                count++;
            }
        }

        return Task.FromResult(count);
    }

    public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var removed = Rows.RemoveAll(r => r.Id == id);
        return Task.FromResult(removed);
    }

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

    public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
    {
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Source == source)
            {
                Rows[i] = Rows[i] with { IsHidden = hidden };
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
    {
        SingleApprovalCalls.Add((naturalKeyType, naturalKeyValue, status));
        return Task.FromResult(ApplyApprovalStatus(naturalKeyType, naturalKeyValue, status));
    }

    public Task<int> SetApprovalStatusAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        string status,
        CancellationToken cancellationToken = default)
    {
        BatchApprovalCalls.Add((keys, status));
        var count = 0;
        foreach (var (type, value) in keys)
        {
            count += ApplyApprovalStatus(type, value, status);
        }

        return Task.FromResult(count);
    }

    // If set, StampPushedToProdAsync throws this exception. Lets the PublishCoordinator
    // local-stamp-failed (non-fatal) path be exercised without a live store.
    public Exception? ThrowOnStamp { get; set; }

    public Task<int> StampPushedToProdAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        DateTimeOffset pushedUtc,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnStamp is not null)
        {
            throw ThrowOnStamp;
        }

        StampCalls.Add((keys, pushedUtc));
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            var naturalKey = ContentIndexExportRow.From(Rows[i]);
            var match = keys.Any(key =>
                key.Type == naturalKey.NaturalKeyType
                && key.Value == naturalKey.NaturalKeyValue);
            if (!match)
            {
                continue;
            }

            Rows[i] = Rows[i] with { PushedToProdUtc = pushedUtc };
            count++;
        }

        return Task.FromResult(count);
    }

    public Task<int> SetVisibilityAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        bool visible,
        CancellationToken cancellationToken = default)
    {
        VisibilityKeyCalls.Add((keys, visible));
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            var naturalKey = ContentIndexExportRow.From(Rows[i]);
            var match = keys.Any(key =>
                key.Type == naturalKey.NaturalKeyType
                && key.Value == naturalKey.NaturalKeyValue);
            if (!match)
            {
                continue;
            }

            Rows[i] = Rows[i] with { IsVisible = visible, IsHidden = false };
            count++;
        }

        return Task.FromResult(count);
    }

    // Mirrors the real store: only rows whose CURRENT seed_managed is true are hidden — the ownership
    // predicate is enforced at write time, so a row flipped to false/null after the caller's snapshot
    // is protected (never hidden, never counted).
    public Task<int> HideSeedManagedAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        CancellationToken cancellationToken = default)
    {
        HideSeedManagedKeyCalls.Add(keys);
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            var naturalKey = ContentIndexExportRow.From(Rows[i]);
            var match = keys.Any(key =>
                key.Type == naturalKey.NaturalKeyType
                && key.Value == naturalKey.NaturalKeyValue);
            if (!match || Rows[i].SeedManaged != true)
            {
                continue;
            }

            Rows[i] = Rows[i] with { IsVisible = false, IsHidden = false };
            count++;
        }

        return Task.FromResult(count);
    }

    public Task<int> SetAwaitingConfirmAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        DateTimeOffset whenUtc,
        CancellationToken cancellationToken = default)
    {
        SetAwaitingConfirmCalls.Add((keys, whenUtc));
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            var naturalKey = ContentIndexExportRow.From(Rows[i]);
            var match = keys.Any(key =>
                key.Type == naturalKey.NaturalKeyType
                && key.Value == naturalKey.NaturalKeyValue);
            if (!match)
            {
                continue;
            }

            Rows[i] = Rows[i] with { AwaitingConfirmUtc = whenUtc };
            count++;
        }

        return Task.FromResult(count);
    }

    public Task<int> ClearAwaitingConfirmAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        CancellationToken cancellationToken = default)
    {
        ClearAwaitingConfirmCalls.Add(keys);
        var count = 0;
        for (var i = 0; i < Rows.Count; i++)
        {
            var naturalKey = ContentIndexExportRow.From(Rows[i]);
            var match = keys.Any(key =>
                key.Type == naturalKey.NaturalKeyType
                && key.Value == naturalKey.NaturalKeyValue);
            if (!match)
            {
                continue;
            }

            Rows[i] = Rows[i] with { AwaitingConfirmUtc = null };
            count++;
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
}
