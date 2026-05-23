---
phase: 12-ai-agnostic-url-page-rename
reviewed: 2026-05-16T20:00:00Z
depth: standard
files_reviewed: 14
files_reviewed_list:
  - CLAUDE.md
  - DeckFlow.Web/Controllers/DeckController.cs
  - DeckFlow.Web/Help/cedh-meta-gap.md
  - DeckFlow.Web/Help/deck-analysis.md
  - DeckFlow.Web/Help/deck-comparison.md
  - DeckFlow.Web/Program.cs
  - DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs
  - DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml
  - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
  - DeckFlow.Web/Views/Deck/DeckComparison.cshtml
  - DeckFlow.Web/Views/Deck/Home.cshtml
  - DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml
  - DeckFlow.Web/wwwroot/css/site-common.css
  - README.md
findings:
  critical: 0
  warning: 2
  info: 4
  total: 6
status: issues_found
---

# Phase 12: Code Review Report

**Reviewed:** 2026-05-16T20:00:00Z
**Depth:** standard
**Files Reviewed:** 14
**Status:** issues_found

## Summary

Phase 12 renames `chatgpt-*` URL slugs to AI-agnostic ones (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`), renames 3 Razor views and 2 Help files, updates 39 `View(...)` literal references, adds 11 `UseRewriter` 301 redirects, renames packet zip filename helpers, and refreshes copy on Home + the three workflow pages with a new `.page-lede` block. The mechanical sweep is largely complete and consistent: every renamed route in `DeckController` has a matching `View(...)` literal pointing at the renamed `.cshtml`, all hub-card hrefs in `Home.cshtml` and `_DeckToolTabs.cshtml` point at the new slugs, and form `action`/`formaction`/`data-clear-href`/`data-upload-action` attributes in the three workflow views are consistent with the new POST routes.

Two warnings remain: (1) README still documents two of these pages by the controller-action-style URL `/Deck/ChatGptPackets` and `/Deck/ChatGptDeckComparison`, which **no longer resolve** because the attribute-routed `[HttpGet("/deck-analysis")]` on those action methods removes them from the conventional route table — those URLs now 404 in production; (2) the `UseRewriter` regex anchors `^chatgpt-packets$` etc. do not tolerate a trailing slash, so `/chatgpt-packets/` lands on the default 404 instead of redirecting (minor — bookmarked variant was unlikely to have a trailing slash since the prior route did not).

Lower-priority info items cover a pre-existing typo in the renamed `DeckAnalysis.cshtml`, the awkward but functional doubled-segment fallback in `SuggestCedhMetaGapZipFileName` when commander name is blank, the absence of test coverage for the new 301 redirects, and a doc-comment-after-attribute pattern in `DeckController` that was inherited (not introduced) by this phase.

No security issues identified. The rewriter targets are hardcoded literals (no user input interpolated), zip artifact filenames continue to flow through `CreateSafePathSegment` (header-safe sanitizer preserved), `[ValidateAntiForgeryToken]` + `[RequestSizeLimit]` are intact on every renamed POST route, and `UseRewriter` is correctly ordered after `UseForwardedHeaders` so the 301 `Location` reflects the browser-visible `https` scheme via `X-Forwarded-Proto`.

## Warnings

### WR-01: README documents two pages with URLs that now return 404

**Files:**
- `README.md:215` — `The ChatGPT Analysis page (\`/Deck/ChatGptPackets\`) guides you...`
- `README.md:330` — `The Deck Comparison page (\`/Deck/ChatGptDeckComparison\`) generates...`

**Issue:** In ASP.NET Core, when an action carries an attribute route (`[HttpGet("/deck-analysis")]`), that action is removed from the conventional route table. The conventional default route `{controller=Home}/{action=Index}/{id?}` will therefore no longer match `/Deck/ChatGptPackets` to `DeckController.ChatGptPackets()` — that request hits no route and returns 404. The README updates earlier in the phase touched several `/chatgpt-*` -> `/deck-*` references (e.g., the zip-contents bullets at 278, 365, 437; the cEDH header at 376) but missed these two prose mentions that use the older `/Deck/Action` form. Anyone landing on README.md and trying the documented URL will get a broken link, and there is no rewriter rule covering this shape either (the rewriter only catches the legacy slug shape `chatgpt-packets`, not `Deck/ChatGptPackets`).

**Fix:**
```diff
- The ChatGPT Analysis page (`/Deck/ChatGptPackets`) guides you through a 5-step workflow.
+ The Deck Analysis page (`/deck-analysis`) guides you through a 5-step workflow.
```
```diff
- The Deck Comparison page (`/Deck/ChatGptDeckComparison`) generates structured ChatGPT prompts for comparing two Commander decklists side by side. It lives under the **ChatGPT** dropdown alongside the Analysis page.
+ The Deck Comparison page (`/deck-comparison`) generates structured ChatGPT prompts for comparing two Commander decklists side by side. It lives in the **Analyze** dropdown alongside the Deck Analysis page.
```
(Second hunk also fixes a separate stale claim: there is no longer a "ChatGPT" dropdown — `_DeckToolTabs.cshtml` exposes an "Analyze" group containing the three workflow links.)

### WR-02: Rewriter regexes do not tolerate a trailing slash on the legacy paths

**File:** `DeckFlow.Web/Program.cs:330-340`

**Issue:** All 11 redirect patterns use `^slug$` (or `^slug/sub$`), strictly anchored at both ends. A request to `/chatgpt-packets/` (with trailing slash) does not match `^chatgpt-packets$`, so the rewriter falls through and the request goes to MVC routing, which has no route for `/chatgpt-packets/` and returns 404. The prior `[HttpGet("/chatgpt-packets")]` attribute also did not accept a trailing slash, so this is consistent with the previous behavior — but if any bookmarks, external links, or search-engine indexes accumulated a `/chatgpt-packets/` variant during the lifetime of the old route, they will silently break instead of redirecting. The blast radius is small but the fix is one line and harmless.

**Fix:**
```csharp
app.UseRewriter(new RewriteOptions()
    .AddRedirect("^chatgpt-packets/?$", "deck-analysis", 301)
    .AddRedirect("^chatgpt-packets/download/?$", "deck-analysis/download", 301)
    .AddRedirect("^chatgpt-packets/upload/?$", "deck-analysis/upload", 301)
    .AddRedirect("^chatgpt-deck-comparison/?$", "deck-comparison", 301)
    // …same /? suffix for the rest
    .AddRedirect("^help/chatgpt-analysis/?$", "help/deck-analysis", 301)
    .AddRedirect("^help/chatgpt-deck-comparison/?$", "help/deck-comparison", 301));
```
Optional: relative-target redirects (`"deck-analysis"` without leading `/`) work in current browsers but are slightly less defensive than absolute. Consider using `"/deck-analysis"` everywhere if you want to remove any ambiguity for crawler/CLI clients that resolve `Location` relative to the request URL differently than browsers.

## Info

### IN-01: Pre-existing typo "analysigs" survived the view rename

**File:** `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml:128`

**Issue:** `<p>This deck input powers the analysigs packet and the optional set-upgrade packet.</p>` — "analysigs" should be "analysis". The typo predates phase 12 (it existed in `ChatGptPackets.cshtml` at the same line before the file rename), but phase 12-03's copy refresh on this view was the natural moment to catch it and did not.

**Fix:**
```html
<p>This deck input powers the analysis packet and the optional set-upgrade packet.</p>
```

### IN-02: cEDH zip filename doubles the slug when no commander is provided

**File:** `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs:542-543`

**Issue:** `SuggestCedhMetaGapZipFileName` uses `"cedh-meta-gap"` as both the fallback for an empty commander name AND the literal suffix appended after the safe-path segment. When commander is null/blank, the produced filename is `cedh-meta-gap-cedh-meta-gap-chatgpt-20251201-120000.zip` — the slug is doubled. It is ugly but not broken; the analogous `SuggestPacketZipFileName` produces `deck-analysis-analysis-chatgpt-...zip` (different stems, slightly less awkward), and `SuggestComparisonZipFileName` produces `deck-comparison-comparison-chatgpt-...zip`. The download path normally has a commander resolved by the time it reaches these helpers, so the fallback path is the edge case where this surfaces. Worth tightening for polish but no behavior bug.

**Fix:** Use distinct fallback that does not collide with the suffix, e.g. `"deckflow-cedh"` or `"meta-gap"`:
```csharp
public static string SuggestCedhMetaGapZipFileName(string commanderName, string? targetAiPlatform = null)
    => $"{CreateSafePathSegment(commanderName, "meta-gap")}-cedh-meta-gap-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
```

### IN-03: No automated test covers the 301 redirects from `chatgpt-*` slugs

**File:** `DeckFlow.Web/Program.cs:329-340`

**Issue:** The phase introduces 11 permanent redirects, none of which have a corresponding `WebApplicationFactory<Program>`-based test asserting the 301 status + `Location` header. Because the project's testing posture is "VSTest unreliable in WSL; rely on dotnet build clean + targeted manual harness or push-and-watch CI" (CLAUDE.md), this is consistent with project norms — but a thin regression test would catch (a) accidental regex changes that break a redirect, (b) pipeline-order regressions that put `UseRewriter` before `UseForwardedHeaders` and break the redirect's `https` Location header, and (c) loss of one of the entries during a future cleanup. Cheap insurance for a 301 that, once cached by a browser, is hard to un-do.

**Fix:** Add a single xUnit theory in `DeckFlow.Web.Tests` exercising one row per legacy slug — assert `StatusCode == 301` and `response.Headers.Location.OriginalString.EndsWith("deck-analysis")` (etc.). Skip if the test framework is unreliable in WSL; CI on push covers it.

### IN-04: Doc comments appear after `[HttpGet]` attributes throughout DeckController

**File:** `DeckFlow.Web/Controllers/DeckController.cs:151-188` (and earlier blocks)

**Issue:** The pattern in every renamed action is:
```csharp
[HttpGet("/deck-analysis")]
/// <summary>
/// Renders the staged ChatGPT packet workflow. …
/// </summary>
public IActionResult ChatGptPackets()
```
A C# `///` doc comment must immediately precede the member it documents — i.e. the attribute is the first thing on the member, the doc comment must precede the attribute. Roslyn does parse and attach the comment here under most rule configurations, but `Sandcastle`/some downstream doc tools do not. The repo also has `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` suppressing the warning, so this stays silent at build time. The phase did not introduce the pattern — it inherited it during the rename — but the affected lines were all touched by this phase, so it's a low-cost moment to normalize.

**Fix:** Move the `///` block above the `[HttpGet]` attribute:
```csharp
/// <summary>
/// Renders the staged ChatGPT packet workflow. Set options load asynchronously on the client.
/// </summary>
[HttpGet("/deck-analysis")]
public IActionResult ChatGptPackets()
```

---

_Reviewed: 2026-05-16T20:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
