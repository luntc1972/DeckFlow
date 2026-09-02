using System.Net;
using DeckFlow.Core.Integration;
using RestSharp;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Regression locks for <see cref="ArchidektApiDeckImporter"/> using captured API fixtures.
/// </summary>
public sealed class ArchidektApiDeckImporterTests
{
    [Fact]
    public async Task ImportAsync_BackgroundFixture_RoutesBackgroundCardToCommanderBoard()
    {
        var importer = CreateImporterReturningFixture("archidekt-background-companion.json");

        var entries = await importer.ImportAsync("https://archidekt.com/decks/3674983");

        var backgroundEntry = Assert.Single(entries, entry => entry.Name == "Passionate Archaeologist");
        Assert.Equal("commander", backgroundEntry.Board);
        Assert.Null(backgroundEntry.Category);
    }

    [Fact]
    public async Task ImportAsync_BackgroundFixture_PreservesExistingEntryOutput()
    {
        var importer = CreateImporterReturningFixture("archidekt-background-companion.json");

        var entries = await importer.ImportAsync("https://archidekt.com/decks/3674983");

        Assert.Equal(79, entries.Count);
        Assert.Equal(2, entries.Count(entry => entry.Board == "commander"));
        Assert.DoesNotContain(entries, entry => string.Equals(entry.Category, "Companion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportAsync_ExcludedCategoryFixture_RoutesExcludedCardsToMaybeboard()
    {
        var importer = CreateImporterReturningFixture("archidekt-includedindeck.json");

        var entries = await importer.ImportAsync("https://archidekt.com/decks/3674983");

        Assert.Equal("commander", Assert.Single(entries, entry => entry.Name == "Etali, Primal Conqueror // Etali, Primal Sickness").Board);
        Assert.Equal("mainboard", Assert.Single(entries, entry => entry.CollectorNumber == "236").Board);
        Assert.Equal("maybeboard", Assert.Single(entries, entry => entry.CollectorNumber == "361").Board);
        Assert.Equal("mainboard", Assert.Single(entries, entry => entry.Name == "Sol Ring").Board);
        Assert.Equal("maybeboard", Assert.Single(entries, entry => entry.Name == "Cavern of Souls").Board);
    }

    [Fact]
    public async Task ImportAsync_ExcludedCategoryFixture_KeepsAllEntries()
    {
        var importer = CreateImporterReturningFixture("archidekt-includedindeck.json");

        var entries = await importer.ImportAsync("https://archidekt.com/decks/3674983");

        Assert.Equal(7, entries.Count);
    }

    [Fact]
    public async Task ImportAsync_ExcludedCategoryFixture_CommanderOutranksExcludedCategory()
    {
        var importer = CreateImporterReturningFixture("archidekt-includedindeck.json");

        var entries = await importer.ImportAsync("https://archidekt.com/decks/3674983");

        // Guards precedence regression: excluded user category must not demote a commander, because that would drop the validated deck count and make ValidateCommanderDeckSize throw.
        Assert.Equal("commander", Assert.Single(entries, entry => entry.Name == "Tevesh Szat, Doom of Fools").Board);
    }

    [Fact]
    public async Task ImportAsync_ExcludedCategoryFixture_UnknownCategoryDoesNotExclude()
    {
        var importer = CreateImporterReturningFixture("archidekt-includedindeck.json");

        var entry = Assert.Single(await importer.ImportAsync("https://archidekt.com/decks/3674983"), entry => entry.Name == "Ancient Tomb");

        Assert.Equal("mainboard", entry.Board);
        Assert.Equal("_Swap In II", entry.Category);
    }

    [Fact]
    public async Task ImportWithMetadataAsync_BackgroundFixture_ReturnsSameEntriesAndParsesMetadata()
    {
        var importer = CreateImporterReturningFixture("archidekt-background-companion.json");

        var expectedEntries = await importer.ImportAsync("https://archidekt.com/decks/3674983");
        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.Equal(expectedEntries.Count, result.Entries.Count);
        Assert.Equal(expectedEntries.Select(entry => entry.Name), result.Entries.Select(entry => entry.Name));
        Assert.NotNull(result.Metadata);
        Assert.Equal(3, result.Metadata!.DeckFormat);
        Assert.False(result.Metadata.Theorycrafted);
        Assert.Equal(DateTimeOffset.Parse("2022-12-13T12:45:24.123628Z"), result.Metadata.CreatedUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-02-07T15:08:08.436920Z"), result.Metadata.UpdatedUtc);
    }

    [Theory]
    [InlineData("3", 3)]
    [InlineData("\"3\"", 3)]
    [InlineData("null", null)]
    [InlineData("{}", null)]
    [InlineData("[]", null)]
    [InlineData("\"abc\"", null)]
    [InlineData("3.5", null)]
    [InlineData("1e999", null)]
    public async Task ImportWithMetadataAsync_EdhBracketVariants_ParsesToNullableIntWithoutThrowing(string rawJsonValue, int? expected)
    {
        var importer = CreateImporterReturningJson(FixtureWithEdhBracket(rawJsonValue));

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Equal(expected, result.Metadata!.EdhBracket);
    }

    [Fact]
    public async Task ImportWithMetadataAsync_EdhBracketMissing_ParsesToNull()
    {
        var importer = CreateImporterReturningJson(FixtureWithoutEdhBracket());

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Null(result.Metadata!.EdhBracket);
    }

    [Theory]
    [InlineData("3", 3)]
    [InlineData("\"3\"", 3)]
    [InlineData("null", null)]
    [InlineData("{}", null)]
    [InlineData("[]", null)]
    [InlineData("\"abc\"", null)]
    [InlineData("3.5", null)]
    [InlineData("1e999", null)]
    public async Task ImportWithMetadataAsync_DeckFormatVariants_ParsesToNullableIntWithoutThrowing(string rawJsonValue, int? expected)
    {
        var importer = CreateImporterReturningJson(FixtureWithDeckFormat(rawJsonValue));

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Equal(expected, result.Metadata!.DeckFormat);
    }

    [Fact]
    public async Task ImportWithMetadataAsync_DeckFormatMissing_ParsesToNull()
    {
        var importer = CreateImporterReturningJson(FixtureWithoutDeckFormat());

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Null(result.Metadata!.DeckFormat);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"false\"", false)]
    [InlineData("null", null)]
    [InlineData("{}", null)]
    [InlineData("[]", null)]
    [InlineData("\"abc\"", null)]
    [InlineData("1", null)]
    public async Task ImportWithMetadataAsync_TheorycraftedVariants_ParsesToNullableBoolWithoutThrowing(string rawJsonValue, bool? expected)
    {
        var importer = CreateImporterReturningJson(FixtureWithTheorycrafted(rawJsonValue));

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Equal(expected, result.Metadata!.Theorycrafted);
    }

    [Fact]
    public async Task ImportWithMetadataAsync_TheorycraftedMissing_ParsesToNull()
    {
        var importer = CreateImporterReturningJson(FixtureWithoutTheorycrafted());

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Null(result.Metadata!.Theorycrafted);
    }

    [Theory]
    [InlineData("\"2022-12-13T12:45:24.123628Z\"", true)]
    [InlineData("\"not-a-date\"", false)]
    [InlineData("123", false)]
    [InlineData("{}", false)]
    [InlineData("null", false)]
    public async Task ImportWithMetadataAsync_CreatedAtVariants_ParsesToNullableTimestampWithoutThrowing(string rawJsonValue, bool expectParsed)
    {
        var importer = CreateImporterReturningJson(FixtureWithCreatedAt(rawJsonValue));

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Equal(79, result.Entries.Count);
        if (expectParsed)
        {
            Assert.NotNull(result.Metadata!.CreatedUtc);
        }
        else
        {
            Assert.Null(result.Metadata!.CreatedUtc);
        }
    }

    [Fact]
    public async Task ImportWithMetadataAsync_CreatedAtMissing_ParsesToNull()
    {
        var importer = CreateImporterReturningJson(FixtureWithoutCreatedAt());

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Equal(79, result.Entries.Count);
        Assert.Null(result.Metadata!.CreatedUtc);
    }

    [Theory]
    [InlineData("\"2026-02-07T15:08:08.436920Z\"", true)]
    [InlineData("\"not-a-date\"", false)]
    [InlineData("123", false)]
    [InlineData("{}", false)]
    [InlineData("null", false)]
    public async Task ImportWithMetadataAsync_UpdatedAtVariants_ParsesToNullableTimestampWithoutThrowing(string rawJsonValue, bool expectParsed)
    {
        var importer = CreateImporterReturningJson(FixtureWithUpdatedAt(rawJsonValue));

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Equal(79, result.Entries.Count);
        if (expectParsed)
        {
            Assert.NotNull(result.Metadata!.UpdatedUtc);
        }
        else
        {
            Assert.Null(result.Metadata!.UpdatedUtc);
        }
    }

    [Fact]
    public async Task ImportWithMetadataAsync_UpdatedAtMissing_ParsesToNull()
    {
        var importer = CreateImporterReturningJson(FixtureWithoutUpdatedAt());

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Equal(79, result.Entries.Count);
        Assert.Null(result.Metadata!.UpdatedUtc);
    }

    [Fact]
    public async Task ImportWithMetadataAsync_BackgroundFixture_CapturesTimestampWhenBracketIsNull()
    {
        var importer = CreateImporterReturningFixture("archidekt-background-companion.json");

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.NotNull(result.Metadata);
        Assert.Null(result.Metadata!.EdhBracket);
        Assert.NotEqual(default, result.Metadata.CapturedUtc);
    }

    [Fact]
    public async Task ImportWithMetadataAsync_BackgroundFixture_RecordsExactlyOneHttpRequest()
    {
        var (importer, handler) = CreateImporterAndHandlerReturningFixture("archidekt-background-companion.json");

        await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("3.5")]
    [InlineData("\"abc\"")]
    [InlineData("{}")]
    [InlineData("1e999")]
    public async Task ImportAsync_MalformedEdhBracket_EntriesUnaffected(string rawJsonValue)
    {
        var importer = CreateImporterReturningJson(FixtureWithEdhBracket(rawJsonValue));

        var entries = await importer.ImportAsync("https://archidekt.com/decks/3674983");

        Assert.Equal(79, entries.Count);
        Assert.Equal(2, entries.Count(entry => entry.Board == "commander"));
        Assert.DoesNotContain(entries, entry => string.Equals(entry.Category, "Companion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportWithMetadataAsync_UnrecognizablePayload_RecordsNoCapture()
    {
        var importer = CreateImporterReturningJson("{}");

        var result = await importer.ImportWithMetadataAsync("https://archidekt.com/decks/3674983");

        Assert.Null(result.Metadata);
        Assert.Empty(result.Entries);
    }

    private static (ArchidektApiDeckImporter Importer, FixtureMessageHandler Handler) CreateImporterAndHandlerReturningJson(string json)
    {
        var handler = new FixtureMessageHandler(json);
        var restClient = new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://archidekt.com"),
            ConfigureMessageHandler = _ => handler
        });

        return (new ArchidektApiDeckImporter(restClient), handler);
    }

    private static (ArchidektApiDeckImporter Importer, FixtureMessageHandler Handler) CreateImporterAndHandlerReturningFixture(string fileName)
        => CreateImporterAndHandlerReturningJson(ReadFixture(fileName));

    private static ArchidektApiDeckImporter CreateImporterReturningJson(string json)
        => CreateImporterAndHandlerReturningJson(json).Importer;

    private static ArchidektApiDeckImporter CreateImporterReturningFixture(string fileName)
        => CreateImporterAndHandlerReturningFixture(fileName).Importer;

    private static string FixtureWithEdhBracket(string rawJsonValue)
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"edhBracket\":null", $"\"edhBracket\":{rawJsonValue}", StringComparison.Ordinal);

    private static string FixtureWithoutEdhBracket()
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"edhBracket\":null,", string.Empty, StringComparison.Ordinal);

    private static string FixtureWithDeckFormat(string rawJsonValue)
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"deckFormat\":3", $"\"deckFormat\":{rawJsonValue}", StringComparison.Ordinal);

    private static string FixtureWithoutDeckFormat()
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"deckFormat\":3,", string.Empty, StringComparison.Ordinal);

