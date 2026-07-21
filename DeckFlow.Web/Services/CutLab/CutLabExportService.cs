using DeckFlow.Core.Exporting;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Builds builder-compatible Cut Lab export payloads from the current working session state.</summary>
public interface ICutLabExportService
{
    /// <summary>Builds the export text blocks and validation summary for the current Cut Lab state.</summary>
    Task<CutLabExportView> BuildExportAsync(
        CutLabState state,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken);
}

/// <summary>Default orchestrator for Cut Lab export composition.</summary>
public sealed class CutLabExportService : ICutLabExportService
{
    private readonly ICutLabAnalysisContextBuilder _contextBuilder;
    private readonly CutLabResolvedCardCache _resolvedCardCache;
    private readonly ICommanderBanListService _banListService;
    private readonly ILogger<CutLabExportService> _logger;

    /// <summary>Creates a new <see cref="CutLabExportService"/>.</summary>
    public CutLabExportService(
        ICutLabAnalysisContextBuilder contextBuilder,
        CutLabResolvedCardCache resolvedCardCache,
        ICommanderBanListService banListService,
        ILogger<CutLabExportService>? logger = null)
    {
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _resolvedCardCache = resolvedCardCache ?? throw new ArgumentNullException(nameof(resolvedCardCache));
        _banListService = banListService ?? throw new ArgumentNullException(nameof(banListService));
        _logger = logger ?? NullLogger<CutLabExportService>.Instance;
    }

