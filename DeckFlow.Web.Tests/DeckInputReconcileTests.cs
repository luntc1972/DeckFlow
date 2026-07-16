using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class DeckInputReconcileTests
{
    [Fact]
    public void MetaGapRequest_NormalizeDeckSource_UsesDeckUrlInUrlMode()
    {
        var request = new MetaGapRequest
        {
            DeckInputSource = DeckInputSource.PublicUrl,
            DeckUrl = "https://moxfield.com/decks/example"
        };

        request.NormalizeDeckSource();

        Assert.Equal("https://moxfield.com/decks/example", request.DeckSource);
    }

    [Fact]
    public void MetaGapRequest_NormalizeDeckSource_UsesDeckTextInPasteMode()
    {
        var request = new MetaGapRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Sol Ring"
        };

        request.NormalizeDeckSource();

        Assert.Equal("1 Sol Ring", request.DeckSource);
    }

    [Fact]
    public void MetaGapRequest_NormalizeDeckSource_SplitsUrlDeckSourceIntoUrlMode()
    {
        var request = new MetaGapRequest
        {
            DeckSource = "https://archidekt.com/decks/example"
        };

        request.NormalizeDeckSource();

        Assert.Equal(DeckInputSource.PublicUrl, request.DeckInputSource);
        Assert.Equal(request.DeckSource, request.DeckUrl);
    }

    [Fact]
    public void MetaGapRequest_NormalizeDeckSource_SplitsTextDeckSourceIntoPasteMode()
    {
        var request = new MetaGapRequest
        {
            DeckSource = "1 Sol Ring"
        };

        request.NormalizeDeckSource();

        Assert.Equal(DeckInputSource.PasteText, request.DeckInputSource);
        Assert.Equal(request.DeckSource, request.DeckText);
    }

    [Fact]
    public void DeckComparisonRequest_NormalizeDeckSources_ReconcilesDecksIndependently()
    {
        var request = new DeckComparisonRequest
        {
            DeckAInputSource = DeckInputSource.PublicUrl,
            DeckAUrl = "https://moxfield.com/decks/deck-a",
            DeckBInputSource = DeckInputSource.PasteText,
            DeckBText = "1 Command Tower"
        };

        request.NormalizeDeckSources();

        Assert.Equal("https://moxfield.com/decks/deck-a", request.DeckASource);
        Assert.Equal("1 Command Tower", request.DeckBSource);
    }

    [Fact]
    public void DeckComparisonRequest_NormalizeDeckSources_SplitsDeckSourcesIntoFields()
    {
        var request = new DeckComparisonRequest
        {
            DeckASource = "https://archidekt.com/decks/deck-a",
            DeckBSource = "1 Sol Ring"
        };

        request.NormalizeDeckSources();

        Assert.Equal(DeckInputSource.PublicUrl, request.DeckAInputSource);
        Assert.Equal(request.DeckASource, request.DeckAUrl);
        Assert.Equal(DeckInputSource.PasteText, request.DeckBInputSource);
        Assert.Equal(request.DeckBSource, request.DeckBText);
    }
}
