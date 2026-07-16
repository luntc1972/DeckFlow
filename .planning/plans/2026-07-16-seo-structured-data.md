# SEO Structured Data (Slice 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single static `WebSite` JSON-LD blob in `_Layout.cshtml` with per-page-type structured data (Home → WebSite+Organization+SoftwareApplication, tool pages → WebPage+BreadcrumbList, help detail → TechArticle+BreadcrumbList) so DeckFlow becomes eligible for Google rich results.

**Architecture:** A pure static `StructuredDataBuilder.ForPath(...)` maps a request path to a schema.org JSON-LD string built with `System.Text.Json`. `_Layout` calls it once. The indexable-path list is extracted to a shared `SeoPaths` type consumed by both the builder and `SitemapController`, which also closes a real gap: `/manabase` and `/bracket` are currently missing from the sitemap.

**Tech Stack:** C# 12 / .NET 10, ASP.NET Core MVC, Razor, `System.Text.Json` (framework — **no new dependencies**), xUnit.

**Line-ending rule:** All touched/new files use **LF** (repo `.gitattributes` enforces). New C# must pass the changed-lines format gate.

**Build/test commands** (WSL → Windows dotnet; VSTest is unreliable in WSL so run tests via the Windows `dotnet.exe`):
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj`
- Test (Web): `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`

---

## File Structure

**Create:**
- `DeckFlow.Web/Seo/SeoPaths.cs` — single source of truth for indexable paths (sitemap) and the tool-page subset (structured data).
- `DeckFlow.Web/Seo/StructuredDataBuilder.cs` — pure path→JSON-LD builder.
- `DeckFlow.Web.Tests/StructuredDataBuilderTests.cs` — unit tests (flat, namespace `DeckFlow.Web.Tests`).

**Modify:**
- `DeckFlow.Web/Controllers/SitemapController.cs` — consume `SeoPaths.Indexable` instead of its private `IndexablePaths`.
- `DeckFlow.Web.Tests/SitemapControllerTests.cs` — expected count 16 → 18; assert `/manabase` and `/bracket` present.
- `DeckFlow.Web/Views/Shared/_Layout.cshtml` — remove the static `structuredDataJson` const; call the builder.

---

## Canonical path sets (used verbatim below)

**Indexable (sitemap)** — 18 paths, superset of today's 16 plus `/manabase`, `/bracket`:
```
/  /sync  /convert  /card-lookup  /mechanic-lookup  /deck-analysis
/deck-comparison  /cedh-meta-gap  /deck-primer  /suggest-categories
/commander-categories  /judge-questions  /manabase  /bracket
/content-kb  /help  /about  /feedback
```

**Tool pages (WebPage + BreadcrumbList)** — 14 paths (Indexable minus `/`, `/help`, `/about`, `/feedback`):
```
/sync  /convert  /card-lookup  /mechanic-lookup  /deck-analysis
/deck-comparison  /cedh-meta-gap  /deck-primer  /suggest-categories
/commander-categories  /judge-questions  /manabase  /bracket  /content-kb
```

---

## Task 1: Shared `SeoPaths` + sitemap reconciliation

**Files:**
- Create: `DeckFlow.Web/Seo/SeoPaths.cs`
- Modify: `DeckFlow.Web/Controllers/SitemapController.cs`
- Test: `DeckFlow.Web.Tests/SitemapControllerTests.cs`

- [ ] **Step 1: Update the sitemap test to expect the two new paths (failing test first)**

In `DeckFlow.Web.Tests/SitemapControllerTests.cs`, inside `SitemapXml_returns_well_formed_absolute_urls_for_indexable_routes`, change the count assertion and add two containment asserts:

```csharp
        Assert.Contains("https://deckflow.test/", urls);
        Assert.Contains("https://deckflow.test/help", urls);
        Assert.Contains("https://deckflow.test/content-kb", urls);
        Assert.Contains("https://deckflow.test/feedback", urls);
        Assert.Contains("https://deckflow.test/manabase", urls);
        Assert.Contains("https://deckflow.test/bracket", urls);
        Assert.Equal(18, urls.Count);
        Assert.All(urls, url => Assert.StartsWith("https://deckflow.test", url, StringComparison.Ordinal));
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "SitemapXml_returns_well_formed_absolute_urls_for_indexable_routes"`
Expected: FAIL — actual count is 16, `/manabase` not found.

- [ ] **Step 3: Create `SeoPaths.cs`**

```csharp
using System.Collections.Generic;

