using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves crawl directives and the public sitemap for search engines.
/// </summary>
public sealed class SitemapController : Controller
{
    private static readonly string[] IndexablePaths =
    {
        "/",
        "/sync",
        "/convert",
        "/card-lookup",
        "/mechanic-lookup",
        "/deck-analysis",
        "/deck-comparison",
        "/cedh-meta-gap",
        "/deck-primer",
        "/suggest-categories",
        "/commander-categories",
        "/judge-questions",
        "/content-kb",
        "/help",
        "/about",
        "/feedback",
    };

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
                IndexablePaths.Select(path => new XElement(
                    ns + "url",
                    new XElement(ns + "loc", BuildAbsoluteUrl(baseUrl, path))))));

        return Content(document.ToString(SaveOptions.DisableFormatting), "application/xml");
    }

    private string BuildBaseUrl()
    {
        return $"{Request.Scheme}://{Request.Host}";
    }

    private static string BuildAbsoluteUrl(string baseUrl, string path)
    {
        return path == "/"
            ? $"{baseUrl}/"
            : $"{baseUrl}{path}";
    }
}
