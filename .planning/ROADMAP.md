# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- ✅ **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-33 (shipped 2026-06-10) — see `.planning/milestones/v1.5-ROADMAP.md`
- ✅ **v1.6 Content KB Retrieval Fix + Value Re-Validation** — Phases 34-40 (shipped 2026-06-12) — see `.planning/milestones/v1.6-ROADMAP.md`
- ✅ **v1.7 Local Harvest & Publish Studio** — Phases 41-50 (shipped 2026-06-17) — see `.planning/milestones/v1.7-ROADMAP.md`
- ✅ **Cycle 8 — Hardening & Backlog Burn-down** — Phases 51-54 (shipped 2026-06-17, `2026.06.4`) — see `.planning/milestones/cycle8-ROADMAP.md`
- ✅ **Cycle 9 — Content Pipeline & Publish-Tracking** — Phases 55-58 (shipped 2026-06-19, `2026.06.5`) — see `.planning/milestones/cycle9-ROADMAP.md`

## Phases

<details>
<summary>✅ v1.0 Polish & Quality (Phases 1-5) — SHIPPED 2026-05-02</summary>

- [x] Phase 1: Visual System Tokens — 3/3 plans (UI-VS-01..04)
- [x] Phase 2: Layout, Hierarchy & UX Copy — 3/3 plans (UI-LH-01..02, UX-01..03)
- [x] Phase 3: Tech-Debt Cleanup — 4/4 plans (TD-01..04)
- [~] Phase 4: Security & Bug Fixes — 4/4 plans, ABANDONED 2026-05-02 (rerouted to Phase 5)
- [x] Phase 5: Security & Bug Fixes v2 — 3/3 plans (BUG-01, BUG-02, TD-04 patch + integration test)

Verification: 27/27 must-haves passed. 15/15 v1 requirements shipped.
Full archive: `.planning/milestones/v1.0-ROADMAP.md`

</details>

<details>
<summary>✅ v1.1 Admin Console (Phases 6-8) — SHIPPED 2026-05-08</summary>

- [x] Phase 6: Admin Shell + Flags Foundation — 7/7 plans (ADMIN-01..05, FLAG-01..05)
- [x] Phase 7: Harvest Controls + Stats — 7/7 plans (HARV-01..07)
- [x] Phase 7.1: Categories Flag + SameOrigin AJAX Fix — 2/2 plans (inserted hotfix)
- [x] Phase 8: Analytics — 5/5 plans (ANL-01..05)

</details>

<details>
<summary>✅ v1.2 Multi-AI Prompts (Phases 9-10) — SHIPPED 2026-05-13</summary>

- [x] Phase 9: Bracket UX + AI Selector Foundation — 3/3 plans (BRKT-01, AISEL-01, AISEL-04 Packets portion)
- [x] Phase 10: Claude + Gemini Artifact Optimization — 5/5 plans (AISEL-02, AISEL-03, AISEL-04 Comparison + CedhMetaGap)

Full archive: `.planning/milestones/v1.2-ROADMAP.md`
Audit: `.planning/milestones/v1.2-MILESTONE-AUDIT.md` — documentation-only gaps, all 5 v1.2 reqs functionally satisfied via manual T1-T8 + filename verify.

</details>

<details>
<summary>✅ v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene (Phases 11-15, 999.1-999.8) — SHIPPED 2026-05-23</summary>

**Production phases (5):**

- [x] Phase 11: Web Design Guidelines Audit Fixes — 10/10 plans (WDG-01..10)
- [x] Phase 12: AI-Agnostic URL + Page Rename — 5/5 plans (RENAME-01..03)
- [x] Phase 13: ChatGpt* Class Rename + Summary Doc Comments — 4/4 plans (CLASSRENAME-01..03)
- [x] Phase 14: Broader Codebase Name-vs-Behavior Audit — 4/4 plans (AUDIT-01..03)
- [x] Phase 15: AiPlatform Value Object Refactor — 3/3 plans (AIPLATFORM-01..03)

**Backlog phases (8) — closed v1.3 quality debt:**

