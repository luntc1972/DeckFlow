using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Registry;
using RestSharp;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Services.Scryfall;

namespace DeckFlow.Web.Services;

/// <summary>
/// A single confirmed or almost-confirmed combo from Commander Spellbook.
/// </summary>
public sealed record SpellbookCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,
    string Instructions,
    int? Popularity = null,
    int? ManaValueNeeded = null);

/// <summary>
/// A combo that is one card away from being complete in the submitted deck.
/// </summary>
public sealed record SpellbookAlmostCombo(
    string MissingCard,
    IReadOnlyList<string> CardsInDeck,
    IReadOnlyList<string> Results,
    string Instructions);

/// <summary>
/// The combo lookup result for a deck.
/// </summary>
public sealed record CommanderSpellbookResult(
    IReadOnlyList<SpellbookCombo> IncludedCombos,
    IReadOnlyList<SpellbookAlmostCombo> AlmostIncludedCombos);

/// <summary>
/// Looks up combos for a deck using the Commander Spellbook API.
/// </summary>
public interface ICommanderSpellbookService
{
    /// <summary>
    /// Returns combos that are fully in the deck and combos that are one card away,
    /// within the deck's color identity. Entries are sent under canonical Scryfall names when resolvable.
    /// Returns null if the API call fails.
    /// </summary>
    Task<CommanderSpellbookResult?> FindCombosAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches and caches combo data from the Commander Spellbook backend API.
/// </summary>
public sealed class CommanderSpellbookService : ICommanderSpellbookService
{
    private const string ApiUrl = "https://backend.commanderspellbook.com/find-my-combos";
    private const int MaxIncluded = 20;
    private const int MaxAlmostIncluded = 15;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline<RestResponse> _resiliencePipeline;
    private readonly IMemoryCache _memoryCache;
    private readonly ICanonicalCardNameResolver _canonicalCardNameResolver;
    private readonly ILogger<CommanderSpellbookService> _logger;
    private readonly Func<string, CancellationToken, Task<string?>> _postJsonAsync;

    internal CommanderSpellbookService(
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        IMemoryCache memoryCache,
        ICanonicalCardNameResolver canonicalCardNameResolver,
        ILogger<CommanderSpellbookService>? logger = null,
        Func<string, CancellationToken, Task<string?>>? postJsonAsync = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(canonicalCardNameResolver);
        _httpClientFactory = httpClientFactory;
        _resiliencePipeline = pipelineProvider.GetPipeline<RestResponse>("spellbook") ?? ResiliencePipeline<RestResponse>.Empty;
        _memoryCache = memoryCache;
        _canonicalCardNameResolver = canonicalCardNameResolver;
        _logger = logger ?? NullLogger<CommanderSpellbookService>.Instance;
        _postJsonAsync = postJsonAsync ?? PostJsonAsync;
    }

