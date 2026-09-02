using System.Net;
using System.Text.Json;
using Polly;
using Polly.Retry;
using RestSharp;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Fetches and parses an Archidekt deck from the Archidekt REST API with exponential-backoff retry.
/// </summary>
public sealed class ArchidektApiDeckImporter : IArchidektDeckImporter
{
    private readonly RestClient _restClient;
    private static readonly AsyncRetryPolicy<RestResponse> RetryPolicy = Policy<RestResponse>
        .HandleResult(response => response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
        .WaitAndRetryAsync(
            retryCount: 6,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250)),
            onRetry: (outcome, timespan, retryAttempt, context) => { });

    /// <summary>
    /// Initializes the Archidekt importer with an optional RestClient instance.
    /// </summary>
    /// <param name="restClient">Optional REST client for test injection.</param>
    public ArchidektApiDeckImporter(RestClient? restClient = null)
    {
        _restClient = restClient ?? new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://archidekt.com"),
            ThrowOnAnyError = false,
        });
    }

    /// <summary>
    /// Imports deck entries from an Archidekt deck, preserving categories and boards.
    /// </summary>
    /// <param name="urlOrDeckId">Deck URL or ID.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    public async Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
    {
        var result = await ImportWithMetadataAsync(urlOrDeckId, cancellationToken).ConfigureAwait(false);
        return result.Entries;
    }

    /// <summary>
    /// Imports deck entries from an Archidekt deck, preserving categories and boards, and
    /// captures curated deck-level metadata from the same payload request (no second request).
    /// </summary>
    /// <param name="urlOrDeckId">Deck URL or ID.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    public async Task<ArchidektDeckImportResult> ImportWithMetadataAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
    {
        if (!ArchidektApiUrl.TryGetDeckId(urlOrDeckId, out var deckId))
        {
            throw new InvalidOperationException($"Unable to determine Archidekt deck id from: {urlOrDeckId}");
        }

        var response = await RetryPolicy.ExecuteAsync(ct => _restClient.ExecuteAsync(CreateDeckRequest(deckId), ct), cancellationToken);
        var body = response.Content ?? string.Empty;
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Archidekt API deck {deckId} returned {(int)response.StatusCode} {response.StatusDescription}: {body[..Math.Min(body.Length, 500)]}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var entries = new List<DeckEntry>();
        var excludedCategoryNames = ReadExcludedCategoryNames(root);
        var metadata = TryExtractMetadata(root);

        if (!root.TryGetProperty("cards", out var cardsElement) || cardsElement.ValueKind != JsonValueKind.Array)
        {
            return new ArchidektDeckImportResult(entries, metadata);
        }

        foreach (var item in cardsElement.EnumerateArray())
        {
            var quantity = item.GetProperty("quantity").GetInt32();
            if (quantity == 0)
            {
                continue;
            }

            var categories = item.TryGetProperty("categories", out var categoriesElement) && categoriesElement.ValueKind == JsonValueKind.Array
                ? categoriesElement.EnumerateArray().Where(cat => cat.ValueKind == JsonValueKind.String).Select(cat => cat.GetString()!).ToList()
                : [];

            var board = DetermineBoard(categories, excludedCategoryNames);
            var userCategories = categories
                .Where(category => !IsBoardCategory(category))
                .ToList();

            var card = item.GetProperty("card");
            var name = card.GetProperty("oracleCard").GetProperty("name").GetString()
                ?? card.GetProperty("displayName").GetString()
                ?? "Unknown";

            entries.Add(new DeckEntry
            {
                Name = name,
                NormalizedName = CardNormalizer.Normalize(name),
                Quantity = quantity,
                Board = board,
                SetCode = card.TryGetProperty("edition", out var editionElement) && editionElement.TryGetProperty("editioncode", out var editionCode)
                    ? editionCode.GetString()
                    : null,
                CollectorNumber = card.TryGetProperty("collectorNumber", out var collectorNumberElement)
                    ? collectorNumberElement.GetString()?.Replace("★", string.Empty, StringComparison.Ordinal)
                    : null,
                Category = userCategories.Count == 0 ? (board == "maybeboard" ? "Maybeboard" : null) : string.Join(",", userCategories),
                IsFoil = item.TryGetProperty("modifier", out var modifierElement)
                    && string.Equals(modifierElement.GetString(), "Foil", StringComparison.OrdinalIgnoreCase),
            });
        }

        return new ArchidektDeckImportResult(entries, metadata);
    }

    /// <summary>
    /// Attempts to extract curated deck-level Archidekt metadata from the deck payload root.
    /// Returns null when the payload is not recognizable as an Archidekt deck payload, or when
    /// any unexpected failure occurs — this method must never throw, so that no metadata value
    /// can introduce a new failure mode into ImportAsync.
    /// </summary>
    /// <param name="root">Root element of the Archidekt deck payload.</param>
    private static ArchidektDeckMetadata? TryExtractMetadata(JsonElement root)
    {
        try
        {
            var hasId = root.TryGetProperty("id", out _);
            var hasName = root.TryGetProperty("name", out _);
            var hasEdhBracket = root.TryGetProperty("edhBracket", out var edhBracketElement);
            var hasDeckFormat = root.TryGetProperty("deckFormat", out var deckFormatElement);
            var hasTheorycrafted = root.TryGetProperty("theorycrafted", out var theorycraftedElement);
            var hasCreatedAt = root.TryGetProperty("createdAt", out var createdAtElement);
            var hasUpdatedAt = root.TryGetProperty("updatedAt", out var updatedAtElement);

            var isRecognizableArchidektPayload = (hasId || hasName)
                && (hasEdhBracket || hasDeckFormat || hasTheorycrafted || hasCreatedAt || hasUpdatedAt);

            if (!isRecognizableArchidektPayload)
            {
                return null;
            }

            return new ArchidektDeckMetadata(
                EdhBracket: hasEdhBracket ? ParseNullableInt(edhBracketElement) : null,
                DeckFormat: hasDeckFormat ? ParseNullableInt(deckFormatElement) : null,
                Theorycrafted: hasTheorycrafted ? ParseNullableBool(theorycraftedElement) : null,
                CreatedUtc: hasCreatedAt ? ParseNullableTimestamp(createdAtElement) : null,
                UpdatedUtc: hasUpdatedAt ? ParseNullableTimestamp(updatedAtElement) : null,
                CapturedUtc: DateTimeOffset.UtcNow);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a JSON element into a nullable integer, guarding on JsonValueKind so no bare
    /// numeric accessor can throw on a malformed or unexpected-kind value.
    /// </summary>
    /// <param name="element">JSON element to parse.</param>
    private static int? ParseNullableInt(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetInt32(out var numericValue) ? numericValue : null;
            case JsonValueKind.String:
                return int.TryParse(element.GetString(), out var parsedValue) ? parsedValue : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Parses a JSON element into a nullable boolean by mapping JsonValueKind directly — no
    /// bare GetBoolean() accessor exists on JsonElement, and a kind comparison cannot throw.
    /// </summary>
    /// <param name="element">JSON element to parse.</param>
    private static bool? ParseNullableBool(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.String:
                return bool.TryParse(element.GetString(), out var parsedValue) ? parsedValue : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Parses a JSON element into a nullable timestamp, guarding on JsonValueKind so a
    /// malformed or wrong-kind value never throws.
    /// </summary>
    /// <param name="element">JSON element to parse.</param>
    private static DateTimeOffset? ParseNullableTimestamp(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(element.GetString(), out var parsedValue) ? parsedValue : null;
    }

    /// <summary>
    /// Builds the project REST request for fetching the deck payload.
    /// </summary>
    /// <param name="deckId">Target deck identifier.</param>
    private static RestRequest CreateDeckRequest(string deckId)
    {
        var request = new RestRequest($"api/decks/{deckId}/", Method.Get);
        request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
        request.AddHeader("Accept", "application/json, text/plain, */*");
        request.AddHeader("Referer", $"https://archidekt.com/decks/{deckId}");
        request.AddHeader("Accept-Language", "en-US,en;q=0.9");
        return request;
    }

    /// <summary>
    /// Reads category names excluded from the deck.
    /// </summary>
    /// <param name="root">Root element of the Archidekt deck payload.</param>
    /// <returns>Category names excluded from the deck.</returns>
    private static HashSet<string> ReadExcludedCategoryNames(JsonElement root)
    {
        var excludedCategoryNames = new HashSet<string>(StringComparer.Ordinal);

        if (root.TryGetProperty("categories", out var deckCategoriesElement) && deckCategoriesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var category in deckCategoriesElement.EnumerateArray())
            {
                if (category.ValueKind == JsonValueKind.Object
                    && category.TryGetProperty("name", out var nameElement)
                    && nameElement.ValueKind == JsonValueKind.String
                    && category.TryGetProperty("includedInDeck", out var includedInDeckElement)
                    && includedInDeckElement.ValueKind == JsonValueKind.False)
                {
                    excludedCategoryNames.Add(nameElement.GetString()!);
                }
            }
        }

        return excludedCategoryNames;
    }

    /// <summary>
    /// Determines which board a card belongs to based on its category list.
    /// </summary>
    /// <param name="categories">List of Archidekt categories attached to the card.</param>
    /// <param name="excludedCategoryNames">Category names excluded from the deck.</param>
    private static string DetermineBoard(List<string> categories, IReadOnlySet<string> excludedCategoryNames)
    {
        if (categories.Any(category => string.Equals(category, "Commander", StringComparison.OrdinalIgnoreCase)))
        {
            return "commander";
        }

        if (categories.Any(category => string.Equals(category, "Maybeboard", StringComparison.OrdinalIgnoreCase)))
        {
            return "maybeboard";
        }

        if (categories.Any(category => string.Equals(category, "Sideboard", StringComparison.OrdinalIgnoreCase)))
        {
            return "maybeboard";
        }

        if (categories.Any(excludedCategoryNames.Contains))
        {
            return "maybeboard";
        }

        return "mainboard";
    }

    /// <summary>
    /// Checks whether the provided category maps to a board designation.
    /// </summary>
    /// <param name="category">Category string to evaluate.</param>
    private static bool IsBoardCategory(string category)
    {
        return string.Equals(category, "Commander", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "Maybeboard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "Sideboard", StringComparison.OrdinalIgnoreCase);
    }
}
