using Microsoft.AspNetCore.Hosting;

namespace DeckFlow.Web.Services;

/// <summary>
/// Resolves the Content KB base directory for seed loading and artifact reads.
/// </summary>
public sealed class ContentKbArtifactPathResolver
{
    private readonly ILogger<ContentKbArtifactPathResolver> _logger;

    /// <summary>
    /// Creates a resolver using the configured content base candidates.
    /// </summary>
    /// <param name="environment">Web host environment.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="logger">Logger.</param>
    public ContentKbArtifactPathResolver(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<ContentKbArtifactPathResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        ContentBase = ResolveContentBase(environment, configuration);
        var contentKbExists = Directory.Exists(Path.Combine(ContentBase, "content-kb"));
        _logger.LogInformation(
            "Content KB content base resolved to {ContentBase}; content-kb exists: {ContentKbExists}.",
            ContentBase,
            contentKbExists);
    }

    /// <summary>
    /// Gets the base directory that contains the <c>content-kb</c> artifact tree.
    /// </summary>
    public string ContentBase { get; }

    /// <summary>
    /// Gets the resolved seed-file path.
    /// </summary>
    public string SeedFilePath => Path.Combine(ContentBase, "content-kb", "seed", "index-seed.json");

    /// <summary>
    /// Resolves a stored artifact path to an absolute filesystem path.
    /// </summary>
    /// <param name="artifactPath">Stored relative artifact path.</param>
    /// <returns>The absolute resolved artifact path.</returns>
    public string ResolveArtifactFullPath(string artifactPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);

        return Path.GetFullPath(Path.Combine(ContentBase, artifactPath));
    }

    private string ResolveContentBase(IWebHostEnvironment environment, IConfiguration configuration)
    {
        foreach (var candidate in EnumerateCandidates(environment, configuration))
        {
            if (Directory.Exists(Path.Combine(candidate, "content-kb")))
            {
                return candidate;
            }
        }

        _logger.LogWarning(
            "No Content KB content-kb directory found in configured candidates; falling back to {ContentRootPath}.",
            environment.ContentRootPath);
        return Path.GetFullPath(environment.ContentRootPath);
    }

    private static IEnumerable<string> EnumerateCandidates(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredBase = configuration["ContentKb:ContentBase"];
        if (!string.IsNullOrWhiteSpace(configuredBase))
        {
            yield return Path.GetFullPath(configuredBase);
        }

        yield return Path.GetFullPath(environment.ContentRootPath);
        yield return Path.GetFullPath(Path.Combine(environment.ContentRootPath, ".."));
        yield return Path.GetFullPath(Directory.GetCurrentDirectory());
    }
}
