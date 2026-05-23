# Phase 12: AI-Agnostic URL + Page Rename - Pattern Map

**Mapped:** 2026-05-16
**Files analyzed:** 9 new/modified files
**Analogs found:** 9 / 9

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Web/Program.cs` | config/middleware | request-response | `DeckFlow.Web/Program.cs:318-329` (existing UseForwardedHeaders + UseHttpsRedirection block) | self (insertion point) |
| `DeckFlow.Web/Controllers/DeckController.cs` | controller | request-response | `DeckController.cs:151-188` (existing chatgpt GET actions) | self (attribute replacement) |
| `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` | utility | transform | `ChatGptPacketArtifactStore.cs:536-543` (existing Suggest* helpers) | self (string literal replacement) |
| `DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml` → `DeckAnalysis.cshtml` | view | request-response | `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml:1-4` (title/model header) | exact role-match |
| `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml` → `DeckComparison.cshtml` | view | request-response | existing file (rename only) | self |
| `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml` → `CedhMetaGap.cshtml` | view | request-response | existing file (rename only) | self |
| `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` | view/partial | request-response | `_DeckToolTabs.cshtml:18` (tool-nav__link href + label) | self (string replacement) |
| `DeckFlow.Web/Views/Deck/Home.cshtml` | view | request-response | `Home.cshtml:11-21` (hub-hero + hub-card hrefs/titles) | self (string replacement) |
| `DeckFlow.Web/wwwroot/css/site-common.css` | config/style | N/A | `site-common.css:209-213` (`.hub-lede` rule) | role-match (muted explainer text) |

---

## Pattern Assignments

### `DeckFlow.Web/Program.cs` — UseRewriter insertion (middleware config)

**Analog:** `DeckFlow.Web/Program.cs:315-329` — the existing forwarded-headers / HTTPS-redirect ordering block, which documents the same scheme-preservation constraint that must also apply to UseRewriter.

**Insertion point pattern** (lines 315-329):
```csharp
// Must run before any middleware that reads request.Scheme/Host (HttpsRedirection,
// security headers, SameOriginRequestValidator in controllers) so those see the
// browser's original scheme/host, not the proxy hop.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Deck/Error");
    app.UseHsts();
}

app.UseDeckFlowSecurityHeaders();

app.UseHttpsRedirection();
```

**UseRewriter goes immediately after `app.UseForwardedHeaders()` and before `app.UseDeckFlowSecurityHeaders()`** — slot position D-05. The `Microsoft.AspNetCore.Rewrite` namespace is in `Microsoft.AspNetCore.App`; no new NuGet package needed.

**Pattern to copy (12 redirect entries per D-04):**
```csharp
// D-03/D-04/D-05: 301 redirects from chatgpt-* slugs to AI-agnostic slugs.
// UseRewriter must follow UseForwardedHeaders so Location headers honor X-Forwarded-Proto.
app.UseRewriter(new RewriteOptions()
    .AddRedirect("^chatgpt-packets$",               "deck-analysis",      301)
    .AddRedirect("^chatgpt-packets/download$",       "deck-analysis/download", 301)
    .AddRedirect("^chatgpt-packets/upload$",         "deck-analysis/upload",   301)
    .AddRedirect("^chatgpt-deck-comparison$",        "deck-comparison",    301)
    .AddRedirect("^chatgpt-deck-comparison/download$","deck-comparison/download", 301)
    .AddRedirect("^chatgpt-deck-comparison/upload$", "deck-comparison/upload",   301)
    .AddRedirect("^chatgpt-cedh-meta-gap$",          "cedh-meta-gap",      301)
    .AddRedirect("^chatgpt-cedh-meta-gap/download$", "cedh-meta-gap/download", 301)
    .AddRedirect("^chatgpt-cedh-meta-gap/upload$",   "cedh-meta-gap/upload",   301));
```

Note: `AddRedirect` regex matches the path without the leading `/`. Verify exact regex anchoring when the planner writes the action step.

---

### `DeckFlow.Web/Controllers/DeckController.cs` — Route attribute replacement (controller)

**Analog:** `DeckFlow.Web/Controllers/DeckController.cs:151-188` — the three existing GET actions with explicit route strings. Replace `chatgpt-*` route strings; action method names + `return View(...)` calls also need updating (D-13).

**Existing GET pattern to replace** (lines 151-188):
```csharp
[HttpGet("/chatgpt-packets")]
/// <summary>
/// Renders the staged ChatGPT packet workflow. Set options load asynchronously on the client.
/// </summary>
public IActionResult ChatGptPackets()
{
    return View("ChatGptPackets", new ChatGptDeckViewModel
    {
        ActiveTab = DeckPageTab.ChatGptPackets,
        Request = new ChatGptDeckRequest(),
    });
}

