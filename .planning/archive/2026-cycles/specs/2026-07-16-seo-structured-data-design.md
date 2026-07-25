# SEO Slice 1 — On-Page Structured Data (JSON-LD)

**Date:** 2026-07-16
**Status:** Approved design, pre-implementation
**Slice:** 1 of 4 in the "spread the word" workstream (2: in-app shareability, 3: content/landing pages, 4: off-page playbook — each its own spec)

## Problem

DeckFlow's on-page SEO is otherwise complete (per-page titles/descriptions, canonical, OpenGraph, Twitter cards, `sitemap.xml`, `robots.txt`). The one weak spot is structured data: `_Layout.cshtml` emits a single hard-coded `WebSite` JSON-LD blob on every page (`_Layout.cshtml:4`). That makes the whole site ineligible for the rich results Google grants to `SoftwareApplication`, `BreadcrumbList`, and article markup.

This slice replaces the static blob with per-page-type JSON-LD to unlock rich-result eligibility. No new dependencies (pure string/JSON). Per-page OG images are explicitly **out of scope** — they move to slice 2 (shareability), where share buttons live and an image-generation decision belongs.

## Goal

Every indexable page emits JSON-LD appropriate to its page type, valid against schema.org, so search engines can render richer results and better understand the site graph.

## Non-Goals

- Per-page OG images (slice 2)
- Sitemap `lastmod`/`priority`/`changefreq` (modern Google largely ignores these; dropped as YAGNI)
- New pages or content (slice 3)
- Off-page submission/distribution (slice 4)
- Any new NuGet/npm dependency

## Design

### Component A — centralized `StructuredDataBuilder`

New file `DeckFlow.Web/Seo/StructuredDataBuilder.cs` — a static helper that maps a request path to a JSON-LD string. `_Layout.cshtml` calls it once and renders the result, replacing the current `structuredDataJson` const.

**Why centralized, not per-view:** the existing 17 views already carry `ViewData["Title"]`/`["Description"]`. Threading a third `ViewData["StructuredData"]` through all of them is churn and easy to forget on new pages. A single path-keyed builder is Open/Closed — a new page type is a new `case`, and existing views/logic stay untouched. The builder is pure (no `HttpContext`, no I/O), so it is trivially unit-testable.

**Signature (indicative):**

```csharp
public static string ForPath(
    string path,            // requestPath, e.g. "/manabase" or "/help/manabase"
    string canonicalUrl,    // absolute canonical for the current page
    string baseUrl,         // scheme+host, for building sibling URLs (breadcrumb home, logo)
    string pageTitle,       // computed "<title> - DeckFlow" already available in _Layout
    string pageDescription) // computed description already available in _Layout
```

Returns a compact JSON-LD string (single node or `@graph`). Never returns null — unmapped paths get the `WebSite` fallback, preserving today's behavior.

**Emitted JSON-LD by page type:**

| Page type | Match | JSON-LD |
|-----------|-------|---------|
| Home | `path == "/"` | `@graph`: `WebSite` + `Organization` (name, url, `logo`) + `SoftwareApplication` (name DeckFlow, `applicationCategory` `GameApplication`, `operatingSystem` `Web`, `offers` `{@type: Offer, price: "0", priceCurrency: "USD"}`, description) |
| Tool page | path in the known tool set (`/sync`, `/convert`, `/card-lookup`, `/mechanic-lookup`, `/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/deck-primer`, `/suggest-categories`, `/commander-categories`, `/judge-questions`, `/content-kb`) | `WebPage` (name=pageTitle, description=pageDescription, url=canonical, `isPartOf` WebSite) + `BreadcrumbList` (Home › Tool) |
| Help detail | `path` starts with `/help/` | `TechArticle` (headline=pageTitle, description=pageDescription, url=canonical) + `BreadcrumbList` (Home › Help › Topic) |
| Help index / About / Feedback / unmapped | else | `WebSite` fallback (current behavior) |

