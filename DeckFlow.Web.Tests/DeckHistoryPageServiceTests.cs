using System.Net;
using DeckFlow.Core.History;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Evolution;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckHistoryPageServiceTests
{
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-07-17T12:00:00Z");

    [Fact]
    public async Task ProcessAsync_DeckOnly_CreatesNewFileAppendsSnapshotAndLeavesPromptEmpty()
    {
        var service = CreateService(FullDeckEntries());

        var result = await service.ProcessAsync(new DeckHistoryRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "Commander\n1 Atraxa, Praetors' Voice",
            DeckName = "Atraxa Midrange",
            Notes = "Initial import",
            Label = "v1",
            TargetAiPlatform = "ChatGPT",
        }, uploadedHistoryJson: null);

        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.File);
        Assert.True(result.Appended);
        Assert.Equal("Atraxa Midrange", result.File!.DeckName);
        Assert.Single(result.File.Versions);
        Assert.Equal(1, result.File.Versions[0].Id);
        Assert.Equal(FixedNow, result.File.Versions[0].Date);
        Assert.Equal("Initial import", result.File.Versions[0].Notes);
        Assert.Equal("v1", result.File.Versions[0].Label);
        Assert.True(string.IsNullOrWhiteSpace(result.PromptText));
        Assert.False(string.IsNullOrWhiteSpace(result.SerializedJson));
    }

    [Fact]
    public async Task ProcessAsync_HistoryOnly_RecomputesDeltasWithoutAppend()
    {
        var service = CreateService();
        var history = BuildHistoryJson(
            Version(1, "2026-07-01T00:00:00Z", ["Atraxa, Praetors' Voice"], [Card("Sol Ring", 1)]),
            Version(2, "2026-07-08T00:00:00Z", ["Atraxa, Praetors' Voice"], [Card("Sol Ring", 1), Card("Arcane Signet", 1)]));

        var result = await service.ProcessAsync(new DeckHistoryRequest
        {
            HistoryJson = history,
            TargetAiPlatform = "Claude",
        }, uploadedHistoryJson: null);

        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.File);
        Assert.False(result.Appended);
        Assert.NotNull(result.File!.Versions[1].Delta);
        Assert.Contains(result.File.Versions[1].Delta!.Adds, card => card.Name == "Arcane Signet");
        Assert.False(string.IsNullOrWhiteSpace(result.PromptText));
    }

    [Fact]
    public async Task ProcessAsync_HistoryAndDeckAtTwoVersions_BuildsPrompt()
    {
        var entries = FullDeckEntries();
        var service = CreateService(entries);
        var history = BuildHistoryJson(
            Version(1, "2026-07-01T00:00:00Z", ["Atraxa, Praetors' Voice"], [Card("Sol Ring", 1)]));

        var result = await service.ProcessAsync(new DeckHistoryRequest
        {
            HistoryJson = history,
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "updated deck",
            DeckName = "Atraxa Midrange",
            Notes = "Added more interaction",
            Label = "v2",
            TargetAiPlatform = "ChatGPT",
        }, uploadedHistoryJson: null);

        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.File);
        Assert.True(result.Appended);
        Assert.Equal(2, result.File!.Versions.Count);
        Assert.False(string.IsNullOrWhiteSpace(result.PromptText));
    }

    [Fact]
    public async Task ProcessAsync_HistoryAndIdenticalDeck_ReturnsWarningWithoutAppend()
    {
        var entries = FullDeckEntries();
        var service = CreateService(entries);
        var history = BuildHistoryJson(
            Version(1, "2026-07-01T00:00:00Z", ["Atraxa, Praetors' Voice"], [Card("Sol Ring", 1)]),
            Version(2, "2026-07-08T00:00:00Z", ["Atraxa, Praetors' Voice"], [Card("Arcane Signet", 1), Card("Plains", 97), Card("Sol Ring", 1)]));

        var result = await service.ProcessAsync(new DeckHistoryRequest
        {
            HistoryJson = history,
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "same deck",
            Notes = "No change",
            TargetAiPlatform = "ChatGPT",
        }, uploadedHistoryJson: null);

        Assert.Null(result.ErrorMessage);
        Assert.False(result.Appended);
        Assert.Contains(
            "The imported deck is identical to the latest version — no new snapshot was added.",
            result.Warnings);
        Assert.Equal(2, result.File!.Versions.Count);
    }

    [Fact]
    public async Task ProcessAsync_IdenticalNonHundredCardDeck_DropsSavedAnywaySuffix()
    {
        var entries = SixtyThreeCardDeckEntries();
        var service = CreateService(entries);
        var history = BuildHistoryJson(
            Version(
                1,
                "2026-07-01T00:00:00Z",
                ["Atraxa, Praetors' Voice"],
                [Card("Arcane Signet", 1), Card("Plains", 60), Card("Sol Ring", 1)]));

        var result = await service.ProcessAsync(new DeckHistoryRequest
        {
            HistoryJson = history,
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "same short deck",
            Notes = "No change",
            TargetAiPlatform = "ChatGPT",
        }, uploadedHistoryJson: null);

        Assert.False(result.Appended);
        Assert.Contains("Deck has 63 cards — Commander decks run 100.", result.Warnings);
        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Contains("Snapshot saved anyway.", StringComparison.Ordinal));
        Assert.Contains(
            "The imported deck is identical to the latest version — no new snapshot was added.",
            result.Warnings);
    }

    [Fact]
    public async Task ProcessAsync_NeitherHistoryNorDeck_ReturnsError()
    {
        var service = CreateService();

        var result = await service.ProcessAsync(new DeckHistoryRequest(), uploadedHistoryJson: null);

        Assert.Equal("Upload a history file, import a deck, or both.", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessAsync_CorruptedHistoryJson_ReturnsParseError()
    {
        var service = CreateService();

        var result = await service.ProcessAsync(new DeckHistoryRequest
        {
            HistoryJson = """{"format":"not-deckflow"}""",
        }, uploadedHistoryJson: null);

        Assert.Contains("not a DeckFlow history file", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_PairSelection_HonorsValidIdsAndFallsBackWhenInvalid()
    {
        var service = CreateService();
        var history = BuildHistoryJson(
            Version(1, "2026-07-01T00:00:00Z", ["Atraxa, Praetors' Voice"], [Card("Sol Ring", 1)]),
            Version(2, "2026-07-08T00:00:00Z", ["Atraxa, Praetors' Voice"], [Card("Sol Ring", 1), Card("Arcane Signet", 1)]),
            Version(3, "2026-07-15T00:00:00Z", ["Atraxa, Praetors' Voice"], [Card("Sol Ring", 1), Card("Fierce Guardianship", 1)]));

        var explicitResult = await service.ProcessAsync(new DeckHistoryRequest
        {
            HistoryJson = history,
            OlderVersionId = 1,
            NewerVersionId = 2,
            TargetAiPlatform = "ChatGPT",
        }, uploadedHistoryJson: null);

        Assert.Equal(1, explicitResult.PairOlderId);
        Assert.Equal(2, explicitResult.PairNewerId);
        Assert.NotNull(explicitResult.PairDiff);
        Assert.Contains(explicitResult.PairDiff!.Adds, card => card.Name == "Arcane Signet");

        var fallbackResult = await service.ProcessAsync(new DeckHistoryRequest
        {
            HistoryJson = history,
            OlderVersionId = 9,
            NewerVersionId = 1,
            TargetAiPlatform = "ChatGPT",
        }, uploadedHistoryJson: null);

        Assert.Equal(2, fallbackResult.PairOlderId);
        Assert.Equal(3, fallbackResult.PairNewerId);
        Assert.NotNull(fallbackResult.PairDiff);
        Assert.Contains(fallbackResult.PairDiff!.Adds, card => card.Name == "Fierce Guardianship");
        Assert.Contains(fallbackResult.PairDiff.Cuts, card => card.Name == "Arcane Signet");
    }

    [Fact]
    public async Task ProcessAsync_LoaderInvalidOperation_SurfacesErrorMessage()
    {
        var service = CreateService(new InvalidOperationException("Deck text not recognized."));

        var result = await service.ProcessAsync(new DeckHistoryRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "bad deck",
        }, uploadedHistoryJson: null);

        Assert.Equal("Deck text not recognized.", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessAsync_MoxfieldUrl_SetsSourceSiteAndPasteLeavesSourceNull()
    {
        var service = CreateService(FullDeckEntries());

        var urlResult = await service.ProcessAsync(new DeckHistoryRequest
        {
            DeckInputSource = DeckInputSource.PublicUrl,
            DeckUrl = "https://www.moxfield.com/decks/test-deck",
            TargetAiPlatform = "ChatGPT",
        }, uploadedHistoryJson: null);

        Assert.Equal("moxfield", urlResult.File!.Source!.Site);
        Assert.Equal("https://www.moxfield.com/decks/test-deck", urlResult.File.Source.Url);

        var pasteResult = await service.ProcessAsync(new DeckHistoryRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "Commander\n1 Atraxa, Praetors' Voice",
            TargetAiPlatform = "ChatGPT",
        }, uploadedHistoryJson: null);

        Assert.Null(pasteResult.File!.Source);
    }

    [Fact]
    public async Task ProcessAsync_NonHundredCardDeck_AddsWarningButStillAppends()
    {
        var service = CreateService(SixtyThreeCardDeckEntries());

        var result = await service.ProcessAsync(new DeckHistoryRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "63-card deck",
            TargetAiPlatform = "Gemini",
        }, uploadedHistoryJson: null);

        Assert.True(result.Appended);
        Assert.Contains(
            "Deck has 63 cards — Commander decks run 100. Snapshot saved anyway.",
            result.Warnings);
    }

    private static DeckHistoryPageService CreateService(params DeckEntry[] entries) =>
        new(new FakeDeckEntryLoader(new DeckSourceLoadResult(entries.ToList(), FallbackNotice: null)), CreateRegistry(), () => FixedNow);

    private static DeckHistoryPageService CreateService(Exception exception) =>
        new(new FakeDeckEntryLoader(exception), CreateRegistry(), () => FixedNow);

    private static EvolutionPromptVariantRegistry CreateRegistry() =>
        new([
            new ChatGptEvolutionPromptVariant(),
            new ClaudeEvolutionPromptVariant(),
            new GeminiEvolutionPromptVariant(),
        ]);

    private static string BuildHistoryJson(params DeckSnapshot[] versions) =>
        DeckHistorySerializer.Serialize(new DeckHistoryFile
        {
            DeckName = "Atraxa Midrange",
            Versions = versions,
        });

    private static DeckSnapshot Version(
        int id,
        string dateUtc,
        IReadOnlyList<string> commander,
        IReadOnlyList<SnapshotCard> cards) => new()
        {
            Id = id,
            Date = DateTimeOffset.Parse(dateUtc),
            Commander = commander,
            Cards = cards,
        };

    private static SnapshotCard Card(string name, int qty) => new()
    {
        Name = name,
        Qty = qty,
    };

    private static DeckEntry[] FullDeckEntries() =>
    [
        Entry("Atraxa, Praetors' Voice", 1, "commander"),
        Entry("Sol Ring", 1, "mainboard"),
        Entry("Arcane Signet", 1, "mainboard"),
        Entry("Plains", 97, "mainboard"),
        Entry("Island", 12, "maybeboard"),
    ];

    private static DeckEntry[] SixtyThreeCardDeckEntries() =>
    [
        Entry("Atraxa, Praetors' Voice", 1, "commander"),
        Entry("Sol Ring", 1, "mainboard"),
        Entry("Arcane Signet", 1, "mainboard"),
        Entry("Plains", 60, "mainboard"),
    ];

    private static DeckEntry Entry(string name, int quantity, string board) => new()
    {
        Name = name,
        NormalizedName = CardNormalizer.Normalize(name),
        Quantity = quantity,
        Board = board,
    };

    private sealed class FakeDeckEntryLoader : IDeckEntryLoader
    {
        private readonly DeckSourceLoadResult? _result;
        private readonly Exception? _exception;

        public FakeDeckEntryLoader(DeckSourceLoadResult result)
        {
            _result = result;
        }

        public FakeDeckEntryLoader(Exception exception)
        {
            _exception = exception;
        }

        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                return Task.FromException<DeckSourceLoadResult>(_exception);
            }

            return Task.FromResult(_result!);
        }

        public void ValidateCommanderDeckSize(
            string systemName,
            IReadOnlyList<DeckEntry> entries,
            int requiredDeckSize = 100)
        {
        }
    }
}
