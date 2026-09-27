namespace DeckFlow.Studio.Services;

/// <summary>Runs the read-only production body-hash coverage audit for content visible on the site.</summary>
public interface IDataCoverageAudit
{
    /// <summary>Audits every visible, approved production content row against its <c>/data</c> artifact.</summary>
    /// <param name="prodConnectionString">Production connection string used only by the read-only reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Counts and non-passing rows.</returns>
    Task<DataCoverageReport> RunAsync(
        string prodConnectionString,
        CancellationToken cancellationToken = default);
}

/// <summary>Classification for a visible, approved row that does not pass the audit.</summary>
public enum DataCoverageFailureBucket
{
    /// <summary>The artifact was downloaded but its body hash differs from the stored hash.</summary>
    Mismatch,

    /// <summary>The artifact could not be downloaded from <c>/data</c>.</summary>
    Missing,

    /// <summary>The row has no usable stored SHA-256 value.</summary>
    NoStoredHash,
}

/// <summary>One visible, approved production row that does not pass the audit.</summary>
/// <param name="ArtifactPath">Production artifact path under <c>/data</c>.</param>
/// <param name="Bucket">Why the row does not pass.</param>
public sealed record DataCoverageFailureRow(string ArtifactPath, DataCoverageFailureBucket Bucket);

/// <summary>Result of one production <c>/data</c> coverage audit.</summary>
public sealed record DataCoverageReport(
    int TotalLiveCount,
    int PresentMatchCount,
    int PresentMismatchCount,
    int MissingCount,
    int NoStoredHashCount,
    IReadOnlyList<DataCoverageFailureRow> FailingRows);
