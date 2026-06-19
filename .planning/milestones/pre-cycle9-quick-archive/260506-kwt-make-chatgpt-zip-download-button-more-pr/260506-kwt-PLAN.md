---
quick_id: 260506-kwt
slug: make-chatgpt-zip-download-button-more-pr
title: Sticky prominent Download (.zip) bar across three ChatGPT pages
created: 2026-05-06
status: planned
---

# Quick Task 260506-kwt — Sticky Download Bar

## Goal

Promote zip download from a result-panel-only action into a top-of-page persistent CTA so the user can save current session state at any step on all three ChatGPT pages.

## Scope

Three Razor views and one shared CSS file. No controller changes — endpoints already exist (shipped in 260506-hgd, commit 5f5764f).

## Files

| File | Action |
|------|--------|
| `DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml` | Insert sticky download bar inside `<form>` (line 71), targeting `~/chatgpt-packets/download` |
| `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml` | Insert sticky download bar inside `<form>` (line 170), targeting `~/chatgpt-deck-comparison/download` |
| `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml` | Insert sticky download bar inside `<form>` (line 39), targeting `~/chatgpt-cedh-meta-gap/download` |
| `DeckFlow.Web/wwwroot/css/site-common.css` | Add `.chatgpt-sticky-download` layout rules (sticky positioning, padding, border, button accent) |

## Design contract

**Bar markup** (place as FIRST child of each `<form>` so it sits above the workflow step tabs):

```html
<div class="chatgpt-sticky-download" role="region" aria-label="Save current session">
    <span class="chatgpt-sticky-download__label">Save your work in progress to a zip file you can re-import later.</span>
    <button type="submit" class="chatgpt-sticky-download__button"
            formaction="@Url.Content("~/<page>/download")"
            formmethod="post">
        Download session (.zip)
    </button>
</div>
```

Page-specific button labels:
- Packets: `Download session (.zip)`
- Comparison: `Download comparison session (.zip)`
- Meta Gap: `Download meta-gap session (.zip)`

**CSS** (in `site-common.css` per project rule — no per-theme duplication):

```css
.chatgpt-sticky-download {
    position: sticky;
    top: 0;
    z-index: 10;
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 10px 14px;
    margin: -8px 0 12px;
    background: var(--panel);
    border: 1px solid var(--line);
    border-radius: 6px;
    box-shadow: 0 2px 6px rgba(0, 0, 0, 0.15);
}

.chatgpt-sticky-download__label {
    flex: 1;
    color: var(--muted);
    font-size: 0.9em;
}

.chatgpt-sticky-download__button {
    /* Inherit existing .run-button look but boost prominence */
    padding: 8px 18px;
    font-weight: 600;
}
```

Reuse `.run-button` class on button as well so theme accent color flows through:

```html
<button type="submit" class="run-button chatgpt-sticky-download__button" ...>
```

## Inline buttons

KEEP existing per-step inline Download buttons in Step 3/Step 5 result panels. They're useful confirmation right after the user gets results and require no churn. Sticky bar becomes the always-available primary CTA; inline buttons remain as secondary confirmations.

## Tasks

1. **Insert sticky bar markup + CSS class** in all three views and add `.chatgpt-sticky-download` rules to `site-common.css`. (Single Codex prompt — multi-file atomic change.)
2. **Build verify** — `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` clean.
3. **Smoke test** — start dev server, load each `/chatgpt-*` page, confirm sticky bar renders at top, scrolls into stuck position, button posts to correct endpoint and returns a zip.

## Verify

- [ ] Sticky bar renders at top of form on all three pages
- [ ] Bar stays visible when scrolling past step tabs
- [ ] Click downloads zip with current form state (partial state OK per scope)
- [ ] Existing per-step inline Download buttons still work
- [ ] Theme look intact across at least 2 themes (e.g., default + one guild)
- [ ] No new build warnings

## Done

Sticky bar live on all three ChatGPT pages with prominent Download (.zip) button always visible regardless of scroll/step.

## Constraints

- Layout CSS goes in `site-common.css` — never per-theme files (project memory rule)
- Use existing CSS tokens (`--panel`, `--line`, `--muted`, `--accent-strong` via `.run-button`)
- No JavaScript needed — pure HTML form with `formaction` override
- Coding routes through Codex MCP (per CLAUDE.md); model: `gpt-5.4` full (multi-file work)

## Risk

- Sticky positioning could conflict with workflow step tabs which may also be styled to stick. Mitigation: review `_WorkflowStepTabs.cshtml` rendering and adjust z-index if needed.
- Mobile breakpoint: bar should collapse gracefully — use flex-wrap fallback.
