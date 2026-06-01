# Phase 22 — Discussion Log

**Date:** 2026-06-01
**Mode:** discuss (default)

Human-reference only; downstream agents read CONTEXT.md.

## Areas discussed (user selected all 4)

| Area | Options | Selected |
|------|---------|----------|
| Audience | public-behind-flag / admin-only / both | **Both** — public browse page (responsive site shell, flag-gated) + minimal admin manage (status + flag toggle + reload-from-seed) (D-01) |
| Artifact serving + index sync | commit-then-deploy / admin upload-to-/data / local→Render direct | **Commit-then-deploy** — artifacts committed to repo; index rows via committed seed loaded idempotently on Render startup; no upload endpoint (D-02) |
| Entry → artifact presentation | detail page + copy / modal / inline-raw | **Detail page + copy-for-ChatGPT** (Markdig render, reuse copy UX) (D-03) |
| Browse/filter | client-side faceted / server-query + pagination | **Client-side faceted** (chips/dropdowns + search, empty-state CTA, no pagination yet) (D-04) |

## Locked by SCs
content_kb_enabled default OFF (SC5); CSRF (ValidateAntiForgeryToken + SameOriginRequestValidator) on any mutating admin POST incl. reload (SC4/P11); 375px responsive + zero theme bleed (SC5); no transcript/audio/spend in index (SC1).

## Deferred
admin upload-to-/data (rejected for commit-then-deploy); server pagination; full admin CRUD; deck-analysis KB integration (v1.5).

## Notes
- "Both" audience: public browse is the main deliverable; admin manage kept deliberately minimal (status + flag + reload), not full CRUD.
- UI hint = yes → plan-phase will gate on a UI-SPEC (run /gsd-ui-phase 22 or plan with --skip-ui).
- 10 Phase-21.2-UAT artifacts are realistic build/UAT seed data.
