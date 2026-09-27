using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

internal sealed class FakeDataCoverageAudit : IDataCoverageAudit
{
    public DataCoverageReport? CannedReport { get; set; }

    public Exception? ExceptionToThrow { get; set; }

    public Task<DataCoverageReport> RunAsync(
        string prodConnectionString,
        CancellationToken cancellationToken = default)
        => ExceptionToThrow is null
            ? Task.FromResult(CannedReport ?? new DataCoverageReport(0, 0, 0, 0, 0, Array.Empty<DataCoverageFailureRow>()))
            : Task.FromException<DataCoverageReport>(ExceptionToThrow);
}
