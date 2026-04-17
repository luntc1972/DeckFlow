using System.Net;
using System.Text.Json;
using RestSharp;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Integration;

public sealed class MoxfieldApiDeckImporter : IMoxfieldDeckImporter
{
    private readonly RestClient _restClient;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse>> _executeAsync;

    /// <summary>
    /// Initializes a new instance with an optional RestClient instance.
    /// </summary>
    /// <param name="restClient">Client used for HTTP requests (tests can override).</param>
    public MoxfieldApiDeckImporter(
        RestClient? restClient = null,
        Func<RestRequest, CancellationToken, Task<RestResponse>>? executeAsync = null)
    {
        _restClient = restClient ?? new RestClient(new RestClientOptions
        {
            ThrowOnAnyError = false,
        });
        _executeAsync = executeAsync ?? ((request, cancellationToken) => _restClient.ExecuteAsync(request, cancellationToken));
    }

    /// <summary>
    /// Imports the requested Moxfield deck and returns deck entries with categories.
    /// When Moxfield rejects the request (typical on cloud IPs), falls back to Commander
    /// Spellbook's public card-list-from-url endpoint.
    /// </summary>
    public async Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
    {
        var result = await ImportWithSourceAsync(urlOrDeckId, cancellationToken).ConfigureAwait(false);
        return result.Entries;
    }

    public async Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
    {
        if (!MoxfieldApiUrl.TryGetDeckId(urlOrDeckId, out var deckId))
        {
            throw new InvalidOperationException($"Unable to determine Moxfield deck id from: {urlOrDeckId}");
        }

        try
        {
            var entries = await FetchDirectAsync(deckId, cancellationToken).ConfigureAwait(false);
            return new MoxfieldImportResult(entries, MoxfieldImportSource.Direct);
        }
        catch (HttpRequestException httpException) when (IsCloudEdgeBlock(httpException))
        {
            var entries = await FetchViaCommanderSpellbookAsync(urlOrDeckId, deckId, cancellationToken).ConfigureAwait(false);
            var statusCode = httpException.StatusCode is null ? "an error" : $"HTTP {(int)httpException.StatusCode.Value}";
            var notice =
                $"Loaded this deck through the Commander Spellbook fallback because Moxfield returned {statusCode} to our server. " +
                "Card printings, set codes, collector numbers, author tags/categories, and maybeboard/sideboard entries are not available through the fallback. " +
                "For full deck metadata, copy the Moxfield deck export text from the Moxfield site and paste it into the deck input instead.";
            return new MoxfieldImportResult(entries, MoxfieldImportSource.CommanderSpellbookFallback, notice);
        }
    }

