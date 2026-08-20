using System.Net;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Shared builder for Cut Lab's resolved-card, classification, and role-assignment analysis context.</summary>
public interface ICutLabAnalysisContextBuilder
{
    /// <summary>Builds the structural-analysis context for the current working list.</summary>
    /// <param name="workingList">Current working-list cards.</param>
    /// <param name="playExperience">Cut Lab play-experience label used to resolve the shared role mode.</param>
    /// <param name="commanderNames">Resolved commander names for the current session.</param>
    /// <param name="preResolvedCards">Optional pre-resolved cards already loaded for this intake.</param>
    /// <param name="poolKey">Optional precomputed pool key for the working list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The analyzed cards, role assignments, and classification inputs for this working list.</returns>
    Task<CutLabAnalysisContext> BuildAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
        string? poolKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>Attempts to retrieve cached resolved cards for the provided pool.</summary>
    bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards);

    /// <summary>Resolves the current pool into reusable Scryfall card payloads.</summary>
    Task<IReadOnlyList<ScryfallCardData>> ResolvePoolCardsAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
        string? poolKey = null,
        bool failOpenOnLookupErrors = true,
        CancellationToken cancellationToken = default);

    /// <summary>Returns whether this builder left any names unattempted after a transient lookup failure.</summary>
    bool HasUnattemptedCards(IReadOnlyList<CutLabPoolCard> workingList) => false;

    /// <summary>Seeds the resolved-card cache for the provided working pool.</summary>
    void PrimeResolvedCardsCache(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<ScryfallCardData> resolvedCards,
        IReadOnlyCollection<string>? unresolvedCardNames = null);

    /// <summary>Seeds a derived pool while preserving transient lookup provenance from its source pool.</summary>
    void PrimeResolvedCardsCache(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<ScryfallCardData> resolvedCards,
        IReadOnlyCollection<string>? unresolvedCardNames,
        IReadOnlyList<CutLabPoolCard> unattemptedSourceWorkingList)
        => PrimeResolvedCardsCache(workingList, resolvedCards, unresolvedCardNames);

    /// <summary>Seeds the provided pool from a previously resolved superset payload when possible.</summary>
    bool TrySeedDerivedPool(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<ScryfallCardData> sourceCards,
        out IReadOnlyList<ScryfallCardData>? seededCards);
}

/// <summary>Shared Cut Lab analysis context used by intake and decision flows.</summary>
/// <param name="AnalyzedCards">Analyzed pool cards with resolved roles and categories.</param>
/// <param name="RolesByCardName">Per-card roles keyed by card name.</param>
/// <param name="RoleCounts">Role counts weighted by card quantity.</param>
/// <param name="CommanderManaValue">Maximum mana value among resolved commander cards.</param>
/// <param name="Mode">Resolved structural-analysis mode.</param>
/// <param name="Classification">Combo and category inputs for structural findings.</param>
/// <param name="ResolvedCards">Resolved card payload reused by downstream Cut Lab flows.</param>
public sealed record CutLabAnalysisContext(
    IReadOnlyList<CutLabAnalyzedCard> AnalyzedCards,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RolesByCardName,
    IReadOnlyDictionary<string, int> RoleCounts,
    double CommanderManaValue,
    ManabaseMode Mode,
    CutLabClassificationContext Classification,
    IReadOnlyList<ScryfallCardData> ResolvedCards);

/// <summary>Per-card combo membership from Commander Spellbook.</summary>
/// <param name="CompleteCombos">Resolved complete combos that contain the card.</param>
/// <param name="NearCombos">Resolved near-combos whose in-deck cards contain the card.</param>
public sealed record CutLabCardComboMembership(
    IReadOnlyList<SpellbookCombo> CompleteCombos,
    IReadOnlyList<SpellbookAlmostCombo> NearCombos);

