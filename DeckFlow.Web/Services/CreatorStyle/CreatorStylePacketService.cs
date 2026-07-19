using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Knowledge.CreatorStyleRubric;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Builds creator-style artifact packets from the creator profile, submitted deck, and validated deck context.
/// </summary>
public interface ICreatorStylePacketService
{
    /// <summary>
    /// Builds a deterministic creator-style artifact packet for the supplied request.
    /// </summary>
    /// <param name="request">Current creator-style request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assembled packet result.</returns>
    Task<CreatorStylePacketResult> BuildAsync(CreatorStyleRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Accepted-only exemplar projection returned by <see cref="CreatorStylePacketService"/>.
/// </summary>
public sealed record CreatorStyleExemplarDeck
{
    /// <summary>
    /// Gets the creator deck identifier.
    /// </summary>
    public required string DeckId { get; init; }

    /// <summary>
    /// Gets the optional creator folder name.
    /// </summary>
    public string? FolderName { get; init; }

    /// <summary>
    /// Gets the upstream confidence marker for this exemplar.
    /// </summary>
    public required string ConfidenceMarker { get; init; }

    /// <summary>
    /// Gets the accepted canonical card names retained for this exemplar.
    /// </summary>
    public required IReadOnlyList<string> CardNames { get; init; }
}

/// <summary>
/// Returns the results of a creator-style packet build.
/// </summary>
// Why: keep JSON-round-trippable; do not convert to get-only.
public sealed record CreatorStylePacketResult
{
    /// <summary>
    /// Gets the deterministic artifact text assembled for downstream critique.
    /// </summary>
    public required string ArtifactText { get; init; }

    /// <summary>
    /// Gets the rubric scores for the submitted deck against the creator profile.
    /// </summary>
    public required RubricScoreResult RubricScores { get; init; }

    /// <summary>
    /// Gets the accepted-only exemplar deck projections.
    /// </summary>
    public required IReadOnlyList<CreatorStyleExemplarDeck> Exemplars { get; init; }

    /// <summary>
    /// Gets the validated creator-whitelist names returned by the pool builder.
    /// </summary>
    public required IReadOnlyList<string> ValidatedWhitelist { get; init; }

    /// <summary>
    /// Gets the validated combo-card names retained after the extra grounding pass.
    /// </summary>
    public required IReadOnlyList<string> ValidatedComboCards { get; init; }

    /// <summary>
    /// Gets a value indicating whether any candidate cards were withheld or upstream grounding degraded.
    /// </summary>
    public required bool GroundingDegraded { get; init; }

    /// <summary>
    /// Gets an optional notice describing degraded or incomplete grounding context.
    /// </summary>
    public string? Notice { get; init; }
}

/// <summary>
/// Orchestrates creator-style packet assembly with a single fail-closed post-whitelist grounding pass.
/// </summary>
public sealed class CreatorStylePacketService : ICreatorStylePacketService
{
    private const int MaxUserTextLength = 200;
    private const string CritiqueInstruction = "Critique this deck ONLY using the cards provided above. Do not invent, suggest, or reference any card that is not listed here.";

    private readonly ICreatorStyleProfileStore? _creatorStyleProfileStore;
    private readonly ISubmittedDeckStatsBuilder? _submittedDeckStatsBuilder;
    private readonly CreatorWhitelistPoolBuilder? _creatorWhitelistPoolBuilder;
    private readonly ICardGroundingGuard? _cardGroundingGuard;
    private readonly ICreatorDeckCacheStore? _creatorDeckCacheStore;
    private readonly ICommanderSpellbookService? _commanderSpellbookService;
    private readonly ILogger<CreatorStylePacketService> _logger;
    private readonly Func<string, CancellationToken, Task<CreatorStyleProfile?>>? _getProfileAsyncOverride;
    private readonly Func<string, CancellationToken, Task<SubmittedDeckAnalysis>>? _buildSubmittedDeckAsyncOverride;
    private readonly Func<string, CardGroundingDeckContext, CancellationToken, Task<CreatorWhitelistPoolBuildResult>>? _buildWhitelistAsyncOverride;
    private readonly Func<IReadOnlyList<string>, CardGroundingDeckContext, CancellationToken, Task<CardGroundingBatchResult>>? _validateAdditionalCardsAsyncOverride;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<CreatorDeckCacheEntry>>>? _getCreatorDecksAsyncOverride;
    private readonly Func<IReadOnlyList<DeckEntry>, CancellationToken, Task<CommanderSpellbookResult?>>? _findCombosAsyncOverride;
    private readonly Func<string, IReadOnlyList<FusedTarget>, SubmittedDeckStats, RubricScoreResult>? _scoreRubricOverride;

    /// <summary>
    /// Creates the production creator-style packet service.
    /// </summary>
    public CreatorStylePacketService(
        ICreatorStyleProfileStore creatorStyleProfileStore,
        ISubmittedDeckStatsBuilder submittedDeckStatsBuilder,
        CreatorWhitelistPoolBuilder creatorWhitelistPoolBuilder,
        ICardGroundingGuard cardGroundingGuard,
        ICreatorDeckCacheStore creatorDeckCacheStore,
        ICommanderSpellbookService commanderSpellbookService,
        ILogger<CreatorStylePacketService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(creatorStyleProfileStore);
        ArgumentNullException.ThrowIfNull(submittedDeckStatsBuilder);
        ArgumentNullException.ThrowIfNull(creatorWhitelistPoolBuilder);
        ArgumentNullException.ThrowIfNull(cardGroundingGuard);
        ArgumentNullException.ThrowIfNull(creatorDeckCacheStore);
        ArgumentNullException.ThrowIfNull(commanderSpellbookService);

        _creatorStyleProfileStore = creatorStyleProfileStore;
        _submittedDeckStatsBuilder = submittedDeckStatsBuilder;
        _creatorWhitelistPoolBuilder = creatorWhitelistPoolBuilder;
        _cardGroundingGuard = cardGroundingGuard;
        _creatorDeckCacheStore = creatorDeckCacheStore;
        _commanderSpellbookService = commanderSpellbookService;
        _logger = logger ?? NullLogger<CreatorStylePacketService>.Instance;
    }

    internal CreatorStylePacketService(
        Func<string, CancellationToken, Task<CreatorStyleProfile?>>? getProfileAsync = null,
        Func<string, CancellationToken, Task<SubmittedDeckAnalysis>>? buildSubmittedDeckAsync = null,
        Func<string, CardGroundingDeckContext, CancellationToken, Task<CreatorWhitelistPoolBuildResult>>? buildWhitelistAsync = null,
        Func<IReadOnlyList<string>, CardGroundingDeckContext, CancellationToken, Task<CardGroundingBatchResult>>? validateAdditionalCardsAsync = null,
        Func<string, CancellationToken, Task<IReadOnlyList<CreatorDeckCacheEntry>>>? getCreatorDecksAsync = null,
        Func<IReadOnlyList<DeckEntry>, CancellationToken, Task<CommanderSpellbookResult?>>? findCombosAsync = null,
        Func<string, IReadOnlyList<FusedTarget>, SubmittedDeckStats, RubricScoreResult>? scoreRubric = null,
        ILogger<CreatorStylePacketService>? logger = null)
    {
        _getProfileAsyncOverride = getProfileAsync;
        _buildSubmittedDeckAsyncOverride = buildSubmittedDeckAsync;
        _buildWhitelistAsyncOverride = buildWhitelistAsync;
        _validateAdditionalCardsAsyncOverride = validateAdditionalCardsAsync;
        _getCreatorDecksAsyncOverride = getCreatorDecksAsync;
        _findCombosAsyncOverride = findCombosAsync;
        _scoreRubricOverride = scoreRubric;
        _logger = logger ?? NullLogger<CreatorStylePacketService>.Instance;
    }

    /// <inheritdoc />
    public async Task<CreatorStylePacketResult> BuildAsync(CreatorStyleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        CreatorStyleProfile? profile = await GetProfileAsync(request.CreatorSlug, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return CreateUnavailableResult("No creator style profile is available for the supplied creator slug.");
        }

        if (profile.InsufficientSample)
        {
            return CreateUnavailableResult("The creator style profile sample is insufficient for artifact generation.");
        }

        SubmittedDeckAnalysis analysis = await BuildSubmittedDeckAsync(request.DeckSource, cancellationToken).ConfigureAwait(false);
        RubricScoreResult rubricScores = _scoreRubricOverride is not null
            ? _scoreRubricOverride(request.CreatorSlug, profile.FusedTargets, analysis.Stats)
            : CreatorStyleRubricScorer.Score(request.CreatorSlug, profile.FusedTargets, analysis.Stats);

        IReadOnlyList<CreatorDeckCacheEntry> creatorDecks = await GetCreatorDecksAsync(request.CreatorSlug, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<CreatorDeckCacheEntry> selectedExemplars = CreatorDeckExemplarSelector.SelectExemplars(creatorDecks, analysis.Stats.DeckSize);

        CreatorWhitelistPoolBuildResult whitelist = await BuildWhitelistAsync(
            request.CreatorSlug,
            analysis.DeckContext,
            cancellationToken).ConfigureAwait(false);

        CommanderSpellbookResult? comboResult = await FindCombosAsync(analysis.Entries, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> comboCandidates = comboResult?.IncludedCombos
            .SelectMany(combo => combo.CardNames)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        HashSet<string> whitelistSet = new(whitelist.AcceptedNames, StringComparer.Ordinal);
        IReadOnlyList<string> additionalCandidates = selectedExemplars
            .SelectMany(deck => deck.Entries.Select(entry => entry.Name.Trim()))
            .Concat(comboCandidates)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Where(name => !whitelistSet.Contains(name))
            .ToArray();

        CardGroundingBatchResult additionalValidation = additionalCandidates.Count == 0
            ? new CardGroundingBatchResult
            {
                Verdicts = [],
                HasUpstreamFailure = false,
            }
            : await ValidateAdditionalCardsAsync(additionalCandidates, analysis.DeckContext, cancellationToken).ConfigureAwait(false);

        Dictionary<string, string> acceptedByOriginal = BuildAcceptedByOriginal(additionalCandidates, additionalValidation.Verdicts);
        IReadOnlyList<CreatorStyleExemplarDeck> exemplars = selectedExemplars
            .Select(deck => new CreatorStyleExemplarDeck
            {
                DeckId = deck.DeckId,
                FolderName = deck.FolderName,
                ConfidenceMarker = deck.ConfidenceMarker,
                CardNames = deck.Entries
                    .Select(entry => ResolveAcceptedCardName(entry.Name.Trim(), whitelistSet, acceptedByOriginal))
                    .Where(static cardName => cardName is not null)
                    .Cast<string>()
                    .ToArray(),
            })
            .ToArray();

        IReadOnlyList<string> validatedComboCards = comboCandidates
            .Where(cardName => acceptedByOriginal.ContainsKey(cardName))
            .Select(cardName => acceptedByOriginal[cardName])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        int excludedCount = additionalCandidates.Count - acceptedByOriginal.Count;
        bool groundingDegraded = whitelist.HasUpstreamFailure
            || additionalValidation.HasUpstreamFailure
            || excludedCount > 0;
        string? notice = groundingDegraded
            ? "Some candidate cards were withheld because grounding could not fully validate them."
            : null;

        if (groundingDegraded)
        {
            _logger.LogWarning(
                "Creator-style grounding degraded for creator {CreatorSlug}; accepted {AcceptedCount} of {CandidateCount} post-whitelist candidates.",
                request.CreatorSlug,
                acceptedByOriginal.Count,
                additionalCandidates.Count);
        }

        return new CreatorStylePacketResult
        {
            ArtifactText = BuildArtifactText(
                request,
                profile,
                rubricScores,
                exemplars,
                whitelist.AcceptedNames,
                validatedComboCards,
                groundingDegraded,
                notice),
            RubricScores = rubricScores,
            Exemplars = exemplars,
            ValidatedWhitelist = whitelist.AcceptedNames,
            ValidatedComboCards = validatedComboCards,
            GroundingDegraded = groundingDegraded,
            Notice = notice,
        };
    }

    private static CreatorStylePacketResult CreateUnavailableResult(string notice)
        => new()
        {
            ArtifactText = string.Empty,
            RubricScores = new RubricScoreResult
            {
                CreatorSlug = string.Empty,
                MetricScores = [],
            },
            Exemplars = [],
            ValidatedWhitelist = [],
            ValidatedComboCards = [],
            GroundingDegraded = true,
            Notice = notice,
        };

    private static Dictionary<string, string> BuildAcceptedByOriginal(
        IReadOnlyList<string> candidateNames,
        IReadOnlyList<CardGroundingVerdict> verdicts)
    {
        var accepted = new Dictionary<string, string>(StringComparer.Ordinal);
        int count = Math.Min(candidateNames.Count, verdicts.Count);
        for (int i = 0; i < count; i++)
        {
            if (verdicts[i].Accepted)
            {
                accepted[candidateNames[i]] = verdicts[i].CanonicalName;
            }
        }

        return accepted;
    }

    private static string? ResolveAcceptedCardName(
        string candidateName,
        IReadOnlySet<string> whitelistSet,
        IReadOnlyDictionary<string, string> acceptedByOriginal)
    {
        if (whitelistSet.Contains(candidateName))
        {
            return candidateName;
        }

        return acceptedByOriginal.TryGetValue(candidateName, out string? canonicalName)
            ? canonicalName
            : null;
    }

    private static string BuildArtifactText(
        CreatorStyleRequest request,
        CreatorStyleProfile profile,
        RubricScoreResult rubricScores,
        IReadOnlyList<CreatorStyleExemplarDeck> exemplars,
        IReadOnlyList<string> validatedWhitelist,
        IReadOnlyList<string> validatedComboCards,
        bool groundingDegraded,
        string? notice)
    {
        var sb = new StringBuilder();
        string sanitizedCreatorSlug = SanitizeUserText(request.CreatorSlug, fallback: profile.Slug);

        if (groundingDegraded)
        {
            sb.Append("Grounding caveat: ");
            sb.AppendLine(string.IsNullOrWhiteSpace(notice) ? "Some referenced cards were withheld after validation." : notice);
            sb.AppendLine();
        }

        sb.AppendLine("Creator Targets");
        sb.Append("Requested Creator: ");
        sb.AppendLine(sanitizedCreatorSlug);
        foreach (FusedTarget target in profile.FusedTargets)
        {
            sb.Append("- Metric: ");
            sb.Append(target.Metric);
            sb.Append("; Value: ");
            sb.Append(FormatNumber(target.Value));
            sb.Append("; Weight: ");
            sb.Append(FormatNumber(target.Weight));

            if (target.StatedMin.HasValue)
            {
                sb.Append("; StatedMin: ");
                sb.Append(FormatNumber(target.StatedMin.Value));
            }

            if (target.StatedMax.HasValue)
            {
                sb.Append("; StatedMax: ");
                sb.Append(FormatNumber(target.StatedMax.Value));
            }

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Exemplar Decklists");
        if (exemplars.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            foreach (CreatorStyleExemplarDeck exemplar in exemplars)
            {
                sb.Append("- DeckId: ");
                sb.Append(exemplar.DeckId);
                sb.Append("; FolderName: ");
                sb.Append(string.IsNullOrWhiteSpace(exemplar.FolderName) ? "(none)" : exemplar.FolderName);
                sb.Append("; ConfidenceMarker: ");
                sb.Append(exemplar.ConfidenceMarker);
                sb.Append("; Cards: ");
                sb.AppendLine(exemplar.CardNames.Count == 0 ? "(none)" : string.Join(", ", exemplar.CardNames));
            }
        }

        sb.AppendLine();
        sb.AppendLine("Validated Synergy Context");
        sb.Append("- Validated Combo Cards: ");
        sb.AppendLine(validatedComboCards.Count == 0 ? "(none)" : string.Join(", ", validatedComboCards));
        sb.Append("- Validated Whitelist: ");
        sb.AppendLine(validatedWhitelist.Count == 0 ? "(none)" : string.Join(", ", validatedWhitelist));

        sb.AppendLine();
        sb.AppendLine("Rubric Scores");
        if (rubricScores.MetricScores.Count == 0)
        {
            sb.AppendLine("- None");
        }
        else
        {
            foreach (RubricMetricScore metricScore in rubricScores.MetricScores)
            {
                sb.Append("- Metric: ");
                sb.Append(metricScore.Metric);
                sb.Append("; Target: ");
                sb.Append(FormatNumber(metricScore.TargetValue));
                sb.Append("; Submitted: ");
                sb.Append(metricScore.SubmittedValue.HasValue ? FormatNumber(metricScore.SubmittedValue.Value) : "n/a");
                sb.Append("; Delta: ");
                sb.Append(metricScore.Delta.HasValue ? FormatNumber(metricScore.Delta.Value) : "n/a");
                sb.Append("; Weight: ");
                sb.Append(FormatNumber(metricScore.Weight));
                sb.Append("; Verdict: ");
                sb.Append(metricScore.Verdict);
                sb.Append("; Confidence: ");
                sb.AppendLine(string.IsNullOrWhiteSpace(metricScore.Confidence) ? "n/a" : metricScore.Confidence);
            }
        }

        sb.AppendLine();
        sb.AppendLine("Instruction");
        sb.AppendLine(CritiqueInstruction);

        return sb.ToString();
    }

    private static string FormatNumber(double value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static string SanitizeUserText(string? value, string fallback)
    {
        string candidate = string.IsNullOrWhiteSpace(value) ? fallback : value;
        string singleLine = CollapseWhitespace(candidate.Replace('\r', '\n')).Trim();
        if (singleLine.Length == 0)
        {
            return fallback;
        }

        return singleLine.Length <= MaxUserTextLength
            ? singleLine
            : singleLine[..MaxUserTextLength];
    }

    private static string CollapseWhitespace(string value)
        => string.Join(" ", value
            .Split(['\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private async Task<CreatorStyleProfile?> GetProfileAsync(string creatorSlug, CancellationToken cancellationToken)
    {
        if (_getProfileAsyncOverride is not null)
        {
            return await _getProfileAsyncOverride(creatorSlug, cancellationToken).ConfigureAwait(false);
        }

        return await _creatorStyleProfileStore!
            .GetBySlugAsync(creatorSlug, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SubmittedDeckAnalysis> BuildSubmittedDeckAsync(string deckSource, CancellationToken cancellationToken)
    {
        if (_buildSubmittedDeckAsyncOverride is not null)
        {
            return await _buildSubmittedDeckAsyncOverride(deckSource, cancellationToken).ConfigureAwait(false);
        }

        return await _submittedDeckStatsBuilder!
            .BuildAsync(deckSource, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CreatorWhitelistPoolBuildResult> BuildWhitelistAsync(
        string creatorSlug,
        CardGroundingDeckContext deckContext,
        CancellationToken cancellationToken)
    {
        if (_buildWhitelistAsyncOverride is not null)
        {
            return await _buildWhitelistAsyncOverride(creatorSlug, deckContext, cancellationToken).ConfigureAwait(false);
        }

        return await _creatorWhitelistPoolBuilder!
            .BuildWithDiagnosticsAsync(creatorSlug, deckContext, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CardGroundingBatchResult> ValidateAdditionalCardsAsync(
        IReadOnlyList<string> candidateNames,
        CardGroundingDeckContext deckContext,
        CancellationToken cancellationToken)
    {
        if (_validateAdditionalCardsAsyncOverride is not null)
        {
            return await _validateAdditionalCardsAsyncOverride(candidateNames, deckContext, cancellationToken).ConfigureAwait(false);
        }

        return await _cardGroundingGuard!
            .ValidateAllAsync(candidateNames, deckContext, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<CreatorDeckCacheEntry>> GetCreatorDecksAsync(string creatorSlug, CancellationToken cancellationToken)
    {
        if (_getCreatorDecksAsyncOverride is not null)
        {
            return await _getCreatorDecksAsyncOverride(creatorSlug, cancellationToken).ConfigureAwait(false);
        }

        return await _creatorDeckCacheStore!
            .GetByCreatorAsync(creatorSlug, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
    {
        if (_findCombosAsyncOverride is not null)
        {
            return await _findCombosAsyncOverride(entries, cancellationToken).ConfigureAwait(false);
        }

        return await _commanderSpellbookService!
            .FindCombosAsync(entries, cancellationToken)
            .ConfigureAwait(false);
    }
}
