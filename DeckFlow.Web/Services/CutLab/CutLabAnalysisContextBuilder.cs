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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The analyzed cards, role assignments, and classification inputs for this working list.</returns>
    Task<CutLabAnalysisContext> BuildAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken = default);
}

/// <summary>Shared Cut Lab analysis context used by intake and decision flows.</summary>
/// <param name="AnalyzedCards">Analyzed pool cards with resolved roles and categories.</param>
/// <param name="RolesByCardName">Per-card roles keyed by card name.</param>
/// <param name="RoleCounts">Role counts weighted by card quantity.</param>
/// <param name="CommanderManaValue">Maximum mana value among resolved commander cards.</param>
/// <param name="Mode">Resolved structural-analysis mode.</param>
/// <param name="Classification">Combo and category inputs for structural findings.</param>
public sealed record CutLabAnalysisContext(
    IReadOnlyList<CutLabAnalyzedCard> AnalyzedCards,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RolesByCardName,
    IReadOnlyDictionary<string, int> RoleCounts,
    double CommanderManaValue,
    ManabaseMode Mode,
    CutLabClassificationContext Classification);

/// <summary>Classification inputs reused by Cut Lab structural findings.</summary>
/// <param name="AlmostIncludedCombos">Near-combo findings from Commander Spellbook.</param>
/// <param name="ComboDataAvailable">Whether combo lookup completed successfully.</param>
/// <param name="CategoryDataAvailable">Whether category lookup completed successfully.</param>
/// <param name="CategoriesByName">Category tags keyed by card name.</param>
/// <param name="ComboNames">Card names present in resolved included combos.</param>
public sealed record CutLabClassificationContext(
    IReadOnlyList<SpellbookAlmostCombo> AlmostIncludedCombos,
    bool ComboDataAvailable,
    bool CategoryDataAvailable,
    IReadOnlyDictionary<string, IReadOnlyList<string>> CategoriesByName,
    IReadOnlySet<string> ComboNames);

