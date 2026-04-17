using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RestSharp;

namespace DeckFlow.Web.Services;

/// <summary>
/// Looks up pasted card names against Scryfall and returns formatted outputs plus missing lines.
/// </summary>
public interface ICardLookupService
{
    /// <summary>
    /// Looks up the provided card list using Scryfall.
    /// </summary>
    Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default);
}

/// <summary>
/// Returns the results of a card lookup.
/// </summary>
public sealed record CardLookupResult(IReadOnlyList<string> VerifiedOutputs, IReadOnlyList<string> MissingLines);

/// <summary>
/// Looks up card lists via Scryfall's collection endpoint.
/// </summary>
public sealed class ScryfallCardLookupService : ICardLookupService
{
    private const int CollectionBatchSize = 75;
    private const int MaxCardsPerSubmission = 100;
    private static readonly Regex QuantityPrefixRegex = new(@"^(?<quantity>\d+)\s+(?<name>.+)$", RegexOptions.Compiled);
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> _executeAsync;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>> _executeSearchAsync;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>> _executeNamedAsync;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallRulingsResponse>>> _executeRulingsAsync;

    public ScryfallCardLookupService(
        RestClient? restClient = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsync = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallRulingsResponse>>>? executeRulingsAsync = null)
    {
        var client = restClient ?? ScryfallRestClientFactory.Create();
        _executeAsync = executeAsync ?? ((request, cancellationToken) => ScryfallThrottle.ExecuteAsync(token => client.ExecuteAsync<ScryfallCollectionResponse>(request, token), cancellationToken));
        _executeSearchAsync = executeSearchAsync ?? ((request, cancellationToken) => ScryfallThrottle.ExecuteAsync(token => client.ExecuteAsync<ScryfallSearchResponse>(request, token), cancellationToken));
        _executeNamedAsync = executeNamedAsync ?? ((request, cancellationToken) => ScryfallThrottle.ExecuteAsync(token => client.ExecuteAsync<ScryfallCard>(request, token), cancellationToken));
        _executeRulingsAsync = executeRulingsAsync ?? ((request, cancellationToken) => ScryfallThrottle.ExecuteAsync(token => client.ExecuteAsync<ScryfallRulingsResponse>(request, token), cancellationToken));
    }

    public async Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
    {
        var parsedLines = ParseLines(cardList);
        if (parsedLines.Count > MaxCardsPerSubmission)
        {
            throw new InvalidOperationException($"Please verify {MaxCardsPerSubmission} non-empty lines or fewer per submission.");
        }

        var resolvedCards = new Dictionary<string, ScryfallCard>(StringComparer.OrdinalIgnoreCase);
        var fallbackCards = new Dictionary<string, ScryfallCard>(StringComparer.OrdinalIgnoreCase);
        var missingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueNames = parsedLines
            .Select(line => line.CardName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var chunk in Chunk(uniqueNames, CollectionBatchSize))
        {
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new
            {
                identifiers = chunk.Select(name => new { name }).ToArray()
            });

            var response = await _executeAsync(request, cancellationToken);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall search returned HTTP {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }

            foreach (var card in response.Data.Data)
            {
                if (!string.IsNullOrWhiteSpace(card.Name))
                {
                    resolvedCards[NormalizeName(card.Name)] = card;
                }
            }

            foreach (var identifier in response.Data.NotFound ?? [])
            {
                if (!string.IsNullOrWhiteSpace(identifier.Name))
                {
                    missingNames.Add(NormalizeName(identifier.Name));
                }
            }
        }

        var verifiedOutputs = new List<string>(parsedLines.Count);
        var missingLines = new List<string>();
        var rulingsByCardId = new Dictionary<string, IReadOnlyList<ScryfallRuling>>(StringComparer.OrdinalIgnoreCase);

        foreach (var parsedLine in parsedLines)
        {
            var normalizedInput = NormalizeName(parsedLine.CardName);
            if (resolvedCards.TryGetValue(normalizedInput, out var card))
            {
                var rulings = await GetCachedRulingsAsync(card, rulingsByCardId, cancellationToken).ConfigureAwait(false);
                verifiedOutputs.Add(FormatCard(card, parsedLine.Quantity, rulings));
                continue;
            }

            if (missingNames.Contains(normalizedInput))
            {
                if (!fallbackCards.TryGetValue(normalizedInput, out var fallbackCard))
                {
                    fallbackCard = await SearchFallbackCardAsync(parsedLine.CardName, cancellationToken).ConfigureAwait(false);
                    if (fallbackCard is not null)
                    {
                        fallbackCards[normalizedInput] = fallbackCard;
                    }
                }

                if (fallbackCard is not null)
                {
                    var rulings = await GetCachedRulingsAsync(fallbackCard, rulingsByCardId, cancellationToken).ConfigureAwait(false);
                    verifiedOutputs.Add(FormatCard(fallbackCard, parsedLine.Quantity, rulings));
                    continue;
                }
            }

            missingLines.Add($"ERROR: {parsedLine.OriginalLine}");
        }