namespace DeckFlow.Web.Seo;

/// <summary>
/// Single source of truth for the public, indexable page paths. Consumed by
/// <see cref="Controllers.SitemapController"/> (sitemap + robots) and
/// <see cref="StructuredDataBuilder"/> (JSON-LD) so the two never drift apart.
/// </summary>
public static class SeoPaths
{
    /// <summary>
    /// Every indexable landing/tool page, in sitemap order. Includes the home,
    /// help index, about, and feedback pages alongside the tool pages.
    /// </summary>
    public static readonly IReadOnlyList<string> Indexable = new[]
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
        "/manabase",
        "/bracket",
        "/content-kb",
        "/help",
        "/about",
        "/feedback",
    };

    /// <summary>
    /// The tool pages that receive WebPage + BreadcrumbList structured data.
    /// Excludes the home page (richer graph), the help index, about, and
    /// feedback (which fall back to the site-wide WebSite node).
    /// </summary>
    public static readonly IReadOnlySet<string> Tools = new HashSet<string>(StringComparer.Ordinal)
    {
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
        "/manabase",
        "/bracket",
        "/content-kb",
    };
}
```

- [ ] **Step 4: Point `SitemapController` at `SeoPaths.Indexable`**

In `DeckFlow.Web/Controllers/SitemapController.cs`, delete the private `IndexablePaths` array (the `private static readonly string[] IndexablePaths = { ... };` block) and add `using DeckFlow.Web.Seo;` at the top with the other usings. Then in `SitemapXml()` replace `IndexablePaths.Select(...)` with `SeoPaths.Indexable.Select(...)`:

```csharp
using System.Xml.Linq;
using DeckFlow.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Serves crawl directives and the public sitemap for search engines.
/// </summary>
public sealed class SitemapController : Controller
{
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
                SeoPaths.Indexable.Select(path => new XElement(
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
```

- [ ] **Step 5: Run the sitemap tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "SitemapControllerTests"`
Expected: PASS (robots test + sitemap test both green; count is 18).

- [ ] **Step 6: Commit**

```bash
git add DeckFlow.Web/Seo/SeoPaths.cs DeckFlow.Web/Controllers/SitemapController.cs DeckFlow.Web.Tests/SitemapControllerTests.cs
git commit -m "feat(seo): extract shared SeoPaths; add manabase+bracket to sitemap"
```

---

## Task 2: `StructuredDataBuilder` — fallback + home graph

**Files:**
- Create: `DeckFlow.Web/Seo/StructuredDataBuilder.cs`
- Test: `DeckFlow.Web.Tests/StructuredDataBuilderTests.cs`

- [ ] **Step 1: Write failing tests for the fallback and home graph**

Create `DeckFlow.Web.Tests/StructuredDataBuilderTests.cs`:

```csharp
using System.Linq;
using System.Text.Json;
using DeckFlow.Web.Seo;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="StructuredDataBuilder"/> JSON-LD generation.
/// </summary>
public sealed class StructuredDataBuilderTests
{
    private const string BaseUrl = "https://www.deckflow.gg";

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Unmapped_path_returns_website_fallback()
    {
        var json = StructuredDataBuilder.ForPath("/about", $"{BaseUrl}/about", BaseUrl, "About", "About DeckFlow.");

        var root = Parse(json);
        Assert.Equal("WebSite", root.GetProperty("@type").GetString());
        Assert.False(root.TryGetProperty("@graph", out _));
    }

    [Fact]
    public void Home_graph_contains_website_organization_and_free_software_application()
    {
        var json = StructuredDataBuilder.ForPath("/", $"{BaseUrl}/", BaseUrl, "DeckFlow", "Deck analysis for cEDH.");

        var graph = Parse(json).GetProperty("@graph").EnumerateArray().ToList();
        var types = graph.Select(node => node.GetProperty("@type").GetString()).ToList();
        Assert.Contains("WebSite", types);
        Assert.Contains("Organization", types);
        Assert.Contains("SoftwareApplication", types);

        var app = graph.Single(node => node.GetProperty("@type").GetString() == "SoftwareApplication");
        var offer = app.GetProperty("offers");
        Assert.Equal("0", offer.GetProperty("price").GetString());
        Assert.Equal("Offer", offer.GetProperty("@type").GetString());

        var org = graph.Single(node => node.GetProperty("@type").GetString() == "Organization");
        Assert.Equal($"{BaseUrl}/og-image.png", org.GetProperty("logo").GetString());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "StructuredDataBuilderTests"`
Expected: FAIL — `StructuredDataBuilder` does not exist (compile error).

- [ ] **Step 3: Create `StructuredDataBuilder.cs` with fallback + home graph**

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DeckFlow.Web.Seo;

/// <summary>
/// Builds schema.org JSON-LD for the current page, keyed by request path.
/// Pure: no <c>HttpContext</c> or I/O. The result is written into the
/// <c>application/ld+json</c> script tag by <c>_Layout.cshtml</c>.
/// </summary>
public static class StructuredDataBuilder
{
    // Default System.Text.Json encoder escapes '<', '>', and '&'
    // (e.g. "</script>" becomes "</script>"), so serializer-produced
    // JSON is always safe to embed inside a <script> block.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    private const string SchemaContext = "https://schema.org";

    /// <summary>
    /// Returns a JSON-LD string for the given request path. Never returns null;
    /// unmapped paths get the site-wide WebSite node (legacy behavior).
    /// </summary>
    /// <param name="path">Request path, e.g. "/manabase" or "/help/mana-base".</param>
    /// <param name="canonicalUrl">Absolute canonical URL of the current page.</param>
    /// <param name="baseUrl">Scheme + host, e.g. "https://www.deckflow.gg".</param>
    /// <param name="rawTitle">Page title without the " - DeckFlow" suffix; may be null/empty.</param>
    /// <param name="description">Resolved (non-empty) page description.</param>
    public static string ForPath(string path, string canonicalUrl, string baseUrl, string? rawTitle, string description)
    {
        var name = string.IsNullOrWhiteSpace(rawTitle) ? "DeckFlow" : rawTitle!;
        var normalized = NormalizePath(path);

        object graph =
            normalized == "/" ? HomeGraph(baseUrl, description)
            : IsHelpDetail(normalized) ? HelpArticleGraph(canonicalUrl, baseUrl, name, description)
            : SeoPaths.Tools.Contains(normalized) ? ToolPageGraph(canonicalUrl, baseUrl, name, description)
            : WebSiteNode();

        return JsonSerializer.Serialize(graph, SerializerOptions);
    }

    private static bool IsHelpDetail(string normalized) =>
        normalized.StartsWith("/help/", StringComparison.Ordinal) && normalized.Length > "/help/".Length;

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        var lower = path.ToLowerInvariant();
        if (lower.Length > 1 && lower.EndsWith('/'))
        {
            lower = lower.TrimEnd('/');
        }

        return lower.Length == 0 ? "/" : lower;
    }

    private static Dictionary<string, object?> WebSiteNode() => new()
    {
        ["@context"] = SchemaContext,
        ["@type"] = "WebSite",
        ["name"] = "DeckFlow",
        ["url"] = "https://www.deckflow.gg",
        ["description"] = "DeckFlow — Magic: The Gathering deck analysis for cEDH and Commander. Compare, analyze, and generate ChatGPT-ready deck prompts.",
    };

    private static Dictionary<string, object?> HomeGraph(string baseUrl, string description) => new()
    {
        ["@context"] = SchemaContext,
        ["@graph"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["@type"] = "WebSite",
                ["@id"] = $"{baseUrl}/#website",
                ["name"] = "DeckFlow",
                ["url"] = $"{baseUrl}/",
                ["description"] = description,
                ["publisher"] = new Dictionary<string, object?> { ["@id"] = $"{baseUrl}/#organization" },
            },
            new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["@id"] = $"{baseUrl}/#organization",
                ["name"] = "DeckFlow",
                ["url"] = $"{baseUrl}/",
                ["logo"] = $"{baseUrl}/og-image.png",
            },
            new Dictionary<string, object?>
            {
                ["@type"] = "SoftwareApplication",
                ["@id"] = $"{baseUrl}/#app",
                ["name"] = "DeckFlow",
                ["url"] = $"{baseUrl}/",
                ["applicationCategory"] = "GameApplication",
                ["operatingSystem"] = "Web",
                ["description"] = description,
                ["offers"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Offer",
                    ["price"] = "0",
                    ["priceCurrency"] = "USD",
                },
            },
        },
    };

