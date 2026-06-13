---
slug: seo
status: complete
date: 2026-06-13
implementer: codex (gpt-5.4 medium)
reviewer: claude
---

# SEO Foundation — Summary

All four approved SEO areas implemented by Codex, reviewed by Claude.

## Changed files
- `DeckFlow.Web/Views/Shared/_Layout.cshtml` — meta description, canonical, OG, Twitter, JSON-LD
- `DeckFlow.Web/Controllers/SitemapController.cs` (NEW) — `/robots.txt` + `/sitemap.xml`
- `DeckFlow.Web/Infrastructure/SecurityHeadersApplicationBuilderExtensions.cs` — JSON-LD CSP hash + Admin `X-Robots-Tag: noindex, nofollow`
- `DeckFlow.Web.Tests/SitemapControllerTests.cs` (NEW)

## Verification
- JSON-LD CSP SHA-256 `zWyI4r0CGRSGSFAtrv/wt8Cm1YrFJECDgxCRIDMXypg=` independently recomputed by Claude → MATCH (no silent CSP block).
- `dotnet build DeckFlow.Web` → 0 warnings / 0 errors.
- `SitemapControllerTests` → 2 passed, 1 skipped.
- Scope clean: only the 4 fenced files touched; HEAD unchanged.

## Skipped / deferred
- Test `Security_headers_add_admin_noindex_only_for_admin_paths` SKIPPED — needs full TestServer host fixture the project lacks; no new test infra added per fence.
- Per-page `ViewData["Description"]` on landing views NOT added (optional) — all pages use the site-wide default description for now.

## Follow-ups for user
- **Drop an OG share image** at `DeckFlow.Web/wwwroot/og-image.png` (≥1200×630). `og:image`/`twitter:image` already reference it; previews are imageless until the asset exists.
- If the JSON-LD block text ever changes, recompute the CSP SHA-256 or the block will be blocked.
- Consider per-page descriptions later for stronger SEO.

## Notes
- Sitemap XML omits the `<?xml?>` declaration (XDocument.ToString behavior); crawlers accept this.
- Not yet committed — pending user visual check (view-source on a page + /robots.txt + /sitemap.xml).
