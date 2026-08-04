using System.Xml.Linq;
using DeckFlow.Web.Seo;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Tools;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves crawl directives and the public sitemap for search engines.
/// </summary>
public sealed class SitemapController : Controller
{
    private const string HelpPath = "/help";
    private const string HelpFeatureFlagKey = "tool.help.enabled";

    private readonly IToolRegistry toolRegistry;
    private readonly IFeatureFlagCache featureFlags;
    private readonly IHelpContentService helpContent;

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapController"/> class.
    /// </summary>
    /// <param name="toolRegistry">The canonical source of tool routes and flags.</param>
    /// <param name="featureFlags">The current feature-flag state.</param>
    /// <param name="helpContent">The canonical source of help topics and their visibility rule.</param>
    public SitemapController(
        IToolRegistry toolRegistry,
        IFeatureFlagCache featureFlags,
        IHelpContentService helpContent)
    {
        this.toolRegistry = toolRegistry;
        this.featureFlags = featureFlags;
        this.helpContent = helpContent;
    }

    /// <summary>
    /// Returns the robots.txt crawl directives for the public site.
    /// </summary>
    [HttpGet("/robots.txt")]
    public ContentResult RobotsTxt()
    {
        var baseUrl = BuildBaseUrl();
        var content = string.Join(
            "\n",
            "User-agent: *",
            "Disallow: /Admin",
            "Disallow: /api",
            "Disallow: /swagger",
            string.Empty,
            $"Sitemap: {baseUrl}/sitemap.xml");

        return Content(content, "text/plain");
    }

    /// <summary>
    /// Returns the XML sitemap for the public indexable landing pages.
    /// </summary>
    [HttpGet("/sitemap.xml")]
    public ContentResult SitemapXml()
    {
        var baseUrl = BuildBaseUrl();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var document = new XDocument(
            new XElement(
                ns + "urlset",
                SeoPaths.Indexable.Where(IsReachable)
                    .Concat(helpContent.GetAll()
                        .Where(topic => helpContent.IsTopicVisible(topic, featureFlags))
                        .Select(topic => $"/help/{topic.Slug}"))
                    .Select(path => new XElement(
                    ns + "url",
                    new XElement(ns + "loc", BuildAbsoluteUrl(baseUrl, path))))));

        return Content(document.ToString(SaveOptions.DisableFormatting), "application/xml");
    }

    private string BuildBaseUrl()
    {
        return $"{Request.Scheme}://{Request.Host}";
    }

    private bool IsReachable(string path)
    {
        if (path == HelpPath)
        {
            return featureFlags.IsEnabled(HelpFeatureFlagKey);
        }

        var tool = toolRegistry.All.FirstOrDefault(definition =>
            definition.Route == path || definition.AdditionalRoutes.Contains(path, StringComparer.Ordinal));

        return tool is null || featureFlags.IsEnabled(tool.FlagKey);
    }

    private static string BuildAbsoluteUrl(string baseUrl, string path)
    {
        return path == "/"
            ? $"{baseUrl}/"
            : $"{baseUrl}{path}";
    }
}
