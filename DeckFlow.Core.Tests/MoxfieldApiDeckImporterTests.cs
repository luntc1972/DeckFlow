using System.Net;
using System.Text.Json;
using DeckFlow.Core.Integration;
using RestSharp;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="MoxfieldApiDeckImporter"/> covering board preservation, API response parsing,
/// and Commander Spellbook fallback behaviour.
/// </summary>
public sealed class MoxfieldApiDeckImporterTests
{
    // Minimal Spellbook JSON body — commanders[] + main[] are the only boards the importer reads.
    private const string SpellbookOkBody = """{"commanders":[],"main":[]}""";

    [Fact]
    public async Task FetchViaCommanderSpellbookAsync_AlwaysForwardsCanonicalUrl_NeverSubmittedUrl()
    {
        // Direct Moxfield fetch returns 403 (cloud-edge block), triggering Spellbook fallback.
        // The forwarded url param must be the reconstructed canonical, never the submitted www URL.
        RestRequest? spellbookRequest = null;
        var callCount = 0;

        var importer = new MoxfieldApiDeckImporter(
            executeAsync: (request, _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call: direct Moxfield fetch — simulate 403 cloud-edge block.
                    return Task.FromResult(new RestResponse
                    {
                        StatusCode = HttpStatusCode.Forbidden,
                        ResponseStatus = ResponseStatus.Completed,
                        IsSuccessStatusCode = false,
                        StatusDescription = "Forbidden",
                        Content = string.Empty
                    });
                }

                // Second call: Commander Spellbook fallback — capture request, return OK.
                spellbookRequest = request;
                return Task.FromResult(new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true,
                    StatusDescription = "OK",
                    Content = SpellbookOkBody
                });
            });

        await importer.ImportAsync("https://www.moxfield.com/decks/abc123");

        Assert.NotNull(spellbookRequest);
        var urlParam = spellbookRequest.Parameters
            .FirstOrDefault(p => string.Equals(p.Name, "url", StringComparison.OrdinalIgnoreCase))
            ?.Value?.ToString();
        Assert.Equal("https://moxfield.com/decks/abc123", urlParam);
    }

    [Fact]
    public async Task FetchViaCommanderSpellbookAsync_SpoofHostInput_ForwardsCanonicalNotSubmitted()
    {
        // Even when the input is a spoof host, MoxfieldApiUrl.TryGetDeckId still extracts
        // the deckId from the path segment. After the fix the Spellbook fallback must forward
        // https://moxfield.com/decks/{deckId} — never the hostile originalUrl.
        // This is the direct SC2 proof: submitted host/query of originalUrl never reaches Spellbook.
        RestRequest? spellbookRequest = null;
        var callCount = 0;

        var importer = new MoxfieldApiDeckImporter(
            executeAsync: (request, _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new RestResponse
                    {
                        StatusCode = HttpStatusCode.Forbidden,
                        ResponseStatus = ResponseStatus.Completed,
                        IsSuccessStatusCode = false,
                        StatusDescription = "Forbidden",
                        Content = string.Empty
                    });
                }

                spellbookRequest = request;
                return Task.FromResult(new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true,
                    StatusDescription = "OK",
                    Content = SpellbookOkBody
                });
            });

        await importer.ImportAsync("https://moxfield.com.evil.tld/decks/abc123?x=1");

        Assert.NotNull(spellbookRequest);
        var urlParam = spellbookRequest.Parameters
            .FirstOrDefault(p => string.Equals(p.Name, "url", StringComparison.OrdinalIgnoreCase))
            ?.Value?.ToString();
        // Canonical reconstruction — hostile host/path/query of originalUrl must never be echoed.
        Assert.Equal("https://moxfield.com/decks/abc123", urlParam);
    }

    [Fact]
    public async Task ImportAsync_PreservesSideboardEntriesAsSideboard()
    {
        var importer = new MoxfieldApiDeckImporter(
            executeAsync: (_, _) => Task.FromResult(new RestResponse
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            ResponseStatus = ResponseStatus.Completed,
            IsSuccessStatusCode = true,
            StatusDescription = "OK",
            Content = """
{
  "commanders": {
    "Atraxa, Praetors' Voice": {
      "quantity": 1,
      "card": { "name": "Atraxa, Praetors' Voice", "set": "c16", "cn": "28" }
    }
  },
  "mainboard": {
    "Sol Ring": {
      "quantity": 1,
      "card": { "name": "Sol Ring", "set": "c16", "cn": "272" }
    }
  },
  "sideboard": {
    "Swords to Plowshares": {
      "quantity": 1,
      "card": { "name": "Swords to Plowshares", "set": "c16", "cn": "74" }
    }
  },
  "maybeboard": {
    "Smothering Tithe": {
      "quantity": 1,
      "card": { "name": "Smothering Tithe", "set": "rna", "cn": "22" }
    }
  }
}
"""
        }));

        var entries = await importer.ImportAsync("https://www.moxfield.com/decks/test-deck-id");

        Assert.Contains(entries, entry => entry.Name == "Atraxa, Praetors' Voice" && entry.Board == "commander");
        Assert.Contains(entries, entry => entry.Name == "Sol Ring" && entry.Board == "mainboard");
        Assert.Contains(entries, entry => entry.Name == "Swords to Plowshares" && entry.Board == "sideboard");
        Assert.Contains(entries, entry => entry.Name == "Smothering Tithe" && entry.Board == "maybeboard");
    }

    [Fact]
    public async Task ImportWithSourceAsync_DirectFixture_DetectsCompanionNameWithoutAddingEntry()
    {
        var fixtureBody = ReadFixture("moxfield-companion-direct.json");
        var importer = CreateImporterReturning(fixtureBody);

        var result = await importer.ImportWithSourceAsync("https://www.moxfield.com/decks/test-deck-id");

        Assert.Equal("Jegantha, the Wellspring", result.DetectedCompanionName);
        Assert.Equal(5, result.Entries.Count);
        Assert.DoesNotContain(result.Entries, entry => entry.Name == "Jegantha, the Wellspring");
    }

    [Fact]
    public async Task ImportWithSourceAsync_DirectFixtureWithoutCompanions_ReturnsNullCompanionName()
    {
        var fixtureBody = ReadFixtureWithoutCompanions();
        var importer = CreateImporterReturning(fixtureBody);

        var result = await importer.ImportWithSourceAsync("https://www.moxfield.com/decks/test-deck-id");

        Assert.Null(result.DetectedCompanionName);
        Assert.Equal(5, result.Entries.Count);
    }

    [Fact]
    public async Task ImportWithSourceAsync_FallbackPath_LeavesDetectedCompanionNameNull()
    {
        var callCount = 0;
        var importer = new MoxfieldApiDeckImporter(
            executeAsync: (_, _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new RestResponse
                    {
                        StatusCode = HttpStatusCode.Forbidden,
                        ResponseStatus = ResponseStatus.Completed,
                        IsSuccessStatusCode = false,
                        StatusDescription = "Forbidden",
                        Content = string.Empty
                    });
                }

                return Task.FromResult(new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true,
                    StatusDescription = "OK",
                    Content = SpellbookOkBody
                });
            });

        var result = await importer.ImportWithSourceAsync("https://www.moxfield.com/decks/test-deck-id");

        Assert.Equal(MoxfieldImportSource.CommanderSpellbookFallback, result.Source);
        Assert.Null(result.DetectedCompanionName);
    }

    private static MoxfieldApiDeckImporter CreateImporterReturning(string body)
        => new(
            executeAsync: (_, _) => Task.FromResult(new RestResponse
            {
                StatusCode = HttpStatusCode.OK,
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = true,
                StatusDescription = "OK",
                Content = body
            }));

    private static string ReadFixture(string fileName)
        => File.ReadAllText(Path.Combine(RepositoryRoot(), "DeckFlow.Core.Tests", "Fixtures", fileName));

    private static string ReadFixtureWithoutCompanions()
    {
        using var document = JsonDocument.Parse(ReadFixture("moxfield-companion-direct.json"));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "companions", StringComparison.Ordinal))
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

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
}