    // Placeholder members completed in Tasks 3 and 4; declared here so the file compiles.
    private static Dictionary<string, object?> ToolPageGraph(string canonicalUrl, string baseUrl, string name, string description) =>
        WebSiteNode();

    private static Dictionary<string, object?> HelpArticleGraph(string canonicalUrl, string baseUrl, string name, string description) =>
        WebSiteNode();
}
```

> Note: `ToolPageGraph` and `HelpArticleGraph` are stubbed to `WebSiteNode()` here only so the file compiles and Task 2's two tests pass. Tasks 3 and 4 replace the stub bodies (and their tests prove the real output). Do not leave them stubbed.

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "StructuredDataBuilderTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Seo/StructuredDataBuilder.cs DeckFlow.Web.Tests/StructuredDataBuilderTests.cs
git commit -m "feat(seo): add StructuredDataBuilder with website fallback and home graph"
```

---

## Task 3: Tool-page graph (WebPage + BreadcrumbList)

**Files:**
- Modify: `DeckFlow.Web/Seo/StructuredDataBuilder.cs`
- Test: `DeckFlow.Web.Tests/StructuredDataBuilderTests.cs`

- [ ] **Step 1: Add a failing test for the tool-page graph**

Append to `StructuredDataBuilderTests`:

```csharp
    [Fact]
    public void Tool_path_returns_webpage_and_breadcrumb_depth_two()
    {
        var json = StructuredDataBuilder.ForPath(
            "/manabase", $"{BaseUrl}/manabase", BaseUrl, "MTG Commander Mana Base Analyzer", "Analyze your mana base.");

        var graph = Parse(json).GetProperty("@graph").EnumerateArray().ToList();
        var types = graph.Select(node => node.GetProperty("@type").GetString()).ToList();
        Assert.Contains("WebPage", types);
        Assert.Contains("BreadcrumbList", types);

        var webPage = graph.Single(node => node.GetProperty("@type").GetString() == "WebPage");
        Assert.Equal("MTG Commander Mana Base Analyzer", webPage.GetProperty("name").GetString());
        Assert.Equal($"{BaseUrl}/manabase", webPage.GetProperty("url").GetString());

        var crumbs = graph.Single(node => node.GetProperty("@type").GetString() == "BreadcrumbList")
            .GetProperty("itemListElement").EnumerateArray().ToList();
        Assert.Equal(2, crumbs.Count);
        Assert.Equal(1, crumbs[0].GetProperty("position").GetInt32());
        Assert.Equal("Home", crumbs[0].GetProperty("name").GetString());
        Assert.Equal($"{BaseUrl}/", crumbs[0].GetProperty("item").GetString());
        Assert.Equal("MTG Commander Mana Base Analyzer", crumbs[1].GetProperty("name").GetString());
        Assert.Equal($"{BaseUrl}/manabase", crumbs[1].GetProperty("item").GetString());
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "Tool_path_returns_webpage_and_breadcrumb_depth_two"`
Expected: FAIL — stub returns a `WebSite` node, so `@graph` is missing.

