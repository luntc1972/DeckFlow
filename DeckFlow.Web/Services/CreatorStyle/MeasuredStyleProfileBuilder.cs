using System.Net;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Orchestrates creator measured-style extraction and persistence.
/// </summary>
public sealed class MeasuredStyleProfileBuilder
{
    private const int ScryfallBatchSize = 75;
    private const int MaxLiftMetrics = 25;
    private static readonly HashSet<string> AnalyzedBoards = new(StringComparer.OrdinalIgnoreCase)
    {
        "mainboard",
        "commander"
    };

    private readonly CreatorProfileDeckCrawler _deckCrawler;
    private readonly CreatorDeckCategoryResolver _categoryResolver;
    private readonly CategoryKnowledgeRepository _categoryKnowledgeRepository;
    private readonly ICommanderSpellbookService _commanderSpellbookService;
    private readonly IScryfallCardResolver _scryfallCardResolver;
    private readonly ICreatorStyleProfileStore _profileStore;
    private readonly ILogger<MeasuredStyleProfileBuilder> _logger;
    private readonly Func<DateTimeOffset> _nowUtc;

    /// <summary>
    /// Creates a measured-style profile builder.
    /// </summary>
    public MeasuredStyleProfileBuilder(
        CreatorProfileDeckCrawler deckCrawler,
        CreatorDeckCategoryResolver categoryResolver,
        CategoryKnowledgeRepository categoryKnowledgeRepository,
        ICommanderSpellbookService commanderSpellbookService,
        IScryfallCardResolver scryfallCardResolver,
        ICreatorStyleProfileStore profileStore,
        ILogger<MeasuredStyleProfileBuilder>? logger = null)
        : this(
            deckCrawler,
            categoryResolver,
            categoryKnowledgeRepository,
            commanderSpellbookService,
            scryfallCardResolver,
            profileStore,
            logger,
            null)
    {
    }