- [x] Phase 999.1: AI-Agnostic Prose Adaptation in Razor Views — 7/7 plans
- [x] Phase 999.2: Claude `<result>` Wrapper — Direct JSON Output — 1/1 plan
- [x] Phase 999.3: Packet Download Session Cache — 3/4 plans (P01 rolled into P02-04)
- [x] Phase 999.4: Truncated-JSON Response UX — 1/1 plan
- [x] Phase 999.5: v1.3 Backlog Catch-up + Test Hardening — 4/4 plans
- [x] Phase 999.6: v1.3 Ship-Gate Test Residual Cleanup — 3/3 plans (9→0 failures; 8/8 SECURITY threats CLOSED)
- [x] Phase 999.7: v1.3 Audit Cleanup — 4/4 plans (closed audit findings F-01, F-02, WDG checkbox + STATE arithmetic drift, 999.5-UAT status)
- [x] Phase 999.8: Remove Legacy `chatgpt-*` 301 Redirects — 1/1 plan (22 lines deleted, 0 added)

**Final test gate:** `Failed: 0, Passed: 497, Skipped: 3, Total: 500` preserved across all closure phases.
**Requirements coverage:** 22/22 SATISFIED (10 WDG + 3 RENAME + 3 CLASSRENAME + 3 AUDIT + 3 AIPLATFORM).
**Final audit:** PASSED (re-audit 2026-05-23 supersedes 2026-05-22 tech_debt; all 7 prior findings closed by 999.7 + 999.8).

Full archive: `.planning/milestones/v1.3-ROADMAP.md`
Requirements archive: `.planning/milestones/v1.3-REQUIREMENTS.md`
Audit archive: `.planning/milestones/v1.3-MILESTONE-AUDIT.md`

</details>

<details>
<summary>✅ v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup (Phases 16-27, 21.1-21.2) — SHIPPED 2026-06-03</summary>

- [x] Phase 16: WDG-04 Focus-Trapped Modal — 1/1 plans (MODAL-01)
- [x] Phase 17: Doc-Comment Backfill Part 1 — 2/2 plans (DOC-01 partial)
- [x] Phase 18: Admin Mobile-Responsive Sweep — 2/2 plans (AMOB-01..04)
- [x] Phase 19: Content KB Foundation — Local Schema + Contracts — 4/4 plans (KB schema/contracts; re-scoped 2026-05-26 to local-harvester model)
- [x] Phase 20: Content KB Ingestion + Transcription (local) — 4/4 plans (KB-03/04/05; UAT 2026-05-27: 5-channel harvest, 10/10 captions)
- [x] Phase 21: Content KB Distillation + Artifact Emit (local) — 4/4 plans (KB-01/02/06/07)
- [x] Phase 21.1: Phase 21 Live-UAT Gate (INSERTED) — 1/1 plans (satisfied via 21.2 live claude-distill UAT, 10/10 at $0)
- [x] Phase 21.2: Pluggable LLM Distill CLI Backends (INSERTED) — 2/2 plans (KB-10/11; codex backend → KB-12 backlog)
- [x] Phase 22: Content KB Site Integration — 4/4 plans (KB-08/09; both UAT checkpoints passed; prod flag flip pending)
- [x] Phase 23: Doc-Comment Backfill Part 2 + Strip NoWarn — 5/5 plans (DOC-01/02; gate scoped to DeckFlow.Web, Core 186 sites deferred)
- [x] Phase 24: Card Category Lookup Fix — quick-fix (CAT-01; live smoke passed 2026-05-25)
- [x] Phase 25: Admin Harvested-Decks Paged Grid — 2/2 plans (AHD-01)
- [x] Phase 26: Category Cache Schema Normalization — 2/2 plans (DBO-01; SC2 amended per b1a5cc8, SC3 69s→0.66ms)
- [x] Phase 27: Deck-Cache Content-Hash Dedup + 5-Day Refresh — 1/1 plans (CAT-02)

