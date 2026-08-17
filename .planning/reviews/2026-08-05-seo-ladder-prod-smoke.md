# SEO ladder — post-deploy prod smoke (2026-08-05)

Deploy `dep-d9pq5mrbc2fs73apob40`, commit `a5f51b0d`, live 20:55:30Z.
Target: https://www.deckflow.gg

## Pass

| Check | `/deckflow-bridge` | `/set-upgrade-analysis` |
| --- | --- | --- |
| HTTP status | 200 | 200 |
| `<h1>` count | 1 | 1 |
| `<title>` unique | ✅ | ✅ |
| meta description present + unique | ✅ | ✅ |
| canonical absolute, https, www | ✅ | ✅ |
| `og:url` matches canonical | ✅ | ✅ |
| JSON-LD present and parses | ✅ | ✅ |
| listed in `/sitemap.xml` | ✅ | ✅ |

`/` and `/sitemap.xml` also 200.

## Finding 1 (MEDIUM) — both new landing pages emit the `WebSite` fallback, not `WebPage`+`BreadcrumbList`

Observed in prod: each page carries exactly one JSON-LD block, `@type: WebSite` — the
unmapped-path fallback, identical on both pages and carrying no page-specific data.

Mechanism, confirmed in source:

- `SeoPaths.cs:27` — `new("/set-upgrade-analysis", true, false)`
- `SeoPaths.cs:39` — `new("/deckflow-bridge", true, false)`

The third positional arg is `IsTool` (`SeoPage(string Path, bool IsIndexable, bool IsTool)`,
`SeoPaths.cs:61`). Both are registered indexable-but-not-tool, so they fall out of
`SeoPaths.Tools` (`:57`), and `StructuredDataBuilder.ForPath:41` —
`SeoPaths.Tools.Contains(normalized) ? ToolPageGraph(...)` — falls through to
`WebSiteNode` (`:44`, `:50`).

Two consequences, not one:

1. No `WebPage` or `BreadcrumbList` node. BreadcrumbList is what drives breadcrumb rich
   results; a bare duplicated `WebSite` node is the weakest structured data of the three
   shapes the builder can emit, on the two pages the ladder added specifically to rank.
2. **No share bar.** `SeoPaths.cs:90` — `IsShareablePage` is
   `normalized == "/" || Tools.Contains(normalized)`. Same `Tools` set, same exclusion.

Defensible for `/deckflow-bridge` (an install page is not a tool and arguably not shareable).
Harder to defend for `/set-upgrade-analysis`, which exists as an SEO landing page for a
deck-analysis mode.

**Not covered by tests.** `StructuredDataBuilderFallbackTests.cs` asserts only that the
fallback uses the request base URL (`:11`, `:18`); nothing asserts these two paths *should*
be fallback, so the current behavior is unpinned rather than deliberate.

Fix, if wanted: flip `IsTool` to `true` for `/set-upgrade-analysis` (and decide separately on
`/deckflow-bridge`), then confirm the share bar appearing is intended — the flag moves both
facts at once. Codex work; credits reset 2026-08-10 09:48.

## Not a finding — recorded so it is not re-investigated

`grep 'application/ld+json'` returns 0 matches on **every** DeckFlow page including `/`.
Razor HTML-encodes the `+`, so the markup ships as `application/ld&#x2B;json`. The HTML
parser decodes the entity, the type is valid, and Google parses it. Grep for `ld&#x2B;json`
or `schema.org` instead.

## Method

`curl` against prod for status/markup; JSON-LD extracted with a regex on the encoded script
tag, `html.unescape`d, and `json.loads`ed to prove it parses rather than merely exists.
Source claims checked against the working tree at `a5f51b0d`.