- [ ] **Step 3: Replace the `ToolPageGraph` stub + add the `Breadcrumb` helper**

In `StructuredDataBuilder.cs`, replace the `ToolPageGraph` stub with the real body and add a private `Breadcrumb` helper (place it just above the `ToolPageGraph` method):

```csharp
    private static Dictionary<string, object?> Breadcrumb(IReadOnlyList<(string Name, string Url)> items)
    {
        var elements = new List<object>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            elements.Add(new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = i + 1,
                ["name"] = items[i].Name,
                ["item"] = items[i].Url,
            });
        }

        return new Dictionary<string, object?>
        {
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = elements,
        };
    }

    private static Dictionary<string, object?> ToolPageGraph(string canonicalUrl, string baseUrl, string name, string description) => new()
    {
        ["@context"] = SchemaContext,
        ["@graph"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["@type"] = "WebPage",
                ["@id"] = $"{canonicalUrl}#webpage",
                ["name"] = name,
                ["description"] = description,
                ["url"] = canonicalUrl,
                ["isPartOf"] = new Dictionary<string, object?> { ["@id"] = $"{baseUrl}/#website" },
            },
            Breadcrumb(new[]
            {
                ("Home", $"{baseUrl}/"),
                (name, canonicalUrl),
            }),
        },
    };
```