**Stats:** 343 commits, 638 files, +54,651/−4,726 LOC, 2026-05-23 → 2026-06-03 (11 days).
**Requirements:** 20/20 active v1.4 REQ-IDs satisfied (KB-12 → backlog). Final tests: Core 257/257, Web 528 pass / 5 PG-skips.
**Audit:** tech_debt (0 critical gaps; artifact-hygiene items carried to v1.5 — see audit archive).

Full archive: `.planning/milestones/v1.4-ROADMAP.md`
Requirements archive: `.planning/milestones/v1.4-REQUIREMENTS.md`
Audit archive: `.planning/milestones/v1.4-MILESTONE-AUDIT.md`

</details>

<details>
<summary>✅ v1.5 Deck Primer Generator + Content KB Integration + Housekeeping (Phases 28-33) — SHIPPED 2026-06-10</summary>

**Milestone Goal:** Ship the Deck Primer Generator as a fourth paste-ready workflow, wire Content KB knowledge into deck-analysis prompts, and clear v1.4 quality debt.

- [x] Phase 28: Housekeeping Bundle — HSK-03/04 (HSK-02 re-demoted) — completed 2026-06-04
- [x] Phase 29: Core XML-Doc Backfill + Gate Widen — HSK-01 — completed 2026-06-05
- [x] Phase 30: Content KB Integration — KBI-01..06; prod UAT passed — completed 2026-06-07
- [x] Phase 32: Expert Context Selection — SEL-01..06; pin/follow/evergreen 4-tier merge — completed 2026-06-08
- [x] Phase 33: Admin Content KB Curation UX — KBUX-01/02 — completed 2026-06-09
- [x] Phase 31: Deck Primer Generator — PRM-01..12; fourth paste-ready workflow — completed 2026-06-09

**Audit:** passed — 30/30 requirements; SEL-02 expert-pin bug diagnosed + fixed at close (`a106c6a`). Full phase details: `.planning/milestones/v1.5-ROADMAP.md`.

</details>

<details>
<summary>✅ v1.6 Content KB Retrieval Fix + Value Re-Validation (Phases 34-40) — SHIPPED 2026-06-12</summary>

- [x] Phase 34: Content KB Retrieval Fix — KBR-01..04 — completed 2026-06-10
- [x] Phase 35: Value Re-Validation Gate — KBV-01..04; gate = MARGINAL → Phase 36 skipped — completed 2026-06-10
- [~] Phase 36: Creator Philosophy-Profile + KB Un-Dark — SKIPPED (gate = MARGINAL; see 35-GATE-VERDICT.md)
- [x] Phase 37: Retire KB Clip-Injection — RET-01..05 — completed 2026-06-10
- [x] Phase 37.5: Rebuild KB Corpus — corpus re-distilled high-signal — completed 2026-06-11
- [x] Phase 37.6: Harvest Video Block + Hard-Delete — VBLK-01..04 — completed 2026-06-11
- [x] Phase 38: DeckController + CommandRunners SRP Split — SRP-01..03; route-parity + live smoke — completed 2026-06-12
- [x] Phase 39: Architecture Review Refactor (Finding A) — extract IDeckEntryLoader + IScryfallCardResolver — completed 2026-06-12
- [x] Phase 40: Core.Tests Health Pass — 320/0 deterministic — completed 2026-06-12

**Stats:** ~122 commits, 2026-06-10 → 2026-06-12. Gate-driven: MARGINAL → retire-pivot. Audit: passed.
Full archive: `.planning/milestones/v1.6-ROADMAP.md`

</details>

---

<details>
<summary>✅ v1.7 Local Harvest & Publish Studio (Phases 41-50) — SHIPPED 2026-06-17</summary>

**Goal:** A standalone local Blazor Server tool to discover YouTube videos, harvest + distill them, review/approve in a UI, and publish approved entries to deckflow.gg — via repo-commit→Render deploy and/or direct prod-DB push.

