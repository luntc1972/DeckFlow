# Phase 22 — Content KB Site Integration · Combined Browser UAT

> **RESULT: PASSED — both checkpoints, 2026-06-02.** Phase closed; ROADMAP `[x]`,
> STATE updated. Remaining: flip `content.kb.enabled` ON in production via live
> `/Admin/Flags` (manual user action).

Consolidated from 22-03 Task 4 (public) + 22-04 Task 3 (admin). Two human-gated
checkpoints; phase cannot close until both pass. Flag `content.kb.enabled`
stays OFF in prod until after approval.

## Prerequisites

- [ ] Start the dev server locally (do NOT auto-launch — user starts it):
  - Windows cmd: `powershell -ExecutionPolicy Bypass -File scripts\run-web-uat.ps1`
  - PowerShell: `.\scripts\run-web-uat.ps1`
  - WSL/bash: `scripts/run-web-uat.sh`
- [ ] Server is on http://localhost:5173
- [ ] Admin creds set by the UAT launcher (default `admin` / `changeme-local`; override via env)
- [ ] `content.kb.enabled` is a DB-backed feature flag (NOT an env var) — toggle via `/Admin/Flags` or the Content KB admin panel
- [ ] Have a couple of entries publishable (publish via the admin grid below)

---

## Part A — Public browse + detail (22-03 Task 4)

- [ ] **1. Flag OFF (default):** GET `/content-kb` returns the 503 maintenance page (NOT a crash, NOT 404); no "Knowledge Base" nav link.
- [ ] **2. Enable flag:** turn on `content.kb.enabled` via `/Admin/Flags`. Reload `/content-kb` → hub-card grid of published entries; nav link visible.
- [ ] **3. Facets live:** type in the search box and change each facet dropdown → cards show/hide live; match-count announces; "Clear filters" resets.
- [ ] **4. Detail + copy:** click an entry → `/content-kb/{id}` renders the artifact; click "Copy for ChatGPT" → clipboard holds clean text WITHOUT frontmatter (`---` block absent).
- [ ] **5. 375px:** grid single-column, controls full-width, copy button full-width, touch targets ≥44px; switch guild theme → no leaked KB styling. *(screenshot appreciated)*
- [ ] **6. Negative routes:** GET `/content-kb/{id}` for a hidden entry → 404; GET `/content-kb/999999` → 404.

**Part A resume signal:** type "approved" or describe issues (375px screenshots appreciated).

---

## Part B — Admin curation (22-04 Task 3)

Authenticate to `/Admin` (BasicAuth, creds from the launcher).

- [ ] **1. Status panel:** GET `/Admin/ContentKb` → total/published/source counts + an "Index generated" timestamp (max indexed_utc, labeled as index-generation time, NOT "Last loaded"); both grids list ALL rows (hidden shown as "Hidden").
- [ ] **2. Publish round-trip:** click "Publish" on one entry → status flips to "Published", success banner; with the flag ON, reload `/content-kb` and confirm that entry appears publicly.
- [ ] **3. Bulk hide:** "Hide All" on a source → first click "Confirm Hide All", second click hides all of that source.
- [ ] **4. Flag toggle:** toggle `content.kb.enabled` via the panel button → public nav link + `/content-kb` appear/disappear.
- [ ] **5. Reload preserves curation:** "Reload Index from Seed" → modal confirms → after reload, previously-published entries are STILL published (curation preserved).
- [ ] **6. CSRF negative — ALL FOUR mutating POSTs** reject a cross-origin / missing-token request (403 or anti-forgery failure):
  - [ ] `SetVisibility`
  - [ ] `BulkSetVisibility`
  - [ ] `ReloadSeed`
  - [ ] flag `Toggle` (the HIGH-4 fix — confirm the flag toggle now rejects a forged cross-origin POST that previously would have passed)
- [ ] **7. 375px:** sidebar collapses to disclosure; both grids card-stack; buttons ≥44px; switch a guild theme on a public page → no admin KB styling leaked. *(screenshot appreciated)*

**Part B resume signal:** type "approved" (flag stays OFF in prod until the post-UAT flip), or describe issues.

---

## On full approval (both parts)

- [ ] Flip ROADMAP phase 22 → `[x]`
- [ ] Update STATE
- [ ] Flip `content.kb.enabled` ON in production
- [ ] Phase 23 (doc-comment backfill + strip NoWarn) unblocked