- [ ] **Step 4: Run to verify it passes**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "StructuredDataBuilderTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Seo/StructuredDataBuilder.cs DeckFlow.Web.Tests/StructuredDataBuilderTests.cs
git commit -m "feat(seo): emit WebPage + BreadcrumbList for tool pages"
```

---

## Task 4: Help-detail graph (TechArticle + BreadcrumbList)

**Files:**
- Modify: `DeckFlow.Web/Seo/StructuredDataBuilder.cs`
- Test: `DeckFlow.Web.Tests/StructuredDataBuilderTests.cs`

- [ ] **Step 1: Add a failing test for the help-detail graph**

Append to `StructuredDataBuilderTests`:

```csharp
    [Fact]
    public void Help_detail_returns_techarticle_and_breadcrumb_depth_three()
    {
        var json = StructuredDataBuilder.ForPath(
            "/help/mana-base", $"{BaseUrl}/help/mana-base", BaseUrl, "Mana Base Help", "How the analyzer works.");

        var graph = Parse(json).GetProperty("@graph").EnumerateArray().ToList();
        var types = graph.Select(node => node.GetProperty("@type").GetString()).ToList();
        Assert.Contains("TechArticle", types);
        Assert.Contains("BreadcrumbList", types);

        var article = graph.Single(node => node.GetProperty("@type").GetString() == "TechArticle");
        Assert.Equal("Mana Base Help", article.GetProperty("headline").GetString());

        var crumbs = graph.Single(node => node.GetProperty("@type").GetString() == "BreadcrumbList")
            .GetProperty("itemListElement").EnumerateArray().ToList();
        Assert.Equal(3, crumbs.Count);
        Assert.Equal("Home", crumbs[0].GetProperty("name").GetString());
        Assert.Equal("Help", crumbs[1].GetProperty("name").GetString());
        Assert.Equal($"{BaseUrl}/help", crumbs[1].GetProperty("item").GetString());
        Assert.Equal("Mana Base Help", crumbs[2].GetProperty("name").GetString());
    }

    [Fact]
    public void Help_index_is_not_treated_as_detail()
    {
        var json = StructuredDataBuilder.ForPath("/help", $"{BaseUrl}/help", BaseUrl, "Help", "Help index.");
        Assert.Equal("WebSite", Parse(json).GetProperty("@type").GetString());
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "Help_detail_returns_techarticle_and_breadcrumb_depth_three"`
Expected: FAIL — stub returns `WebSite`.

- [ ] **Step 3: Replace the `HelpArticleGraph` stub**

In `StructuredDataBuilder.cs`, replace the `HelpArticleGraph` stub body with:

```csharp
    private static Dictionary<string, object?> HelpArticleGraph(string canonicalUrl, string baseUrl, string name, string description) => new()
    {
        ["@context"] = SchemaContext,
        ["@graph"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["@type"] = "TechArticle",
                ["headline"] = name,
                ["description"] = description,
                ["url"] = canonicalUrl,
            },
            Breadcrumb(new[]
            {
                ("Home", $"{baseUrl}/"),
                ("Help", $"{baseUrl}/help"),
                (name, canonicalUrl),
            }),
        },
    };