- [x] Phase 41: Studio Scaffold + Secrets Wiring (1/1) — completed 2026-06-13
- [x] Phase 42: Orchestrator Extraction (5/5) — completed 2026-06-13
- [x] Phase 43: Approval Status + Safe Upsert (2/2) — completed 2026-06-13
- [x] Phase 44: Admin Grid Lazy Paging (3/3) — completed 2026-06-14
- [x] Phase 45: Harvest + Distill UI (4/4) — completed 2026-06-15
- [x] Phase 46: Review Queue + Commit-Publish Path (5/5) — completed 2026-06-16
- [x] Phase 47: Direct Prod-DB + SCP Publish Path (3/3) — completed 2026-06-16
- [x] Phase 48: UI Audit + Remediation (3/3) — completed 2026-06-17
- [x] Phase 49: Dapper Data-Access Adoption (5/5) — completed 2026-06-14
- [x] Phase 50: Code-Style Enforcement — ReSharper Reconciliation + PR Gate (4/4) — completed 2026-06-14

Verification: 10/10 phases verified (4 passed, 6 human_needed = automated PASS, operator smoke deferred). 23/23 requirements satisfied. Audit: tech_debt (integration clean). Full detail: `.planning/milestones/v1.7-ROADMAP.md`.

</details>

---

<details>
<summary>✅ Cycle 8 — Hardening & Backlog Burn-down (Phases 51-54) — SHIPPED 2026-06-17, `2026.06.4`</summary>

- [x] Phase 51: Verify v1.7 on main + non-prod UAT (HARD-01, HARD-03, OPS-01) — completed 2026-06-17
- [x] Phase 52: Live prod-publish verification (HARD-02) — completed 2026-06-17
- [x] Phase 53: Architecture backlog burn-down (ARCH-01, ARCH-02) — completed 2026-06-17
- [x] Phase 54: Feature debt — SpellbookCombo ranking + Gemini size-verify (FEAT-01, FEAT-02) — completed 2026-06-17

Full archive: `.planning/milestones/cycle8-ROADMAP.md`
Requirements archive: `.planning/milestones/cycle8-REQUIREMENTS.md`

</details>

---

<details>
<summary>✅ Cycle 9 — Content Pipeline & Publish-Tracking (Phases 55-58) — SHIPPED 2026-06-19, `2026.06.5`</summary>

- [x] Phase 55: Publish-State Foundation (PUB-01, PUB-02) — completed 2026-06-18
- [x] Phase 56: Studio Surfaces (BROWSE-01/02/03, REM-01/02, ADD-01, PUB-03) — completed 2026-06-18
- [x] Phase 57: Admin Surface + Distill Quality (SITE-01, DIST-01) — completed 2026-06-18
- [x] Phase 58: Dogfood (DOGFOOD-01) — completed 2026-06-19 (all 4 SCs PASS; SC2 DirectPush publish-visible fix + SECURED 9/9)

Full archive: `.planning/milestones/cycle9-ROADMAP.md`
Requirements archive: `.planning/milestones/cycle9-REQUIREMENTS.md`

</details>

---

## Backlog

### Harden deck-source host matching (SSRF/abuse) (BACKLOG — captured 2026-06-20; HIGH priority — address at next milestone START)

**Goal:** Close a host-spoofing hole in deck-URL loading shared by every deck tool. Origin: Codex code review of the mana-base feature (2026-06-20), deferred out of that feature branch because the fix is in shared code touching all deck tools.

