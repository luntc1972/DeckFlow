---
slug: seo
created: 2026-06-13
mode: quick
implementer: codex (gpt-5.4 medium)
reviewer: claude
---

# Quick Task: SEO Foundation

Add baseline SEO to public DeckFlow site (deckflow.gg). Site currently has only
`<title>` + viewport. Four areas, all greenfield.

## Scope (user-approved, all four)

1. **Meta + OG/Twitter** — per-page `<meta name="description">` + `<link rel="canonical">`
   + Open Graph + Twitter Card tags in `_Layout.cshtml`, driven by `ViewData["Description"]`
   with a sensible site-wide default. Absolute URLs built from the forwarded-aware request
   (scheme + host) so they are correct on prod and localhost — no hardcoded base URL.
2. **robots.txt + sitemap.xml** — served dynamically by a new `SitemapController`
   (`GET /robots.txt`, `GET /sitemap.xml`). robots: `Disallow: /Admin`, `Disallow: /api`,
   `Disallow: /swagger`; `Sitemap:` line with request-absolute URL. sitemap: the indexable
   landing routes (list below), absolute URLs from request.
3. **JSON-LD structured data** — one site-wide `WebApplication` (or `WebSite`+`Organization`)
   `<script type="application/ld+json">` block in `_Layout`. CONSTANT content so its SHA-256
   is stable. The hash MUST be added to CSP `script-src` (current CSP is `script-src 'self'`,
   which blocks inline scripts incl. ld+json).
4. **Admin noindex** — `X-Robots-Tag: noindex, nofollow` response header for `/Admin/*`
   in `SecurityHeadersApplicationBuilderExtensions`.

## Indexable routes for sitemap (verified [HttpGet])

`/`, `/sync`, `/convert`, `/card-lookup`, `/mechanic-lookup`, `/deck-analysis`,
`/deck-comparison`, `/cedh-meta-gap`, `/deck-primer`, `/suggest-categories`,
`/commander-categories`, `/judge-questions`, `/content-kb`, `/help`, `/about`, `/feedback`

Exclude: search/download/POST endpoints, `/api/*`, `/Admin/*`, `/swagger`, `/help/{slug}`
and `/content-kb/{id}` detail pages (dynamic — defer).

## Key constraints

- CSP `img-src 'self' data:` → og:image must be self-hosted. Only `favicon.ico` exists; no
  share image. og:image references `~/og-image.png` (conventional path) — note in SUMMARY that
  the user must drop a ≥1200×630 PNG there for rich previews; tag is harmless until then.
- Build absolute URLs via `Request.Scheme` + `Request.Host` (UseForwardedHeaders already
  populates these correctly behind the proxy). Do NOT hardcode deckflow.gg.
- JSON-LD block content must be constant so one CSP hash covers it. If per-page JSON-LD is
  wanted later, that needs a nonce — out of scope here.
- Preserve existing `_Layout` title/theme/CSP logic and LF line endings. .editorconfig pinned.

## Files (ALLOWED SET — fence)

- `DeckFlow.Web/Views/Shared/_Layout.cshtml` — meta/canonical/OG/Twitter/JSON-LD in `<head>`
- `DeckFlow.Web/Controllers/SitemapController.cs` — NEW; /robots.txt + /sitemap.xml
- `DeckFlow.Web/Infrastructure/SecurityHeadersApplicationBuilderExtensions.cs` — JSON-LD hash
  in CSP script-src + Admin X-Robots-Tag
- `DeckFlow.Web.Tests/SitemapControllerTests.cs` — NEW; robots + sitemap + admin-noindex tests
- (optional) `ViewData["Description"]` added to the main landing views for per-page descriptions

## Tests

- SitemapController: robots body has Disallow lines + Sitemap absolute URL; sitemap is
  well-formed XML, contains the landing routes, uses absolute URLs from forwarded host.
- Admin path → X-Robots-Tag noindex present; non-admin path → absent.

## Out of scope

- OG share image asset (binary), per-page JSON-LD, /help/{slug} + content-kb detail in sitemap,
  hreflang, AMP, any config/env changes.
