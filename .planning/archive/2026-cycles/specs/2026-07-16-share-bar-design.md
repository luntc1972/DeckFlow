# SEO Slice 2 — "Share DeckFlow" Bar

**Date:** 2026-07-16
**Status:** Approved design, pre-implementation
**Slice:** 2 of 4 in the "spread the word" workstream (1: structured data — shipped; 3: content/landing pages; 4: off-page playbook — delivered)

## Problem

DeckFlow has no in-app way for a user to tell someone else about it. The goal of this slice is a lightweight, visible **share bar** that promotes the tool (not the user's ephemeral result — tool results are session-generated and have no persistent URL).

## Decisions (locked in brainstorming)

- **What is shared:** the current page (a tool page or the home page) — its canonical URL + a short pitch. Promotes DeckFlow.
- **Channels:** Copy link · Native share (mobile Web Share API) · Reddit · X · Bluesky. (No Discord button — native share covers it on mobile.)
- **Link-preview image:** the existing single site `og-image.png` (no per-tool images this slice).
- **Placement:** a horizontal bar **above the footer**.
- **Pages:** the 14 `SeoPaths.Tools` pages **plus** `/` (home). Reuses the slice-1 shared path list.

## Non-Goals

- Shareable result permalinks / result persistence (a separate milestone).
- Per-tool OG images or dynamic OG generation (RAM cap + no-new-package rule).
- Share buttons on help/about/feedback/content-kb pages.
- Any new NuGet/npm dependency.

## Design

### Component 1 — `ShareLinks` helper (C#)

New `DeckFlow.Web/Seo/ShareLinks.cs` — pure static builder, cohesive with `SeoPaths`/`StructuredDataBuilder` in the `DeckFlow.Web.Seo` namespace.

```csharp
public sealed record ShareLinks(
    string CanonicalUrl,
    string ShareTitle,
    string ShareText,
    string RedditUrl,
    string XUrl,
    string BlueskyUrl);

public static class ShareLinkBuilder
{
    public static ShareLinks Build(string canonicalUrl, string? rawTitle);
}
```

- `ShareTitle` = `rawTitle` if non-empty else `"DeckFlow"`.
- `ShareText` = `$"{ShareTitle} — free MTG deck tool for Commander & cEDH"`.
- `RedditUrl` = `https://www.reddit.com/submit?url={enc(canonicalUrl)}&title={enc(ShareTitle)}`
- `XUrl` = `https://twitter.com/intent/tweet?text={enc(ShareText)}&url={enc(canonicalUrl)}`
- `BlueskyUrl` = `https://bsky.app/intent/compose?text={enc(ShareText + " " + canonicalUrl)}`
- Encoding via `Uri.EscapeDataString`. Copy + native share are client-side (no server URL needed) — the partial exposes the raw URL + text via `data-` attributes for JS.

### Component 2 — `_ShareBar.cshtml` partial

New `DeckFlow.Web/Views/Shared/_ShareBar.cshtml`, `@model DeckFlow.Web.Seo.ShareLinks`.

- Wrapped in `<section class="share-bar" aria-label="Share DeckFlow">` with a small heading (visually a label, e.g. "Share DeckFlow").
- **Copy** and **Native share** are `<button type="button">` (no navigation): `data-share-url="@Model.CanonicalUrl"` and `data-share-text="@Model.ShareText"`.
  - The native-share button carries a `share-bar__native` class and is **hidden by default** (`hidden` attribute); `share-bar.ts` reveals it only when `navigator.share` exists.
- **Reddit / X / Bluesky** are real `<a href="@Model.RedditUrl" target="_blank" rel="noopener noreferrer">` links — they work with JS disabled.
- Each control has a text label and/or `aria-label`. No icon-only controls without an accessible name.

### Component 3 — `share-bar.ts`

New `DeckFlow.Web/wwwroot/ts/share-bar.ts` → compiles to `wwwroot/js/share-bar.js` (compiled JS is gitignored — never commit it).

- On load: query `.share-bar`. If none, return.
- **Copy button:** reuse the existing clipboard pattern (see `card-lookup.ts:155`) — `navigator.clipboard.writeText(`${text}\n${url}`)`, swap label to "Copied" for ~2s, handle failure with a "Copy failed" state. Reuse the `data-copyOriginalText` idiom.
- **Native button:** feature-detect `navigator.share`; if present, remove `hidden` and wire a click to `navigator.share({ title, text, url })`, swallowing `AbortError` (user cancels). If absent, leave hidden.
- No framework; plain DOM, matches the other `ts/*.ts` modules (module: "none").

### Component 4 — CSS in `site-common.css`

Per the project constraint, **layout CSS goes in `site-common.css`, not `site.css`**. Add a `.share-bar` block:
- Flex row, `flex-wrap: wrap`, centered, gap; a top divider to separate from body content.
- Buttons/links styled with existing theme tokens (`var(--panel)`, `var(--border)`, text tokens) so all guild themes inherit correctly. No hard-coded colors.
- Responsive: wraps to multiple rows on narrow viewports; adequate tap targets (min ~40px height) on mobile.
- No token additions expected; if one is needed it goes in each theme's `:root` (per constraint) — avoid if possible.

### Component 5 — `_Layout.cshtml` wiring

- Add `using`/fully-qualified call to compute, after the existing structured-data block:
  ```csharp
  var showShareBar = DeckFlow.Web.Seo.SeoPaths.IsShareablePage(requestPath.Value ?? "/");
  var shareLinks = showShareBar
      ? DeckFlow.Web.Seo.ShareLinkBuilder.Build(canonicalUrl, rawPageTitle)
      : null;
  ```
- Render the partial between `@RenderBody()` (currently `_Layout.cshtml:104`) and `<footer class="page-footer">` (`:106`):
  ```razor
  @if (shareLinks is not null)
  {
      <partial name="_ShareBar" model="shareLinks" />
  }
  ```
- Emit the script only when the bar is shown, alongside the page's other scripts near `</body>`:
  ```razor
  @if (shareLinks is not null)
  {
      <script src="~/js/share-bar.js" asp-append-version="true"></script>
  }
  ```

### Component 6 — `SeoPaths.IsShareablePage`

Add to `SeoPaths` a shared normalize + membership helper so both slice-1 logic and the share bar agree on "tool page or home":

```csharp
public static bool IsShareablePage(string path); // Normalize(path) == "/" || Tools.Contains(Normalize(path))
public static string Normalize(string? path);     // lower-invariant, strip trailing slash (except root)
```

`StructuredDataBuilder` already has a private `NormalizePath`; to avoid two implementations, it should call `SeoPaths.Normalize` (small refactor — behavior identical, covered by existing builder tests).

## Data Flow

Request → view renders → `_Layout` computes canonical + rawTitle (existing) → if `SeoPaths.IsShareablePage(path)`, builds `ShareLinks` and renders `_ShareBar` above the footer + loads `share-bar.js` → browser: real `<a>` intents work immediately; `share-bar.ts` wires copy and (mobile) native share. No request state mutated; builder pure.

## Error Handling

- `navigator.clipboard` failure → visible "Copy failed" state, no throw.
- `navigator.share` absent → native button stays hidden; copy + intents still available.
- `navigator.share` `AbortError` (user cancels) → swallowed silently.
- Unshareable path → `shareLinks` is null → no bar, no script, no behavior change.

## Testing

- **`ShareLinkBuilderTests`** (xUnit, no new deps): each channel URL has the right host/path and correctly `Uri.EscapeDataString`-encoded url+title; `ShareText` contains the pitch; empty/null title falls back to "DeckFlow"; a title with `&`/spaces/`#` encodes without breaking the query string.
- **`ShareBarViewTests`** (file-scan guard, mirrors `PageMetadataViewTests`): `_ShareBar.cshtml` contains the three intent `<a>` links, the copy + native buttons, the `aria-label`, and the native button's `hidden` + `share-bar__native` markers.
- **`SeoPaths.IsShareablePage`** covered in `StructuredDataBuilderTests` neighbor or a small `SeoPathsTests`: `/`, a tool path, `/help`, `/about` → true/true/false/false; trailing-slash + uppercase normalize.
- **TypeScript:** no TS unit-test harness is wired into CI in this repo; `share-bar.ts` is covered by the `tsc` build (must compile clean under `strict`) plus the UI checks below. (Consistent with existing `ts/*.ts`.)
- **UI verification (required for a web-page change):** Playwright/manual — desktop + mobile viewports, across at least 2 themes (Classic + one guild theme): bar renders above footer on `/` and a tool page; NOT on `/help`/`/about`; copy button copies and shows "Copied"; intent links open with prefilled text; native button hidden on desktop. Screenshots at both viewports.
- Regression: full `DeckFlow.Web.Tests` suite green; existing `PageMetadataViewTests`, `StructuredDataBuilderTests`, `SitemapControllerTests` unaffected.

## Backward Compatibility

- Purely additive: new partial + helper + TS + CSS block; one conditional include + one script tag in `_Layout`.
- No route, response-header, or existing-view change beyond `_Layout`.
- Compiled `share-bar.js` is gitignored (Docker rebuilds TS at deploy) — do not stage it.
- `SeoPaths.Normalize` refactor of `StructuredDataBuilder` is behavior-preserving (existing tests guard it).

## Open Questions / Assumptions

- **Pitch wording** — `"{Title} — free MTG deck tool for Commander & cEDH"`. If too long for X, it still fits (X allows 280; title ~40 + pitch ~40). Acceptable.
- **X intent host** — `twitter.com/intent/tweet` still redirects to x.com and is the stable intent endpoint; keep it. **[verify at implementation]** whether `x.com/intent/tweet` is preferred — either works.
- **Bluesky intent** — `bsky.app/intent/compose?text=` is the documented compose intent; URL is appended into the text (Bluesky has no separate url param). Confirm the param name at implementation.
- **Heading text** — "Share DeckFlow" label above/beside the buttons; final copy is a trivial view string.
