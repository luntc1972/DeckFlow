using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Content;

/// <summary>
/// Three-outcome result of reading <c>index-seed.json</c>. <see cref="SeedAvailable"/> is
/// <see langword="true"/> ONLY when the seed file was present and successfully parsed —
/// <see cref="NaturalKeys"/> may legitimately be empty in that case (a valid empty seed).
/// <see cref="SeedAvailable"/> is <see langword="false"/> when the file is absent, unreadable, or
/// failed to parse; <see cref="NaturalKeys"/> is empty-but-MEANINGLESS in that case. Callers that
/// gate destructive or classification behavior MUST treat <c>SeedAvailable == false</c> as
/// "do nothing", never as "empty set" (T-91-03).
/// </summary>
/// <param name="SeedAvailable">Whether the seed file was present and successfully parsed.</param>
/// <param name="NaturalKeys">
/// U+0000-joined <c>"{Type}\u0000{Value}"</c> natural-key set, matching the
/// <see cref="ContentNaturalKey"/> / <see cref="ContentSyncDiffClassifier"/> anti-collision key
/// format (SYNC-05). Empty when <see cref="SeedAvailable"/> is <see langword="false"/>, or when the
/// seed legitimately contains zero entries.
/// </param>
public sealed record SeedIndexReadResult(bool SeedAvailable, IReadOnlySet<string> NaturalKeys);

/// <summary>
/// Shared Core reader for <c>index-seed.json</c> natural-key membership. <see cref="Read"/> is the
/// ONLY public read API — there is deliberately NO convenience overload that returns a bare key
/// set, so every downstream consumer (D-02 backfill, seed-drift classifier, reconcile
/// orchestrator, Apply) carries the <see cref="SeedIndexReadResult.SeedAvailable"/> flag BY
/// CONSTRUCTION and can never collapse an unavailable seed into empty membership (Codex BLOCK).
/// Pure/static (no DI, no I/O beyond the one file read) so it can be reused verbatim by both the
/// Web host's D-02 backfill and the Studio reconcile orchestrator.
/// </summary>
public static class SeedIndexFileReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly IReadOnlySet<string> EmptyKeySet = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Reads and parses <paramref name="seedFilePath"/> into a <see cref="SeedIndexReadResult"/>.
    /// Never throws: a missing file, an I/O failure, or a malformed-JSON parse failure all return
    /// <c>SeedAvailable == false</c> with an empty key set rather than propagating an exception.
    /// </summary>
    /// <param name="seedFilePath">Absolute or relative path to <c>index-seed.json</c>.</param>
    /// <param name="logger">Optional logger for read/parse failures.</param>
    /// <returns>
    /// A 3-outcome result: present-and-parsed with entries, present-and-parsed as a valid empty
    /// seed, or absent/unreadable/parse-failed.
    /// </returns>
    public static SeedIndexReadResult Read(string seedFilePath, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedFilePath);

        if (!File.Exists(seedFilePath))
        {
            logger?.LogInformation(
                "Content KB seed file not found at {SeedFilePath}; seed unavailable.",
                seedFilePath);
            return new SeedIndexReadResult(false, EmptyKeySet);
        }

        try
        {
            using var stream = File.OpenRead(seedFilePath);
            var entries = JsonSerializer.Deserialize<SeedIndexEntry[]>(stream, JsonOptions)
                ?? Array.Empty<SeedIndexEntry>();

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.NaturalKeyType) || string.IsNullOrWhiteSpace(entry.NaturalKeyValue))
                {
                    continue;
                }

                // Why: U+0000 NULL separator matches the shipped Codex anti-collision format
                // (ContentNaturalKey / ContentSyncDiffClassifier, SYNC-05) — never a printable separator.
                keys.Add($"{entry.NaturalKeyType}\u0000{entry.NaturalKeyValue}");
            }

            // Why: parsed successfully — SeedAvailable is true even when keys is empty (a valid
            // empty seed is a legitimate outcome, distinct from unavailable, T-91-03).
            return new SeedIndexReadResult(true, keys);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Why: a read or parse failure must NEVER masquerade as an empty-but-available seed —
            // callers gating destructive behavior depend on this flag being trustworthy (T-91-03).
            logger?.LogWarning(
                ex,
                "Content KB seed file at {SeedFilePath} could not be read or parsed; seed unavailable.",
                seedFilePath);
            return new SeedIndexReadResult(false, EmptyKeySet);
        }
    }

    /// <summary>
    /// Minimal seed-entry shape for natural-key parsing — mirrors
    /// <c>ContentKbSeedLoader.ContentKbSeedEntry</c>'s <c>natural_key_type</c>/<c>natural_key_value</c>
    /// fields (camelCase JSON) without pulling in the full row shape this reader doesn't need.
    /// </summary>
    private sealed record SeedIndexEntry
    {
        public string? NaturalKeyType { get; init; }

        public string? NaturalKeyValue { get; init; }
    }
}
