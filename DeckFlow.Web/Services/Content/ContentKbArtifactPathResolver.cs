using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Hosting;

namespace DeckFlow.Web.Services;

/// <summary>
/// Describes the result of resolving a Content KB artifact path.
/// </summary>
public enum ContentKbArtifactResolution
{
    /// <summary>
    /// The stored path is structurally unsafe or escapes the allowed subtree.
    /// </summary>
    InvalidPath,

    /// <summary>
    /// The stored path is valid, but no artifact file exists under the git or overlay roots.
    /// </summary>
    MissingFile,

    /// <summary>
    /// A matching artifact file was found and resolved safely.
    /// </summary>
    Resolved,
}

/// <summary>
/// Resolves the Content KB base directory for seed loading and artifact reads.
/// </summary>
public sealed class ContentKbArtifactPathResolver
{
    private readonly IFeatureFlagCache _flagCache;
    private readonly ILogger<ContentKbArtifactPathResolver> _logger;
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    /// Creates a resolver using the configured content base candidates.
    /// </summary>
    /// <param name="environment">Web host environment.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="flagCache">
    /// Feature-flag cache consulted for <c>sync.directpush-gitbody</c> (SYNC-07): when ON, a
    /// git-tree miss returns <see cref="ContentKbArtifactResolution.MissingFile"/> without
    /// consulting the <see cref="DataOverlayBase"/> fallback.
    /// </param>
    /// <param name="logger">Logger.</param>
    public ContentKbArtifactPathResolver(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IFeatureFlagCache flagCache,
        ILogger<ContentKbArtifactPathResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(flagCache);
        ArgumentNullException.ThrowIfNull(logger);

        _flagCache = flagCache;
        _logger = logger;
        ContentBase = ResolveContentBase(environment, configuration);
        DataOverlayBase = ResolveDataOverlayBase(configuration);
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
    /// Gets the optional persistent data overlay root containing the <c>content-kb</c> artifact tree.
    /// </summary>
    public string? DataOverlayBase { get; }

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

    /// <summary>
    /// Resolves a stored artifact path to an existing artifact under the git root or optional data overlay.
    /// </summary>
    /// <param name="artifactPath">Stored relative artifact path.</param>
    /// <param name="resolvedFullPath">Resolved absolute path when found.</param>
    /// <returns>The resolution state for the requested artifact path.</returns>
    public ContentKbArtifactResolution TryResolveExistingArtifact(string artifactPath, out string resolvedFullPath)
    {
        resolvedFullPath = string.Empty;
        if (!IsSafeArtifactPath(artifactPath))
        {
            return ContentKbArtifactResolution.InvalidPath;
        }

        var gitRoot = Path.Combine(ContentBase, "content-kb");
        var gitPath = Path.GetFullPath(Path.Combine(ContentBase, artifactPath));
        if (!IsContainedUnderRoot(gitPath, gitRoot))
        {
            return ContentKbArtifactResolution.InvalidPath;
        }

        if (File.Exists(gitPath))
        {
            resolvedFullPath = gitPath;
            return ContentKbArtifactResolution.Resolved;
        }

        // SYNC-07/D-01/D-11: under the flag, git is the ONLY body source - a git-tree miss is a
        // real miss, never masked by the legacy /data-SFTP-first overlay. Flag OFF (default)
        // preserves the byte-identical git-then-overlay fallback below.
        if (_flagCache.IsEnabled("sync.directpush-gitbody"))
        {
            return ContentKbArtifactResolution.MissingFile;
        }

        if (DataOverlayBase is null)
        {
            return ContentKbArtifactResolution.MissingFile;
        }

        var overlayPath = Path.GetFullPath(Path.Combine(DataOverlayBase, artifactPath["content-kb/".Length..]));
        if (!IsContainedUnderRoot(overlayPath, DataOverlayBase))
        {
            return ContentKbArtifactResolution.InvalidPath;
        }

        if (File.Exists(overlayPath))
        {
            resolvedFullPath = overlayPath;
            return ContentKbArtifactResolution.Resolved;
        }

        return ContentKbArtifactResolution.MissingFile;
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

    private static string? ResolveDataOverlayBase(IConfiguration configuration)
    {
        var dataDir = configuration["MTG_DATA_DIR"];
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        }

        return string.IsNullOrWhiteSpace(dataDir)
            ? null
            : Path.GetFullPath(Path.Combine(dataDir, "content-kb"));
    }

    private static bool IsSafeArtifactPath(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath) ||
            Path.IsPathRooted(artifactPath) ||
            !artifactPath.StartsWith("content-kb/", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var segment in artifactPath.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsContainedUnderRoot(string candidatePath, string rootPath)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        var comparisonRoot = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(comparisonRoot, StringComparison.OrdinalIgnoreCase);
    }
}
