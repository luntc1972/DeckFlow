using System.Formats.Tar;
using System.IO.Compression;
using DeckFlow.CLI;

namespace DeckFlow.Core.Tests;

public sealed class EdhrecDataDownloadCommandRunnerTests
{
    [Fact]
    public async Task RunAsync_DownloadsBothOfficialArchivesAndExtractsCsvFiles()
    {
        using var tempRoot = new TempDirectory();
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["https://edhrec.com/data/averages.tgz"] = CreateTgz("averages-jul26-test", "averages.csv", "commander,avg_land\nAtraxa,36\n"),
            ["https://edhrec.com/data/data.tgz"] = CreateTgz("data-jul26-test", "edhrec.csv", "commander,card,count\nAtraxa,Sol Ring,10\n")
        };
        using var httpClient = new HttpClient(new StubHttpMessageHandler(payloads));

        EdhrecDataDownloadResult result = await EdhrecDataDownloadCommandRunner.RunAsync(
            tempRoot.Path,
            dataset: "all",
            extract: true,
            overwrite: true,
            httpClient);

        Assert.Equal(2, result.Files.Count);
        Assert.Collection(
            result.Files,
            file =>
            {
                Assert.Equal("averages", file.Dataset);
                Assert.Equal("averages.tgz", Path.GetFileName(file.ArchivePath));
                Assert.Equal("averages-jul26-test/averages.csv", file.ExtractedCsvPath);
            },
            file =>
            {
                Assert.Equal("data", file.Dataset);
                Assert.Equal("data.tgz", Path.GetFileName(file.ArchivePath));
                Assert.Equal("data-jul26-test/edhrec.csv", file.ExtractedCsvPath);
            });

        Assert.True(File.Exists(Path.Combine(tempRoot.Path, "averages.tgz")));
        Assert.True(File.Exists(Path.Combine(tempRoot.Path, "data.tgz")));
        Assert.True(File.Exists(Path.Combine(tempRoot.Path, "averages-jul26-test", "averages.csv")));
        Assert.True(File.Exists(Path.Combine(tempRoot.Path, "data-jul26-test", "edhrec.csv")));
    }

    private static byte[] CreateTgz(string rootDirectory, string fileName, string content)
    {
        using var sourceRoot = new TempDirectory();
        var directoryPath = Path.Combine(sourceRoot.Path, rootDirectory);
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(Path.Combine(directoryPath, fileName), content);

        using var tarStream = new MemoryStream();
        TarFile.CreateFromDirectory(sourceRoot.Path, tarStream, includeBaseDirectory: false);
        tarStream.Position = 0;

        using var tgzStream = new MemoryStream();
        using (var gzipStream = new GZipStream(tgzStream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            tarStream.CopyTo(gzipStream);
        }

        return tgzStream.ToArray();
    }

    private sealed class StubHttpMessageHandler(IReadOnlyDictionary<string, byte[]> payloads) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.NotNull(request.RequestUri);
            Assert.True(payloads.TryGetValue(request.RequestUri.ToString(), out var payload), $"Unexpected URL: {request.RequestUri}");
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            });
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "deckflow-edhrec-" + Guid.NewGuid().ToString("N"));

        public TempDirectory()
            => Directory.CreateDirectory(Path);

        public void Dispose()
            => Directory.Delete(Path, recursive: true);
    }
}