    /// <inheritdoc/>
    public async Task<CommanderSpellbookResult?> FindCombosAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var commanders = entries
            .Where(e => string.Equals(e.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Select(e => ToSpellbookName(e.Name))
            .ToList();

        var main = entries
            .Where(e => string.Equals(e.Board, "mainboard", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(e.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Select(e => ToSpellbookName(e.Name))
            .ToList();

        if (main.Count == 0)
        {
            return null;
        }

        IReadOnlyDictionary<string, string> sentNames;
        try
        {
            sentNames = await ResolveSentNamesAsync(main, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Why: Caller cancellation returns null, as it already does during the Spellbook POST.
            return null;
        }
        List<string> RemapNames(IEnumerable<string> names) => names
            .Select(n => sentNames.TryGetValue(n, out var sentName) && !string.IsNullOrWhiteSpace(sentName) ? sentName : n)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        commanders = RemapNames(commanders);
        main = RemapNames(main);

        // Why: Results depend on sent names, so aliases share cached results and resolved calls bypass fail-open entries.
        var cacheKey = $"spellbook:{string.Join("|", commanders)}::{string.Join("|", main)}";
        if (_memoryCache.TryGetValue<CommanderSpellbookResult>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var requestBody = BuildRequestJson(commanders, main);
        string? responseJson;
        try
        {
            responseJson = await _postJsonAsync(requestBody, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Commander Spellbook API call failed; continuing without combo data.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return null;
        }

        CommanderSpellbookResult? result;
        try
        {
            result = ParseResponse(responseJson, main);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Commander Spellbook response; continuing without combo data.");
            return null;
        }

        if (result is not null)
        {
            _memoryCache.Set(cacheKey, result, CacheDuration);
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveSentNamesAsync(
        IReadOnlyList<string> main,
        CancellationToken cancellationToken)
    {
        var names = main.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var misses = new List<string>();
        foreach (var name in names)
        {
            var key = "spellbook-canonical-name:" + name.ToUpperInvariant();
            if (_memoryCache.TryGetValue<string>(key, out var canonical))
            {
                if (!string.IsNullOrEmpty(canonical))
                {
                    result[name] = canonical;
                }
            }
            else
            {
                misses.Add(name);
            }
        }

        if (misses.Count == 0)
        {
            return result;
        }

        try
        {
            var resolved = await _canonicalCardNameResolver.ResolveCanonicalNamesAsync(misses, cancellationToken).ConfigureAwait(false);
            foreach (var name in misses)
            {
                var key = "spellbook-canonical-name:" + name.ToUpperInvariant();
                resolved.TryGetValue(name, out var canonical);
                _memoryCache.Set(key, canonical ?? string.Empty, CacheDuration);
                if (!string.IsNullOrEmpty(canonical))
                {
                    result[name] = canonical;
                }
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Why: A Scryfall outage must not hide combos; null still means Spellbook is unavailable.
            _logger.LogWarning(ex, "Card-name canonicalization failed; sending {CardCount} Commander Spellbook names unresolved.", misses.Count);
            return result;
        }
    }

    internal static string BuildRequestJson(IReadOnlyList<string> commanders, IReadOnlyList<string> main)
    {
        var obj = new
        {
            commanders = commanders.Select(n => new { card = n, quantity = 1 }).ToList(),
            main = main.Where(n => !commanders.Contains(n, StringComparer.OrdinalIgnoreCase))
                       .Select(n => new { card = n, quantity = 1 })
                       .ToList()
        };
        return JsonSerializer.Serialize(obj);
    }

    private static string ToSpellbookName(string name)
    {
        // Why: Spellbook uses the same combined face-name form as Scryfall.
        return ScryfallCardResolver.NormalizeForScryfall(name.Trim());
    }

    /// <summary>Parses combos while preserving Spellbook's original card names.</summary>
    /// <param name="json">The Spellbook response JSON.</param>
    /// <param name="deckCardNames">Raw owned card names, normalized internally with <see cref="CardNormalizer.Normalize"/>.</param>
    internal static CommanderSpellbookResult? ParseResponse(string json, IReadOnlyList<string> deckCardNames)
    {
        var deckCardKeys = new HashSet<string>(deckCardNames.Select(CardNormalizer.Normalize), StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results))
        {
            return null;
        }

        var included = results.TryGetProperty("included", out var inc)
            ? ParseVariants(inc).Take(MaxIncluded).ToList()
            : [];

        var almostIncluded = results.TryGetProperty("almostIncluded", out var almost)
            ? ParseAlmostVariants(almost, deckCardKeys).Take(MaxAlmostIncluded).ToList()
            : [];

        return new CommanderSpellbookResult(included, almostIncluded);
    }

    private static IEnumerable<SpellbookCombo> ParseVariants(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var variant in array.EnumerateArray())
        {
            var cards = ExtractCardNames(variant);
            var results = ExtractResults(variant);
            var instructions = ExtractInstructions(variant);

            if (cards.Count == 0 || results.Count == 0)
            {
                continue;
            }

            // Top-level ranking scalars (popularity, manaValueNeeded). Use TryGetInt32
            // (NOT GetInt32) — GetInt32 throws on a decimal or out-of-Int32-range JSON
            // number even when ValueKind==Number. TryGetInt32 returns false → null, so a
            // malformed/absent value degrades to null and never fails the whole result.
            int? popularity = variant.TryGetProperty("popularity", out var pop)
                && pop.ValueKind == JsonValueKind.Number
                && pop.TryGetInt32(out var popVal)
                ? popVal
                : null;

            int? manaValueNeeded = variant.TryGetProperty("manaValueNeeded", out var mv)
                && mv.ValueKind == JsonValueKind.Number
                && mv.TryGetInt32(out var mvVal)
                ? mvVal
                : null;

            yield return new SpellbookCombo(cards, results, instructions, popularity, manaValueNeeded);
        }
    }

    private static IEnumerable<SpellbookAlmostCombo> ParseAlmostVariants(JsonElement array, HashSet<string> deckCardNames)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var variant in array.EnumerateArray())
        {
            var allCards = ExtractCardNames(variant);
            var results = ExtractResults(variant);
            var instructions = ExtractInstructions(variant);

            if (allCards.Count == 0 || results.Count == 0)
            {
                continue;
            }

            var missing = new List<string>();
            var inDeck = new List<string>();
            foreach (var card in allCards)
            {
                var destination = deckCardNames.Contains(CardNormalizer.Normalize(card)) ? inDeck : missing;
                destination.Add(card);
            }

            // Only include if exactly one card is missing (one card away)
            if (missing.Count != 1)
            {
                continue;
            }

            yield return new SpellbookAlmostCombo(missing[0], inDeck, results, instructions);
        }
    }

    // Commander Spellbook variant shape notes for the deferred candidate-matching phase:
    // - `requires[]` is the template-slot list (for example `template.name`, `template.scryfallQuery`,
    //   and `template.scryfallApi` describe the "View N Cards" enumeration source for that slot).
    // - `of[].id` is the parent/grouping key for "variants of this combo".
    // - `includes[].id` records sub-combo composition and is distinct from `of`.
    // - `variantCount` is the sibling variant count for the same grouped combo.
    // - `popularity` is already parsed below and is the EDHREC deck-count source.
    // TODO(follow-on phase): requires/of/includes shape — see 110.1-RESEARCH.md §1b
    private static IReadOnlyList<string> ExtractCardNames(JsonElement variant)
    {
        if (!variant.TryGetProperty("uses", out var uses) || uses.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return uses.EnumerateArray()
            .Select(use => use.TryGetProperty("card", out var card)
                && card.TryGetProperty("name", out var name)
                ? name.GetString() ?? string.Empty
                : string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
    }

    private static IReadOnlyList<string> ExtractResults(JsonElement variant)
    {
        if (!variant.TryGetProperty("produces", out var produces) || produces.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return produces.EnumerateArray()
            .Select(p => p.TryGetProperty("feature", out var feature)
                && feature.TryGetProperty("name", out var name)
                ? name.GetString() ?? string.Empty
                : string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ExtractInstructions(JsonElement variant)
    {
        if (variant.TryGetProperty("description", out var desc))
        {
            var text = desc.GetString() ?? string.Empty;
            // Trim to first 300 chars to keep prompt size manageable
            return text.Length > 300 ? text[..300].TrimEnd() + "…" : text;
        }

        return string.Empty;
    }

    private async Task<string?> PostJsonAsync(string requestBody, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("commander-spellbook");
        var restClient = new RestClient(httpClient);
        var request = new RestRequest(ApiUrl, Method.Post);
        request.AddStringBody(requestBody, ContentType.Json);

        var response = await _resiliencePipeline.ExecuteAsync(
            async ct => await restClient.ExecuteAsync(request, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessful)
        {
            throw new HttpRequestException(
                $"Spellbook POST failed: HTTP {(int)response.StatusCode}",
                inner: null,
                statusCode: response.StatusCode);
        }

        return response.Content;
    }
}
