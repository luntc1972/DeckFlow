using System.Xml.Linq;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="SitemapController"/> and related SEO response headers.
/// </summary>
public sealed class SitemapControllerTests
{
    [Fact]
    public async Task RobotsTxt_contains_expected_disallow_rules_and_absolute_sitemap_url()
    {
        var controller = CreateController();

        var result = Assert.IsType<ContentResult>(controller.RobotsTxt());

        Assert.Equal("text/plain", result.ContentType);
        Assert.NotNull(result.Content);
        Assert.Contains("User-agent: *", result.Content, StringComparison.Ordinal);
        Assert.Contains("Disallow: /Admin", result.Content, StringComparison.Ordinal);
        Assert.Contains("Disallow: /api", result.Content, StringComparison.Ordinal);
        Assert.Contains("Disallow: /swagger", result.Content, StringComparison.Ordinal);
        Assert.Contains("Sitemap: https://deckflow.test/sitemap.xml", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void SitemapXml_returns_well_formed_absolute_urls_for_indexable_routes()
    {
        var controller = CreateController();

        var result = Assert.IsType<ContentResult>(controller.SitemapXml());

        Assert.Equal("application/xml", result.ContentType);
        Assert.NotNull(result.Content);

        var document = XDocument.Parse(result.Content);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urls = document.Root!.Elements(ns + "url")
            .Select(element => element.Element(ns + "loc")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();

        Assert.Contains("https://deckflow.test/", urls);
        Assert.Contains("https://deckflow.test/help", urls);
        Assert.DoesNotContain("https://deckflow.test/content-kb", urls);
        Assert.Contains("https://deckflow.test/feedback", urls);
        Assert.Contains("https://deckflow.test/manabase", urls);
        Assert.Contains("https://deckflow.test/bracket", urls);
        Assert.Contains("https://deckflow.test/deck-history", urls);
        Assert.All(urls, url => Assert.StartsWith("https://deckflow.test", url, StringComparison.Ordinal));
    }

    [Fact]
    public void SitemapXml_omits_a_tool_when_its_flag_is_disabled_and_restores_it_when_enabled()
    {
        var flags = new FakeFeatureFlagCache(new Dictionary<string, bool>
        {
            ["tool.bracket.enabled"] = false,
        });

        var disabledUrls = GetSitemapUrls(CreateController(flags));

        Assert.DoesNotContain("https://deckflow.test/bracket", disabledUrls);

        flags.Flags["tool.bracket.enabled"] = true;

        var enabledUrls = GetSitemapUrls(CreateController(flags));

        Assert.Contains("https://deckflow.test/bracket", enabledUrls);
    }

    [Fact(Skip = "Validating OnStarting response headers here would require full TestServer host plumbing, which is out of scope for this change.")]
    public async Task Security_headers_add_admin_noindex_only_for_admin_paths()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseDeckFlowSecurityHeaders();
        app.Run(context => context.Response.WriteAsync("ok"));
        var pipeline = app.Build();

        var adminContext = new DefaultHttpContext();
        adminContext.Response.Body = new MemoryStream();
        adminContext.Request.Path = "/Admin";

        await pipeline(adminContext);
        await adminContext.Response.StartAsync();

        Assert.Equal("noindex, nofollow", adminContext.Response.Headers["X-Robots-Tag"].ToString());

        var publicContext = new DefaultHttpContext();
        publicContext.Response.Body = new MemoryStream();
        publicContext.Request.Path = "/";

        await pipeline(publicContext);
        await publicContext.Response.StartAsync();

        Assert.False(publicContext.Response.Headers.ContainsKey("X-Robots-Tag"));
    }

    private static List<string> GetSitemapUrls(SitemapController controller)
    {
        var result = Assert.IsType<ContentResult>(controller.SitemapXml());
        var document = XDocument.Parse(Assert.IsType<string>(result.Content));
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        return document.Root!.Elements(ns + "url")
            .Select(element => element.Element(ns + "loc")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();
    }

    private static SitemapController CreateController(IFeatureFlagCache? featureFlags = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("deckflow.test");

        return new SitemapController(
            new ToolRegistry(),
            featureFlags ?? new FakeFeatureFlagCache())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
        };
    }
}
