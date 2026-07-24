using System.Formats.Tar;
using System.IO.Compression;

namespace DeckFlow.CLI;

internal sealed record EdhrecDataDownloadResult(IReadOnlyList<EdhrecDataDownloadFile> Files);

internal sealed record EdhrecDataDownloadFile(
    string Dataset,
    string Url,
    string ArchivePath,
    string? ExtractedCsvPath);

internal static class EdhrecDataDownloadCommandRunner
{
    private const string UserAgent = "DeckFlow.CLI/1.0 (+https://github.com/luntc1972/DeckFlow)";

    private static readonly EdhrecDataset[] Datasets =
    [
        new("averages", "https://edhrec.com/data/averages.tgz", "averages.tgz", "averages.csv"),
        new("data", "https://edhrec.com/data/data.tgz", "data.tgz", "edhrec.csv")
    ];

    public static async Task<int> RunAsync(string outputDirectory, string dataset, bool extract, bool overwrite)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        var result = await RunAsync(outputDirectory, dataset, extract, overwrite, httpClient);

        foreach (var file in result.Files)
        {
            Console.WriteLine($"{file.Dataset}: {file.ArchivePath}");
            if (!string.IsNullOrWhiteSpace(file.ExtractedCsvPath))
            {
                Console.WriteLine($"{file.Dataset} csv: {file.ExtractedCsvPath}");
            }
        }

        return 0;
    }

    internal static async Task<EdhrecDataDownloadResult> RunAsync(
        string outputDirectory,
        string dataset,
        bool extract,
        bool overwrite,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(httpClient);

        var selectedDatasets = ResolveDatasets(dataset);
        Directory.CreateDirectory(outputDirectory);

        var files = new List<EdhrecDataDownloadFile>(selectedDatasets.Count);
        foreach (var selectedDataset in selectedDatasets)
        {
            var archivePath = Path.GetFullPath(Path.Combine(outputDirectory, selectedDataset.ArchiveFileName));
            if (overwrite || !File.Exists(archivePath))
            {
                await DownloadArchiveAsync(httpClient, selectedDataset.Url, archivePath, cancellationToken);
            }

            var extractedCsvPath = extract
                ? ExtractArchive(archivePath, outputDirectory, selectedDataset.CsvFileName, overwrite)
                : null;

            files.Add(new EdhrecDataDownloadFile(
                selectedDataset.Name,
                selectedDataset.Url,
                archivePath,
                extractedCsvPath is null ? null : ToRelativeSlashPath(outputDirectory, extractedCsvPath)));
        }

        return new EdhrecDataDownloadResult(files);
    }

    private static IReadOnlyList<EdhrecDataset> ResolveDatasets(string dataset)
    {
        if (string.Equals(dataset, "all", StringComparison.OrdinalIgnoreCase))
        {
            return Datasets;
        }

        foreach (var candidate in Datasets)
        {
            if (string.Equals(dataset, candidate.Name, StringComparison.OrdinalIgnoreCase))
            {
                return [candidate];
            }
        }

        throw new ArgumentException("Dataset must be one of: all, averages, data.", nameof(dataset));
    }

    private static async Task DownloadArchiveAsync(
        HttpClient httpClient,
        string url,
        string archivePath,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var outputStream = File.Create(archivePath);
        await responseStream.CopyToAsync(outputStream, cancellationToken);
    }

    private static string ExtractArchive(string archivePath, string outputDirectory, string csvFileName, bool overwrite)
    {
        using var archiveStream = File.OpenRead(archivePath);
        using var gzipStream = new GZipStream(archiveStream, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzipStream, outputDirectory, overwriteFiles: overwrite);

        var extractedCsvPath = Directory
            .EnumerateFiles(outputDirectory, csvFileName, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (extractedCsvPath is null)
        {
            throw new InvalidDataException($"Extracted archive did not contain {csvFileName}.");
        }

        return extractedCsvPath;
    }

    private static string ToRelativeSlashPath(string baseDirectory, string path)
        => Path.GetRelativePath(baseDirectory, path).Replace(Path.DirectorySeparatorChar, '/');

    private sealed record EdhrecDataset(string Name, string Url, string ArchiveFileName, string CsvFileName);
}
