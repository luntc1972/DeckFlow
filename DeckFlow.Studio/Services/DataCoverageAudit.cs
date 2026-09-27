using System.Text;
using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Read-only implementation of <see cref="IDataCoverageAudit"/>. It reads production only through
/// <see cref="IProdContentReader"/> and downloads artifacts only through
/// <see cref="ISshArtifactDownloader"/>; it has no production write dependency.
/// </summary>
public sealed class DataCoverageAudit : IDataCoverageAudit
{
    private readonly IProdContentReader _prodReader;
    private readonly ISshArtifactDownloader _artifactDownloader;

    /// <summary>Creates the audit over the registered read-only production reader and SFTP downloader.</summary>
    public DataCoverageAudit(IProdContentReader prodReader, ISshArtifactDownloader artifactDownloader)
    {
        _prodReader = prodReader;
        _artifactDownloader = artifactDownloader;
    }

    /// <inheritdoc/>
    public async Task<DataCoverageReport> RunAsync(
        string prodConnectionString,
        CancellationToken cancellationToken = default)
    {
        var publishedRows = (await _prodReader.ReadAllAsync(prodConnectionString, cancellationToken))
            .Where(row => row.IsVisible && string.Equals(row.ApprovalStatus, "approved", StringComparison.Ordinal))
            .ToList();
        var failures = new List<DataCoverageFailureRow>();
        var hashableRows = new List<DeckFlow.Core.Knowledge.ContentSiteIndexRow>();

        foreach (var row in publishedRows)
        {
            if (IsSha256(row.BodySha256))
            {
                hashableRows.Add(row);
            }
            else
            {
                failures.Add(new DataCoverageFailureRow(row.ArtifactPath, DataCoverageFailureBucket.NoStoredHash));
            }
        }

        var stagingRoot = Path.Combine(Path.GetTempPath(), $"deckflow-data-coverage-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stagingRoot);
            var downloads = hashableRows
                .Select((row, index) => new SshDownloadRequest(row.ArtifactPath, $"{index}.md"))
                .ToList();
            var results = await _artifactDownloader.DownloadArtifactsAsync(downloads, stagingRoot, cancellationToken: cancellationToken);
            var matchCount = 0;
            var mismatchCount = 0;
            var missingCount = 0;

            for (var index = 0; index < hashableRows.Count; index++)
            {
                var row = hashableRows[index];
                var result = results[index];
                if (!result.Success || !File.Exists(result.LocalPath))
                {
                    failures.Add(new DataCoverageFailureRow(row.ArtifactPath, DataCoverageFailureBucket.Missing));
                    missingCount++;
                    continue;
                }

                var rawArtifact = await File.ReadAllTextAsync(result.LocalPath, Encoding.UTF8, cancellationToken);
                var actualHash = ContentSiteIndexContentSignature.ComputeBodySha256(rawArtifact);
                if (string.Equals(actualHash, row.BodySha256, StringComparison.OrdinalIgnoreCase))
                {
                    matchCount++;
                }
                else
                {
                    failures.Add(new DataCoverageFailureRow(row.ArtifactPath, DataCoverageFailureBucket.Mismatch));
                    mismatchCount++;
                }
            }

            return new DataCoverageReport(
                publishedRows.Count,
                matchCount,
                mismatchCount,
                missingCount,
                failures.Count(row => row.Bucket == DataCoverageFailureBucket.NoStoredHash),
                failures);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
