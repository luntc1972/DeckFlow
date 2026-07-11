using DeckFlow.Core.Content;
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
    private readonly string _gitRootWithSeparator;
    private readonly string? _overlayRootWithSeparator;

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
        _gitRootWithSeparator = EnsureTrailingSeparator(Path.GetFullPath(Path.Combine(ContentBase, "content-kb")));
        _overlayRootWithSeparator = DataOverlayBase is null
            ? null
            : EnsureTrailingSeparator(Path.GetFullPath(DataOverlayBase));
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
    public string SeedFilePath => Path.Combine(ContentBase, ContentKbPaths.SeedRelativePath);

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
        var gitResolution = TryResolveGitPath(artifactPath, out resolvedFullPath);
        if (gitResolution != ContentKbArtifactResolution.MissingFile)
        {
            return gitResolution;
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
        if (_overlayRootWithSeparator is null || !IsContainedUnderRoot(overlayPath, _overlayRootWithSeparator))
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

    /// <summary>
    /// Resolves a stored artifact path against the git <c>/app</c> tree ONLY - never the
    /// <see cref="DataOverlayBase"/> fallback, regardless of <c>sync.directpush-gitbody</c> flag
    /// state. Used by the D-09 (REVISED) deployed-body-hash endpoint, which must confirm the
    /// deployed git body independent of the serving flag's rollout state.
    /// </summary>
    /// <param name="artifactPath">Stored relative artifact path.</param>
    /// <param name="resolvedFullPath">Resolved absolute git path when found.</param>
    /// <returns>The resolution state for the requested artifact path.</returns>
    public ContentKbArtifactResolution TryResolveGitArtifact(string artifactPath, out string resolvedFullPath)
        => TryResolveGitPath(artifactPath, out resolvedFullPath);

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

    private ContentKbArtifactResolution TryResolveGitPath(string artifactPath, out string resolvedFullPath)
    {
        resolvedFullPath = string.Empty;
        if (!IsSafeArtifactPath(artifactPath))
        {
            return ContentKbArtifactResolution.InvalidPath;
        }

        var gitPath = Path.GetFullPath(Path.Combine(ContentBase, artifactPath));
        if (!IsContainedUnderRoot(gitPath, _gitRootWithSeparator))
        {
            return ContentKbArtifactResolution.InvalidPath;
        }

        if (!File.Exists(gitPath))
        {
            return ContentKbArtifactResolution.MissingFile;
        }

        resolvedFullPath = gitPath;
        return ContentKbArtifactResolution.Resolved;
    }

    private static bool IsSafeArtifactPath(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath) ||
            Path.IsPathRooted(artifactPath) ||
            !artifactPath.StartsWith("content-kb/", StringComparison.Ordinal))
        {
            return false;
        }

        if (artifactPath.Contains("..", StringComparison.Ordinal))
        {
            foreach (var segment in artifactPath.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == "..")
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsContainedUnderRoot(string candidatePath, string rootPathWithSeparator)
        => candidatePath.StartsWith(rootPathWithSeparator, StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
}