    internal MeasuredStyleProfileBuilder(
        CreatorProfileDeckCrawler deckCrawler,
        CreatorDeckCategoryResolver categoryResolver,
        CategoryKnowledgeRepository categoryKnowledgeRepository,
        ICommanderSpellbookService commanderSpellbookService,
        IScryfallCardResolver scryfallCardResolver,
        ICreatorStyleProfileStore profileStore,
        ILogger<MeasuredStyleProfileBuilder>? logger,
        Func<DateTimeOffset>? nowUtc)
    {
        ArgumentNullException.ThrowIfNull(deckCrawler);
        ArgumentNullException.ThrowIfNull(categoryResolver);
        ArgumentNullException.ThrowIfNull(categoryKnowledgeRepository);
        ArgumentNullException.ThrowIfNull(commanderSpellbookService);
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);
        ArgumentNullException.ThrowIfNull(profileStore);
        _deckCrawler = deckCrawler;
        _categoryResolver = categoryResolver;
        _categoryKnowledgeRepository = categoryKnowledgeRepository;
        _commanderSpellbookService = commanderSpellbookService;
        _scryfallCardResolver = scryfallCardResolver;
        _profileStore = profileStore;
        _logger = logger ?? NullLogger<MeasuredStyleProfileBuilder>.Instance;
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Builds and persists a measured creator style profile for the supplied creator slug.
    /// </summary>
    /// <param name="creatorSlug">Creator slug.</param>
    /// <param name="platform">Creator platform identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted measured creator style profile.</returns>
    public async Task<CreatorStyleProfile> BuildAsync(
        string creatorSlug,
        string platform,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        IReadOnlyList<CreatorDeckSample> crawledSamples = await _deckCrawler
            .CrawlAsync(creatorSlug, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<CreatorDeckSample> filteredSamples = StapleStripper.FilterOversized(crawledSamples);
        IReadOnlyList<CreatorDeckSample> flaggedSamples = StapleStripper.FlagNearPrecons(filteredSamples);
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories = await _categoryResolver
            .ResolveAsync(flaggedSamples, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlySet<string> personalStaples = StapleStripper.ComputePersonalStaples(flaggedSamples);
        IReadOnlyList<CreatorDeckSample> strippedSamples = StapleStripper.StripStaples(flaggedSamples, personalStaples);
        IReadOnlyList<CreatorDeckSample> weightedSamples = FolderWeighting.ApplyWeights(
            strippedSamples,
            flaggedSamples
                .Where(sample => sample.FolderId.HasValue)
                .GroupBy(sample => sample.FolderId!.Value)
                .ToDictionary(group => group.Key, group => group.First().FolderWeight),
            weightsUncurated: flaggedSamples.All(sample => Math.Abs(sample.FolderWeight - 1.0) < 0.0001));

        int rawDeckCount = FolderWeighting.RawDeckCount(weightedSamples);
        double effectiveSampleSize = FolderWeighting.EffectiveSampleSize(weightedSamples);
        GlobalCategoryBaseline baseline = await _categoryKnowledgeRepository
            .GetGlobalCategoryBaselineAsync(cancellationToken)
            .ConfigureAwait(false);

        List<MeasuredMetric> metrics = BuildCategoryMetrics(weightedSamples, cardCategories, rawDeckCount, effectiveSampleSize);
        metrics.AddRange(BuildLiftMetrics(weightedSamples, cardCategories, baseline, rawDeckCount, effectiveSampleSize));
        metrics.Add(await BuildComboDensityMetricAsync(weightedSamples, rawDeckCount, effectiveSampleSize, cancellationToken).ConfigureAwait(false));
        metrics.AddRange(await BuildKarstenMetricsAsync(weightedSamples, rawDeckCount, effectiveSampleSize, cancellationToken).ConfigureAwait(false));
        metrics = metrics
            .OrderBy(metric => metric.Metric, StringComparer.Ordinal)
            .ToList();

        var profile = new CreatorStyleProfile
        {
            Slug = creatorSlug,
            Platform = platform,
            MinDecks = rawDeckCount,
            InsufficientSample = rawDeckCount < CreatorStyleProfile.MinDeckFloor,
            MeasuredMetrics = metrics,
            StatedRules = Array.Empty<StatedRule>(),
            FusedTargets = Array.Empty<FusedTarget>(),
            UpdatedUtc = _nowUtc()
        };

        await _profileStore.UpsertAsync(profile, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    private static List<MeasuredMetric> BuildCategoryMetrics(
        IReadOnlyList<CreatorDeckSample> samples,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories,
        int rawDeckCount,
        double effectiveSampleSize)
    {
        var metrics = new List<MeasuredMetric>();
        List<IReadOnlyDictionary<string, int>> perDeckCounts = samples
            .Select(sample => CategoryCounter.CountPerDeck(sample, cardCategories))
            .ToList();
        IEnumerable<string> categories = perDeckCounts
            .SelectMany(counts => counts.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase);

        foreach (string category in categories)
        {
            IReadOnlyList<double> perDeckValues = perDeckCounts
                .Select(counts => counts.TryGetValue(category, out var count) ? (double)count : 0d)
                .ToArray();

            metrics.Add(new MeasuredMetric
            {
                Metric = $"category_ratio:{category}",
                Value = perDeckValues.Count == 0 ? 0 : perDeckValues.Average(),
                NumDecks = rawDeckCount,
                Distribution = BuildDistribution(perDeckValues, effectiveSampleSize)
            });
        }

        return metrics;
    }

    private static IEnumerable<MeasuredMetric> BuildLiftMetrics(
        IReadOnlyList<CreatorDeckSample> samples,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories,
        GlobalCategoryBaseline baseline,
        int rawDeckCount,
        double effectiveSampleSize)
    {
        return LiftCalculator.ComputeLift(samples, cardCategories, baseline)
            .Take(MaxLiftMetrics)
            .Select(item => new MeasuredMetric
            {
                Metric = $"lift:{item.CategoryA}|{item.CategoryB}",
                Value = item.Lift,
                NumDecks = rawDeckCount,
                Distribution = BuildDistribution([item.Lift], effectiveSampleSize)
            })
            .ToList();
    }

    private async Task<MeasuredMetric> BuildComboDensityMetricAsync(
        IReadOnlyList<CreatorDeckSample> samples,
        int rawDeckCount,
        double effectiveSampleSize,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<double> comboCounts = await Task.WhenAll(
                samples.Select(sample => ResolveComboCountAsync(sample.Entries, cancellationToken)))
            .ConfigureAwait(false);

        return AverageMetric("combo_density:included_per_deck", comboCounts, rawDeckCount, effectiveSampleSize);
    }

    private async Task<IReadOnlyList<MeasuredMetric>> BuildKarstenMetricsAsync(
        IReadOnlyList<CreatorDeckSample> samples,
        int rawDeckCount,
        double effectiveSampleSize,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ManabaseReport> reports = await Task.WhenAll(
                samples.Select(sample => AnalyzeDeckAsync(sample.Entries, cancellationToken)))
            .ConfigureAwait(false);

        IReadOnlyList<double> landDelta = reports.Select(report => report.LandDelta).ToArray();
        IReadOnlyList<double> targetLands = reports.Select(report => report.TargetLands).ToArray();
        IReadOnlyList<double> healthScores = reports.Select(report => ToHealthScore(report.Health)).ToArray();

        return
        [
            AverageMetric("karsten:land_delta", landDelta, rawDeckCount, effectiveSampleSize),
            AverageMetric("karsten:target_lands", targetLands, rawDeckCount, effectiveSampleSize),
            AverageMetric("karsten:health_score", healthScores, rawDeckCount, effectiveSampleSize)
        ];
    }

    private static MeasuredMetric AverageMetric(
        string metricName,
        IReadOnlyList<double> values,
        int rawDeckCount,
        double effectiveSampleSize)
    {
        return new MeasuredMetric
        {
            Metric = metricName,
            Value = values.Count == 0 ? 0 : values.Average(),
            NumDecks = rawDeckCount,
            Distribution = BuildDistribution(values, effectiveSampleSize)
        };
    }

    private async Task<double> ResolveComboCountAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken)
    {
        CommanderSpellbookResult? result = await _commanderSpellbookService
            .FindCombosAsync(entries, cancellationToken)
            .ConfigureAwait(false);

        return result?.IncludedCombos.Count ?? 0;
    }

    private async Task<ManabaseReport> AnalyzeDeckAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken)
    {
        List<DeckEntry> deckCards = ReflagInferredCommanders(entries.ToList())
            .Where(entry => AnalyzedBoards.Contains(entry.Board ?? string.Empty))
            .ToList();

        if (deckCards.Count == 0)
        {
            return EmptyReport();
        }

        ScryfallCardNameIndex index = await ResolveCardsAsync(deckCards, cancellationToken).ConfigureAwait(false);
        var deckEntries = new List<DeckCardEntry>();

        foreach (DeckEntry entry in deckCards)
        {
            ScryfallCardData? card;
            if (!index.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out card))
            {
                ScryfallCard? fallback = await _scryfallCardResolver
                    .SearchFallbackCardAsync(entry.Name, cancellationToken)
                    .ConfigureAwait(false);
                if (fallback is not null)
                {
                    card = ScryfallCardDataMapper.ToCardData(fallback);
                    index.Add(card);
                }
            }

            if (card is null)
            {
                _logger.LogDebug("Skipping unresolved creator-style manabase card {CardName}.", entry.Name);
                continue;
            }

            deckEntries.Add(new DeckCardEntry
            {
                Card = card,
                Quantity = entry.Quantity,
                IsCommander = string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)
            });
        }

        if (deckEntries.Count == 0)
        {
            return EmptyReport();
        }

        IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        // Why: the measured-style substrate must stay deterministic and creator-to-creator comparable,
        // so it fixes Karsten to Casual and leaves every experimental Analyze flag at its default false
        // value; any future cEDH-mode fusion belongs above this substrate, not inside it.
        return ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);
    }

    private async Task<ScryfallCardNameIndex> ResolveCardsAsync(
        IReadOnlyList<DeckEntry> deckCards,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var identifiers = new List<object>();
        foreach (DeckEntry entry in deckCards)
        {
            string? printing = ScryfallCardNameIndex.PrintingKey(entry.SetCode, entry.CollectorNumber);
            string key = printing ?? $"name:{entry.Name}";
            if (!seen.Add(key))
            {
                continue;
            }

            identifiers.Add(printing is not null
                ? new { set = entry.SetCode, collector_number = entry.CollectorNumber }
                : (object)new { name = entry.Name });
        }

        var index = new ScryfallCardNameIndex();
        for (int offset = 0; offset < identifiers.Count; offset += ScryfallBatchSize)
        {
            object[] batch = identifiers.Skip(offset).Take(ScryfallBatchSize).ToArray();
            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(new { identifiers = batch });

            RestResponse<ScryfallCollectionResponse> response = await _scryfallCardResolver
                .ExecuteCollectionAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices || response.Data is null)
            {
                throw new HttpRequestException(
                    $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during creator-style manabase analysis.",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            foreach (ScryfallCard card in response.Data.Data)
            {
                index.Add(ScryfallCardDataMapper.ToCardData(card));
            }
        }

        return index;
    }

    private static List<DeckEntry> ReflagInferredCommanders(List<DeckEntry> entries)
    {
        IReadOnlyList<string> commanderNames = CommanderInference.InferLeadingCommanderNames(entries);
        if (commanderNames.Count == 0)
        {
            return entries;
        }

        var commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return entries
            .Select(entry => commanderNameSet.Contains(entry.Name)
                && !string.Equals(entry.Board, "sideboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.Board, "maybeboard", StringComparison.OrdinalIgnoreCase)
                ? entry with { Board = "commander" }
                : entry)
            .ToList();
    }

    private static MetricDistribution BuildDistribution(IReadOnlyList<double> values, double effectiveSampleSize)
    {
        if (values.Count == 0)
        {
            return new MetricDistribution
            {
                Mean = 0,
                Min = 0,
                Max = 0,
                StdDev = 0,
                EffectiveSampleSize = effectiveSampleSize
            };
        }

        double mean = values.Average();
        double variance = values.Sum(value => Math.Pow(value - mean, 2)) / values.Count;

        return new MetricDistribution
        {
            Mean = mean,
            Min = values.Min(),
            Max = values.Max(),
            StdDev = Math.Sqrt(variance),
            EffectiveSampleSize = effectiveSampleSize
        };
    }

    private static double ToHealthScore(ManabaseHealth health)
        => health switch
        {
            ManabaseHealth.Healthy => 3,
            ManabaseHealth.Functional => 2,
            ManabaseHealth.Workable => 1,
            _ => 0
        };

    private static ManabaseReport EmptyReport()
        => new()
        {
            ActualLands = 0,
            TargetLands = 0,
            ColorFindings = Array.Empty<ColorSourceFinding>(),
            Mode = ManabaseMode.Casual,
            Castability = Array.Empty<CardCastability>(),
            ColorSpellCounts = new Dictionary<ManaColor, int>(),
            CommanderColors = Array.Empty<ManaColor>(),
            LandTarget = null,
            TapAnalysis = null,
            MulliganEvaluation = null,
            DemandingCards = Array.Empty<DemandingCard>(),
            RampSourceNames = Array.Empty<string>(),
            RampAndDrawNames = Array.Empty<string>(),
            UnsupportedInteractions = Array.Empty<UnsupportedInteraction>(),
            Summary = string.Empty
        };
}