        return new CardLookupResult(verifiedOutputs, missingLines);
    }

    private async Task<IReadOnlyList<ScryfallRuling>> GetCachedRulingsAsync(
        ScryfallCard card,
        Dictionary<string, IReadOnlyList<ScryfallRuling>> cache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(card.Id))
        {
            return Array.Empty<ScryfallRuling>();
        }

        if (cache.TryGetValue(card.Id, out var cached))
        {
            return cached;
        }

        var fetched = await FetchRulingsAsync(card.Id, cancellationToken).ConfigureAwait(false);
        cache[card.Id] = fetched;
        return fetched;
    }

    private async Task<IReadOnlyList<ScryfallRuling>> FetchRulingsAsync(string cardId, CancellationToken cancellationToken)
    {
        var request = new RestRequest($"cards/{cardId}/rulings", Method.Get);
        var response = await _executeRulingsAsync(request, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
        {
            return Array.Empty<ScryfallRuling>();
        }

        return response.Data.Data ?? (IReadOnlyList<ScryfallRuling>)Array.Empty<ScryfallRuling>();
    }

    private static IEnumerable<List<string>> Chunk(IReadOnlyList<string> values, int size)
    {
        for (var index = 0; index < values.Count; index += size)
        {
            var count = Math.Min(size, values.Count - index);
            var chunk = new List<string>(count);
            for (var itemIndex = 0; itemIndex < count; itemIndex++)
            {
                chunk.Add(values[index + itemIndex]);
            }

            yield return chunk;
        }
    }

    private static string NormalizeName(string cardName)
        => cardName
            .Trim()
            .Replace('\u2019', '\'')
            .Replace('\u2018', '\'')
            .Replace('\u02BC', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .ToLowerInvariant();

    private async Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
    {
        foreach (var query in new[]
        {
            $"(printed:\"{cardName}\" OR name:\"{cardName}\")",
            cardName
        })
        {
            var request = new RestRequest("cards/search", Method.Get);
            request.AddQueryParameter("q", query);
            request.AddQueryParameter("unique", "prints");
            request.AddQueryParameter("include_multilingual", "true");

            var response = await _executeSearchAsync(request, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                continue;
            }

            var match = response.Data.Data.FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        var namedRequest = new RestRequest("cards/named", Method.Get);
        namedRequest.AddQueryParameter("fuzzy", cardName);
        var namedResponse = await _executeNamedAsync(namedRequest, cancellationToken).ConfigureAwait(false);
        if ((int)namedResponse.StatusCode >= 200 && (int)namedResponse.StatusCode < 300 && namedResponse.Data is not null)
        {
            return namedResponse.Data;
        }

        return null;
    }

    private static string FormatCard(ScryfallCard card, int? quantity, IReadOnlyList<ScryfallRuling> rulings)
    {
        var sections = new List<string>();
        sections.Add(quantity.HasValue ? $"{quantity.Value} {card.Name}" : card.Name);
        if (!string.IsNullOrWhiteSpace(card.ManaCost))
        {
            sections.Add(card.ManaCost);
        }

        sections.Add(card.TypeLine);

        if (!string.IsNullOrWhiteSpace(card.OracleText))
        {
            sections.Add(string.Empty);
            sections.Add(card.OracleText);
        }

        if (!string.IsNullOrWhiteSpace(card.Power) && !string.IsNullOrWhiteSpace(card.Toughness))
        {
            sections.Add($"{card.Power}/{card.Toughness}");
        }

        if (rulings.Count > 0)
        {
            sections.Add(string.Empty);
            sections.Add("Rulings:");
            foreach (var ruling in rulings)
            {
                if (string.IsNullOrWhiteSpace(ruling.Comment))
                {
                    continue;
                }

                var datePrefix = string.IsNullOrWhiteSpace(ruling.PublishedAt) ? string.Empty : $"({ruling.PublishedAt}) ";
                sections.Add($"- {datePrefix}{ruling.Comment}");
            }
        }

        return string.Join(Environment.NewLine, sections);
    }

    private static List<ParsedCardLine> ParseLines(string cardList)
    {
        var parsedLines = new List<ParsedCardLine>();
        using var reader = new StringReader(cardList ?? string.Empty);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var match = QuantityPrefixRegex.Match(trimmed);
            if (match.Success)
            {
                parsedLines.Add(new ParsedCardLine(
                    trimmed,
                    match.Groups["name"].Value.Trim(),
                    int.Parse(match.Groups["quantity"].Value)));
                continue;
            }

            parsedLines.Add(new ParsedCardLine(trimmed, trimmed, null));
        }

        return parsedLines;
    }

    private sealed record ParsedCardLine(string OriginalLine, string CardName, int? Quantity);
}
