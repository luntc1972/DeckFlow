using System;
using System.Collections.Generic;
using DeckSyncWorkbench.Core.Reporting;
using DeckSyncWorkbench.Web.Services;
using Xunit;

namespace DeckSyncWorkbench.Web.Tests;

public sealed class DeckControllerTests
{
    [Fact]
    public void BuildNoSuggestionsMessage_UsesCacheRefreshNotice_WhenNoDecks()
    {
        var totals = new CardDeckTotals(0, new Dictionary<string, int>());
        var message = CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage("Guardian Project", totals);

        Assert.Equal("No card categories for Guardian Project have been observed in the cached data yet. Run Show Categories again to refresh the cache.", message);
    }

    [Fact]
    public void BuildNoSuggestionsMessage_UsesGeneralMessage_WhenDecksExist()
    {
        var totals = new CardDeckTotals(5, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["mainboard"] = 5
        });
        var message = CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage("Guardian Project", totals);

        Assert.Equal("No category suggestions were found for Guardian Project. You can run the lookup again to retry the live Archidekt and EDHREC checks.", message);
    }
}
