using System.Net;
using System.Runtime.CompilerServices;
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
  "boards": {
    "commanders": {
      "cards": {
        "1234": {
          "quantity": 1,
          "card": { "name": "Atraxa, Praetors' Voice", "set": "c16", "cn": "28" }
        }
      }
    },
    "mainboard": {
      "cards": {
        "2345": {
          "quantity": 1,
          "card": { "name": "Sol Ring", "set": "c16", "cn": "272" }
        }
      }
    },
    "sideboard": {
      "cards": {
        "3456": {
          "quantity": 1,
          "card": { "name": "Swords to Plowshares", "set": "c16", "cn": "74" }
        }
      }
    },
    "maybeboard": {
      "cards": {
        "4567": {
          "quantity": 1,
          "card": { "name": "Smothering Tithe", "set": "rna", "cn": "22" }
        }
      }
    }
  }
}
"""
            }));

        var entries = await importer.ImportAsync("https://www.moxfield.com/decks/test-deck-id");

        Assert.Contains(entries, entry => entry.Name == "Atraxa, Praetors' Voice" && entry.Board == "commander");
        Assert.Contains(entries, entry => entry.Name == "Sol Ring" && entry.Board == "mainboard" && entry.Quantity == 1 && entry.SetCode == "c16" && entry.CollectorNumber == "272");
        Assert.Contains(entries, entry => entry.Name == "Swords to Plowshares" && entry.Board == "sideboard" && entry.Quantity == 1 && entry.SetCode == "c16" && entry.CollectorNumber == "74");
        Assert.Contains(entries, entry => entry.Name == "Smothering Tithe" && entry.Board == "maybeboard" && entry.Quantity == 1 && entry.SetCode == "rna" && entry.CollectorNumber == "22");
    }

    [Fact]
    public async Task ImportWithSourceAsync_DirectFixture_DetectsCompanionNameWithoutAddingEntry()
    {
        var fixtureBody = ReadFixture("moxfield-companion-direct.json");
        var importer = CreateImporterReturning(fixtureBody);

        var result = await importer.ImportWithSourceAsync("https://www.moxfield.com/decks/test-deck-id");

        Assert.Equal("Jegantha, the Wellspring", result.DetectedCompanionName);
        Assert.Equal(5, result.Entries.Count);
        Assert.Contains(result.Entries, entry => entry.Name == "Kraum, Ludevic's Opus" && entry.Board == "commander" && entry.Quantity == 1 && entry.SetCode == "c16" && entry.CollectorNumber == "39");
        Assert.Contains(result.Entries, entry => entry.Name == "Sol Ring" && entry.Board == "mainboard" && entry.Quantity == 1 && entry.SetCode == "c16" && entry.CollectorNumber == "272");
        Assert.Contains(result.Entries, entry => entry.Name == "Arcane Signet" && entry.Board == "mainboard" && entry.Quantity == 1 && entry.SetCode == "eld" && entry.CollectorNumber == "331");
        Assert.Contains(result.Entries, entry => entry.Name == "Command Tower" && entry.Board == "mainboard" && entry.Quantity == 1 && entry.SetCode == "c16" && entry.CollectorNumber == "285");
        Assert.Contains(result.Entries, entry => entry.Name == "Ponder" && entry.Board == "mainboard" && entry.Quantity == 1 && entry.SetCode == "c21" && entry.CollectorNumber == "118");
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

    private static string ReadFixture(string fileName, [CallerFilePath] string sourceFilePath = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "Fixtures", fileName));

    private static string ReadFixtureWithoutCompanions()
    {
        using var document = JsonDocument.Parse(ReadFixture("moxfield-companion-direct.json"));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "boards", StringComparison.Ordinal) && property.Value.ValueKind == JsonValueKind.Object)
                {
                    writer.WritePropertyName(property.Name);
                    writer.WriteStartObject();
                    foreach (var boardProperty in property.Value.EnumerateObject())
                    {
                        if (!string.Equals(boardProperty.Name, "companions", StringComparison.Ordinal))
                        {
                            boardProperty.WriteTo(writer);
                        }
                    }

                    writer.WriteEndObject();
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