[HttpGet("/chatgpt-deck-comparison")]
/// <summary>
/// Renders the staged ChatGPT deck-comparison workflow.
/// </summary>
public IActionResult ChatGptDeckComparison()
{
    return View("ChatGptDeckComparison", new ChatGptDeckComparisonViewModel
    {
        ActiveTab = DeckPageTab.ChatGptDeckComparison,
        Request = new ChatGptDeckComparisonRequest(),
    });
}

[HttpGet("/chatgpt-cedh-meta-gap")]
/// <summary>
/// Renders the staged cEDH meta-gap workflow.
/// </summary>
public IActionResult ChatGptCedhMetaGap()
{
    return View("ChatGptCedhMetaGap", new ChatGptCedhMetaGapViewModel
    {
        ActiveTab = DeckPageTab.ChatGptCedhMetaGap,
        Request = new ChatGptCedhMetaGapRequest(),
    });
}
```

**All 12 route attribute strings to replace** (from grep at lines 151-1009):
| Old attribute | New attribute |
|---------------|---------------|
| `[HttpGet("/chatgpt-packets")]` | `[HttpGet("/deck-analysis")]` |
| `[HttpPost("/chatgpt-packets")]` | `[HttpPost("/deck-analysis")]` |
| `[HttpPost("/chatgpt-packets/download")]` | `[HttpPost("/deck-analysis/download")]` |
| `[HttpPost("/chatgpt-packets/upload")]` | `[HttpPost("/deck-analysis/upload")]` |
| `[HttpGet("/chatgpt-deck-comparison")]` | `[HttpGet("/deck-comparison")]` |
| `[HttpPost("/chatgpt-deck-comparison")]` | `[HttpPost("/deck-comparison")]` |
| `[HttpPost("/chatgpt-deck-comparison/download")]` | `[HttpPost("/deck-comparison/download")]` |
| `[HttpPost("/chatgpt-deck-comparison/upload")]` | `[HttpPost("/deck-comparison/upload")]` |
| `[HttpGet("/chatgpt-cedh-meta-gap")]` | `[HttpGet("/cedh-meta-gap")]` |
| `[HttpPost("/chatgpt-cedh-meta-gap")]` | `[HttpPost("/cedh-meta-gap")]` |
| `[HttpPost("/chatgpt-cedh-meta-gap/download")]` | `[HttpPost("/cedh-meta-gap/download")]` |
| `[HttpPost("/chatgpt-cedh-meta-gap/upload")]` | `[HttpPost("/cedh-meta-gap/upload")]` |

**View name string update pattern** (line 466 representative):
```csharp
// Old:
return View("ChatGptPackets", new ChatGptDeckViewModel { ... });
// New (after view file renamed to DeckAnalysis.cshtml):
return View("DeckAnalysis", new ChatGptDeckViewModel { ... });
```
Apply the same substitution for `"ChatGptDeckComparison"` → `"DeckComparison"` and `"ChatGptCedhMetaGap"` → `"CedhMetaGap"`. All occurrences of these literal strings (GET + POST handlers + error fallback paths) must change.

---

### `DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs` — Suggest* filename helpers (utility/transform)

**Analog:** Self — lines 536-543.

**Existing helpers** (lines 536-543):
```csharp
public static string SuggestPacketZipFileName(string? commanderName, string? targetAiPlatform = null)
    => $"{CreateSafePathSegment(commanderName, "deckflow-packet")}-analysis-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

public static string SuggestComparisonZipFileName(string? commanderName, string? targetAiPlatform = null)
    => $"{CreateSafePathSegment(commanderName, "deck-comparison")}-compare2-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

public static string SuggestCedhMetaGapZipFileName(string commanderName, string? targetAiPlatform = null)
    => $"{CreateSafePathSegment(commanderName, "cedh-meta-gap")}-cedh-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