**Problem:** Platform detection uses substring host matching — `DeckFlow.Core/Loading/DeckEntryLoader.cs` `LoadFromSourceAsync` (~L121/L127) and `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` (~L105) test `uri.Host.Contains("moxfield.com")` / `Contains("archidekt.com")`. So a hostile URL like `https://moxfield.com.evil.tld/decks/123` is treated as a trusted deck source on anonymous public endpoints (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/manabase`). In the Moxfield-fallback path the original attacker URL is then forwarded to Commander Spellbook as `url=…`, widening the abuse surface.

**Fix:** Require exact host or approved-subdomain matching (`host == "moxfield.com" || host.EndsWith(".moxfield.com")`; same for archidekt.com), and on the Moxfield fallback always reconstruct the canonical `https://moxfield.com/decks/{deckId}` URL instead of forwarding the submitted one. Add regression tests for spoofed hosts (`moxfield.com.evil.tld`, `evilmoxfield.com`, `moxfield.com@evil.tld`).

**Requirements:** TBD
**Plans:** 0 plans

Scope note: shared Core code — affects all deck tools, needs its own change + regression tests. Promote via `/gsd-review-backlog` at next cycle open.

### Validate Content KB value — A/B ChatGPT output with vs without expert context (BACKLOG — high priority; was todo, moved 2026-06-19 at Cycle-9 close)

**Goal:** Gating experiment — prove the Content KB actually makes ChatGPT's deck analysis *better* before investing further (e.g. the creator philosophy-profile redesign). `content.kb.enabled` is OFF in prod and the subsystem is unproven on end-output quality (Cycle 9 validated the *pipeline* produces cleaner distills, but not that injected context lifts the final ChatGPT answer).

**Experiment:** For a handful of representative decks, generate the analysis prompt twice — with vs without expert-context clips — run both through ChatGPT, compare answer quality (signal beyond ChatGPT's own MTG knowledge, actionable specificity, creator-voice). Judge blind if feasible.

**Decision criteria:** Clear lift → green-light philosophy-profile build + flip `content.kb.enabled` ON. Marginal → reconsider the KB (per-deck targeted retrieval or user-supplied sources instead of whole-channel pre-distill).

**Spike-able** via `/gsd-spike` — lightweight, no production code to start. Origin: Phase 30 UAT (2026-06-09). Promote via `/gsd-review-backlog`.

### Studio "Pull from Prod" — prod→local sync (BACKLOG — captured 2026-06-19 during Phase 58 dogfood; for next cycle)

**Goal:** Make the Studio local store reflect current production data. Today Studio is strictly one-way (DirectPush local→prod); the local `content-kb.db` drifts from prod (prod 109 rows, local only what was harvested + push-stamped). Add a "Pull from Prod" page/action — the inverse of `DirectPush.razor` — so an operator can mirror prod into local.

**Scope (Option B — full pull; Option A read-only "Prod vs Local" drift view was the cheaper alternative, deferred):**
- Read prod rows via `prodStore.GetAllRowsAsync` (plumbing already exists — DirectPush Stage 1 uses it for the diff).
- Upsert them into the local `IContentSiteIndexStore` (`UpsertRowAsync` exists).
- SCP-**download** the artifact `.md` files from prod `/data` into local `content-kb/` — **new**: only SCP upload exists today (`ISshArtifactUploader`); needs a download counterpart.
- Read-only prod DB connection acceptable for the row read; SCP read for artifacts. AI-never-writes-prod rule unaffected (this only writes LOCAL).

**Open design questions (resolve when planned):**
- Merge semantics: prod-wins vs local-wins for in-flight local edits.
- `approval_status` handling (prod defaults `pending`; local may be `approved`) — don't clobber local approvals?
- `is_visible` / `is_hidden` / `pushed_to_prod_utc` reconciliation.

**Requirements:** TBD
**Plans:** 0 plans

Origin: operator wants Studio to mirror prod (Phase 58 dogfood, 2026-06-19). Promote via `/gsd-review-backlog` when the next cycle opens.

### Studio UI polish pass (BACKLOG — captured 2026-06-19; medium priority)

**Goal:** Give DeckFlow.Studio a real design pass. Today it's the **stock Blazor Server template** — default nav sidebar (`MainLayout`/`NavMenu`), default 64-line `site.css` (stock `#1b6ec2` blue), a 19-line placeholder `Home.razor`, and 6 pages each hand-rolling raw Bootstrap (`Harvest.razor` is 1651 lines). Functional but unbranded and inconsistent. The public site got a UI audit (Phase 48, 16→20/24); Studio never has.

**Scope — three tiers (operator-only desktop tool; no functional/feature changes):**

*P1 — shell + design system:*
- Shared Studio design tokens (color/spacing/type) in one stylesheet; align with deckflow.gg brand (logo/title, accent, optional dark mode).
- Replace the stock template chrome: app header/nav, page-title pattern, consistent `.content` layout container.
- Turn `Home.razor` into a real landing/dashboard — pipeline state at a glance (counts by VideoStatus / PublishState, quick links to Harvest/Review/Publish).

*P2 — per-page consistency:*
- Unify status/publish-state badges into shared CSS classes driven by `VideoStatus` / `PublishState` (today colors are defined ad-hoc per page in Review/Publish/Harvest/Blocked).
- Consistent table, form, alert, button-hierarchy, and primary-action patterns across all 6 pages (Home/Harvest/Review/Publish/DirectPush/Blocked).
- Systematic loading + empty states (some spinners exist; not uniform).

*P2 — channel/source column + filter (concrete, highest-value ask):*
- **Show the source/channel each video came from in the grids.** `ContentSiteIndexRow.Source` already holds it (e.g. "The Command Zone") but it is **not surfaced** — the Review grid shows only Title/Tags/Status/Publish State/Actions and `ReviewViewModel` doesn't even expose `Source`. Add a **Source/Channel column** to the Review grid (and wherever a per-row list exists).
- **Filter by channel/source** in those grids — a dropdown/segmented filter populated from the distinct `Source` values (mirror the web `/Admin/ContentKb` creator-filter behavior; that grid is source-grouped). Lets the operator focus one channel at a time (e.g. dedup @salubrioussnail, find Based-Deck-Dept never-published).
- Low-risk: data + persistence already exist; this is ViewModel field + column + client-side filter state.

*P3 — optional:*
- Responsive / table-overflow handling, dark mode, keyboard affordances.

**Approach:** run as a `/gsd-ui-phase` (UI-SPEC design contract) → implement → `/gsd-ui-review` audit, same flow as Phase 48 for the public site.

**Explicitly out:** no new features, no behavior change (Pull-from-Prod etc. are separate); Studio stays a local operator tool. Promote via `/gsd-review-backlog`.

### Codex Distill Backend (BACKLOG — low priority; was Phase 21.3, demoted 2026-06-01; re-demoted 2026-06-04 after Phase 28 discovery)

> Investigation 2026-06-04 (codex 0.136.0, Phase 28-03 / `28-DISCOVERY.md`): `--sandbox read-only` documented as "can read files in workspace" (structural evidence from binary). No `--no-tools` flag exists. `deny_read` glob mechanism requires `codex-linux-sandbox` + bubblewrap infrastructure not present, with no documented global read disable. Re-investigable when a future codex version provides documented read-blocking. D-03 re-demote applied; user ratified 2026-06-04.

**Goal:** Add the `codex` provider to the Phase 21.2 distill backend factory, with a PROVEN tool/read-isolation boundary for untrusted transcript input. codex `exec` is an agent and `--sandbox read-only` blocks writes but not reads, so a prompt-injected transcript could read+echo local files; ship codex only once the read boundary is demonstrably closed (verified no-tools mode, OR a sandbox/container exposing only stdin). claude backend (Phase 21.2) already covers the subscription-distill use case, so codex is a nice-to-have second provider — low priority.
**Requirements:** KB-12 (codex CLI distill backend with proven untrusted-input read isolation)
**Depends on:** Phase 21.2 (provider factory + CliCommandSpec + CLI service seam — shipped)
**Plans:** 0 plans

Acceptance when promoted: `DECKFLOW_LLM_PROVIDER=codex` works via the existing factory/CliCommandSpec seam (no new arch; openai+claude unchanged); the codex spawn provably cannot read arbitrary local files under a malicious transcript (documented isolation + sentinel-file exfil test); same JSON-repair/ValidateTags/timeout/ledger-bypass guarantees; live codex distill over the UAT db emits valid artifacts + spend=0; E5/E6 human sample passes. Promote via `/gsd-phase` (renumber) when prioritized.

### edhtop16 Filter Defaults vs DeckFlow Filter Defaults (BACKLOG — unnumbered, was 999.3 before collision with active Packet Download Session Cache phase; renumber when promoted)

**Goal:** [Captured for future planning]
**Requirements:** TBD
**Plans:** 0 plans

Captured 2026-05-17 during Phase 13 UAT T5. cEDH Meta-Gap fails to find Plagon, Lord of the Beach decks even though edhtop16.com shows multiple recent entries (2025-05 through 2026-01). DeckFlow filters (Six Months + Top Performing + minEventSize) return zero matches; edhtop16.com site UI likely uses different default filter window/event-size threshold/standing cutoff.

Repro (2026-05-17 14:18:57 + 14:19:09 in `web-20260517.log`):

- Commander: "Plagon, Lord of the Beach"
- Filters: SixMonths, TopPerforming, minEventSize=default, maxStanding=default
- Result: `InvalidOperationException` at `MetaGapService.cs:160` — "No EDH Top 16 decks matched your filters..."
- edhtop16.com browser shows entries from 2026-01-04, 2026-01-18, 2025-09-27, 2025-05-24

Pre-existing — predates Phase 13 (MetaGapService logic unchanged by rename). Investigate:

1. edhtop16 GraphQL `commander(name)` lookup: does "Plagon, Lord of the Beach" match the stored canonical name exactly?
2. Default DeckFlow form filter values vs site UI defaults — alignment audit.
3. minEventSize=50 default may be too restrictive — site UI may use 30.
4. timePeriod=SixMonths may map to ≤180 days where site uses calendar months (sometimes 183-184 days).

Plans:

- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 39 architecture findings — deferred (ARCH-02 executes Finding A; rest below per SC3)

See `.planning/phases/39-architecture-review/39-AUDIT.md` + `39-AUDIT-CODEX.md` (two independent audits).
- **B — Split `CategoryKnowledgeRepository`** (1276 LOC, 24 methods → Schema / DeckQueue / CardCategory). Live-path Core god-repo; strong existing test net (17 facts + parity + dedup). Effort L / Risk M.
- **C — Split `ContentKbCommandRunners`** (1508 LOC, 5 sub-domains → Harvest / Distill / Source runners). Internal seams pin behavior. Effort M / Risk M. **NOTE: v1.7 Phase 42 (ORCH-01) addresses Finding C as a side-effect by extracting domain logic to Core.**
- **D — Finish `Services/` concern-foldering + extract `Program.cs` `AddDeckFlowXxx()`** (48 flat files; empty `Services/Content/` = stalled migration). Pure file/namespace moves. Effort M / Risk L.
- **E — Relocate misplaced domain logic to `DeckFlow.Core`** (deck-stat classifiers in DeckComparisonService; distill cost/validation in ContentKbCommandRunners). Effort M / Risk L.
- **F — Strengthen dual-dialect abstraction** (33 `IsPostgres`/`IsSqlite` branches across 7 stores → dialect render methods; remove Web `Feedback*` SQL from Core `IRelationalDialect`). ⚠ Postgres path has no automated test. Effort M / Risk M.
- **ADR-note tier:** G packet cache-key `IPacketCacheKeyStrategy` · H `IScryfallThrottle` seam · I `IMemoryCache` SizeLimit doc · J `System.CommandLine` beta4 deliberate-pin ADR · K residual test gaps (middleware-ordering integration test; Polly policy-shape assertion).

### Deferred to v1.7+ (per v1.5/v1.6 scope decisions)

- **Gemini paste-limit workaround** (`DECKFLOW_GEMINI_ENABLED` stays flag-gated; needs split-message vs direct-API path decision)
- **SpellbookCombo ranking fields** (PRM-08 — parser drops `manaValueNeeded`/`popularity`/`uses`; priority ranking degraded)
- **Embedding/vector retrieval** (pgvector / ONNX sentence-transformers) — deferred until corpus >~500 videos; RAM-cap risk at current size

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 shipped 2026-05-23 | v1.4 shipped 2026-06-03 | v1.5 shipped 2026-06-10 | v1.6 shipped 2026-06-12 | v1.7 shipped 2026-06-17 | Cycle 8 shipped 2026-06-17 | Cycle 9 shipped 2026-06-19*