    private static string FixtureWithTheorycrafted(string rawJsonValue)
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"theorycrafted\":false", $"\"theorycrafted\":{rawJsonValue}", StringComparison.Ordinal);

    private static string FixtureWithoutTheorycrafted()
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"theorycrafted\":false,", string.Empty, StringComparison.Ordinal);

    private static string FixtureWithCreatedAt(string rawJsonValue)
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"createdAt\":\"2022-12-13T12:45:24.123628Z\"", $"\"createdAt\":{rawJsonValue}", StringComparison.Ordinal);

    private static string FixtureWithoutCreatedAt()
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"createdAt\":\"2022-12-13T12:45:24.123628Z\",", string.Empty, StringComparison.Ordinal);

    private static string FixtureWithUpdatedAt(string rawJsonValue)
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"updatedAt\":\"2026-02-07T15:08:08.436920Z\"", $"\"updatedAt\":{rawJsonValue}", StringComparison.Ordinal);

    private static string FixtureWithoutUpdatedAt()
        => ReadFixture("archidekt-background-companion.json")
            .Replace("\"updatedAt\":\"2026-02-07T15:08:08.436920Z\",", string.Empty, StringComparison.Ordinal);

    private static string ReadFixture(string fileName)
        => File.ReadAllText(Path.Combine(RepositoryRoot(), "DeckFlow.Core.Tests", "Fixtures", fileName));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeckFlow.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private sealed class FixtureMessageHandler(string content) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
