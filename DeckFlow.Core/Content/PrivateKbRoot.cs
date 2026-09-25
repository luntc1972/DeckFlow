namespace DeckFlow.Core.Content;

/// <summary>
/// Resolves the private filesystem root that contains Content KB artifacts.
/// </summary>
public sealed class PrivateKbRoot
{
    private const int MaximumCanonicalizationPasses = 64;
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
            if (string.Equals(current, canonical, StringComparison.OrdinalIgnoreCase))
            {
                return canonical;
            }

            current = canonical;
        }

        throw new InvalidOperationException($"Unable to resolve {directoryPath}: too many symbolic link resolutions.");
    }

    private static string CanonicalizeDirectoryPass(string directoryPath)
    {
        var root = Path.GetPathRoot(directoryPath) ?? throw new InvalidOperationException($"Unable to resolve {directoryPath}.");
        var current = root;
        var remainder = directoryPath[root.Length..];
        foreach (var segment in remainder.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.IsNullOrEmpty(segment))
            {
                continue;
            }

            var candidate = new DirectoryInfo(Path.Combine(current, segment));
            current = candidate.LinkTarget is null
                ? candidate.FullName
                : candidate.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? throw new InvalidOperationException($"Unable to resolve {directoryPath}.");
        }

        return Path.GetFullPath(current);
    }

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
