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

    private static ArchidektApiDeckImporter CreateImporterReturningFixture(string fileName)
    {
        var restClient = new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://archidekt.com"),
            ConfigureMessageHandler = _ => new FixtureMessageHandler(ReadFixture(fileName))
        });

        return new ArchidektApiDeckImporter(restClient);
    }

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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
    }
}
