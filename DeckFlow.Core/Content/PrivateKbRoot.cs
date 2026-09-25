namespace DeckFlow.Core.Content;

/// <summary>
/// Resolves the private filesystem root that contains Content KB artifacts.
/// </summary>
public sealed class PrivateKbRoot
{
    private const int MaximumCanonicalizationPasses = 64;
    // Why: Unix path comparisons are case-sensitive, unlike Windows and macOS defaults.
    private static StringComparison PathComparison => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// The environment variable that supplies the private KB root.
    /// </summary>
    public const string EnvironmentVariableName = "DECKFLOW_KB_ROOT";

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivateKbRoot"/> class.
    /// </summary>
    /// <param name="configuredRoot">The configured private KB root.</param>
    public PrivateKbRoot(string? configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must name an existing private KB root.");
        }

        var configuredPath = Path.GetFullPath(configuredRoot);
        if (!Directory.Exists(configuredPath))
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must name an existing directory: {configuredPath}");
        }

        Root = CanonicalizeDirectory(configuredPath);
        if (!Directory.Exists(Root))
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must name an existing directory: {Root}");
        }

        if (IsInsideProductCheckout(configuredPath) || IsInsideProductCheckout(Root))
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must not be inside a DeckFlow product checkout.");
        }
    }

    /// <summary>
    /// Gets the canonical absolute private KB root.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Gets the Content KB artifact directory.
    /// </summary>
    public string ContentKbDir => Path.Combine(Root, "content-kb");

    /// <summary>
    /// Gets the Content KB index seed file path.
    /// </summary>
    public string SeedFile => Path.Combine(ContentKbDir, "seed", "index-seed.json");

    /// <summary>
    /// Gets the creator-style seed directory.
    /// </summary>
    public string CreatorStyleSeedDir => Path.Combine(Root, "creator-style-seed");

    /// <summary>
    /// Resolves the root from <see cref="EnvironmentVariableName"/>.
    /// </summary>
    /// <returns>The configured private KB root.</returns>
    public static PrivateKbRoot FromEnvironment()
        => new(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>
    /// Resolves the root through a supplied environment lookup.
    /// </summary>
    /// <param name="environmentVariableGetter">Environment variable lookup.</param>
    /// <returns>The configured private KB root.</returns>
    public static PrivateKbRoot FromEnvironment(Func<string, string?> environmentVariableGetter)
    {
        ArgumentNullException.ThrowIfNull(environmentVariableGetter);
        return new PrivateKbRoot(environmentVariableGetter(EnvironmentVariableName));
    }

    private static string CanonicalizeDirectory(string directoryPath)
    {
        var current = Path.GetFullPath(directoryPath);
        for (var pass = 0; pass < MaximumCanonicalizationPasses; pass++)
        {
            var canonical = CanonicalizeDirectoryPass(current);
            if (string.Equals(current, canonical, PathComparison))
            {
                return canonical;
            }

            current = canonical;
        }

        throw new InvalidOperationException($"Unable to resolve {EnvironmentVariableName}: too many symbolic link resolutions.");
    }

    private static string CanonicalizeDirectoryPass(string directoryPath)
    {
        var root = Path.GetPathRoot(directoryPath) ?? throw new InvalidOperationException($"Unable to resolve {directoryPath}.");
        try
        {
            var current = root;
            var pendingSegments = new LinkedList<string>(SplitSegments(directoryPath[root.Length..]));
            var linkExpansions = 0;
            while (pendingSegments.First is not null)
            {
                var segment = pendingSegments.First.Value;
                pendingSegments.RemoveFirst();
                if (string.IsNullOrEmpty(segment) || segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    current = Directory.GetParent(current)?.FullName ?? current;
                    continue;
                }

                var candidate = new DirectoryInfo(Path.Combine(current, segment));
                var linkTarget = candidate.LinkTarget;
                if (linkTarget is null)
                {
                    current = candidate.FullName;
                    continue;
                }

                if (++linkExpansions > MaximumCanonicalizationPasses)
                {
                    throw new InvalidOperationException($"Unable to resolve {EnvironmentVariableName}: too many symbolic link resolutions.");
                }

                var targetRoot = Path.GetPathRoot(linkTarget);
                if (Path.IsPathRooted(linkTarget))
                {
                    current = targetRoot ?? throw new InvalidOperationException($"Unable to resolve {linkTarget}.");
                    linkTarget = linkTarget[targetRoot.Length..];
                }

                var targetSegments = SplitSegments(linkTarget);
                for (var index = targetSegments.Length - 1; index >= 0; index--)
                {
                    pendingSegments.AddFirst(targetSegments[index]);
                }
            }

            return Path.GetFullPath(current);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException($"Unable to resolve {EnvironmentVariableName}.", exception);
        }
    }

    private static string[] SplitSegments(string path)
        => path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

    private static bool IsInsideProductCheckout(string directoryPath)
    {
        for (var directory = new DirectoryInfo(directoryPath); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeckFlow.sln")) &&
                (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git"))))
            {
                return true;
            }
        }

        return false;
    }
}
