using DeckFlow.CLI;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Scryfall;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Verifies CLI collection-protocol request mapping.
/// </summary>
public sealed class ManabaseCommandRunnerTests
{
    [Fact]
    public void CreateCollectionRequest_PrintingAndNameEntries_PreservesIdentifierShapes()
    {
        IReadOnlyList<DeckEntry> entries =
        [
            Entry("Orcish Bowmasters", "ltr", "103"),
            Entry("Sol Ring", null, null),
        ];

        var request = ManabaseCommandRunner.CreateCollectionRequest(entries);

        Assert.Collection(
            request.Identifiers,
            printing =>
            {
                Assert.Null(printing.Name);
                Assert.Equal("ltr", printing.Set);
                Assert.Equal("103", printing.CollectorNumber);
            },
            name => Assert.Equal("Sol Ring", name.Name));
    }

    [Fact]
    public void GetNotFoundLabels_PrintingIdentifier_PreservesPrintingDiagnostic()
    {
        var response = new ScryfallCollectionProtocolResponse(
            System.Net.HttpStatusCode.OK,
            [],
            [ScryfallCollectionIdentifier.ForPrinting("ltr", "103")],
            HasPayload: true);

        IReadOnlyList<string> labels = ManabaseCommandRunner.GetNotFoundLabels(response);

        Assert.Equal(["ltr #103"], labels);
    }

    private static DeckEntry Entry(string name, string? setCode, string? collectorNumber) => new()
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = 1,
        Board = "mainboard",
        SetCode = setCode,
        CollectorNumber = collectorNumber,
    };
}
