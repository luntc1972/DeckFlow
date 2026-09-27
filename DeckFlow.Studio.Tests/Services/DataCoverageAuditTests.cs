using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests.Services;

/// <summary>Tests the read-only production <c>/data</c> body-hash audit.</summary>
public sealed class DataCoverageAuditTests
{
    [Fact]
    public async Task RunAsync_LiveMatchingBody_CountsMatch()
    {
        var body = "# Body\n";
        var row = MakeRow("match", ContentSiteIndexContentSignature.ComputeBodySha256(body));
        var report = await RunAsync(row, body);

        Assert.Equal(1, report.PresentMatchCount);
        Assert.Empty(report.FailingRows);
    }

    [Fact]
    public async Task RunAsync_LiveDifferentBody_CountsMismatch()
    {
        var row = MakeRow("mismatch", ContentSiteIndexContentSignature.ComputeBodySha256("expected\n"));
        var report = await RunAsync(row, "actual\n");

        Assert.Equal(1, report.PresentMismatchCount);
        Assert.Equal(DataCoverageFailureBucket.Mismatch, Assert.Single(report.FailingRows).Bucket);
    }

    [Fact]
    public async Task RunAsync_MissingFile_CountsMissing()
    {
        var row = MakeRow("missing", ContentSiteIndexContentSignature.ComputeBodySha256("body\n"));
        var report = await RunAsync(row, null);

        Assert.Equal(1, report.MissingCount);
        Assert.Equal(DataCoverageFailureBucket.Missing, Assert.Single(report.FailingRows).Bucket);
    }

    [Fact]
    public async Task RunAsync_AllDownloadsFail_ThrowsWithoutRecordingCoverage()
    {
        var row = MakeRow("all-fail", ContentSiteIndexContentSignature.ComputeBodySha256("body\n"));
        var reader = new FakeProdContentReader();
        reader.Rows.Add(row);
        var downloader = new FakeSshArtifactDownloader { FailureKind = SshDownloadFailureKind.Transport };
        downloader.FilesToFail.Add(row.ArtifactPath);

        var exception = await Assert.ThrowsAsync<DataCoverageTransportException>(() => new DataCoverageAudit(reader, downloader)
            .RunAsync("Host=example", CancellationToken.None));

        Assert.Equal(DataCoverageTransportException.OperatorMessage, exception.Message);
    }

    [Fact]
    public async Task RunAsync_RejectedDownload_CountsMissingWithoutThrow()
    {
        var row = MakeRow("rejected", ContentSiteIndexContentSignature.ComputeBodySha256("body\n"));
        var reader = new FakeProdContentReader();
        reader.Rows.Add(row);
        var downloader = new FakeSshArtifactDownloader { FailureKind = SshDownloadFailureKind.Rejected };
        downloader.FilesToFail.Add(row.ArtifactPath);

        var report = await new DataCoverageAudit(reader, downloader).RunAsync("Host=example", CancellationToken.None);

        Assert.Equal(1, report.MissingCount);
        Assert.Equal(DataCoverageFailureBucket.Missing, Assert.Single(report.FailingRows).Bucket);
    }

    [Fact]
    public async Task RunAsync_NotFoundAlongsideSuccess_CountsMissingWithoutThrow()
    {
        var matching = MakeRow("matching", ContentSiteIndexContentSignature.ComputeBodySha256("matching\n"));
        var missing = MakeRow("missing", ContentSiteIndexContentSignature.ComputeBodySha256("missing\n"));
        var reader = new FakeProdContentReader();
        reader.Rows.Add(matching);
        reader.Rows.Add(missing);
        var downloader = new FakeSshArtifactDownloader();
        downloader.FileContents[matching.ArtifactPath] = "matching\n";
        downloader.FilesToFail.Add(missing.ArtifactPath);

        var report = await new DataCoverageAudit(reader, downloader).RunAsync("Host=example", CancellationToken.None);

        Assert.Equal(1, report.PresentMatchCount);
        Assert.Equal(1, report.MissingCount);
        Assert.Equal(missing.ArtifactPath, Assert.Single(report.FailingRows).ArtifactPath);
    }