```

- [ ] **Step 4: Run to verify it passes**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "StructuredDataBuilderTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Seo/StructuredDataBuilder.cs DeckFlow.Web.Tests/StructuredDataBuilderTests.cs
git commit -m "feat(seo): emit TechArticle + BreadcrumbList for help detail pages"
```

---

## Task 5: JSON-validity + script-escaping guard tests

**Files:**
- Test: `DeckFlow.Web.Tests/StructuredDataBuilderTests.cs` (no production change — these lock in safety)

- [ ] **Step 1: Add the guard tests**

Append to `StructuredDataBuilderTests`:

```csharp
    [Theory]
    [InlineData("/")]
    [InlineData("/manabase")]
    [InlineData("/help/mana-base")]
    [InlineData("/about")]
    [InlineData("/feedback")]
    public void Every_branch_emits_parseable_json(string path)
    {
        var json = StructuredDataBuilder.ForPath(path, $"{BaseUrl}{path}", BaseUrl, "Title", "Description.");

        // Throws if the output is not valid JSON.
        using var _ = JsonDocument.Parse(json);
        Assert.StartsWith("{", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Title_with_script_tag_is_escaped_and_json_stays_valid()
    {
        var hostile = "Pwned</script><script>alert(1)</script>";

        var json = StructuredDataBuilder.ForPath("/manabase", $"{BaseUrl}/manabase", BaseUrl, hostile, "Desc.");

        using var _ = JsonDocument.Parse(json);
        // The default encoder escapes '<' to <, so no literal closing tag survives.
        Assert.DoesNotContain("</script>", json, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 2: Run to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "StructuredDataBuilderTests"`
Expected: PASS (all builder tests; 8 total).

- [ ] **Step 3: Commit**

```bash
git add DeckFlow.Web.Tests/StructuredDataBuilderTests.cs
git commit -m "test(seo): guard JSON validity and script-tag escaping in structured data"
```

---

## Task 6: Wire the builder into `_Layout.cshtml`

**Files:**
- Modify: `DeckFlow.Web/Views/Shared/_Layout.cshtml`

- [ ] **Step 1: Remove the static const**

Delete this line (currently `_Layout.cshtml:4`):

```csharp
    const string structuredDataJson = "{\"@context\":\"https://schema.org\",\"@type\":\"WebSite\",\"name\":\"DeckFlow\",\"url\":\"https://www.deckflow.gg\",\"description\":\"DeckFlow — Magic: The Gathering deck analysis for cEDH and Commander. Compare, analyze, and generate ChatGPT-ready deck prompts.\"}";
```

Keep the `defaultDescription` const on line 3.

- [ ] **Step 2: Compute the JSON-LD from the builder**

In the `@{ ... }` block, immediately after the existing `var openGraphImageUrl = ...;` line, add:

```csharp
    var rawPageTitle = Convert.ToString(ViewData["Title"]);
    var structuredDataJson = DeckFlow.Web.Seo.StructuredDataBuilder.ForPath(
        requestPath.Value ?? "/",
        canonicalUrl,
        $"{requestScheme}://{requestHost}",
        rawPageTitle,
        pageDescription);
```

(`requestPath`, `canonicalUrl`, `requestScheme`, `requestHost`, `pageDescription` are all already declared above this point in the same block.)