Notes:
- The tool-path set is the same list `SitemapController` already hard-codes. To keep a single source of truth, that list is extracted to a shared constant (e.g. `DeckFlow.Web/Seo/SiteMapPaths.cs` or a shared static) consumed by both `SitemapController` and `StructuredDataBuilder`. This is a small, in-scope refactor that removes a lurking drift risk — the two lists must agree.
- JSON is built with `System.Text.Json` (already in the framework) and `JavaScriptEncoder`-safe escaping, not hand-concatenated strings, so titles/descriptions with quotes cannot break the `<script>` block.
- Output is emitted via `@Html.Raw(...)` exactly as today. Because the JSON is serializer-produced and the only interpolated values are server-computed page titles/descriptions (not user input), there is no injection surface beyond what exists now. `</script>`-in-content is neutralized by `JavaScriptEncoder.Default` (escapes `<`).

### Component B — `_Layout.cshtml` wiring

Replace:
```csharp
const string structuredDataJson = "{...WebSite...}";
```
with a call after the existing `canonicalUrl` / `openGraphImageUrl` computation:
```csharp
var structuredDataJson = DeckFlow.Web.Seo.StructuredDataBuilder.ForPath(
    requestPath.Value ?? "/", canonicalUrl, $"{requestScheme}://{requestHost}", pageTitle, pageDescription);
```
The `<script type="application/ld+json">@Html.Raw(structuredDataJson)</script>` line is unchanged.

## Data Flow

Request → MVC renders view → `_Layout` computes title/description/canonical (unchanged) → `_Layout` calls `StructuredDataBuilder.ForPath(...)` → builder switches on path, serializes the matching schema.org object graph → `_Layout` writes it into the `ld+json` script tag. No request state mutated; builder is a pure function of its arguments.

## Error Handling

- Builder never throws on unmapped paths — returns the `WebSite` fallback.
- Null/empty title or description fall back to the site defaults already computed in `_Layout` (builder receives the already-resolved values, so this is inherited).
- Serialization uses a single shared `JsonSerializerOptions` (no per-call allocation of options).

## Testing

New `DeckFlow.Web.Tests/Seo/StructuredDataBuilderTests.cs` (xUnit, no new deps):
- Home returns an `@graph` containing `WebSite`, `Organization`, and `SoftwareApplication` with `offers.price == "0"`.
- A representative tool path (`/manabase`... note: `/manabase` is served but is it in the tool set? confirm at implementation — see Open Questions) returns `WebPage` + `BreadcrumbList` of depth 2.
- A help path (`/help/some-slug`) returns `TechArticle` + `BreadcrumbList` of depth 3.
- Unmapped path (`/about`) returns the `WebSite` fallback.
- Output of every branch parses as valid JSON (`JsonDocument.Parse` does not throw).
- A title containing `"` and `</script>` serializes without breaking JSON and without a literal `</script>` in the output.

Regression: existing `SitemapControllerTests` and `PageMetadataViewTests` must stay green — especially if the path list is extracted to a shared constant, `SitemapController` behavior must be byte-identical.

## Backward Compatibility

- Unmapped paths keep the exact `WebSite` output → no regression for pages not explicitly upgraded.
- No route, URL, or response-header change. Only the `ld+json` script body changes on home/tool/help pages.
- No schema migration, no config, no new env var.

## Open Questions / Assumptions

- **Route vs. path set:** the sitemap uses `/card-lookup`, `/deck-analysis`, etc. Confirm at implementation that `_Layout`'s `requestPath.Value` matches these exactly (leading slash, no trailing slash, lowercase) so tool-page matching fires. `/manabase` appears in views but the sitemap list uses different tool slugs — reconcile the canonical tool-path list during implementation from the actual routes, not assumptions.
- **Organization logo:** `Organization.logo` needs an absolute image URL. Reuse `~/og-image.png` (already absolute-ized in `_Layout`) or a dedicated logo asset — decide at implementation; og-image is an acceptable default.
- **Help breadcrumb middle node:** `Home › Help › Topic` assumes `/help` is a valid intermediate URL (it is — the help index route exists).
```