    [Fact]
    public async Task RunAsync_TransportFailureAlongsideSuccess_ThrowsWithoutRecordingCoverage()
    {
        var matching = MakeRow("matching", ContentSiteIndexContentSignature.ComputeBodySha256("matching\n"));
        var unavailable = MakeRow("unavailable", ContentSiteIndexContentSignature.ComputeBodySha256("unavailable\n"));
        var reader = new FakeProdContentReader();
        reader.Rows.Add(matching);
        reader.Rows.Add(unavailable);
        var downloader = new FakeSshArtifactDownloader { FailureKind = SshDownloadFailureKind.Transport };
        downloader.FileContents[matching.ArtifactPath] = "matching\n";
        downloader.FilesToFail.Add(unavailable.ArtifactPath);

        var exception = await Assert.ThrowsAsync<DataCoverageTransportException>(() => new DataCoverageAudit(reader, downloader)
            .RunAsync("Host=example", CancellationToken.None));

        Assert.Equal(DataCoverageTransportException.OperatorMessage, exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-hash")]
    public async Task RunAsync_NullOrMalformedStoredHash_CountsNoStoredHash(string? storedHash)
    {
        var row = MakeRow("no-hash", storedHash);
        var report = await RunAsync(row, "body\n");

        Assert.Equal(1, report.NoStoredHashCount);
        Assert.Equal(DataCoverageFailureBucket.NoStoredHash, Assert.Single(report.FailingRows).Bucket);
    }

    [Fact]
    public async Task RunAsync_FrontMatterBody_MatchesStoredBodyHash()
    {
        const string rawArtifact = "---\ntitle: Example\n---\n# Body\n";
        var row = MakeRow("normalized", ContentSiteIndexContentSignature.ComputeBodySha256("# Body\n"));
        var report = await RunAsync(row, rawArtifact);

        Assert.Equal(1, report.PresentMatchCount);
    }

    [Fact]
    public async Task RunAsync_CrLfBody_MatchesLfStoredBodyHash()
    {
        var row = MakeRow("crlf", ContentSiteIndexContentSignature.ComputeBodySha256("# Body\n"));
        var report = await RunAsync(row, "# Body\r\n");

        Assert.Equal(1, report.PresentMatchCount);
    }

    [Fact]
    public async Task RunAsync_NotVisibleRow_IsIgnored()
    {
        var row = MakeRow("not-visible", ContentSiteIndexContentSignature.ComputeBodySha256("body\n")) with { IsVisible = false };
        var report = await RunAsync(row, null);

        Assert.Equal(0, report.TotalLiveCount);
    }

    [Fact]
    public async Task RunAsync_VisibleApprovedRowWithoutPublishedUtc_IsAudited()
    {
        var row = MakeRow("approved-no-upload-date", ContentSiteIndexContentSignature.ComputeBodySha256("body\n")) with
        {
            PublishedUtc = null,
        };
        var report = await RunAsync(row, "body\n");

        Assert.Equal(1, report.TotalLiveCount);
    }

    [Fact]
    public async Task RunAsync_VisibleNotApprovedRow_IsIgnored()
    {
        var row = MakeRow("pending", ContentSiteIndexContentSignature.ComputeBodySha256("body\n")) with
        {
            ApprovalStatus = "pending",
        };
        var report = await RunAsync(row, null);

        Assert.Equal(0, report.TotalLiveCount);
    }

    private async Task<DataCoverageReport> RunAsync(ContentSiteIndexRow row, string? downloadedContent)
    {
        var reader = new FakeProdContentReader();
        reader.Rows.Add(row);
        var downloader = new FakeSshArtifactDownloader();
        if (downloadedContent is null)
        {
            downloader.FilesToFail.Add(row.ArtifactPath);
        }
        else
        {
            downloader.FileContents[row.ArtifactPath] = downloadedContent;
        }

        var audit = new DataCoverageAudit(reader, downloader);
        return await audit.RunAsync("Host=example", CancellationToken.None);
    }

    private static ContentSiteIndexRow MakeRow(string id, string? bodySha256)
        => new()
        {
            Id = 1,
            Source = "test-channel",
            Title = "Test content",
            VideoUrl = "https://example.test/video",
            ArtifactPath = $"content-kb/test-channel/{id}.md",
            PublishedUtc = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
            IndexedUtc = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = id,
            BodySha256 = bodySha256,
            IsVisible = true,
            ApprovalStatus = "approved",
        };
}