/// <summary>Default shared builder for Cut Lab analysis context.</summary>
public sealed class CutLabAnalysisContextBuilder : ICutLabAnalysisContextBuilder
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyCategories =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    private readonly IScryfallCardResolver _cardResolver;
    private readonly CutLabResolvedCardCache _resolvedCardCache;
    private readonly ICommanderSpellbookService? _spellbook;
    private readonly ICategoryKnowledgeStore? _categoryKnowledge;
    private readonly ILogger<CutLabAnalysisContextBuilder> _logger;

    /// <summary>Creates a new <see cref="CutLabAnalysisContextBuilder"/>.</summary>
    /// <param name="cardResolver">Shared Scryfall resolver pipeline.</param>
    /// <param name="resolvedCardCache">Resolved-card cache keyed by working-pool hash.</param>
    /// <param name="spellbook">Optional Commander Spellbook lookup dependency.</param>
    /// <param name="categoryKnowledge">Optional category lookup dependency.</param>
    /// <param name="logger">Structured logger.</param>
    public CutLabAnalysisContextBuilder(
        IScryfallCardResolver cardResolver,
        CutLabResolvedCardCache resolvedCardCache,
        ICommanderSpellbookService? spellbook = null,
        ICategoryKnowledgeStore? categoryKnowledge = null,
        ILogger<CutLabAnalysisContextBuilder>? logger = null)
    {
        _cardResolver = cardResolver ?? throw new ArgumentNullException(nameof(cardResolver));
        _resolvedCardCache = resolvedCardCache ?? throw new ArgumentNullException(nameof(resolvedCardCache));
        _spellbook = spellbook;
        _categoryKnowledge = categoryKnowledge;
        _logger = logger ?? NullLogger<CutLabAnalysisContextBuilder>.Instance;
    }

    /// <inheritdoc />
    public async Task<CutLabAnalysisContext> BuildAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workingList);
        ArgumentNullException.ThrowIfNull(playExperience);
        ArgumentNullException.ThrowIfNull(commanderNames);

        string poolKey = CutLabResolvedCardCache.ComputePoolKey(
            workingList.Select(card => (card.Name, card.Quantity)).ToArray());
        IReadOnlyList<ScryfallCardData> resolvedCards = await ResolveCardsAsync(workingList, poolKey, cancellationToken).ConfigureAwait(false);
        CutLabClassificationContext classification = await LoadClassificationContextAsync(
            workingList,
            commanderNames,
            cancellationToken).ConfigureAwait(false);

        HashSet<string> commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, ScryfallCardData> cardsByName = resolvedCards.ToDictionary(
            card => card.Name,
            StringComparer.OrdinalIgnoreCase);
        ManabaseMode mode = CutLabRoleAssigner.ResolveMode(playExperience);
        Dictionary<string, IReadOnlyList<string>> rolesByCardName = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> roleCounts = new(StringComparer.OrdinalIgnoreCase);
        List<CutLabAnalyzedCard> analyzedCards = new(workingList.Count);
        double commanderManaValue = 0;

        foreach (CutLabPoolCard entry in workingList)
        {
            IReadOnlyList<string> categories = classification.CategoriesByName.TryGetValue(entry.Name, out IReadOnlyList<string>? hit)
                ? hit
                : Array.Empty<string>();
            IReadOnlyList<string> roles = [];
            double manaValue = 0;

            if (cardsByName.TryGetValue(entry.Name, out ScryfallCardData? card))
            {
                CardFact fact = ScryfallCardFactMapper.ToCardFact(card, entry.Quantity, commanderNameSet.Contains(entry.Name));
                roles = CutLabRoleAssigner.AssignRoles(
                    fact,
                    categories,
                    classification.ComboNames.Contains(entry.Name),
                    mode);
                manaValue = fact.ManaValue;

                foreach (string role in roles)
                {
                    roleCounts[role] = roleCounts.TryGetValue(role, out int count)
                        ? count + entry.Quantity
                        : entry.Quantity;
                }

                if (commanderNameSet.Contains(entry.Name))
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
            });
        }

        return new CutLabAnalysisContext(
            analyzedCards,
            rolesByCardName,
            roleCounts,
            commanderManaValue,
            mode,
            classification);
    }

    private async Task<IReadOnlyList<ScryfallCardData>> ResolveCardsAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        string poolKey,
        CancellationToken cancellationToken)
    {
        if (_resolvedCardCache.TryGet(poolKey, out IReadOnlyList<ScryfallCardData>? cachedCards) && cachedCards is not null)
        {
            return cachedCards;
        }

        List<ScryfallCardData> resolvedCards = new(workingList.Count);
        foreach (CutLabPoolCard poolCard in workingList)
        {
            try
            {
                ScryfallCard? resolved = await _cardResolver.ResolveSingleAsync(poolCard.Name, cancellationToken).ConfigureAwait(false);
                if (resolved is null)
                {
                    _logger.LogWarning("Cut Lab analysis context could not resolve {CardName}; continuing without card facts.", poolCard.Name);
                    continue;
                }

                resolvedCards.Add(ScryfallCardDataMapper.ToCardData(resolved));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Cut Lab analysis context failed resolving {CardName}; continuing fail-open.", poolCard.Name);
            }
        }

        _resolvedCardCache.Set(poolKey, resolvedCards);
        return resolvedCards;
    }

    private async Task<CutLabClassificationContext> LoadClassificationContextAsync(
        IReadOnlyList<CutLabPoolCard> workingList,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken)
    {
        HashSet<string> comboNames = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<SpellbookAlmostCombo> almostIncludedCombos = [];
        bool comboDataAvailable = false;

        if (_spellbook is not null)
        {
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
                            comboNames.Add(cardName);
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
        }

        CategoryLookupResult categories = await GetCategoriesFailOpenAsync(
            workingList
                .Select(card => card.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            cancellationToken).ConfigureAwait(false);

        return new CutLabClassificationContext(
            almostIncludedCombos,
            comboDataAvailable,
            categories.CategoryDataAvailable,
            categories.CategoriesByName,
            comboNames);
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
        HashSet<string> commanderNameSet = commanderNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<DeckEntry> entries = new(workingList.Count);

        foreach (CutLabPoolCard card in workingList)
        {
            entries.Add(new DeckEntry
            {
                Name = card.Name,
                NormalizedName = card.Name.ToLowerInvariant(),
                Quantity = card.Quantity,
                Board = commanderNameSet.Contains(card.Name) ? "commander" : "mainboard",
            });
        }

        return entries;
    }

    private sealed record CategoryLookupResult(
        IReadOnlyDictionary<string, IReadOnlyList<string>> CategoriesByName,
        bool CategoryDataAvailable);
}
