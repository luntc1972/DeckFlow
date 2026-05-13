# Phase 10: Claude + Gemini Artifact Optimization - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-09
**Phase:** 10-claude-gemini-artifact-optimization
**Areas discussed:** Claude artifact shape, Gemini artifact shape, Response contract, Surface rollout

---

## Claude artifact shape

| Option | Description | Selected |
|--------|-------------|----------|
| Flat XML-tagged sections | Single block with semantic tags (`<deck>`, `<commander>`, `<reference>`, `<schema>`, `<instructions>`). Mirrors current ChatGPT layout but with XML tags Claude can reliably reference. | ✓ |
| Role-block format | Explicit API role blocks. Cosmetic noise for paste-in flow since Claude.ai web UI doesn't parse them. | |
| Tagged sections + brief preamble | XML skeleton plus a short markdown preamble for human readability. Hybrid; slight redundancy. | |

**User's choice:** Flat XML-tagged sections with markdown allowed inside content tags. No API role blocks.
**Notes:** User asked the underlying question first ("what is the best way for Claude to receive instructions and return them") and then "is markdown the best thing to send or xml" before locking. Decision was driven by Anthropic's documented training preference for XML tags in long-input prompts plus the unambiguous extract semantics of XML tag boundaries vs markdown headings.

---

## Response contract — JSON vs native

| Option | Description | Selected |
|--------|-------------|----------|
| All 3 AIs return JSON inside `<result>` tags | Single import path. Slight regression risk on the default ChatGPT flow. | |
| Claude+Gemini wrap in `<result>`; ChatGPT keeps fenced JSON; parser tries XML first, falls back to fence | Zero regression on default flow. Single import code path with two probes. Backwards-compatible with old saved zips. | ✓ |
| Full-XML response (no inner JSON) | Forces XML→C# DTO mapping rewrite. Massive blast radius for marginal gain. | |

**User's choice:** Option A2 — all three AIs are *instructed* to wrap JSON in `<result>` tags; the ChatGPT prompt keeps the existing fenced-JSON request as backup; parser tries XML extract first then falls back to fenced JSON.
**Notes:** User raised this question proactively as an architectural insight — "should each of them just return xml chatgpt claude gemini so there only needs to be one importer/generate response". Confirmed the engineering instinct (single import path) and chose the safer regression profile.

---

## Gemini artifact shape

| Option | Description | Selected |
|--------|-------------|----------|
| Light differentiation | Same markdown skeleton as ChatGPT, with Gemini-tuned tweaks: explicit step-by-step, stronger persona, firmer schema-strictness language, plus `<result>` wrapper. | ✓ |
| Full restructure | Dedicated layout convention. Hard to justify — Gemini handles markdown fine. | |
| Research first | Defer to research phase. Adds a research cycle. | |

**User's choice:** Light differentiation.
**Notes:** Honest framing that ChatGPT-vs-Gemini divergence is naturally subtler than the Claude divergence (XML vs markdown is a sharper structural signal than markdown vs markdown-with-tweaks). Decision honors AISEL-03 ("distinct from ChatGPT and Claude formats") via the instruction layer + the markdown-vs-XML skeleton split between Gemini and Claude artifacts.

---

## Surface rollout — pages + persistence

| Option | Description | Selected |
|--------|-------------|----------|
| All 3 pages, full round-trip | Claude+Gemini artifacts on Packets, Comparison, CedhMetaGap. Add a request-context file to Comparison and CedhMetaGap zips so AI selection persists everywhere. | ✓ |
| All 3 pages, Packets-only round-trip | Lighter scope; mild UX inconsistency on resume — Comparison and CedhMetaGap selectors reset to ChatGPT after re-upload. | |
| Packets only, defer rest | Smallest scope. Leaves v1.2 shipping a partial feature. | |

**User's choice:** All 3 pages, full round-trip.
**Notes:** Closes AISEL-04 fully (Phase 9 only delivered Packets round-trip). Adds infrastructure work — extending zip layouts for Comparison and CedhMetaGap with a request-context file analogous to `01-request-context.txt` on Packets.

---

## Claude's Discretion

- Exact wording and tone of the per-AI instruction layers (researcher pulls from current Anthropic / Google docs).
- File organization for the new per-AI prompt builders (one class, three classes, or strategy interface — planner's call within the constraint that dispatch happens inside the service, not at the controller).
- Whether the new request-context file for Comparison / CedhMetaGap zips carries only `target_ai_platform` or also adds the form-state fields that Packets persists today (planner decides scope based on what those zips actually need to round-trip).
- Test approach (manual round-trip check by user vs golden-file tests).

## Deferred Ideas

- **Full-XML response pipeline** — Claude could return XML directly instead of JSON-inside-XML. Deferred to a future phase. Massive blast radius vs marginal robustness gain.
- **API-mode integration** (Anthropic Messages API, Gemini API, OpenAI API). v1.3+ candidate; this milestone stays paste-into-web-UI.
- **Per-AI golden-file tests** for prompt content. Optional; planner can tag as supplemental scope.
- **AI-selector keyboard hint** (e.g., `<kbd>1</kbd>` shortcuts to switch AI). Aside; not in v1.2 scope.