/// <summary>Classification inputs reused by Cut Lab structural findings.</summary>
/// <param name="AlmostIncludedCombos">Near-combo findings from Commander Spellbook.</param>
/// <param name="ComboDataAvailable">Whether combo lookup completed successfully.</param>
/// <param name="CategoryDataAvailable">Whether category lookup completed successfully.</param>
/// <param name="CategoriesByName">Category tags keyed by card name.</param>
/// <param name="CardComboMembership">Per-card combo membership keyed by normalized card name.</param>
public sealed record CutLabClassificationContext(
    IReadOnlyList<SpellbookAlmostCombo> AlmostIncludedCombos,
    bool ComboDataAvailable,
    bool CategoryDataAvailable,
    IReadOnlyDictionary<string, IReadOnlyList<string>> CategoriesByName,
    IReadOnlyDictionary<string, CutLabCardComboMembership> CardComboMembership)
{
    /// <summary>Compatibility overload for callers that still provide name-only combo membership.</summary>
    /// <param name="almostIncludedCombos">Near-combo findings from Commander Spellbook.</param>
    /// <param name="comboDataAvailable">Whether combo lookup completed successfully.</param>
    /// <param name="categoryDataAvailable">Whether category lookup completed successfully.</param>
    /// <param name="categoriesByName">Category tags keyed by card name.</param>
    /// <param name="comboNames">Card names present in resolved included combos.</param>
    public CutLabClassificationContext(
        IReadOnlyList<SpellbookAlmostCombo> almostIncludedCombos,
        bool comboDataAvailable,
        bool categoryDataAvailable,
        IReadOnlyDictionary<string, IReadOnlyList<string>> categoriesByName,
        IReadOnlySet<string> comboNames)
        : this(
            almostIncludedCombos,
            comboDataAvailable,
            categoryDataAvailable,
            categoriesByName,
            BuildCompatibilityMembership(comboNames))
    {
    }

    private static IReadOnlyDictionary<string, CutLabCardComboMembership> BuildCompatibilityMembership(IReadOnlySet<string> comboNames)
    {
        ArgumentNullException.ThrowIfNull(comboNames);

        Dictionary<string, CutLabCardComboMembership> membership = new(CutLabCardNames.Comparer);
        foreach (string cardName in comboNames)
        {
            membership[CutLabCardNames.Normalize(cardName)] = new CutLabCardComboMembership([], []);
        }

        return membership;
    }
}