    private async Task<List<DeckEntry>> FetchDirectAsync(string deckId, CancellationToken cancellationToken)
    {
        var request = new RestRequest(MoxfieldApiUrl.BuildDeckApiUri(deckId), Method.Get);
        request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
        request.AddHeader("Accept", "application/json, text/plain, */*");
        request.AddHeader("Referer", "https://moxfield.com/");
        request.AddHeader("Accept-Language", "en-US,en;q=0.9");

        var response = await _executeAsync(request, cancellationToken);
        var body = response.Content ?? string.Empty;
        if (!response.IsSuccessful)
        {
            throw new HttpRequestException(
                $"Moxfield API deck {deckId} returned {(int)response.StatusCode} {response.StatusDescription}: {body[..Math.Min(body.Length, 500)]}",
                inner: null,
                statusCode: response.StatusCode);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var authorTags = ReadAuthorTags(root);
        var entries = new List<DeckEntry>();

        AddBoardEntries(root, "commanders", "commander", authorTags, entries);
        AddBoardEntries(root, "mainboard", "mainboard", authorTags, entries);
        AddBoardEntries(root, "maybeboard", "maybeboard", authorTags, entries);
        AddBoardEntries(root, "sideboard", "sideboard", authorTags, entries);

        return entries;
    }

    private async Task<List<DeckEntry>> FetchViaCommanderSpellbookAsync(string originalUrl, string deckId, CancellationToken cancellationToken)
    {
        var moxfieldUrl = originalUrl.Contains("moxfield.com", StringComparison.OrdinalIgnoreCase)
            ? originalUrl
            : $"https://moxfield.com/decks/{deckId}";

        var request = new RestRequest("https://backend.commanderspellbook.com/card-list-from-url", Method.Get);
        request.AddQueryParameter("url", moxfieldUrl);
        request.AddHeader("User-Agent", "DeckFlow/1.0 (+https://github.com/luntc1972/DeckFlow)");
        request.AddHeader("Accept", "application/json");

        var response = await _executeAsync(request, cancellationToken);
        var body = response.Content ?? string.Empty;
        if (!response.IsSuccessful)
        {
            throw new HttpRequestException(
                $"Moxfield API deck {deckId} was blocked and the Commander Spellbook fallback also failed with {(int)response.StatusCode}.",
                inner: null,
                statusCode: response.StatusCode);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var entries = new List<DeckEntry>();
        AddSpellbookEntries(root, "commanders", "commander", entries);
        AddSpellbookEntries(root, "main", "mainboard", entries);
        return entries;
    }

    private static bool IsCloudEdgeBlock(HttpRequestException exception)
    {
        var code = (int?)exception.StatusCode;
        if (code is null) return false;
        // Cloudflare-style blocks and 5xx failures the fallback might tolerate.
        return code.Value is 401 or 403 or 429 or 451 or >= 500 and <= 599;
    }

    private static void AddSpellbookEntries(JsonElement root, string propertyName, string board, List<DeckEntry> entries)
    {
        if (!root.TryGetProperty(propertyName, out var list) || list.ValueKind != JsonValueKind.Array) return;

        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("card", out var cardProp) || cardProp.ValueKind != JsonValueKind.String) continue;
            var name = cardProp.GetString();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var quantity = item.TryGetProperty("quantity", out var qtyProp) && qtyProp.ValueKind == JsonValueKind.Number
                ? qtyProp.GetInt32()
                : 1;
            if (quantity == 0) continue;

            entries.Add(new DeckEntry
            {
                Name = name,
                NormalizedName = CardNormalizer.Normalize(name),
                Quantity = quantity,
                Board = board,
                SetCode = null,
                CollectorNumber = null,
                Category = null,
                IsFoil = false,
            });
        }
    }

    /// <summary>
    /// Reads any author-supplied tags that may be attached to cards.
    /// </summary>
    /// <param name="root">Root JSON element representing the deck payload.</param>
    private static Dictionary<string, string?> ReadAuthorTags(JsonElement root)
    {
        var tags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("authorTags", out var authorTagsElement) || authorTagsElement.ValueKind != JsonValueKind.Object)
        {
            return tags;
        }

        foreach (var property in authorTagsElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var values = property.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            tags[property.Name] = values.Count == 0 ? null : string.Join(",", values);
        }

        return tags;
    }

    /// <summary>
    /// Adds entries for the specified board section (commanders/main/side/maybe).
    /// </summary>
    /// <param name="root">Root JSON element representing the deck payload.</param>
    /// <param name="propertyName">JSON property for the desired board.</param>
    /// <param name="board">Target board label.</param>
    /// <param name="authorTags">Mapped author tags by card name.</param>
    /// <param name="entries">Accumulator for parsed entries.</param>
    private static void AddBoardEntries(JsonElement root, string propertyName, string board, Dictionary<string, string?> authorTags, List<DeckEntry> entries)
    {
        if (!root.TryGetProperty(propertyName, out var boardElement) || boardElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in boardElement.EnumerateObject())
        {
            var entry = property.Value;
            var quantity = entry.GetProperty("quantity").GetInt32();
            if (quantity == 0)
            {
                continue;
            }

            var card = entry.GetProperty("card");
            var name = card.GetProperty("name").GetString() ?? property.Name;
            authorTags.TryGetValue(name, out var category);

        entries.Add(new DeckEntry
        {
                Name = name,
                NormalizedName = CardNormalizer.Normalize(name),
                Quantity = quantity,
                Board = board,
                SetCode = card.TryGetProperty("set", out var setElement) ? setElement.GetString() : null,
                CollectorNumber = card.TryGetProperty("cn", out var cnElement) ? cnElement.GetString()?.Replace("★", string.Empty, StringComparison.Ordinal) : null,
                Category = string.IsNullOrWhiteSpace(category) ? (board == "maybeboard" ? "Maybeboard" : null) : category,
                IsFoil = entry.TryGetProperty("isFoil", out var foilElement) && foilElement.ValueKind == JsonValueKind.True,
            });
        }
    }
}
