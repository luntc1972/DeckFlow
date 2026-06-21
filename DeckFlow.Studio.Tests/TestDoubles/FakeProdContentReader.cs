using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// In-memory test fake for <see cref="IProdContentReader"/> — the DISTINCT read-only prod side
/// (R2), kept separate from the local <see cref="FakeContentSiteIndexStore"/>. It returns a seeded
/// row set from <see cref="ReadAllAsync"/> and counts reads. It deliberately exposes NO write /
/// upsert / delete / approval / schema method — that absence IS the structural write-free
/// guarantee: no apply path can mutate prod through this reader even by mistake (R1/R2).
/// </summary>
internal sealed class FakeProdContentReader : IProdContentReader
{
    /// <summary>Seeded prod rows returned from <see cref="ReadAllAsync"/>.</summary>
    public List<ContentSiteIndexRow> Rows { get; } = new();

    /// <summary>Number of times the page read prod through this reader (proves it is the read path).</summary>
    public int ReadCallCount { get; private set; }

    /// <summary>If set, <see cref="ReadAllAsync"/> throws with this message (may carry a sentinel).</summary>
    public string? ReadFailureMessage { get; set; }

    public Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ReadCallCount++;
        if (ReadFailureMessage is not null)
        {
            throw new InvalidOperationException(ReadFailureMessage);
        }

        return Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Rows.ToList());
    }
}
