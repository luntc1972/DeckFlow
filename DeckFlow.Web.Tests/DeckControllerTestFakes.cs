using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Tests;

internal sealed class FakeDeckSyncService : IDeckSyncService
{
    public Task<DeckSyncResult> CompareDecksAsync(DeckDiffRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

internal sealed class FakeDeckConvertService : IDeckConvertService
{
    public Task<DeckConvertResult> ConvertAsync(DeckConvertRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

/// <summary>
/// Placeholder stub for controller tests that do not exercise the deck analysis path;
/// throws <see cref="NotImplementedException"/> if called unexpectedly.
/// </summary>
internal sealed class StubDeckAnalysisPacketService : IDeckAnalysisPacketService
{
    public Task<DeckAnalysisPacketResult> BuildAsync(DeckAnalysisRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<string?> TryComputeCacheKeyAsync(DeckAnalysisRequest request, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

internal sealed class StubDeckPrimerPacketService : IDeckPrimerPacketService
{
    public Task<DeckPrimerPacketResult> BuildAsync(DeckPrimerRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<string?> TryComputeCacheKeyAsync(DeckPrimerRequest request, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

internal sealed class FakeDeckComparisonService : IDeckComparisonService
{
    public DeckComparisonResult Result { get; set; } = new(
        "comparison summary",
        "deck a list",
        "deck b list",
        "deck a combos",
        "deck b combos",
        "comparison context",
        "comparison prompt",
        "comparison follow-up prompt",
        "{}",
        new DeckComparisonResponse
        {
            DeckAName = "Deck A",
            DeckBName = "Deck B",
            DeckACommander = "Atraxa, Praetors' Voice",
            DeckBCommander = "Tymna the Weaver",
            DeckAGameplan = "Snowball permanents.",
            DeckBGameplan = "Interactive value.",
            DeckABracket = "Bracket 3: Upgraded",
            DeckBBracket = "Bracket 4: Optimized",
            ManaConsistencyComparison = "Deck B is smoother.",
            ComboComparison = "Deck A has the cleaner combo finish."
        },
        null);

    public Task<DeckComparisonResult> BuildAsync(DeckComparisonRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Result);

    public Task<string?> TryComputeCacheKeyAsync(DeckComparisonRequest request, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

/// <summary>
/// Test stub that returns a hardcoded <see cref="MetaGapResult"/> regardless of input.
/// Used to isolate controller tests from meta-gap service behavior.
/// </summary>
internal sealed class StubMetaGapService : IMetaGapService
{
    public Task<MetaGapResult> BuildAsync(MetaGapRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new MetaGapResult(
            "meta gap summary",
            "Tymna / Kraum",
            Array.Empty<EdhTop16Entry>(),
            "meta gap prompt",
            "{}",
            new MetaGapResponse
            {
                MetaGap = new MetaGapData
                {
                    Commander = "Tymna / Kraum",
                    RefDeckCount = 3,
                    MetaSummary = "Meta summary.",
                    OptimizationPath = "Optimization path."
                }
            }));

    public Task<string?> TryComputeCacheKeyAsync(MetaGapRequest request, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

/// <summary>
/// Stateful fake that returns the <see cref="MetaGapResult"/> supplied at construction,
/// allowing tests to configure the returned result per scenario.
/// </summary>
internal sealed class FakeMetaGapService : IMetaGapService
{
    private readonly MetaGapResult _result;

    public FakeMetaGapService(MetaGapResult result)
    {
        _result = result;
    }

    public Task<MetaGapResult> BuildAsync(MetaGapRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(_result);

    public Task<string?> TryComputeCacheKeyAsync(MetaGapRequest request, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

internal sealed class ThrowingMetaGapService : IMetaGapService
{
    private readonly Exception _exception;

    public ThrowingMetaGapService(Exception exception)
    {
        _exception = exception;
    }

    public Task<MetaGapResult> BuildAsync(MetaGapRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<MetaGapResult>(_exception);

    public Task<string?> TryComputeCacheKeyAsync(MetaGapRequest request, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

internal sealed class ThrowingDeckAnalysisPacketService : IDeckAnalysisPacketService
{
    private readonly Exception _exception;

    public ThrowingDeckAnalysisPacketService(Exception exception)
    {
        _exception = exception;
    }

    public Task<DeckAnalysisPacketResult> BuildAsync(DeckAnalysisRequest request, CancellationToken cancellationToken = default)
        => Task.FromException<DeckAnalysisPacketResult>(_exception);

    public Task<string?> TryComputeCacheKeyAsync(DeckAnalysisRequest request, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

/// <summary>
/// Stateful fake that captures the last <see cref="DeckAnalysisRequest"/> passed to
/// <see cref="IDeckAnalysisPacketService.BuildAsync"/> so the consuming test can assert call arguments.
/// </summary>
internal sealed class FakeDeckAnalysisPacketService : IDeckAnalysisPacketService
{
    public DeckAnalysisRequest? LastRequest { get; private set; }

    public DeckAnalysisPacketResult Result { get; set; } = new(
        "summary",
        "Test Deck | AI Deck Analysis",
        "{}",
        "reference",
        "analysis",
        "set-upgrade",
        null,
        null);

    public Task<DeckAnalysisPacketResult> BuildAsync(DeckAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(Result);
    }

    public Task<string?> TryComputeCacheKeyAsync(DeckAnalysisRequest request, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);
}

internal sealed class FakeScryfallSetService : IScryfallSetService
{
    public Task<IReadOnlyList<ScryfallSetOption>> GetSetsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ScryfallSetOption>>(Array.Empty<ScryfallSetOption>());

    public Task<string> BuildSetPacketAsync(IReadOnlyList<string> setCodes, IReadOnlyList<string>? commanderColorIdentity = null, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);
}

internal sealed class ThrowingCardSearchService : ICardSearchService
{
    private readonly Exception _exception;

    public ThrowingCardSearchService(Exception exception)
    {
        _exception = exception;
    }

    public Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default)
        => Task.FromException<IReadOnlyList<string>>(_exception);

    public Task<IReadOnlyList<string>> SearchCommandersAsync(string query, CancellationToken cancellationToken = default)
        => Task.FromException<IReadOnlyList<string>>(_exception);
}

internal sealed class StubCardSearchService : ICardSearchService
{
    private readonly IReadOnlyList<string> _commanderResults;

    public StubCardSearchService(params string[] commanderResults)
    {
        _commanderResults = commanderResults;
    }

    public string? LastCommanderQuery { get; private set; }

    public Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<IReadOnlyList<string>> SearchCommandersAsync(string query, CancellationToken cancellationToken = default)
    {
        LastCommanderQuery = query;
        return Task.FromResult(_commanderResults);
    }
}

internal sealed class FakeCardLookupService : ICardLookupService
{
    public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
        => Task.FromResult(new CardLookupResult(Array.Empty<string>(), Array.Empty<string>()));

    public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
        => Task.FromResult<SingleCardLookupResult?>(null);
}

internal sealed class ThrowingCardLookupService : ICardLookupService
{
    private readonly Exception _exception;

    public ThrowingCardLookupService(Exception exception)
    {
        _exception = exception;
    }

    public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
        => Task.FromException<CardLookupResult>(_exception);

    public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
        => Task.FromException<SingleCardLookupResult?>(_exception);
}

/// <summary>
/// Canned-response stub that returns a fixed successful <see cref="CardLookupResult"/>
/// with "Sol Ring"; used to test successful card lookup flows without hitting Scryfall.
/// </summary>
internal sealed class StubSuccessfulCardLookupService : ICardLookupService
{
    public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
        => Task.FromResult(new CardLookupResult(new[] { "Sol Ring" }, Array.Empty<string>()));

    public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
        => Task.FromResult<SingleCardLookupResult?>(new SingleCardLookupResult("Sol Ring", "Sol Ring", Array.Empty<string>()));
}

/// <summary>
/// Canned-response stub that returns a fixed successful single-card result for
/// "Monastery Swiftspear" with the Prowess mechanic; used to test single-card lookup flows.
/// </summary>
internal sealed class StubSuccessfulSingleCardLookupService : ICardLookupService
{
    public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
        => Task.FromResult(new CardLookupResult(Array.Empty<string>(), Array.Empty<string>()));

    public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
        => Task.FromResult<SingleCardLookupResult?>(new SingleCardLookupResult("Monastery Swiftspear", "Monastery Swiftspear", new[] { "Prowess" }));
}

internal sealed class AlternateNameSingleCardLookupService : ICardLookupService
{
    public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
        => Task.FromResult(new CardLookupResult(Array.Empty<string>(), Array.Empty<string>()));

    public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
        => Task.FromResult<SingleCardLookupResult?>(new SingleCardLookupResult("Ancient Greenwarden", "Ancient Greenwarden", new[] { "Landfall" }));
}

internal sealed class MultiMechanicSingleCardLookupService : ICardLookupService
{
    public Task<CardLookupResult> LookupAsync(string cardList, CancellationToken cancellationToken = default)
        => Task.FromResult(new CardLookupResult(Array.Empty<string>(), Array.Empty<string>()));

    public Task<SingleCardLookupResult?> LookupSingleAsync(string cardName, CancellationToken cancellationToken = default)
        => Task.FromResult<SingleCardLookupResult?>(new SingleCardLookupResult("Monastery Swiftspear", "Monastery Swiftspear", new[] { "Prowess", "Landfall" }));
}

internal sealed class FakeCategorySuggestionService : ICategorySuggestionService
{
    public Task<CategorySuggestionResult> SuggestAsync(CategorySuggestionRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

internal sealed class FakeMechanicLookupService : IMechanicLookupService
{
    public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
        => Task.FromResult(MechanicLookupResult.NotFound(mechanicName, "https://magic.wizards.com/en/rules", null));
}

/// <summary>
/// Canned-response stub that returns a fixed successful <see cref="MechanicLookupResult"/>
/// for Prowess; used to test mechanic lookup flows without invoking the rules service.
/// </summary>
internal sealed class StubSuccessfulMechanicLookupService : IMechanicLookupService
{
    public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
        => Task.FromResult(new MechanicLookupResult(
            mechanicName,
            true,
            "Prowess",
            "702.108",
            "Exact rules section",
            "702.108. Prowess",
            "A keyword ability that causes a creature to get +1/+1 whenever its controller casts a noncreature spell.",
            "https://magic.wizards.com/en/rules",
            "https://media.wizards.com/2026/downloads/MagicCompRules%2020260227.txt"));
}

internal sealed class PartiallyFailingMechanicLookupService : IMechanicLookupService
{
    public Task<MechanicLookupResult> LookupAsync(string mechanicName, CancellationToken cancellationToken = default)
        => mechanicName == "Landfall"
            ? Task.FromException<MechanicLookupResult>(new HttpRequestException("Rules source unavailable."))
            : Task.FromResult(new MechanicLookupResult(
                mechanicName,
                true,
                mechanicName,
                "702.108",
                "Exact rules section",
                $"{mechanicName} rules text",
                null,
                "https://magic.wizards.com/en/rules",
                "https://media.wizards.com/2026/downloads/MagicCompRules%2020260227.txt"));
}
