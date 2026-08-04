using DeckFlow.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class BridgeControllerTests
{
    [Fact]
    public void Index_returns_the_bridge_view()
    {
        var result = Assert.IsType<ViewResult>(new BridgeController().Index());

        Assert.Null(result.ViewName);
    }

    [Fact]
    public void Bridge_view_sets_unique_install_page_metadata()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DeckFlow.Web", "Views", "Bridge", "Index.cshtml");
        var content = File.ReadAllText(path);

        Assert.Contains("ViewData[\"Title\"] = \"Install the DeckFlow Bridge Extension\"", content, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"Description\"] = \"Install DeckFlow Bridge to import Moxfield decks from your logged-in browser when direct deck imports are unavailable.\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_install_url_is_redirected_to_the_bridge_route_before_static_files()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DeckFlow.Web", "Program.cs");
        var content = File.ReadAllText(path);
        var redirectGuard = "context.Request.Path.Equals(\"/extension-install.html\", StringComparison.OrdinalIgnoreCase)";
        var staticFiles = "app.UseStaticFiles();";

        Assert.Contains(redirectGuard, content, StringComparison.Ordinal);
        Assert.Contains("context.Response.Redirect(\"/deckflow-bridge\", permanent: true);", content, StringComparison.Ordinal);
        Assert.True(content.IndexOf(redirectGuard, StringComparison.Ordinal) < content.IndexOf(staticFiles, StringComparison.Ordinal));
    }
}