- [ ] **Step 3: Confirm the render line is unchanged**

The existing line stays exactly as-is:

```html
    <script type="application/ld+json">@Html.Raw(structuredDataJson)</script>
```

- [ ] **Step 4: Build the web project**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj`
Expected: Build succeeded, 0 warnings / 0 errors.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Views/Shared/_Layout.cshtml
git commit -m "feat(seo): render per-page structured data from StructuredDataBuilder"
```

---

## Task 7: Full verification, live check, simplify

**Files:** none (verification only)

- [ ] **Step 1: Full web test suite**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`
Expected: all pass (previous green count + 8 new builder tests; sitemap now asserts 18).

- [ ] **Step 2: Start the test server (no browser window)**

Run: `scripts/run-web-test.sh` (sets `DECKFLOW_DISABLE_AUTO_BROWSER=true`). Wait for Kestrel to report listening.

- [ ] **Step 3: Curl the ld+json on three page types and eyeball it**

```bash
for p in "" "manabase" "help"; do
  echo "=== /$p ==="
  curl -s "http://localhost:5173/$p" | grep -A1 'application/ld+json'
done
# Then a real help slug (pick one that exists):
curl -s "http://localhost:5173/help" | grep -oE '/help/[a-z0-9-]+' | head -1
```
Expected: `/` shows an `@graph` with WebSite/Organization/SoftwareApplication; `/manabase` shows WebPage+BreadcrumbList; a `/help/<slug>` shows TechArticle+BreadcrumbList. Stop the server when done.

- [ ] **Step 4: Validate the JSON-LD structure**

Paste the `/` and `/manabase` JSON-LD into Google's Rich Results Test (https://search.google.com/test/rich-results) or the Schema Markup Validator (https://validator.schema.org/). Expected: no errors; SoftwareApplication and Breadcrumb detected. (Operator step — record result; this also seeds the slice-4 playbook.)

- [ ] **Step 5: Run `/simplify` on the diff**

Run the `/simplify` skill against the branch diff; apply any reduction it finds (e.g. collapsing duplicated node-construction). Re-run Task 7 Step 1 after any change.

- [ ] **Step 6: Update README if behavior is user-visible**

Structured data is invisible to page visitors, so a README behavior change is unlikely required. If the README has an SEO/《discoverability》note, add one line that per-page JSON-LD (SoftwareApplication/Breadcrumb/TechArticle) is emitted. Otherwise skip and note "no README change — no user-visible behavior."

- [ ] **Step 7: Final commit if anything changed in steps 5–6**

```bash
git add -A
git commit -m "chore(seo): simplify structured data builder + docs"
```

---

## Self-Review notes (author)

- **Spec coverage:** Component A (builder) → Tasks 2–5; `_Layout` wiring (Component B of spec) → Task 6; shared path list + drift fix → Task 1; tests → Tasks 2–5; regression (sitemap/metadata) → Task 1 + Task 7 Step 1. Sitemap enrichment (spec Component B "sitemap") was dropped per user ("A only"); the only sitemap change here is the coverage fix (+manabase/+bracket), which is required for those tool pages to carry structured data consistently and be indexed.
- **Open questions resolved:** tool-path slugs pinned from live route attributes; `/manabase` + `/bracket` confirmed absent from sitemap and added. Organization logo → `~/og-image.png` (existing asset). Help breadcrumb middle node → `/help` (valid route).
- **Type consistency:** `StructuredDataBuilder.ForPath(string, string, string, string?, string)`, `SeoPaths.Indexable` / `SeoPaths.Tools`, private helpers `HomeGraph`/`ToolPageGraph`/`HelpArticleGraph`/`Breadcrumb`/`WebSiteNode`/`NormalizePath`/`IsHelpDetail` used consistently across tasks. Stubs in Task 2 are explicitly replaced in Tasks 3–4.
- **No new dependencies.** LF endings. Changed-lines format gate applies to new C#.
