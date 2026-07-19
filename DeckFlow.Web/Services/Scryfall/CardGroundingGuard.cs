using System.Net;
using System.Text.Json;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using RestSharp;

namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// Strict Scryfall-backed guard that validates candidate cards against deck-context safety rules.
/// </summary>
public sealed class CardGroundingGuard(IScryfallCardResolver resolver, IMemoryCache cache) : ICardGroundingGuard
{
    private const int ScryfallBatchSize = 75;
    private static readonly TimeSpan PositiveCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public async Task<CardGroundingVerdict> TryValidateAsync(
        string candidateName,
        CardGroundingDeckContext deckContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deckContext);

        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return CreateRejectedVerdict(candidateName, CardGroundingRejectReason.NotFound);
        }

        var resolution = await GetOrFetchResolutionAsync(candidateName, cancellationToken).ConfigureAwait(false);
        return CreateVerdict(candidateName, resolution, deckContext);
    }

    /// <inheritdoc />
    public async Task<CardGroundingBatchResult> ValidateAllAsync(
        IReadOnlyList<string> candidateNames,
        CardGroundingDeckContext deckContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateNames);
        ArgumentNullException.ThrowIfNull(deckContext);

        if (candidateNames.Count == 0)
        {
            return new CardGroundingBatchResult
            {
                Verdicts = [],
                HasUpstreamFailure = false,
            };
        }

        var resolutions = await LoadBatchResolutionsAsync(candidateNames, cancellationToken).ConfigureAwait(false);
        var verdicts = new List<CardGroundingVerdict>(candidateNames.Count);
        foreach (var candidateName in candidateNames)
        {
            if (string.IsNullOrWhiteSpace(candidateName))
            {
                verdicts.Add(CreateRejectedVerdict(candidateName, CardGroundingRejectReason.NotFound));
                continue;
            }

            verdicts.Add(CreateVerdict(candidateName, resolutions[candidateName], deckContext));
        }

        return new CardGroundingBatchResult
        {
            Verdicts = verdicts,
            HasUpstreamFailure = verdicts.Any(verdict => verdict.RejectReason == CardGroundingRejectReason.UpstreamUnavailable),
        };
    }

    private async Task<Dictionary<string, CardResolution>> LoadBatchResolutionsAsync(
        IReadOnlyList<string> candidateNames,
        CancellationToken cancellationToken)
    {
        var resolutions = new Dictionary<string, CardResolution>(candidateNames.Count, StringComparer.Ordinal);
        var uniqueCandidates = new List<string>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidateName in candidateNames)
        {
            if (string.IsNullOrWhiteSpace(candidateName))
            {
                continue;
            }

            if (resolutions.ContainsKey(candidateName))
            {
                continue;
            }

            var cacheKey = BuildCacheKey(candidateName);
            if (cache.TryGetValue<CardResolution>(cacheKey, out var cachedResolution))
            {
                resolutions[candidateName] = cachedResolution!;
                continue;
            }

            if (seenKeys.Add(cacheKey))
            {
                uniqueCandidates.Add(candidateName);
            }
        }

        for (var offset = 0; offset < uniqueCandidates.Count; offset += ScryfallBatchSize)
        {
            IReadOnlyList<string> batch = uniqueCandidates.Skip(offset).Take(ScryfallBatchSize).ToArray();
            await ResolveBatchChunkAsync(batch, resolutions, cancellationToken).ConfigureAwait(false);
        }

        foreach (var candidateName in candidateNames)
        {
            if (string.IsNullOrWhiteSpace(candidateName) || resolutions.ContainsKey(candidateName))
            {
                continue;
            }

            resolutions[candidateName] = await GetOrFetchResolutionAsync(candidateName, cancellationToken).ConfigureAwait(false);
        }

        return resolutions;
    }

    private async Task ResolveBatchChunkAsync(
        IReadOnlyList<string> candidateNames,
        IDictionary<string, CardResolution> resolutions,
        CancellationToken cancellationToken)
    {
        if (candidateNames.Count == 0)
        {
            return;
        }

        try
        {
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new
            {
                identifiers = candidateNames.Select(name => new { name }).ToArray(),
            });

            RestResponse<ScryfallCollectionResponse> response =
                await resolver.ExecuteCollectionAsync(request, cancellationToken).ConfigureAwait(false);

            ScryfallThrottle.ThrowIfUpstreamUnavailable(response.StatusCode);
            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during card grounding.",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            var matchedCardsByInput = candidateNames.ToDictionary(
                name => name,
                _ => (ScryfallCard?)null,
                StringComparer.Ordinal);

            foreach (var card in response.Data.Data)
            {
                var matchedInput = candidateNames.FirstOrDefault(candidateName =>
                    string.Equals(CardNormalizer.Normalize(card.Name), CardNormalizer.Normalize(candidateName), StringComparison.Ordinal));
                if (matchedInput is not null)
                {
                    matchedCardsByInput[matchedInput] = card;
                }
            }

            foreach (var candidateName in candidateNames)
            {
                if (matchedCardsByInput[candidateName] is { } exactCard)
                {
                    var exactResolution = CreateResolvedCard(exactCard);
                    CacheResolution(candidateName, exactResolution);
                    resolutions[candidateName] = exactResolution;
                    continue;
                }

                var fuzzyResolution = await ResolveFuzzyOnlyAsync(candidateName, cancellationToken).ConfigureAwait(false);
                if (fuzzyResolution.ResolutionReason != CardGroundingRejectReason.UpstreamUnavailable)
                {
                    CacheResolution(candidateName, fuzzyResolution);
                }

                resolutions[candidateName] = fuzzyResolution;
            }
        }
        catch
        {
            foreach (var candidateName in candidateNames)
            {
                resolutions[candidateName] = CreateUnresolvedCard(candidateName, CardGroundingRejectReason.UpstreamUnavailable);
            }
        }
    }

    private async Task<CardResolution> GetOrFetchResolutionAsync(string candidateName, CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(candidateName);
        if (cache.TryGetValue<CardResolution>(cacheKey, out var cachedResolution))
        {
            return cachedResolution!;
        }

        try
        {
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new { identifiers = new object[] { new { name = candidateName } } });

            RestResponse<ScryfallCollectionResponse> response =
                await resolver.ExecuteCollectionAsync(request, cancellationToken).ConfigureAwait(false);

            ScryfallThrottle.ThrowIfUpstreamUnavailable(response.StatusCode);
            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during card grounding.",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            var exactCard = response.Data.Data.FirstOrDefault(card =>
                string.Equals(CardNormalizer.Normalize(card.Name), CardNormalizer.Normalize(candidateName), StringComparison.Ordinal));
            if (exactCard is not null)
            {
                var exactResolution = CreateResolvedCard(exactCard);
                CacheResolution(candidateName, exactResolution);
                return exactResolution;
            }

            var fuzzyResolution = await ResolveFuzzyOnlyAsync(candidateName, cancellationToken).ConfigureAwait(false);
            CacheResolution(candidateName, fuzzyResolution);
            return fuzzyResolution;
        }
        catch
        {
            return CreateUnresolvedCard(candidateName, CardGroundingRejectReason.UpstreamUnavailable);
        }
    }

    private async Task<CardResolution> ResolveFuzzyOnlyAsync(string candidateName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await resolver.ExecuteNamedFuzzyAsync(candidateName, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return CreateUnresolvedCard(candidateName, GetNotFoundReason(response.Content));
            }

            if (response.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices && response.Data is not null)
            {
                return CreateResolvedCard(response.Data);
            }

            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
            {
                throw new HttpRequestException(
                    $"Scryfall named fuzzy lookup returned HTTP {(int)response.StatusCode} during card grounding.",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            return CreateUnresolvedCard(candidateName, CardGroundingRejectReason.NotFound);
        }
        catch
        {
            return CreateUnresolvedCard(candidateName, CardGroundingRejectReason.UpstreamUnavailable);
        }
    }

    private static CardGroundingVerdict CreateVerdict(
        string candidateName,
        CardResolution resolution,
        CardGroundingDeckContext deckContext)
    {
        if (resolution.ResolutionReason != CardGroundingRejectReason.None)
        {
            return CreateRejectedVerdict(resolution.CanonicalName, resolution.ResolutionReason);
        }

        var legalities = resolution.CommanderLegalityStatus is null
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["commander"] = resolution.CommanderLegalityStatus,
            };

        if (!CardGroundingRules.IsLegalForCommander(legalities))
        {
            return CreateRejectedVerdict(resolution.CanonicalName, CardGroundingRejectReason.NotLegal);
        }

        if (!CardGroundingRules.IsWithinColorIdentity(resolution.ColorIdentity, deckContext.CommanderColorIdentity))
        {
            return CreateRejectedVerdict(resolution.CanonicalName, CardGroundingRejectReason.IdentityViolation);
        }

        if (CardGroundingRules.IsSingletonViolation(resolution.CanonicalName, resolution.TypeLine, deckContext.DeckCardNames))
        {
            return CreateRejectedVerdict(resolution.CanonicalName, CardGroundingRejectReason.SingletonDuplicate);
        }

        if (!CardGroundingRules.IsCastable(resolution.ManaCost, deckContext.DeckProducedColors))
        {
            return CreateRejectedVerdict(resolution.CanonicalName, CardGroundingRejectReason.Uncastable);
        }

        return new CardGroundingVerdict
        {
            Accepted = true,
            CanonicalName = resolution.CanonicalName,
            RejectReason = CardGroundingRejectReason.None,
        };
    }

    private static CardGroundingVerdict CreateRejectedVerdict(string canonicalName, CardGroundingRejectReason rejectReason)
        => new()
        {
            Accepted = false,
            CanonicalName = canonicalName,
            RejectReason = rejectReason,
        };

    private static CardResolution CreateResolvedCard(ScryfallCard card)
        => new()
        {
            CanonicalName = card.Name,
            ColorIdentity = card.ColorIdentity,
            CommanderLegalityStatus = GetCommanderLegalityStatus(card.Legalities),
            ManaCost = card.ManaCost,
            TypeLine = card.TypeLine,
            ResolutionReason = CardGroundingRejectReason.None,
        };

    private static CardResolution CreateUnresolvedCard(string candidateName, CardGroundingRejectReason resolutionReason)
        => new()
        {
            CanonicalName = candidateName,
            ColorIdentity = null,
            CommanderLegalityStatus = null,
            ManaCost = null,
            TypeLine = string.Empty,
            ResolutionReason = resolutionReason,
        };

    private static string? GetCommanderLegalityStatus(IReadOnlyDictionary<string, string>? legalities)
    {
        if (legalities is null)
        {
            return null;
        }

        return legalities.TryGetValue("commander", out var commanderStatus)
            ? commanderStatus
            : null;
    }

    private static CardGroundingRejectReason GetNotFoundReason(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return CardGroundingRejectReason.NotFound;
        }

        var error = JsonSerializer.Deserialize<ScryfallErrorResponse>(content);
        return string.Equals(error?.Type, "ambiguous", StringComparison.Ordinal)
            ? CardGroundingRejectReason.Ambiguous
            : CardGroundingRejectReason.NotFound;
    }

    private void CacheResolution(string candidateName, CardResolution resolution)
    {
        if (resolution.ResolutionReason == CardGroundingRejectReason.UpstreamUnavailable)
        {
            return;
        }

        cache.Set(
            BuildCacheKey(candidateName),
            resolution,
            resolution.ResolutionReason == CardGroundingRejectReason.None ? PositiveCacheTtl : NegativeCacheTtl);
    }

    private static string BuildCacheKey(string candidateName)
        => "card-grounding-guard:" + candidateName.Trim().ToLowerInvariant();

    private sealed record CardResolution
    {
        public required string CanonicalName { get; init; }

        public required IReadOnlyList<string>? ColorIdentity { get; init; }

        public required string? CommanderLegalityStatus { get; init; }

        public required string? ManaCost { get; init; }

        public required string TypeLine { get; init; }

        public required CardGroundingRejectReason ResolutionReason { get; init; }
    }
}