/// <summary>Default shared builder for Cut Lab analysis context.</summary>
internal sealed class CutLabAnalysisContextBuilder : ICutLabAnalysisContextBuilder
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyCategories =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    private readonly IScryfallCardResolver _cardResolver;
    private readonly CutLabResolvedCardCache _resolvedCardCache;
    private readonly ScryfallReferenceResolver _scryfallReferenceResolver;
    private readonly ICommanderSpellbookService? _spellbook;
    private readonly ICategoryKnowledgeStore? _categoryKnowledge;
    private readonly ILogger<CutLabAnalysisContextBuilder> _logger;

    // Why (B-1, round-1 review): names a swallowed transient failure left unattempted for a given
    // pool key, tracked only for the lifetime of this instance. Safe as instance state because
    // ICutLabAnalysisContextBuilder is registered AddScoped (CutLabServiceCollectionExtensions.cs),
    // so its lifetime is exactly one HTTP request; it exists solely so PrimeResolvedCardsCache can
    // avoid recording a transient casualty as a permanent "Scryfall says missing" entry within that
    // same request.
    private readonly Dictionary<string, HashSet<string>> _unattemptedNamesByPoolKey = new(StringComparer.Ordinal);

    /// <summary>Creates a new <see cref="CutLabAnalysisContextBuilder"/>.</summary>
    /// <param name="cardResolver">Shared Scryfall resolver pipeline.</param>
    /// <param name="resolvedCardCache">Resolved-card cache keyed by working-pool hash.</param>
    /// <param name="scryfallReferenceResolver">Shared Scryfall reference-resolution collaborator.</param>
    /// <param name="spellbook">Optional Commander Spellbook lookup dependency.</param>
    /// <param name="categoryKnowledge">Optional category lookup dependency.</param>
    /// <param name="logger">Structured logger.</param>
    public CutLabAnalysisContextBuilder(
        IScryfallCardResolver cardResolver,
        CutLabResolvedCardCache resolvedCardCache,
        ScryfallReferenceResolver scryfallReferenceResolver,
        ICommanderSpellbookService? spellbook = null,
        ICategoryKnowledgeStore? categoryKnowledge = null,
        ILogger<CutLabAnalysisContextBuilder>? logger = null)
    {
        _cardResolver = cardResolver ?? throw new ArgumentNullException(nameof(cardResolver));
        _resolvedCardCache = resolvedCardCache ?? throw new ArgumentNullException(nameof(resolvedCardCache));
        _scryfallReferenceResolver = scryfallReferenceResolver ?? throw new ArgumentNullException(nameof(scryfallReferenceResolver));
        _spellbook = spellbook;
        _categoryKnowledge = categoryKnowledge;
        _logger = logger ?? NullLogger<CutLabAnalysisContextBuilder>.Instance;
    }

    /// <inheritdoc />
    public async Task<CutLabAnalysisContext> BuildAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
        string? poolKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workingList);
        ArgumentNullException.ThrowIfNull(playExperience);
        ArgumentNullException.ThrowIfNull(commanderNames);

        string resolvedPoolKey = poolKey ?? CutLabResolvedCardCache.ComputePoolKey(workingList);
        Task<IReadOnlyList<ScryfallCardData>> resolvedCardsTask = ResolveCardsAsync(
            workingList,
            resolvedPoolKey,
            preResolvedCards,
            failOpenOnLookupErrors: true,
            cancellationToken);
        Task<CutLabClassificationContext> classificationTask = LoadClassificationContextAsync(workingList, commanderNames, cancellationToken);
        await Task.WhenAll(resolvedCardsTask, classificationTask).ConfigureAwait(false);
        IReadOnlyList<ScryfallCardData> resolvedCards = await resolvedCardsTask.ConfigureAwait(false);
        CutLabClassificationContext classification = await classificationTask.ConfigureAwait(false);

        HashSet<string> commanderNameSet = commanderNames
            .Select(CutLabCardNames.Normalize)
            .ToHashSet(CutLabCardNames.Comparer);
        IReadOnlyDictionary<string, ScryfallCardData> cardsByName = CutLabCardNames.ToLastWinsDictionary(
            resolvedCards,
            card => card.Name,
            card => card);
        ManabaseMode mode = CutLabRoleAssigner.ResolveMode(playExperience);
        Dictionary<string, IReadOnlyList<string>> rolesByCardName = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> roleCounts = new(StringComparer.OrdinalIgnoreCase);
        List<CutLabAnalyzedCard> analyzedCards = new(workingList.Count);
        double commanderManaValue = 0;

        foreach (CutLabPoolCard entry in workingList)
        {
            string normalizedEntryName = CutLabCardNames.Normalize(entry.Name);
            // Why: Resolve uses flagged pool cards when present and falls back to state.Commander, so
            // neither representation alone is sufficient; this covers flagged-but-unnamed and named-but-unflagged cards.
            bool isCommander = entry.IsCommander || commanderNameSet.Contains(normalizedEntryName);
            IReadOnlyList<string> categories = classification.CategoriesByName.TryGetValue(normalizedEntryName, out IReadOnlyList<string>? hit)
                ? hit
                : Array.Empty<string>();
            IReadOnlyList<string> roles = [];
            double manaValue = 0;

            if (cardsByName.TryGetValue(normalizedEntryName, out ScryfallCardData? card))
            {
                CardFact fact = ScryfallCardFactMapper.ToCardFact(card, entry.Quantity, isCommander);
                roles = CutLabRoleAssigner.AssignRoles(
                    fact,
                    categories,
                    classification.CardComboMembership.ContainsKey(normalizedEntryName),
                    mode);
                manaValue = fact.ManaValue;

                foreach (string role in roles)
                {
                    roleCounts[role] = roleCounts.TryGetValue(role, out int count)
                        ? count + entry.Quantity
                        : entry.Quantity;
                }

                if (isCommander)
                {
                    commanderManaValue = Math.Max(commanderManaValue, fact.ManaValue);
                }
            }

            rolesByCardName[entry.Name] = roles;
            analyzedCards.Add(new CutLabAnalyzedCard(
                entry.Name,
                manaValue,
                roles.Contains("lands", StringComparer.Ordinal),
                roles,
                categories)
            {
                Quantity = entry.Quantity,
                TypeLine = entry.TypeLine,
                IsLocked = entry.IsLocked,
                IsCommander = isCommander,
            });
        }

        return new CutLabAnalysisContext(
            analyzedCards,
            rolesByCardName,
            roleCounts,
            commanderManaValue,
            mode,
            classification,
            resolvedCards);
    }

    /// <inheritdoc />
    public bool TryGetCachedResolvedCards(IReadOnlyList<CutLabPoolCard> workingList, out IReadOnlyList<ScryfallCardData>? cards)
    {
        ArgumentNullException.ThrowIfNull(workingList);

        return _resolvedCardCache.TryGet(CutLabResolvedCardCache.ComputePoolKey(workingList), out cards);
    }

    /// <inheritdoc />
    public bool HasUnattemptedCards(IReadOnlyList<CutLabPoolCard> workingList)
    {
        ArgumentNullException.ThrowIfNull(workingList);

        return _unattemptedNamesByPoolKey.TryGetValue(
            CutLabResolvedCardCache.ComputePoolKey(workingList),
            out HashSet<string>? unattemptedNames)
            && unattemptedNames.Count > 0;
    }

    /// <inheritdoc />
    public bool TrySeedDerivedPool(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<ScryfallCardData> sourceCards,
        out IReadOnlyList<ScryfallCardData>? seededCards)
    {
        ArgumentNullException.ThrowIfNull(workingList);
        ArgumentNullException.ThrowIfNull(sourceCards);

        IReadOnlyDictionary<string, ScryfallCardData> sourceByName = CutLabCardNames.ToLastWinsDictionary(
            sourceCards,
            card => card.Name,
            card => card);
        List<ScryfallCardData> filteredCards = new(workingList.Count);
        HashSet<string> seen = new(CutLabCardNames.Comparer);

        foreach (CutLabPoolCard poolCard in workingList)
        {
            string normalizedName = CutLabCardNames.Normalize(poolCard.Name);
            if (!seen.Add(normalizedName)
                || !sourceByName.TryGetValue(normalizedName, out ScryfallCardData? card))
            {
                continue;
            }

            filteredCards.Add(card);
        }

        seededCards = AugmentResolvedCardsWithSyntheticBasics(workingList, filteredCards);

        // `seen` already holds every distinct normalized working-list name (its Add runs for
        // each card as the first operand of the || above), so reuse its count instead of a
        // second normalize+distinct pass over the whole working list.
        return seededCards.Count == seen.Count;
    }

    /// <inheritdoc />
    /// <remarks>
    /// B-1 (round-1 review): <c>CutLabPageService.ProcessAsync</c> passes every <c>Card is null</c>
    /// name as unresolved, which for a swallowed-429 casualty would mean "Scryfall says this card
    /// does not exist" -- a claim nothing ever verified. Any name this instance's own
    /// <see cref="ResolvePoolCardsAsync"/> left unattempted for this exact pool key is subtracted
    /// from the caller-supplied list before it reaches the cache.
    /// </remarks>
    public void PrimeResolvedCardsCache(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<ScryfallCardData> resolvedCards,
        IReadOnlyCollection<string>? unresolvedCardNames = null)
        => PrimeResolvedCardsCache(workingList, resolvedCards, unresolvedCardNames, workingList);

    /// <inheritdoc />
    public void PrimeResolvedCardsCache(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<ScryfallCardData> resolvedCards,
        IReadOnlyCollection<string>? unresolvedCardNames,
        IReadOnlyList<CutLabPoolCard> unattemptedSourceWorkingList)
    {
        ArgumentNullException.ThrowIfNull(workingList);
        ArgumentNullException.ThrowIfNull(resolvedCards);
        ArgumentNullException.ThrowIfNull(unattemptedSourceWorkingList);

        string poolKey = CutLabResolvedCardCache.ComputePoolKey(workingList);
        string unattemptedSourcePoolKey = CutLabResolvedCardCache.ComputePoolKey(unattemptedSourceWorkingList);
        IReadOnlyCollection<string>? effectiveUnresolvedNames = unresolvedCardNames;
        if (unresolvedCardNames is not null
            && _unattemptedNamesByPoolKey.TryGetValue(unattemptedSourcePoolKey, out HashSet<string>? unattemptedNames)
            && unattemptedNames.Count > 0)
        {
            effectiveUnresolvedNames = unresolvedCardNames
                .Where(name => !unattemptedNames.Contains(CutLabCardNames.Normalize(name)))
                .ToArray();
        }

        _resolvedCardCache.Set(poolKey, resolvedCards, effectiveUnresolvedNames);
    }

    internal static IReadOnlyList<ScryfallCardData> AugmentResolvedCardsWithSyntheticBasics(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<ScryfallCardData> resolvedCards)
    {
        ArgumentNullException.ThrowIfNull(workingList);
        ArgumentNullException.ThrowIfNull(resolvedCards);

        HashSet<string> resolvedNames = resolvedCards
            .Select(card => CutLabCardNames.Normalize(card.Name))
            .ToHashSet(CutLabCardNames.Comparer);
        List<ScryfallCardData>? augmented = null;

        foreach (CutLabPoolCard poolCard in workingList)
        {
            if (!CutLabBasicLands.Contains(poolCard.Name))
            {
                continue;
            }

            string normalizedName = CutLabCardNames.Normalize(poolCard.Name);
            if (!resolvedNames.Add(normalizedName))
            {
                continue;
            }

            augmented ??= new List<ScryfallCardData>(resolvedCards);
            augmented.Add(CutLabBasicLands.SyntheticCardData(poolCard.Name));
        }

        return augmented ?? resolvedCards;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScryfallCardData>> ResolvePoolCardsAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<ScryfallCardData>? preResolvedCards = null,
        string? poolKey = null,
        bool failOpenOnLookupErrors = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workingList);

        return ResolveCardsAsync(
            workingList,
            poolKey ?? CutLabResolvedCardCache.ComputePoolKey(workingList),
            preResolvedCards,
            failOpenOnLookupErrors,
            cancellationToken);
    }

    private async Task<IReadOnlyList<ScryfallCardData>> ResolveCardsAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        string poolKey,
        IReadOnlyList<ScryfallCardData>? preResolvedCards,
        bool failOpenOnLookupErrors,
        CancellationToken cancellationToken)
    {
        if (preResolvedCards is not null)
        {
            IReadOnlyDictionary<string, ScryfallCardData> preResolvedByName = CutLabCardNames.ToLastWinsDictionary(
                preResolvedCards,
                card => card.Name,
                card => card);
            IReadOnlyList<ScryfallCardData> preResolvedForPool = AugmentResolvedCardsWithSyntheticBasics(
                workingList,
                BuildOrderedResolvedCards(workingList, preResolvedByName));
            _resolvedCardCache.Set(poolKey, preResolvedForPool);
        }

        Dictionary<string, ScryfallCardData> resolvedByName = new(CutLabCardNames.Comparer);
        IReadOnlySet<string> knownMissingNames = new HashSet<string>(CutLabCardNames.Comparer);
        if (_resolvedCardCache.TryGet(poolKey, out IReadOnlyList<ScryfallCardData>? cachedCards) && cachedCards is not null)
        {
            foreach (ScryfallCardData cachedCard in cachedCards)
            {
                resolvedByName[CutLabCardNames.Normalize(cachedCard.Name)] = cachedCard;
            }

            if (_resolvedCardCache.TryGetKnownMissingNames(poolKey, out IReadOnlySet<string>? cachedMissingNames)
                && cachedMissingNames is not null)
            {
                knownMissingNames = cachedMissingNames;
            }
        }

        foreach (ScryfallCardData resolvedCard in AugmentResolvedCardsWithSyntheticBasics(workingList, resolvedByName.Values.ToArray()))
        {
            resolvedByName[CutLabCardNames.Normalize(resolvedCard.Name)] = resolvedCard;
        }

        List<CutLabPoolCard> missingPoolCards = EnumerateMissingPoolCards(workingList, resolvedByName, knownMissingNames);
        HashSet<string> knownMissingNamesSet = knownMissingNames.ToHashSet(CutLabCardNames.Comparer);

        // Why (UAT finding 2, 2026-08-19): chunking here pinned the POST count at THIS loop's chunk
        // count, so the partition-BEFORE-chunking fix inside ResolveBatchAsync could never collapse
        // POSTs across these chunks -- each kept a cold member and each still POSTed. The resolver
        // now plans the groups: warm names keep their original chunk boundaries (ADR-0004 ambiguity
        // pool unchanged) and the cold remainder is pooled and re-chunked, which is the saving. The
        // loop itself stays, because it is also this method's failure isolation -- one throwing
        // group must not discard the groups that already resolved.
        foreach (IReadOnlyList<string> requestNames in _scryfallReferenceResolver.PlanBatchResolveGroups(
                     missingPoolCards.Select(poolCard => poolCard.Name).ToList()))
        {
            try
            {
                ScryfallBatchResolution batchResolution = await _scryfallReferenceResolver.ResolveBatchAsync(
                    requestNames,
                    // Why: the batch call above already failed this identifier on cards/collection
                    // moments earlier, so re-POSTing via ResolveSingleAsync is dead weight -- the
                    // live probe in 111.1-REVIEWS.md §0 proves the removed POST rescues nothing that
                    // SearchFallbackCardAsync does not also resolve. ResolveSingleAsync stays
                    // untouched: CutLabSimulationService and ManabaseAnalysisService call it as
                    // their ONLY collection attempt, where the POST is not redundant.
                    (name, ct) => _cardResolver.SearchFallbackCardAsync(name, ct),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                HashSet<string> resolvedRequestNames = batchResolution.Resolutions
                    .Select(resolution => CutLabCardNames.Normalize(resolution.RequestName))
                    .ToHashSet(CutLabCardNames.Comparer);

                foreach (ScryfallReferenceResolution resolution in batchResolution.Resolutions)
                {
                    ScryfallCardData cardData = ScryfallCardDataMapper.ToCardData(resolution.Card);
                    resolvedByName[CutLabCardNames.Normalize(cardData.Name)] = cardData;
                }

                foreach (string unresolvedName in requestNames.Where(name => !resolvedRequestNames.Contains(CutLabCardNames.Normalize(name))))
                {
                    knownMissingNamesSet.Add(CutLabCardNames.Normalize(unresolvedName));
                    _logger.LogWarning("Cut Lab analysis context could not resolve {CardName}; continuing without card facts.", unresolvedName);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (!failOpenOnLookupErrors && !IsTransientRateLimit(exception))
                {
                    throw;
                }

                foreach (string requestName in requestNames)
                {
                    _logger.LogWarning(exception, "Cut Lab analysis context failed resolving {CardName}; continuing fail-open.", requestName);
                }

                // Why (round-1 review W-8, corrected by blind verification of wave 1): this break
                // fires for EVERY swallowed exception reaching this catch, not only a 429. When
                // failOpenOnLookupErrors is false, only a transient rate limit (IsTransientRateLimit)
                // survives the throw guard above, so the upstream really is actively rate-limiting.
                // But under failOpenOnLookupErrors: true (the BuildAsync default), a 503 or a timeout
                // in an early chunk also lands here -- continuing to POST further chunks into a
                // failing upstream is bounded but counterproductive either way. The remaining chunks
                // are, by construction, also unattempted -- the post-loop diff below picks them up.
                break;
            }
        }

        // B-1 (round-1 review): compute unattempted names as "requested but landed in neither
        // resolvedByName nor knownMissingNamesSet" rather than tracking them incrementally inside
        // the loop above -- this single diff also covers every chunk skipped by the break, with no
        // separate draining step needed.
        HashSet<string> unattemptedNames = EnumerateMissingPoolCards(missingPoolCards, resolvedByName, knownMissingNamesSet)
            .Select(poolCard => CutLabCardNames.Normalize(poolCard.Name))
            .ToHashSet(CutLabCardNames.Comparer);
        if (unattemptedNames.Count > 0)
        {
            _unattemptedNamesByPoolKey[poolKey] = unattemptedNames;
            _logger.LogWarning(
                "Cut Lab analysis context left {Count} card(s) unattempted after a transient failure; they will be re-attempted on the next resolve of this pool.",
                unattemptedNames.Count);
        }
        else
        {
            _unattemptedNamesByPoolKey.Remove(poolKey);
        }

        IReadOnlyList<ScryfallCardData> resolvedCards = AugmentResolvedCardsWithSyntheticBasics(
            workingList,
            BuildOrderedResolvedCards(workingList, resolvedByName));
        // Why (B-1): this write stays unconditional. Unattempted names are in neither resolvedByName
        // nor knownMissingNamesSet, so a later ResolveCardsAsync for the same key re-enumerates them
        // via EnumerateMissingPoolCards and re-attempts them, while every successfully-resolved card
        // stays cached. Skipping the write on a partial pool would instead force a full re-resolve of
        // the whole pool on retry -- more Scryfall traffic, not less.
        _resolvedCardCache.Set(poolKey, resolvedCards, knownMissingNamesSet);
        return resolvedCards;
    }

    /// <summary>
    /// A 429 is transient: <c>ScryfallThrottle</c> (via <c>ScryfallCardResolver</c>) has
    /// already exhausted its own Retry-After budget by the time this is reached, so the correct
    /// intake behavior is to import the pool with that card's facts missing rather than fail the
    /// whole import. This deliberately also covers <c>ScryfallReferenceCollectionException</c>
    /// (which derives from <see cref="HttpRequestException"/> and preserves the upstream status), so
    /// a 429 on the batch <c>cards/collection</c> call is treated identically to a 429 on a per-card
    /// fallback.
    /// </summary>
    private static bool IsTransientRateLimit(Exception exception)
        => exception is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests };

    private static List<CutLabPoolCard> EnumerateMissingPoolCards(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyDictionary<string, ScryfallCardData> resolvedByName,
        IReadOnlySet<string> knownMissingNames)
    {
        List<CutLabPoolCard> missing = new(workingList.Count);
        HashSet<string> seen = new(CutLabCardNames.Comparer);

        foreach (CutLabPoolCard poolCard in workingList)
        {
            string normalizedName = CutLabCardNames.Normalize(poolCard.Name);
            if (!seen.Add(normalizedName)
                || resolvedByName.ContainsKey(normalizedName)
                || knownMissingNames.Contains(normalizedName))
            {
                continue;
            }

            missing.Add(poolCard);
        }

        return missing;
    }

    private static List<ScryfallCardData> BuildOrderedResolvedCards(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyDictionary<string, ScryfallCardData> resolvedByName)
    {
        List<ScryfallCardData> orderedCards = new(workingList.Count);
        HashSet<string> seen = new(CutLabCardNames.Comparer);

        foreach (CutLabPoolCard poolCard in workingList)
        {
            string normalizedName = CutLabCardNames.Normalize(poolCard.Name);
            if (!seen.Add(normalizedName)
                || !resolvedByName.TryGetValue(normalizedName, out ScryfallCardData? resolvedCard))
            {
                continue;
            }

            orderedCards.Add(resolvedCard);
        }

        return orderedCards;
    }

    private static IReadOnlyList<(string Name, int Quantity)> ToPoolKeyEntries(IReadOnlyList<CutLabPoolCard> workingList)
        => workingList.Select(card => (card.Name, card.Quantity)).ToArray();

    private async Task<CutLabClassificationContext> LoadClassificationContextAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken)
    {
        Task<SpellbookLookupResult> spellbookTask = LoadSpellbookFailOpenAsync(workingList, commanderNames, cancellationToken);
        Task<CategoryLookupResult> categoriesTask = GetCategoriesFailOpenAsync(
            workingList
                .Select(card => card.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cancellationToken);
        await Task.WhenAll(spellbookTask, categoriesTask).ConfigureAwait(false);
        SpellbookLookupResult spellbook = await spellbookTask.ConfigureAwait(false);
        CategoryLookupResult categories = await categoriesTask.ConfigureAwait(false);

        return new CutLabClassificationContext(
            spellbook.AlmostIncludedCombos,
            spellbook.ComboDataAvailable,
            categories.CategoryDataAvailable,
            CutLabCardNames.ToLastWinsDictionary(
                categories.CategoriesByName,
                pair => pair.Key,
                pair => pair.Value),
            spellbook.CardComboMembership);
    }

    private async Task<SpellbookLookupResult> LoadSpellbookFailOpenAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken)
    {
        Dictionary<string, (List<SpellbookCombo> CompleteCombos, List<SpellbookAlmostCombo> NearCombos)> comboMembershipBuilder =
            new(CutLabCardNames.Comparer);
        IReadOnlyList<SpellbookAlmostCombo> almostIncludedCombos = [];
        bool comboDataAvailable = false;

        if (_spellbook is null)
        {
            return new SpellbookLookupResult(
                almostIncludedCombos,
                comboDataAvailable,
                new Dictionary<string, CutLabCardComboMembership>(CutLabCardNames.Comparer));
        }

        try
        {
            CommanderSpellbookResult? combos = await _spellbook.FindCombosAsync(
                BuildSpellbookEntries(workingList, commanderNames),
                cancellationToken).ConfigureAwait(false);
            comboDataAvailable = combos is not null;
            if (combos is not null)
            {
                almostIncludedCombos = combos.AlmostIncludedCombos;
                foreach (SpellbookCombo combo in combos.IncludedCombos)
                {
                    foreach (string cardName in combo.CardNames)
                    {
                        string normalizedCardName = CutLabCardNames.Normalize(cardName);
                        if (!comboMembershipBuilder.TryGetValue(normalizedCardName, out (List<SpellbookCombo> CompleteCombos, List<SpellbookAlmostCombo> NearCombos) membership))
                        {
                            membership = ([], []);
                            comboMembershipBuilder[normalizedCardName] = membership;
                        }

                        membership.CompleteCombos.Add(combo);
                    }
                }

                foreach (SpellbookAlmostCombo almostCombo in combos.AlmostIncludedCombos)
                {
                    foreach (string cardName in almostCombo.CardsInDeck)
                    {
                        string normalizedCardName = CutLabCardNames.Normalize(cardName);
                        if (!comboMembershipBuilder.TryGetValue(normalizedCardName, out (List<SpellbookCombo> CompleteCombos, List<SpellbookAlmostCombo> NearCombos) membership))
                        {
                            membership = ([], []);
                            comboMembershipBuilder[normalizedCardName] = membership;
                        }

                        membership.NearCombos.Add(almostCombo);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cut Lab: Commander Spellbook fetch failed; continuing without combo roles.");
        }

        Dictionary<string, CutLabCardComboMembership> cardComboMembership = new(CutLabCardNames.Comparer);
        foreach ((string normalizedCardName, (List<SpellbookCombo> CompleteCombos, List<SpellbookAlmostCombo> NearCombos) membership) in comboMembershipBuilder)
        {
            cardComboMembership[normalizedCardName] = new CutLabCardComboMembership(membership.CompleteCombos, membership.NearCombos);
        }

        return new SpellbookLookupResult(almostIncludedCombos, comboDataAvailable, cardComboMembership);
    }

    private async Task<CategoryLookupResult> GetCategoriesFailOpenAsync(
        IReadOnlyCollection<string> cardNames,
        CancellationToken cancellationToken)
    {
        if (_categoryKnowledge is null || cardNames.Count == 0)
        {
            return new CategoryLookupResult(EmptyCategories, false);
        }

        try
        {
            IReadOnlyDictionary<string, IReadOnlyList<string>> categories =
                await _categoryKnowledge.GetCategoriesForNamesAsync(cardNames, cancellationToken).ConfigureAwait(false);
            return new CategoryLookupResult(categories, true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cut Lab: batch category lookup failed; using heuristics only.");
            return new CategoryLookupResult(EmptyCategories, false);
        }
    }

    private static List<DeckEntry> BuildSpellbookEntries(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<string> commanderNames)
    {
        HashSet<string> commanderNameSet = commanderNames
            .Select(CutLabCardNames.Normalize)
            .ToHashSet(CutLabCardNames.Comparer);
        List<DeckEntry> entries = new(workingList.Count);

        foreach (CutLabPoolCard card in workingList)
        {
            entries.Add(new DeckEntry
            {
                Name = card.Name,
                NormalizedName = CutLabCardNames.Normalize(card.Name),
                Quantity = card.Quantity,
                Board = commanderNameSet.Contains(CutLabCardNames.Normalize(card.Name)) ? "commander" : "mainboard",
            });
        }

        return entries;
    }

    private sealed record CategoryLookupResult(
        IReadOnlyDictionary<string, IReadOnlyList<string>> CategoriesByName,
        bool CategoryDataAvailable);

    private sealed record SpellbookLookupResult(
        IReadOnlyList<SpellbookAlmostCombo> AlmostIncludedCombos,
        bool ComboDataAvailable,
        IReadOnlyDictionary<string, CutLabCardComboMembership> CardComboMembership);
}