```

**Changes required per D-10:**
- `SuggestPacketZipFileName`: fallback `"deckflow-packet"` → `"deck-analysis"` (mid-segment `analysis` stays)
- `SuggestComparisonZipFileName`: mid-segment `compare2` → `comparison`
- `SuggestCedhMetaGapZipFileName`: mid-segment `cedh` → `cedh-meta-gap`
- AI fallback `"chatgpt"` in all three helpers: DO NOT CHANGE (user-chosen AI segment per Phase 10 commit `00e5bdd`)

**Target result:**
```csharp
public static string SuggestPacketZipFileName(string? commanderName, string? targetAiPlatform = null)
    => $"{CreateSafePathSegment(commanderName, "deck-analysis")}-analysis-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

public static string SuggestComparisonZipFileName(string? commanderName, string? targetAiPlatform = null)
    => $"{CreateSafePathSegment(commanderName, "deck-comparison")}-comparison-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

public static string SuggestCedhMetaGapZipFileName(string commanderName, string? targetAiPlatform = null)
    => $"{CreateSafePathSegment(commanderName, "cedh-meta-gap")}-cedh-meta-gap-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
```

---

### `DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml` → `DeckAnalysis.cshtml` (view rename + H1 + lede)

**Analog:** `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml:1-4` — shows `ViewData["Title"]` and `@model` header pattern that other pages already follow.

**Existing file header** (`ChatGptPackets.cshtml` lines 1-4):
```razor
@model DeckFlow.Web.Models.ChatGptDeckViewModel
@{
    ViewData["Title"] = "ChatGPT Analysis";
```

**Changes per D-06, D-07, D-09:**
1. File renamed to `DeckAnalysis.cshtml` (git mv)
2. `ViewData["Title"]` → `"Deck Analysis"` (title renders as `"Deck Analysis - DeckFlow"` per `_Layout.cshtml:43`)
3. H1 text `"ChatGPT Analysis"` → `"Deck Analysis"` (only Page 1 H1 changes — D-06)
4. Add `<p class="page-lede">Generate a prompt to paste into ChatGPT, Claude, or Gemini.</p>` immediately after the H1 (exact copy per D-07)
5. `@model` directive stays on `ChatGptDeckViewModel` — no change (D-14)

**`<title>` rendering pattern** (from `_Layout.cshtml:43`):
```razor
<title>@ViewData["Title"] - DeckFlow</title>
```
So setting `ViewData["Title"] = "Deck Analysis"` produces `"Deck Analysis - DeckFlow"` — matches the AI-agnostic pattern.

---

### `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml` → `DeckComparison.cshtml` (view rename + lede)

**Analog:** Self — existing file, rename only with lede addition.

**Existing file header** (lines 1-3):
```razor
@model DeckFlow.Web.Models.ChatGptDeckComparisonViewModel
@{
    ViewData["Title"] = "Deck Comparison";
```

**Changes per D-07, D-09:**
1. File renamed to `DeckComparison.cshtml` (git mv)
2. `ViewData["Title"]` already reads `"Deck Comparison"` — no change needed (D-09: only Page 1 strings change)
3. Add `<p class="page-lede">Generate a prompt comparing two decks. Paste into ChatGPT, Claude, or Gemini.</p>` after the H1 (exact copy per D-07)
4. `@model` directive stays on `ChatGptDeckComparisonViewModel` — no change (D-14)

---

### `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml` → `CedhMetaGap.cshtml` (view rename + lede)

**Analog:** Self — existing file, rename only with lede addition.

**Existing file header** (lines 1-3):
```razor
@model DeckFlow.Web.Models.ChatGptCedhMetaGapViewModel
@{
    ViewData["Title"] = "cEDH Meta Gap";
```

**Changes per D-07, D-09:**
1. File renamed to `CedhMetaGap.cshtml` (git mv)
2. `ViewData["Title"]` already reads `"cEDH Meta Gap"` — no change needed
3. Add `<p class="page-lede">Generate a prompt analyzing your deck against current cEDH meta. Paste into ChatGPT, Claude, or Gemini.</p>` after the H1 (exact copy per D-07)
4. `@model` directive stays on `ChatGptCedhMetaGapViewModel` — no change (D-14)

---

### `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` — nav label (view partial)

**Analog:** Self — line 18.

**Existing nav-link pattern** (line 18):
```razor
<a class="tool-nav__link @(Model == DeckPageTab.ChatGptPackets ? "is-active" : string.Empty)"
   href="@Url.Content("~/chatgpt-packets")">ChatGPT Analysis</a>
```

**Changes per D-09:**
- `href`: `~/chatgpt-packets` → `~/deck-analysis`
- Link text: `ChatGPT Analysis` → `Deck Analysis`
- Lines 19-20 (Deck Comparison, cEDH Meta Gap hrefs): update href slugs only — labels already AI-agnostic
  - `~/chatgpt-deck-comparison` → `~/deck-comparison`
  - `~/chatgpt-cedh-meta-gap` → `~/cedh-meta-gap`

**Complete Analyze dropdown after change:**
```razor
<a class="tool-nav__link @(Model == DeckPageTab.ChatGptPackets ? "is-active" : string.Empty)" href="@Url.Content("~/deck-analysis")">Deck Analysis</a>
<a class="tool-nav__link @(Model == DeckPageTab.ChatGptDeckComparison ? "is-active" : string.Empty)" href="@Url.Content("~/deck-comparison")">Deck Comparison</a>
<a class="tool-nav__link @(Model == DeckPageTab.ChatGptCedhMetaGap ? "is-active" : string.Empty)" href="@Url.Content("~/cedh-meta-gap")">cEDH Meta Gap</a>
```

---

### `DeckFlow.Web/Views/Deck/Home.cshtml` — hub card titles + hrefs (view)

**Analog:** Self — lines 11-31.

**Existing hub-hero and hub-card pattern** (lines 11-31):
```razor
<a class="hub-hero" href="@Url.Content("~/chatgpt-packets")">
    <span class="hub-hero__eyebrow">Headline workflow</span>
    <span class="hub-hero__title">Analyze Your Deck with ChatGPT</span>
    <span class="hub-hero__description">Five-step workflow: load your deck, pick your questions, copy the prompt, paste into ChatGPT, review the structured response.</span>
</a>

<section class="hub-group" aria-labelledby="hub-group-analyze">
    <h2 id="hub-group-analyze" class="hub-group__title">Analyze</h2>
    <div class="hub-grid">
        <a class="hub-card" href="@Url.Content("~/chatgpt-packets")">
            <h3 class="hub-card__title">ChatGPT Analysis</h3>
            ...
        </a>
        <a class="hub-card hub-card--primary" href="@Url.Content("~/chatgpt-deck-comparison")">
        ...
        <a class="hub-card" href="@Url.Content("~/chatgpt-cedh-meta-gap")">
```

**Changes per D-09 (only Page 1 "ChatGPT Analysis" label changes):**
- `hub-hero` href: `~/chatgpt-packets` → `~/deck-analysis`
- `hub-hero__title`: `"Analyze Your Deck with ChatGPT"` → `"Analyze Your Deck"` (AI-agnostic; brainstorm Mock A)
- First `hub-card` href: `~/chatgpt-packets` → `~/deck-analysis`
- First `hub-card__title`: `"ChatGPT Analysis"` → `"Deck Analysis"`
- Second `hub-card` href: `~/chatgpt-deck-comparison` → `~/deck-comparison` (label stays "Deck Comparison")
- Third `hub-card` href: `~/chatgpt-cedh-meta-gap` → `~/cedh-meta-gap` (label stays "cEDH Meta Gap")

---

### `DeckFlow.Web/wwwroot/css/site-common.css` — `.page-lede` rule (CSS utility)

**Analog:** `site-common.css:209-213` — the `.hub-lede` rule. Same role: muted explainer text under a heading, using `var(--muted)` and `var(--fs-base)`.

**.hub-lede existing rule** (lines 209-213):
```css
.hub-lede {
  margin: 1rem 0 0.5rem;
  color: var(--muted);
  font-size: var(--fs-base);
}
```

**Why not `.mode-note`:** `.mode-note` lives only in individual guild theme forks (e.g., `site-bant.css:833`), not in `site-common.css`, and carries border + background styling (dashed border, theme-colored background panel). `.page-lede` is a plain inline explainer sentence — closer in spirit and placement to `.hub-lede`.

**New `.page-lede` rule to add to `site-common.css` (at end of file, after last `details.info-tooltip` block):**
```css
/* === Phase 12 (RENAME-02) — page-lede explainer line ===
   Muted one-line explainer under an H1 on the three AI workflow pages.
   Per CLAUDE.md D-07, cross-cutting rules live in site-common.css. */
.page-lede {
  margin: 0.25rem 0 1rem;
  color: var(--muted);
  font-size: var(--fs-base);
}
```

**File currently ends at line 1388** — append after line 1388.

---

### `README.md` and `DeckFlow.Web/Help/**/*.md` — URL doc sweep (docs)

**Analog:** Direct string replacement — no code pattern to copy, purely mechanical.

**Occurrences to replace** (from grep):
- `README.md:278`: `/chatgpt-packets` → `/deck-analysis`
- `README.md:365`: `/chatgpt-deck-comparison` → `/deck-comparison`
- `README.md:376`: `/chatgpt-cedh-meta-gap` → `/cedh-meta-gap` (also update surrounding "ChatGPT" copy where it refers to the page, not the AI tool)
- `README.md:437`: `/chatgpt-cedh-meta-gap` → `/cedh-meta-gap`
- `DeckFlow.Web/Help/cedh-meta-gap.md:9`: `/chatgpt-cedh-meta-gap` → `/cedh-meta-gap`

**Acceptance gate (D-15):** After sweep, `grep -r "chatgpt-" --include="*.md" --include="*.js" .` should return hits only inside the `UseRewriter` block in `Program.cs`.

---

### `browser-extensions/deckflow-bridge/{background.js,deckflow-bridge.js}` — extension URL sweep (JS)

**Analog:** `deckflow-bridge.js:4-10` — `defaultAllowedOrigins` array pattern; `background.js` fetches Moxfield API, not DeckFlow URLs. No `chatgpt-*` URL references were found in either extension file during analysis — the extension does not hardcode DeckFlow page URLs. Confirm with `grep -r "chatgpt-" browser-extensions/` before treating as a no-op.

**If references are found:** Replace inline string literal; no structural change needed. Follow existing string literal style (single quotes, no trailing comma on last entry).

**Extension version bump (D-16):** If `background.js` or `deckflow-bridge.js` receive any URL edits, bump `"version"` in `browser-extensions/deckflow-bridge/manifest.json`. If no edits are needed, no bump required. The `ZipDeckFlowBridge` MSBuild target re-packages the extension automatically on next build.

---

## Shared Patterns

### Url.Content helper for internal hrefs
**Source:** `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml:18-20` and `Views/Deck/Home.cshtml:11-31`
**Apply to:** All href changes in `_DeckToolTabs.cshtml` and `Home.cshtml`
```razor
href="@Url.Content("~/deck-analysis")"
```
Use `~/` tilde-prefix consistently — matches existing convention throughout all views.

### ViewData["Title"] pattern
**Source:** `DeckFlow.Web/Views/Shared/_Layout.cshtml:43`
**Apply to:** All three renamed view files
```razor
ViewData["Title"] = "Deck Analysis";
// Renders as: <title>Deck Analysis - DeckFlow</title>
```

### Cross-cutting CSS placement rule (CLAUDE.md D-07)
**Source:** `DeckFlow.Web/wwwroot/css/site-common.css:8-11` (Phase 11 cross-cutting section comment)
**Apply to:** `.page-lede` rule
All layout/utility CSS that must apply across all 22 guild themes goes in `site-common.css`, never in `site.css` or any guild fork file.

### Explicit View() name in controller returns
**Source:** `DeckFlow.Web/Controllers/DeckController.cs:157, 170, 183` (and all POST error-fallback returns)
**Apply to:** All `return View(...)` calls in the three affected action methods
```csharp
// Always pass explicit view name string — do not rely on action-name convention
return View("DeckAnalysis", new ChatGptDeckViewModel { ... });
return View("DeckComparison", new ChatGptDeckComparisonViewModel { ... });
return View("CedhMetaGap", new ChatGptCedhMetaGapViewModel { ... });
```

---

## No Analog Found

All files in scope have either self-analogs or close role-matches. No files require falling back to RESEARCH.md patterns.

---

## Metadata

**Analog search scope:** `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Services/`, `DeckFlow.Web/Views/`, `DeckFlow.Web/wwwroot/css/`, `browser-extensions/deckflow-bridge/`, `README.md`, `DeckFlow.Web/Help/`
**Files scanned:** 14 source files read or grepped
**Pattern extraction date:** 2026-05-16
