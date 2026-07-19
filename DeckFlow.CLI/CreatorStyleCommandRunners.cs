using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.CLI;

/// <summary>
/// Creator-style CLI command runners.
/// </summary>
public static class CreatorStyleCommandRunners
{
    /// <summary>
    /// Exports creator style profiles and creator deck-cache rows to tracked JSON seed files.
    /// </summary>
    /// <param name="db">Optional path to the content KB database.</param>
    /// <param name="output">Optional destination directory for both seed files.</param>
    /// <param name="slugs">Explicit creator slugs to export.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunCreatorStyleIndexExportAsync(
        FileInfo? db,
        FileInfo? output,
        IReadOnlyList<string> slugs)
    {
        try
        {
            var profileStore = new CreatorStyleProfileStore(ContentKbCliPaths.ResolveDatabasePath(db));
            var deckCacheStore = new CreatorDeckCacheStore(ContentKbCliPaths.ResolveCreatorDeckCacheDatabasePath());
            var export = await BuildCreatorStyleSeedExportAsync(profileStore, deckCacheStore, slugs).ConfigureAwait(false);
            var (profileOutputPath, deckCacheOutputPath) = ResolveOutputPaths(output);

            Directory.CreateDirectory(Path.GetDirectoryName(profileOutputPath) ?? Directory.GetCurrentDirectory());
            Directory.CreateDirectory(Path.GetDirectoryName(deckCacheOutputPath) ?? Directory.GetCurrentDirectory());

            await File.WriteAllTextAsync(profileOutputPath, SerializeCreatorStyleExport(export.Profiles)).ConfigureAwait(false);
            await File.WriteAllTextAsync(deckCacheOutputPath, SerializeCreatorStyleExport(export.DeckCacheRows)).ConfigureAwait(false);

            Console.WriteLine($"Exported {export.Profiles.Count} creator style profiles to {profileOutputPath}");
            Console.WriteLine($"Exported {export.DeckCacheRows.Count} creator deck-cache rows to {deckCacheOutputPath}");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// Builds the creator-style seed export collections for the supplied slugs.
    /// </summary>
    /// <param name="profileStore">Profile store.</param>
    /// <param name="deckCacheStore">Deck-cache store.</param>
    /// <param name="explicitSlugs">Explicit creator slugs to export.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assembled creator-style seed export.</returns>
    public static async Task<CreatorStyleSeedExport> BuildCreatorStyleSeedExportAsync(
        ICreatorStyleProfileStore profileStore,
        ICreatorDeckCacheStore deckCacheStore,
        IReadOnlyList<string> explicitSlugs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profileStore);
        ArgumentNullException.ThrowIfNull(deckCacheStore);
        ArgumentNullException.ThrowIfNull(explicitSlugs);

        var slugs = await ResolveProfileSlugsAsync(profileStore, explicitSlugs, cancellationToken).ConfigureAwait(false);
        var profiles = new List<CreatorStyleProfile>(slugs.Count);
        var deckCacheRows = new List<CreatorDeckCacheEntry>();

        foreach (var slug in slugs)
        {
            var profile = await profileStore.GetBySlugAsync(slug, cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                throw new InvalidOperationException($"Creator style profile '{slug}' was not found.");
            }

            profiles.Add(profile);
            var creatorDeckRows = await deckCacheStore.GetByCreatorAsync(slug, cancellationToken).ConfigureAwait(false);
            deckCacheRows.AddRange(creatorDeckRows);
        }

        return new CreatorStyleSeedExport(profiles, deckCacheRows);
    }

    /// <summary>
    /// Serializes a creator-style seed export collection with tracked-file formatting.
    /// </summary>
    /// <typeparam name="T">Collection row type.</typeparam>
    /// <param name="rows">Rows to serialize.</param>
    /// <returns>Indented camelCase JSON with a trailing newline.</returns>
    public static string SerializeCreatorStyleExport<T>(IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var json = JsonSerializer.Serialize(
            rows,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            });
        return json + "\n";
    }

    private static Task<IReadOnlyList<string>> ResolveProfileSlugsAsync(
        ICreatorStyleProfileStore store,
        IReadOnlyList<string> explicitSlugs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(explicitSlugs);

        IReadOnlyList<string> slugs = explicitSlugs
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Select(slug => slug.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (slugs.Count > 0)
        {
            return Task.FromResult(slugs);
        }

        return ResolveAllProfileSlugsAsync(store, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ResolveAllProfileSlugsAsync(
        ICreatorStyleProfileStore store,
        CancellationToken cancellationToken)
    {
        var summaries = await store.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return summaries
            .Select(summary => summary.Slug)
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static (string ProfileOutputPath, string DeckCacheOutputPath) ResolveOutputPaths(FileInfo? output)
    {
        var outputRoot = output?.FullName;
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            return (
                ContentKbPaths.CreatorStyleProfileSeedRelativePath,
                ContentKbPaths.CreatorDeckCacheSeedRelativePath);
        }

        if (LooksLikeDirectory(outputRoot))
        {
            var directoryPath = Path.GetFullPath(outputRoot);
            return (
                Path.Combine(directoryPath, Path.GetFileName(ContentKbPaths.CreatorStyleProfileSeedRelativePath)),
                Path.Combine(directoryPath, Path.GetFileName(ContentKbPaths.CreatorDeckCacheSeedRelativePath)));
        }

        throw new InvalidOperationException("--output must be a directory for creator-style exports.");
    }

    private static bool LooksLikeDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            return true;
        }

        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return true;
        }

        return !Path.HasExtension(path);
    }
}

/// <summary>
/// Creator-style seed export payload.
/// </summary>
/// <param name="Profiles">Full creator style profiles.</param>
/// <param name="DeckCacheRows">Creator deck-cache rows keyed by exported profile slug.</param>
public sealed record CreatorStyleSeedExport(
    IReadOnlyList<CreatorStyleProfile> Profiles,
    IReadOnlyList<CreatorDeckCacheEntry> DeckCacheRows);
