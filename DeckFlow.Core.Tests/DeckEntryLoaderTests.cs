using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers source-autodetect deck loading via <see cref="DeckEntryLoader"/>.
/// </summary>
public sealed class DeckEntryLoaderTests
{
    [Fact]
    public async Task LoadFromSourceAsync_MoxfieldUrl_ReturnsEntriesAndFallbackNotice()
    {
        var expectedEntries = new List<DeckEntry>
        {
            new() { Name = "Atraxa, Praetors' Voice", NormalizedName = "atraxa, praetors' voice", Quantity = 1, Board = "commander" },
            new() { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard" }
        };
        var importer = new FakeMoxfieldDeckImporter(
            _ => expectedEntries,
            _ => new MoxfieldImportResult(expectedEntries, MoxfieldImportSource.CommanderSpellbookFallback, "Used fallback import."));
        var loader = CreateLoader(importer: importer);

        var result = await loader.LoadFromSourceAsync(" https://www.moxfield.com/decks/example ", cancellationToken: CancellationToken.None);

        Assert.Collection(
            result.Entries,
            entry =>
            {
                Assert.Equal("Atraxa, Praetors' Voice", entry.Name);
                Assert.Equal("commander", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            });
        Assert.Equal("Used fallback import.", result.FallbackNotice);
        Assert.Equal(" https://www.moxfield.com/decks/example ", importer.LastImportWithSourceArgument);
    }

    [Fact]
    public async Task LoadFromSourceAsync_ArchidektUrl_ReturnsEntriesAndNullNotice()
    {
        var expectedEntries = new List<DeckEntry>
        {
            new() { Name = "Kinnan, Bonder Prodigy", NormalizedName = "kinnan, bonder prodigy", Quantity = 1, Board = "commander" },
            new() { Name = "Arcane Signet", NormalizedName = "arcane signet", Quantity = 1, Board = "mainboard" }
        };
        var importer = new FakeArchidektDeckImporter(_ => expectedEntries);
        var loader = CreateLoader(archidektImporter: importer);

        var result = await loader.LoadFromSourceAsync(" https://archidekt.com/decks/123 ");

        Assert.Same(expectedEntries, result.Entries);
        Assert.Null(result.FallbackNotice);
        Assert.Equal(" https://archidekt.com/decks/123 ", importer.LastImportArgument);
    }

    [Fact]
    public async Task LoadFromSourceAsync_PastedMoxfieldText_ParsesEntries()
    {
        var loader = CreateLoader();

        var result = await loader.LoadFromSourceAsync("""
Commander
1 Atraxa, Praetors' Voice

Deck
1 Sol Ring
""");

        Assert.Collection(
            result.Entries,
            entry =>
            {
                Assert.Equal("Atraxa, Praetors' Voice", entry.Name);
                Assert.Equal("commander", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            });
        Assert.Null(result.FallbackNotice);
    }

    [Fact]
    public async Task LoadFromSourceAsync_PastedArchidektStyleSectionText_ParsesEntries()
    {
        var loader = CreateLoader();

        // Real production cascade tries MoxfieldParser first, and this Archidekt-style
        // section-header paste is accepted there without falling through.
        var result = await loader.LoadFromSourceAsync("""
Commander
Atraxa, Praetors' Voice

Deck
1 Sol Ring
""");

        Assert.Collection(
            result.Entries,
            entry =>
            {
                Assert.Equal("Atraxa, Praetors' Voice", entry.Name);
                Assert.Equal("commander", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            });
        Assert.Null(result.FallbackNotice);
    }

    [Theory]
    [InlineData("https://moxfield.com.evil.tld/decks/abc")]
    [InlineData("https://evilmoxfield.com/decks/abc")]
    [InlineData("https://moxfield.com@evil.tld/decks/abc")]
    public async Task LoadFromSourceAsync_SpoofedMoxfieldUrl_DoesNotRouteToMoxfieldImporter(string spoofUrl)
    {
        // Spoof URLs must never reach the Moxfield importer, regardless of whether the parser
        // cascade accepts or rejects the input. The null-capture assertion is the security proof:
        // if the importer were called (a future regression), LastImportWithSourceArgument would be set.
        var importer = new FakeMoxfieldDeckImporter(_ => []);
        var loader = CreateLoader(importer: importer);

        // The spoof URL falls through to the parser cascade (treated as pasted text, not a
        // recognised deck URL). The importer must never be invoked.
        await loader.LoadFromSourceAsync(spoofUrl);

        Assert.Null(importer.LastImportWithSourceArgument);
    }

    [Theory]
    [InlineData("https://archidekt.com.evil.tld/decks/abc")]
    [InlineData("https://evilarchidekt.com/decks/abc")]
    [InlineData("https://archidekt.com@evil.tld/decks/abc")]
    public async Task LoadFromSourceAsync_SpoofedArchidektUrl_DoesNotRouteToArchidektImporter(string spoofUrl)
    {
        // Spoof URLs must never reach the Archidekt importer. See spoof-Moxfield variant above.
        var archidektImporter = new FakeArchidektDeckImporter(_ => []);
        var loader = CreateLoader(archidektImporter: archidektImporter);

        await loader.LoadFromSourceAsync(spoofUrl);

        Assert.Null(archidektImporter.LastImportArgument);
    }

    [Fact]
    public async Task LoadFromSourceAsync_UnrecognizedTextWithThrowNotRecognized_ThrowsExactMessage()
    {
        var loader = CreateLoader();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadFromSourceAsync("*F*"));

        Assert.Equal("The submitted deck was not recognized as a Moxfield URL, Archidekt URL, Moxfield export, or Archidekt export.", exception.Message);
    }

    [Fact]
    public async Task LoadFromSourceAsync_UnrecognizedTextWithPropagateParseException_PropagatesDeckParseException()
    {
        var loader = CreateLoader();

        await Assert.ThrowsAsync<DeckParseException>(() => loader.LoadFromSourceAsync(
            "*F*",
            UnrecognizedPasteBehavior.PropagateParseException));
    }

    private static DeckEntryLoader CreateLoader(
        IMoxfieldDeckImporter? importer = null,
        IArchidektDeckImporter? archidektImporter = null)
        => new(
            importer ?? new FakeMoxfieldDeckImporter(_ => []),
            archidektImporter ?? new FakeArchidektDeckImporter(_ => []),
            new MoxfieldParser(),
            new ArchidektParser());

    private sealed class FakeMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        private readonly Func<string, List<DeckEntry>> _importAsync;
        private readonly Func<string, MoxfieldImportResult> _importWithSourceAsync;

        public FakeMoxfieldDeckImporter(
            Func<string, List<DeckEntry>> importAsync,
            Func<string, MoxfieldImportResult>? importWithSourceAsync = null)
        {
            _importAsync = importAsync;
            _importWithSourceAsync = importWithSourceAsync ?? (url => new MoxfieldImportResult(_importAsync(url), MoxfieldImportSource.Direct));
        }

        public string? LastImportArgument { get; private set; }

        public string? LastImportWithSourceArgument { get; private set; }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            LastImportArgument = urlOrDeckId;
            return Task.FromResult(_importAsync(urlOrDeckId));
        }

        public Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            LastImportWithSourceArgument = urlOrDeckId;
            return Task.FromResult(_importWithSourceAsync(urlOrDeckId));
        }
    }

    private sealed class FakeArchidektDeckImporter : IArchidektDeckImporter
    {
        private readonly Func<string, List<DeckEntry>> _importAsync;

        public FakeArchidektDeckImporter(Func<string, List<DeckEntry>> importAsync)
        {
            _importAsync = importAsync;
        }

        public string? LastImportArgument { get; private set; }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            LastImportArgument = urlOrDeckId;
            return Task.FromResult(_importAsync(urlOrDeckId));
        }
    }
}