    /// <inheritdoc />
    public async Task<CutLabExportView> BuildExportAsync(
        CutLabState state,
        string playExperience,
        IReadOnlyList<string> commanderNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(playExperience);
        ArgumentNullException.ThrowIfNull(commanderNames);

        IReadOnlyList<CutLabPoolCard> keptWorkingList = CutLabWorkingList.Derive(state.Pool, state.Decisions);
        IReadOnlyList<DeckEntry> finalEntries = ReconstructFinalEntries(keptWorkingList, state.OriginalEntries, out IReadOnlyList<string> reconstructionWarnings);
        IReadOnlyList<DeckEntry> originalEntries = ToDeckEntries(state.OriginalEntries);

        string poolKey = CutLabResolvedCardCache.ComputePoolKey(keptWorkingList);
        IReadOnlyList<ScryfallCardData>? preResolvedCards = TryGetPreResolvedCards(state.Pool, keptWorkingList, poolKey);
        CutLabAnalysisContext context = await _contextBuilder
            .BuildAsync(
                keptWorkingList,
                playExperience,
                commanderNames,
                preResolvedCards,
                preResolvedCards is null ? null : poolKey,
                cancellationToken)
            .ConfigureAwait(false);
        _resolvedCardCache.Set(poolKey, context.ResolvedCards);

        IReadOnlyDictionary<string, ScryfallCardData> resolvedByName = context.ResolvedCards
            .GroupBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IReadOnlyList<string>?> cardIdentitiesByName = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> unverifiedCardNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (DeckEntry entry in finalEntries)
        {
            if (resolvedByName.TryGetValue(entry.Name, out ScryfallCardData? resolvedCard))
            {
                cardIdentitiesByName[entry.Name] = resolvedCard.ColorIdentity;
            }
            else
            {
                unverifiedCardNames.Add(entry.Name);
            }
        }

        HashSet<string> commanderIdentity = new(StringComparer.OrdinalIgnoreCase);
        foreach (string commanderName in commanderNames)
        {
            if (!resolvedByName.TryGetValue(commanderName, out ScryfallCardData? commanderCard)
                || commanderCard.ColorIdentity is null)
            {
                continue;
            }

            foreach (string color in commanderCard.ColorIdentity)
            {
                commanderIdentity.Add(color);
            }
        }

        List<string> warnings = [];
        IReadOnlySet<string> bannedCardNamesPresent;
        try
        {
            IReadOnlyList<string> bannedCards = await _banListService.GetBannedCardsAsync(cancellationToken).ConfigureAwait(false);
            HashSet<string> bannedSet = bannedCards.ToHashSet(StringComparer.OrdinalIgnoreCase);
            bannedCardNamesPresent = finalEntries
                .Select(entry => entry.Name)
                .Where(bannedSet.Contains)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Cut Lab export: banlist fetch failed; continuing without legality check.");
            warnings.Add("Banned-card check unavailable right now - legality was not verified for this export.");
            bannedCardNamesPresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        CutLabExportResult result = CutLabExportComposer.Compose(
            finalEntries,
            originalEntries,
            commanderIdentity,
            cardIdentitiesByName,
            unverifiedCardNames,
            bannedCardNamesPresent);

        return new CutLabExportView
        {
            HasExport = true,
            MoxfieldFullListText = result.MoxfieldFullListText,
            ArchidektFullListText = result.ArchidektFullListText,
            MoxfieldPatchText = result.MoxfieldPatchText,
            ArchidektPatchText = result.ArchidektPatchText,
            CountOk = result.CountOk,
            OffCount = result.OffCount,
            HardBlock = result.HardBlock,
            IllegalColorIdentity = result.IllegalColorIdentity,
            UnverifiedColorIdentity = result.UnverifiedColorIdentity,
            BanlistOffenders = result.BanlistOffenders,
            ReconstructionWarnings = reconstructionWarnings,
            Warnings = warnings,
        };
    }

    private IReadOnlyList<ScryfallCardData>? TryGetPreResolvedCards(
        IReadOnlyList<CutLabPoolCard> fullPool,
        IReadOnlyList<CutLabPoolCard> keptWorkingList,
        string poolKey)
    {
        if (_resolvedCardCache.TryGet(poolKey, out IReadOnlyList<ScryfallCardData>? directHit))
        {
            return directHit;
        }

        if (_contextBuilder.TryGetCachedResolvedCards(keptWorkingList, out IReadOnlyList<ScryfallCardData>? cachedCards)
            && cachedCards is not null)
        {
            _resolvedCardCache.Set(poolKey, cachedCards);
            return cachedCards;
        }

        if (_contextBuilder.TryGetCachedResolvedCards(fullPool, out IReadOnlyList<ScryfallCardData>? fullPoolCards)
            && fullPoolCards is not null
            && _contextBuilder.TrySeedDerivedPool(keptWorkingList, fullPoolCards, out IReadOnlyList<ScryfallCardData>? seededCards)
            && seededCards is not null)
        {
            _resolvedCardCache.Set(poolKey, seededCards);
            return seededCards;
        }

        return null;
    }

    private static IReadOnlyList<DeckEntry> ReconstructFinalEntries(
        IReadOnlyList<CutLabPoolCard> keptWorkingList,
        IReadOnlyList<CutLabOriginalEntry> originalEntries,
        out IReadOnlyList<string> reconstructionWarnings)
    {
        List<string> warnings = [];
        List<DeckEntry> finalEntries = [];

        foreach (CutLabPoolCard keptCard in keptWorkingList)
        {
            string normalizedName = CardNormalizer.Normalize(keptCard.Name);
            List<CutLabOriginalEntry> matches = originalEntries
                .Where(entry => string.Equals(CardNormalizer.Normalize(entry.Name), normalizedName, StringComparison.Ordinal))
                .ToList();
            int remaining = keptCard.Quantity;

            foreach (CutLabOriginalEntry match in matches)
            {
                if (remaining <= 0)
                {
                    break;
                }

                int matchedQuantity = Math.Min(remaining, Math.Max(match.Quantity, 1));
                finalEntries.Add(new DeckEntry
                {
                    Name = keptCard.Name,
                    NormalizedName = normalizedName,
                    Quantity = matchedQuantity,
                    Board = keptCard.IsCommander ? "commander" : NormalizeBoard(match.Board),
                    SetCode = match.SetCode,
                    CollectorNumber = match.CollectorNumber,
                    Category = match.Category,
                });
                remaining -= matchedQuantity;
            }

            if (remaining <= 0)
            {
                continue;
            }

            finalEntries.Add(new DeckEntry
            {
                Name = keptCard.Name,
                NormalizedName = normalizedName,
                Quantity = remaining,
                Board = keptCard.IsCommander ? "commander" : "mainboard",
            });

            if (!keptCard.IsCommander)
            {
                warnings.Add($"Original export metadata was unavailable for {keptCard.Name}; exported it as mainboard.");
            }
        }

        reconstructionWarnings = warnings;
        return finalEntries;
    }

    private static IReadOnlyList<DeckEntry> ToDeckEntries(IReadOnlyList<CutLabOriginalEntry> originalEntries)
        => originalEntries
            .Select(entry => new DeckEntry
            {
                Name = entry.Name,
                NormalizedName = CardNormalizer.Normalize(entry.Name),
                Quantity = entry.Quantity,
                Board = NormalizeBoard(entry.Board),
                SetCode = entry.SetCode,
                CollectorNumber = entry.CollectorNumber,
                Category = entry.Category,
            })
            .ToArray();

    private static string NormalizeBoard(string? board)
        => string.IsNullOrWhiteSpace(board) ? "mainboard" : board.Trim();
}
