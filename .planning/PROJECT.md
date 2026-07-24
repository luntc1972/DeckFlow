# DeckFlow

## What This Is

DeckFlow is a Magic: The Gathering deck analysis tool for cEDH and Commander players, deployed live at https://www.deckflow.gg. It pulls deck data from Archidekt and Moxfield, generates AI-agnostic prompt artifacts (ChatGPT / Claude / Gemini) for deck analysis, and provides synergy/category knowledge derived from the user's own crawled deck history. Audience: serious deck-builders who want a structured "compare, analyze, decide" workflow rather than a one-click recommender.

## Core Value

**Every supported workflow must produce output the user can paste into ChatGPT, Claude, or Gemini and get back a useful answer in one round-trip — without the user reformatting anything.** Visual polish, theme variety, and admin tooling all serve that core. If the prompt artifacts are wrong or missing, nothing else matters.

## Current State

**Shipped:** Cycle 14 — Deeper Deck Evaluation (2026-07-03, CalVer `2026.07.1`) — phases 79-81, developed on branch `plan/cycle-14-deck-eval-depth` and squash-merged to `main` (`701ec2fa`). Three flag-gated read dimensions on the existing engines, zero new deps: **Interaction & Answers Audit** — bucketed, card-backed interaction counts (removal / board wipes / counterspells / protection-recursion / stax-taxation) + coverage-gap advisories in `/deck-analysis` (Phase 79, `analysis.interaction-audit`); **Win-Condition & Combo Map** — ranked Commander Spellbook combos + one-card-away near-combos in coarse early/mid/late assembly bands, disclosing "combo data unavailable" vs "no win conditions" (Phase 80, `analysis.wincon-map`); **Opening-Hand / Mulligan Evaluator** — keepable-hand band, keep-size process, colors/curve, and spell-attributed representative openers off the existing London-mulligan sim (no second pass; cast% byte-identical) on `/manabase` (Phase 81, `analysis.mulligan-eval`). Build 0/0; Core 1053 / Web 1158 pass; CI green on `main` (`28694830980`); milestone audit **PASSED** (13/13 requirements, 5/5 integration); headless live smoke desktop 1280 + mobile 390. All three cycle flags seeded OFF. Prior milestones: **Cycle 13 — Deck Evaluation & Creator Output** (2026-06-30, `2026.06.10`); **Cycle 12 — Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence** (2026-06-27, `2026.06.9`, archived).

**Shipped (latest):** **Cycle 19 — Cut Lab Upgrade Hardening** (2026-07-24, CalVer `2026.07.9`) — phases 108-111 on branch `gsd/cycle19-cut-lab-upgrade`, merged to `main` and archived. Server-authored Cut Lab UI patch contract, what-if service consolidation, navigation/pool discovery, combo intelligence (Phase 110.1), and a regression gate that caught and fixed four WCAG defects. Cut Lab remains dark in prod (`tool.cut-lab.enabled` OFF) pending operator flag-flip UAT. Prior: **Cycle 18 — Cut Lab** (2026-07-23, `2026.07.8`), phases 101-107.

**Shipped:** **Cycle 16 — Content-KB Prod↔Git↔Studio Sync Hardening** (2026-07-11, CalVer `2026.07.3`) — phases 88-93, developed on branch `plan/cycle-16-kb-sync`. A Studio/ops hardening cycle making the Content-KB drift-proof: git is the single source of truth for bodies, the prod index row subordinate and reconstructable from the seed. Shipped: the index-row integrity hotfix (no visible-while-`pending` prod rows, composite-key diffing), a unified body-inclusive `body_sha256` with render-time mismatch detection, DirectPush git-`/app`-only serving + `index-seed.json` re-export + hash-gated expand→verify→contract (flag `sync.directpush-gitbody`), a prod↔git↔seed Reconcile page with gated seed-drift soft-hide (flag `sync.reconcile`), field-authoritative Pull hardening, a round-trip integration test, and a Studio **Git Body Coverage** pre-flip audit. 17/17 requirements satisfied and a clean security review; both flags ship **OFF** (live flip gated behind the operator pre-flip walk). Prior milestones: **Cycle 15 — Cleanup, Refactor & Visual Polish** (2026-07-05, `2026.07.2`); **Cycle 14 — Deeper Deck Evaluation** (2026-07-03, `2026.07.1`).

**Next:** **Cycle 20 — Personal Tools** reframes Cycle 17's creator-style work as admin-only tooling (`docs/research/personal-tools-admin-reframe-design.md`). Cycle 17 itself never shipped publicly: the 2026-07-19 legal review turned creator-crawl off as a public feature, and the branch `plan/cycle-17-creator-style` is now 777 commits behind `main`. Its code is ported forward, not rebased; the branch is preserved at origin as the historical record. Operator debt from Cycles 12–14 (manual prod deploy + 7 flag flips) is **DONE** (confirmed 2026-07-04). Cycle-16 operator gates outstanding: push branch + squash→main + tag `2026.07.3`, and the FU-3 pre-flip walk before flipping `sync.*` ON (both flags ship OFF). Carry-forward backlog still open: scheduled/bulk harvest (AUTO-03/04), SEO/growth lane (SEO-01..05), matchup/meta-threat read (cedh-meta-gap lane), ADMIN-01 (Flags sorting), a future manabase-engine refactor (needs a numeric-parity harness first), and a KB "commander advice" content class for filtered videos.

## Current Milestone: Cycle 20 — Personal Tools

**Goal:** Land Cycle 17's creator-style intelligence and Deck Tendencies on `main` as admin-only personal tools, carrying real data.

**Target features:**
- Port the Cycle 17 engine (Phases 94–98) and its Web services onto a fresh branch off `main`, leaving the stale planning-doc tree behind.
- Reframe the creator-style surface to `/Admin/CreatorStyle` behind the existing BasicAuth branch, dropping Phase 100's public plumbing (feature flag, `ToolRegistry` entry, SEO/sitemap wiring, public help topic).
- Bring Deck Tendencies (`/Admin/CreatorProfile`) into the same personal-tools section, with an `/Admin` landing entry for both.
- Add a `creator-style-import-stated` CLI command plus a hand-authored stated-rules seed sourced from the P89/P90 prototype, since the distill toolchain is unavailable.
- Run the operator sequence end to end so `/Admin/CreatorStyle` renders a real critique rather than an empty state.

**Key context:** Driven by the 2026-07-19 creator-crawl legal call (public creator-style is off the table) plus 777 commits of drift on `plan/cycle-17-creator-style`. The approved design spec at `docs/research/personal-tools-admin-reframe-design.md` is authoritative. Work happens on `feat/personal-tools`; the old branch stays untouched at origin as the historical record. Phase numbering continues after Cycle 19, so Cycle 20 starts at Phase 112.

## Shipped Milestone: Cycle 19 — Cut Lab Upgrade Hardening (SHIPPED 2026-07-24, `2026.07.9`)

**Goal:** Reduce Cut Lab drift risk and improve the mobile/operator workflow before the tool moves from dark launch toward broader use.

**Target features:**
- Replace client-side Cut Lab domain re-derivation with server-authored UI patch DTOs so counts, export eligibility, proposals, finding rows, and serialized state come from one source of truth.
- Consolidate what-if preview and commit behavior behind a single service used by both JSON and no-JS controller paths.
- Add Cut-Lab-scoped anchors, mobile jump navigation, collapsible sections, lock-pool filtering/search, package assignment help, and text-first card/combo context disclosures.
- Preserve today's shipped card-pill behavior: role-group and Structural card evidence pills lock/unlock canonical pool cards, while non-card evidence remains inert.

**Key context:** Cycle 18 shipped and is archived at `.planning/milestones/ws-cut-lab-2026-07-23/`. This milestone promotes the archived follow-up backlog (`BACKLOG-cut-lab-followups-2026-07-22.md`) into active GSD work. Phase numbering continues after the shipped Cut Lab phases 101-107, so Cycle 19 starts at Phase 108. Cycle 17 remains a separate worktree thread and is not part of this milestone.

## Shipped Milestone: Cycle 16 — Content-KB Prod↔Git↔Studio Sync Hardening (SHIPPED 2026-07-11, `2026.07.3`)

**Goal:** Make the Content-KB publish loop convergent and drift-proof — git is the single source of truth for bodies, the prod index row is subordinate and reconstructable from git, and every sync path (Publish, DirectPush, Pull, seed reload) is an idempotent, body-hash-verified one-way keyed upsert.

**Target features** (from `docs/research/kb-prod-sync-roadmap.md` + `kb-prod-sync-fix-design.md`, incl. Codex gpt-5.4-high plan-review adjustments and the 2026-07-05 live prod drift audit; Codex-revised sequencing):

- **Index-row integrity hotfix (ships first)** — DirectPush writes `approved` on insert/update (kills the visible-while-`approval_status='pending'` public exposure, C1); `ContentSyncDiffClassifier` keyed by `(natural_key_type, natural_key_value)` composite instead of `PinId` (C4 collision); fix the false "no DDL against prod" comment / guard the diff read (SYNC-04/05/06).
- **Content-hash foundation** — `body_sha256` column on `content_site_index` (both dialects + seed JSON), ONE unified body-inclusive signature replacing the two divergent schemes, render guard refusing rows whose on-disk body hash ≠ stored hash (SYNC-01/02/03; makes the CP437-mojibake class detectable).
- **DirectPush correctness + seed sync** — split per Codex HIGH: **P-a architecture flip** (bodies reach prod only via git `/app`; drop the `/data`-SFTP-first overlay) then **P-b ordering + stamping** (DirectPush re-exports `index-seed.json` so deploys can't revert it; hash-gated expand-contract ordering — body deployed+verified before `is_visible` flips; `pushed_to_prod_utc` stamped only after prod confirms). Flag `sync.directpush-gitbody` (SYNC-07..10).
- **Reconcile + seed lifecycle** — row-level seed-management marker FIRST (Codex HIGH: seed-delete unsafe without it), then a NEW prod↔git↔seed reconciler + persistent discrepancy store (published-orphans / file-orphans / seed-drift / body-hash-mismatch; deterministic IDs, resolution-by-absence, dry-run before destructive), then gated seed-delete for removals. Flag `sync.reconcile` (SYNC-11/12 split).
- **Pull hardening** — per-field master on Pull-from-Prod: body+content ← git tree, DB-only operator fields (`is_visible`/`is_hidden`/`approval_status`) ← prod preserved not clobbered; stale-checkout (`git pull` first) guard; divergence surfaced to operator, never silent-adopted (SYNC-13/14/15).
- **Round-trip integration test** — distill → Publish/DirectPush → prod store → web body resolution → deploy/reseed → PullFromProd → reconcile on containerized Postgres + real git tree; served body == published body, `body_sha256` matches end-to-end, no-revert-after-reseed (SYNC-16).

**Out of scope this cycle:** Cycle 17 creator-style features; retiring DirectPush entirely (P-a/P-b make it consistent; retirement is a later-cycle decision); CDC/queue-based sync (upsert + hash + ordering fits 512MB/Render); public-app feature changes (Studio/ops cycle — the only public-surface change is the hash-mismatch render guard and the C1 visibility fix).

**Key context:** Live prod drift audit (2026-07-05, read-only) validates the cycle: 106 prod rows with only 36 in the approved seed (70 not reconstructable), 57 hidden+pending rows re-accumulated after a manual 63-row delete, ~328 file-without-row orphans, 32 mojibake bodies (15 prod-visible, repaired out-of-band — systemic fix is the body hash). Decisions owed at plan time: approval ownership (local-authoritative for DirectPush — confirm), `sync.*` flag plumbing home (web-DB flag vs Studio config vs both; Studio doesn't register the web flag system today). CalVer, NAMED not numbered (ADR 0002); phase numbering continues from 87 → 88+. Developed on branch `plan/cycle-16-kb-sync` + worktree `../deckflow-cycle16`.

## Shipped Milestone: Cycle 15 — Cleanup, Refactor & Visual Polish (SHIPPED 2026-07-05, `2026.07.2`)

**Goal:** Pay down accumulated tech-debt and finish deferred polish without changing public behavior — every paste artifact byte-identical, every theme render unchanged.

**Target features:**
- **Packet-service family SRP split** — extract shared prompt-building + Scryfall-reference-resolution collaborators from the four parallel god-services (`DeckAnalysisPacketService` 2372 LOC / `DeckComparisonService` 1033 / `MetaGapService` 956 / `DeckPrimerPacketService` 904). Behavior-neutral; artifacts byte-identical (ADR-0001 prompt decoupling holds — no shared prompt-prose helper).
- **`--accent-strong` semantic-token migration** — finish migrating the 27 theme files off the overloaded `--accent-strong` onto `--link`/`--danger`/`--focus`/`--cta-border`; fixes error-text-reads-as-link in red guild themes.
- **UI audit re-score → ≥20/24** — re-run the 6-pillar UI audit, measure current score, fix gaps to clear ≥20/24; includes the owed DirectPush Stage 4 live desktop+mobile verify + no-op success-copy fix (`DirectPush.razor:441`).
- **`chatgpt-*` naming cleanup** — rename ~1545 `chatgpt-*` refs (CSS classes/data-attrs across 25 theme forks + TS constants + views) to AI-agnostic names; render byte-identical.
- **Refactor-review sweep** — a code-review pass to surface remaining SRP/duplication targets (candidates: `deck-sync.ts` 2877, `Harvest.razor.cs` 1222); fold confirmed items into scope.
- **Admin flags sort by on/off** — the one small admin-UX addition: `/Admin/Flags` sortable by enabled state so the current toggle picture is scannable (view-only; no flag semantics change).

**Out of scope this cycle:** manabase engine refactor (`CastabilitySimulator`/`ManabaseAnalyzer`/`ManabaseClassifier` — behavior-critical, no byte-identical gate, just heavily worked in Cycles 12/14; needs a numeric-parity harness first → deferred to backlog); feature lanes (cedh-meta-gap / SEO / auto-harvest); framework migration; the manabase engine's numeric behavior.

**Key context:** Pure tech-debt cycle, zero net-new user features — the byte-identical artifact + theme-render constraint is the milestone gate. Prompt-variant decoupling (ADR-0001) holds. CalVer, NAMED not numbered (ADR 0002); phase numbering continues from 81 → 82+. DeckController god-class split is already DONE (verified 2026-07-04 — split across `DeckPacketController`/`DeckLookup`/`DeckSync`/`DeckPrimer`), so it is NOT in scope.

## Shipped Milestone: Cycle 14 — Deeper Deck Evaluation (SHIPPED 2026-07-03, `2026.07.1` — archived, see `.planning/milestones/cycle14-ROADMAP.md`)

**Goal:** Extend the deck-analysis paste-artifact engine with three deeper read dimensions, all building on the existing Monte-Carlo castability sim, Commander Spellbook integration, multi-axis score, and `DeckStatClassifier` — each flag-gated and byte-identical when OFF.

**Target features:**
- **Interaction & answers audit** — count and categorize the deck's interaction (removal, counterspells, stax, protection, board wipes) and flag coverage gaps; new paste-artifact section + view readout.
- **Win-condition & combo map** — enumerate the deck's win lines and combo pieces (deeper Commander Spellbook use), with redundancy and an assembly-turn read; surfaces "how this deck wins."
- **Opening-hand / mulligan evaluator** — surface keepable-hand probability plus a color/curve read off the existing Monte-Carlo simulation, as a discrete deck-eval metric.

**Out of scope this cycle:** matchup / meta-threat read (deferred — deepens cedh-meta-gap, a separate lane); folder-level deck sharing; live-stream overlays; rebuilding the castability engine (only new readouts on top of it).

**Key context:** CalVer, NAMED not numbered (ADR 0002); phase numbering continues from 78 → 79+. **Cycle 13** (P75-78) shipped to `main` as `2026.06.10` — see Current State above. Prompt-variant decoupling (ADR-0001) holds — any new artifact renders in ChatGpt/Claude/Gemini variants WITHOUT a shared helper. Each new feature is flag-gated and seeded OFF, byte-identical until an operator enables it. Developed on branch `plan/cycle-14-deck-eval-depth` + worktree `deckflow-cycle14`.

## Shipped Milestone: Cycle 11 — Security, Visibility Control & Creator-Lens (SHIPPED 2026-06-25, `2026.06.8`)

**Goal:** Close two HIGH-priority security/data holes, give the admin full tool-visibility control over the public site, validate whether the Content KB actually improves AI output, and run a design pass on Studio.

**Target features:**
- **SSRF / host-spoof fix** — replace substring host matching in `DeckEntryLoader.LoadFromSourceAsync` + `MoxfieldApiDeckImporter` with exact/approved-subdomain matching; reconstruct canonical Moxfield URL on the fallback path; spoof-host regression tests. Shared Core code touching every deck tool (HIGH backlog, captured 2026-06-20 from Codex review).
- **Prod artifact gap remediation** — 86 of 109 prod `content_site_index` rows have no `.md` on Render `/data`; confirm whether the live site serves content from `/data` or the DB column, then re-upload, reconcile down, or downgrade to cosmetic (HIGH backlog, found Phase 60 live verify).
- **Admin tool-visibility toggles** — admin can turn off any public tile/page (Analysis, Comparison, etc.); one toggle cascades to the home tile + help entry + nav dropdown link; when every tool in a nav section is off, the section header + dropdown disappear too. Backed by a single tool registry (route, section, label, help-topic, flag-key, tile copy) folding the existing ad-hoc manabase/content.kb/categories flags into one model.
- **KBVAL A/B gate** — prove the Content KB lifts ChatGPT output (with vs without expert clips, blind-judged where feasible) before further KB investment; decision gates the creator-philosophy phase and the `content.kb.enabled` flip.
- **Creator-philosophy research** — distilled per-creator style-card + RAG-over-transcript design (provenance, contradiction-preservation, temporal drift); research/design only, and only if KBVAL shows clear lift.
- **Studio UI design pass (P1 + P3)** — Studio shell/design-tokens/dashboard + responsive/dark-mode, via `/gsd-ui-phase` (P2 per-page consistency already shipped Cycle 10).

**Out of scope this cycle:** Deck Primer generator (→ Cycle 12); scheduled/bulk auto-harvest (AUTO-03/04); SEO/growth lane; the creator-philosophy *build* (research only this cycle — build waits on KBVAL).

**Key context:** CalVer milestone, NAMED not numbered (ADR 0002); phase numbering continues from 63 → 64+. KBVAL→creator-philosophy gate honored (Phase 5 drops if KBVAL marginal). SEED-001 (KB add/remove + publish-tracking) audited closed — shipped Cycle 9. Gating Analysis off is allowed but it is the core workflow; admin UI warns rather than blocks.

## Shipped Milestone: Cycle 10 — Studio Automation, Sync & Polish (SHIPPED 2026-06-21, `2026.06.6` — archived, see `.planning/milestones/cycle10-ROADMAP.md`)

**Goal:** Cut manual steps from the Studio harvest→publish pipeline, give the operator a true prod↔local reconcile view, and make video selection + pipeline state fast and obvious.

**Target features:**
- **Auto-distill** harvested videos — no separate manual distill step
- **Auto-approve** distills above a quality/confidence threshold; low-confidence still enters the review queue
- **Pull-from-Prod reconcile/sync** — pull prod `content_site_index` + artifacts and surface diffs (prod-newer / missing-local / diverged) so the operator can reconcile
- **Saved creator list + dropdown picker** — manage curated creators and pick from a dropdown to browse (replaces paste-channel-URL-each-time; `Harvest.razor` currently has no persisted source list)
- **Default selection view = unharvested only** — hide already-harvested videos by default with a toggle to show all (today all videos show, harvested ones merely badged)
- **Skip/ignore candidate** — lightweight "don't show this video in selection again" distinct from Block (Block hard-deletes artifacts + blocklists; too heavy for a never-harvested candidate)
- **Studio UI polish** — clearer status badges, harvest/review ergonomics, error/feedback states, layout/navigation, creator-based filtering, and the `MainLayout.razor` About-link scaffold fix (points at ASP.NET docs today)

**Out of scope this cycle:** Validate-KB-value A/B experiment (KB stays dark; `content.kb.enabled` flip deferred again); scheduled or bulk/auto creator-source harvest (operator manually curates which videos enter); SEO/growth lane.

**Key context:** Builds on Cycle 9 publish-tracking (`published_utc` + shared `PublishStateDeriver`, `VideoStatusResolver`). CalVer milestone, NAMED not numbered (ADR 0002); phase numbering continues from 58 → 59+. Studio remains operator-local tooling; harvest/block stay Studio-only by design.

## Shipped Milestone: Cycle 9 — Content Pipeline & Publish-Tracking (SHIPPED 2026-06-19, `2026.06.5` — archived, see `.planning/milestones/cycle9-ROADMAP.md`)

**Goal:** Close the Studio publish-visibility gap and raise KB content quality — so the operator can see what's live, block/remove bad entries, and harvest distills that produce better paste-ready knowledge.

**Target features:**
- **SEED-001 — KB add/remove/block + unified publish-tracking** (scope A+B+C, pre-approved 2026-06-17): Studio Block (hard-delete + blocklist) + Blocked-list + Unblock (wire existing Core methods); `published_utc` migration (SQLite + Postgres) + derived publish-state `{Never / Pushed-hidden / Published / Local-newer}` shown in Studio Review/Publish AND `/Admin/ContentKb`; confirm add-single-video-by-URL polish.
- **Distill prompt quality** — rework the transcript→KB distill prompt for better paste-ready output; current distill providers kept (no model swap).
- **New harvest runs in-cycle** — real YouTube + LLM distill spend on new content to validate the distill-prompt and publish-tracking work end-to-end.

**Key context:** Born from two real Cycle-8 incidents (@salubrioussnail duplicate source, Based Deck Department 20 videos stuck `approval_status=pending`/never-published). CalVer milestone, NAMED not numbered (ADR 0002); phase numbering continues from 54 → 55+. SEO/growth lane explicitly deferred. Open carry-forward debt: `deckflow_admin` credential deletion (password rotated), operator live Gemini paste before flipping `DECKFLOW_GEMINI_ENABLED` in prod.

## Shipped Milestone: Cycle 8 — Hardening & Backlog Burn-down (SHIPPED 2026-06-17, `2026.06.4` — archived, see `.planning/milestones/cycle8-ROADMAP.md`)

First CalVer release (ADR 0002). Debt-burn cycle, no net-new user features: verified the shipped v1.7 Studio/publish pipeline end-to-end (non-prod operator-UAT smokes + a **live prod publish run** proving the content-columns-only upsert preserves admin flags on 86 pre-existing rows), fixed F-51-PG-01 (TEXT-vs-`timestamptz` Postgres `42883` in `AddDeckIdsAsync`), burned down the Phase 39 architecture backlog (CategoryKnowledgeRepository god-file split, `Program.cs` DI extraction + `Services/` foldering, deck-stat classifiers → Core, `Feedback*` dialect-leak removal — PASS 8/8 + SECURED 4/4), and resolved feature debt (SpellbookCombo ranking fields captured + Deck Primer priority-rank; Gemini artifacts verified within the ~30k paste ceiling, flag default-off). v1.7 merged to `main`; Render deploys from `main`. 8/8 requirements (FEAT-01 PASS-WITH-NOTES); closed as tech-debt (every phase individually verified). **Public-app behavior unchanged** (Gemini stays flag-gated). Requirement outcomes in `.planning/milestones/cycle8-REQUIREMENTS.md`. Carry-forward: `deckflow_admin` credential deletion (password rotated), operator live Gemini paste before prod flag-flip, full dialect-branch collapse (PG DDL parity prereq).

## Shipped Milestone: v1.7 Local Harvest & Publish Studio (SHIPPED 2026-06-17 — archived, see `.planning/milestones/v1.7-ROADMAP.md`)

A standalone local Blazor Server console (DeckFlow.Studio) that discovers YouTube videos (channel browse + paste URLs/IDs), harvests + LLM-distills them with a spend dry-run gate, reviews/approves in a queue, and publishes approved Content-KB entries to deckflow.gg via two paths: git commit-publish of the LF-normalized seed (→ Render deploy) and a direct prod push (SSH.NET SCP of artifacts to the Render `/data` disk + a content-columns-only Postgres upsert that preserves admin fields). Closed alongside internal hardening (orchestrator extraction, Dapper, admin-grid paging, format gate) and the deckflow.gg visual refresh (Phase 48). Requirement outcomes in `.planning/milestones/v1.7-REQUIREMENTS.md`. **Public-app behavior unchanged** except the Phase 48 visual polish; the Studio is operator-local tooling. Prod-DB direct write is a NEW authenticated path; its live end-to-end smoke needs operator prod secrets and is deferred (tracked in the milestone audit).

## Shipped Milestone: v1.6 Content KB Retrieval Fix + Value Re-Validation (SHIPPED 2026-06-12 — archived, see `.planning/milestones/v1.6-ROADMAP.md`)

Gate-driven milestone that pivoted: the KBV value gate = MARGINAL → retired prompt clip-injection, kept the KB browse-only + rebuilt its corpus, then shipped the DeckController/CommandRunners SRP split (38) + a packet-service dedup refactor (39, Finding A) + a Core.Tests health pass (40). Audit **passed**; requirement outcomes in `.planning/milestones/v1.6-REQUIREMENTS.md`.

## Shipped Milestone: v1.5 Deck Primer Generator + Content KB Integration + Housekeeping (SHIPPED 2026-06-10 — archived, see `.planning/milestones/v1.5-ROADMAP.md`)

**Goal (achieved):** Ship the Deck Primer Generator as a fourth paste-ready workflow, wire Content KB knowledge into deck-analysis prompts, and clear v1.4 quality debt.

**Target features:**

1. **Deck Primer Generator** — new paste-ready workflow + tab (peer of DeckAnalysis / DeckComparison / CedhMetaGap): decklist + bracket → ChatGPT-ready prompt producing a complete Moxfield primer in one round-trip. 31-section catalog, bracket presets (cEDH + Casual/Upgraded) with B+C hybrid per-section selection (5 collapsible groups), combo grounding via Commander Spellbook + fenced speculative asks, bracket-routed matchups (EdhTop16 named archetypes for bracket 5, generic strategy buckets for 1–4; no EDHREC in v1). Preceded by combo-data-richness spike (`spike-combo-data-to-primer-grounding` todo). Design pre-decided in seed + `.planning/notes/deck-primer-prompt-design.md`.
2. **Content KB → deck-analysis integration** — inject curated expert content into deck-analysis prompts + "What experts say" panel (deferred from original KB vision). Prod flag `content.kb.enabled` flip is a prerequisite/part of this work.
3. **Housekeeping debt bundle** — DeckFlow.Core XML-doc backfill (186 sites) + widen doc-warning gate to Core, KB-12 codex distill backend, VERIFICATION.md hygiene (7 v1.4 phases missing files, stale UAT labels), v1.4 artifact-hygiene items.

**Explicitly excluded:** Gemini paste-limit unblock — deferred again to v1.6 (stays flag-gated via `DECKFLOW_GEMINI_ENABLED`).

<details>
<summary>📦 v1.4 milestone detail (SHIPPED 2026-06-03 — archived)</summary>

## v1.4 Milestone: Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup

**Goal:** Land 3 v1.3 backlog items (admin focus-trapped modal, doc-comment NoWarn backlog, admin pages mobile-responsive sweep) and ship Phase 1 of the Content Knowledge Base — admin-curated YouTube channel + podcast list, transcript ingestion (YouTube captions + Whisper fallback with monthly $ cap), per-video LLM summary + per-clip timestamped excerpts, tagged by archetype/strategy + format/bracket + card category. Deck-analysis integration (prompt injection + UI panel), new-deck-building guide, AND Gemini paste-limit unblock DEFERRED to v1.5.

**Target features:**

1. **Admin focus-trapped modal (WDG-04 modal)** — replace deferred `onsubmit` confirm in AdminFeedback/Detail.cshtml with styled focus-trapped modal; close v1.3 WDG-04 override
2. **Doc-comment NoWarn backlog** — strip `NoWarn 1591;1573;1587` from DeckFlow.Web.csproj; backfill XML `<summary>` doc-comments on ~88 v1.1-era undocumented Web types (controllers, services, models, view models)
3. **Admin pages mobile-responsive sweep** — extend WDG-04 site-common.css a11y primitives (touch-action, focus-visible, ≥44px touch targets) to admin shell; admin.css responsive rules; sidebar collapse on narrow viewports; admin tables overflow-x or card-stack pattern; forms single-column on narrow
4. **Content Knowledge Base Phase 1 (ingestion + storage)**:
   - Admin-managed curated source list (YouTube channels + podcast RSS feeds): CRUD UI + Postgres tables
   - Transcript pipeline: YouTube auto-captions first; Whisper API fallback for missing captions / audio-only podcasts
   - Per-video LLM summary + per-clip timestamped excerpts
   - Tagging: archetype/strategy + format/bracket + card category (NOT commander/color — integration model in v1.5 generalizes)
   - Manual admin-triggered harvest (no scheduler in v1.4)
   - Hard monthly $ cap on Whisper spend; admin UI displays spend; abort harvest when cap hit
   - New Postgres tables: sources, videos, transcripts, summaries, clips, content_tags
   - Reuse v1.1 HarvestRunStore + IFeatureFlagCache patterns where possible

**Deferred to v1.5:**
- Deck-analysis integration of content (prompt injection + DeckFlow UI "What experts say" panel)
- New-deck-building guide (interactive wizard)
- Scheduled (cron) harvest cadence
- Gemini paste-limit workaround (DECKFLOW_GEMINI_ENABLED stays flag-gated through v1.4)

**Other v1.3 candidates NOT in v1.4 scope (v1.5+):**
- IN-01 _AiSelector vs view-level Normalize Gemini-flag fallback divergence
- v1.1 phase-dir archive move
- CSS-class / data-attribute / TS-constant chatgpt-* cleanup
- v13-harvest-worker-stalled debug follow-up
- edhtop16 filter-defaults mismatch
- audit-open scanner vocabulary alignment

</details>

## Requirements

### Validated

<!-- Shipped and confirmed working in production at deckflow.gg. -->

- ✓ Archidekt and Moxfield deck import (multi-format URL parsing) — production
- ✓ ChatGPT analysis prompt generator with multi-card picker and saved sessions — production
- ✓ Deck-vs-deck reconcile (Moxfield ↔ Archidekt, either direction) — production
- ✓ Category knowledge crawl + observations DB (PostgreSQL on Render) — production
- ✓ Card lookup with Scryfall integration + RestSharp/Polly resilience pipeline — production
- ✓ 25 guild-themed Razor views with single shell-max-width token — production
- ✓ Public feedback form + basic-auth-protected `/Admin/feedback` — production
- ✓ ARIA-1.2 `df-select` combobox rolled across single-select form controls — production
- ✓ Mobile responsive layout via `site-mobile.css` (≤ 50 lines) — production
- ✓ Help center `/help` and `/about` with Markdig content pipeline (DisableHtml hardening) — production
- ✓ Browser extension package (`deckflow-bridge.zip`) served from `/extensions/` — production
- ✓ Pluggable SQLite/Postgres storage with auto-creating schema (`EnsureSchemaAsync`) — production
- ✓ Skip-link, ARIA labelled-by, copy announcer accessibility baseline — production
- ✓ 6-step type scale + semantic color tokens (`--link`, `--danger`, `--cta-border`, `--focus`) propagated to all 25 guild themes — v1.0 (UI-VS-01..04)
- ✓ Hub primary-CTA + inline-style cleanup + voice-aligned page titles + `/feedback` busy-state — v1.0 (UI-LH-01..02, UX-01..03)
- ✓ Test-only factories moved out of prod assembly + single-ctor service standardization + generated JS untracked + `ForwardedHeadersOptions` Path B-rawpeer with `CF-Connecting-IP` — v1.0 (TD-01..04)
- ✓ Scryfall Tagger restored for cEDH staples (auto-cookie revert + Cloudflare BIC headers + `AutomaticDecompression`) — v1.0 (BUG-01)
- ✓ Postgres-backed admin brute-force throttle with `CF-Connecting-IP` partition + same fix on `/feedback` rate-limiter — v1.0 (BUG-02 + TD-04 patch)
- ✓ Localhost integration test regression guard for Tagger cookie-replay path — v1.0
- ✓ TargetCommanderBracket selector visually prominent on ChatGPT Packets page — v1.2 (BRKT-01)
- ✓ AI target selector (ChatGPT / Claude / Gemini) on all three ChatGPT analysis pages — v1.2 (AISEL-01)
- ✓ Claude-optimized artifact format + instructions — v1.2 (AISEL-02)
- ✓ Gemini-optimized artifact format + instructions — v1.2 (AISEL-03, flag-gated since 2026-05-13)
- ✓ AI selection preserved in zip round-trip — v1.2 (AISEL-04)
- ✓ cEDH meta-gap Step 1 state preserved in zip round-trip (fetched entries + filters + selections, regenerate without re-fetching edhtop16) — v1.2 (AISEL-04 closeout, 10-05)
- ✓ AI-agnostic URLs + page labels (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`; H1/nav/hub labels + Mock A explainer lines; site-common.css `.page-lede`; AI-agnostic zip artifact filenames preserving Phase 10 AI-segment invariant) — v1.3 (RENAME-01..03; Phase 12). Legacy chatgpt-* 301 redirects shipped 2026-05-08 (Phase 12) and retired 2026-05-22 (Phase 999.8) after 2+ weeks live.
- ✓ Web Design Guidelines audit fixes (10 sweep PRs: site-common.css a11y primitives, admin focus-visible, df-typeahead keyboard nav + ARIA combobox, ARIA tablist server-render, CSP inline-handler removal, info-tooltip a11y, table semantics, URL/textarea autocomplete, Razor `selected=` bool sweep, AdminHarvest live-region) — v1.3 (WDG-01..10; Phase 11)
- ✓ ChatGpt* C# class rename to AI-agnostic names with XML `<summary>` doc-comment backfill on every renamed type — v1.3 (CLASSRENAME-01..03; Phase 13)
- ✓ Broader codebase name-vs-behavior audit across 5 projects + missing doc-comment backfill, Release build clean — v1.3 (AUDIT-01..03; Phase 14)
- ✓ Sealed `AiPlatform` record value object replacing stringly-typed `TargetAiPlatform` (OCP 3/10 → 8/10) — v1.3 (AIPLATFORM-01..03; Phase 15)
- ✓ AI-agnostic prose adaptation across 3 workflow Razor views + C# exception messages + Help markdown (hybrid pattern: universal noun above `_AiSelector`, `@aiPlatform.DisplayName` injection below) — v1.3 (Phase 999.1)
- ✓ Claude `<result>` wrapper stripped from 5 prompt variants (direct JSON fenced-block output) — v1.3 (Phase 999.2)
- ✓ Packet download session cache (per-request in-memory cache keyed by request hash, TTL bounded; eliminates full Scryfall pipeline replay on preview → download) — v1.3 (Phase 999.3)
- ✓ Truncated-JSON response UX (user-facing "wait for AI to finish generating" message replaces raw stack trace on `JsonReaderException`) — v1.3 (Phase 999.4)
- ✓ v1.3 ship-gate test hardening: 9→0 residual test failures resolved across 3 plans (stale tests caught up to shipped renames, F-ENV-COLLECTION serialization for env-mutating tests, F-PROD-CONTRACT `IHarvestRunStore.GetByIdAsync` production-bug fix) — v1.3 (Phase 999.6)
- ✓ Content KB pipeline: local CLI harvest (YouTube captions + Whisper fallback + spend caps) → pluggable openai/claude LLM distill → markdown prompt artifacts → slim Postgres index → flag-gated browse with admin per-entry curation, CSRF+SameOrigin-guarded — v1.4 (KB-01..11; Phases 19-22 + 21.2)
- ✓ Category cache on integer-keyed star schema with prod full-reset (hot commander query 69s→0.66ms), Sol Ring read-time CategoryFilter fix, content-hash dedup + 5-day refresh — v1.4 (DBO-01, CAT-01, CAT-02; Phases 26/24/27)
- ✓ Admin shell mobile-responsive (`admin-common.css`/`admin-mobile.css`, ≥320px, ≥44px targets) + harvested-decks paged grid + focus-trapped `<dialog>` confirm modal — v1.4 (AMOB-01..04, AHD-01, MODAL-01; Phases 18/25/16)
- ✓ DeckFlow.Web fully XML-documented + doc-warning gate live (`NoWarn` stripped, editorconfig warning severity scoped `[DeckFlow.Web/**.cs]`, probe-proven) — v1.4 (DOC-01/02; Phases 17+23)
- ✓ Deck Primer Generator — fourth paste-ready workflow (`/deck-primer`): 31-section catalog (5 collapsible groups), bracket presets, Commander Spellbook combo grounding (null-graceful), bracket-routed matchups (EdhTop16 / generic fallback), category-knowledge distribution, per-AI artifact variants via PacketArtifactStore zip round-trip — v1.5 (PRM-01..12; Phase 31)
- ✓ Content KB integration into deck-analysis — Expert Context block (top-K curated clips as attributed pull-quotes) injected into all 3 analysis prompt variants + "What Experts Say" panel + admin relevance-score preview; ships flag-gated/dark — v1.5 (KBI-01..06; Phase 30)
- ✓ Expert Context Selection — pin videos / follow creators / evergreen flag over auto-relevance via 4-tier fill merge; `is_evergreen` self-healing SQLite+Postgres migration; typeahead endpoints — v1.5 (SEL-01..06; Phase 32)
- ✓ Admin Content KB Curation UX — instant client-side filter/search over the entries list + readability sweep (zebra/sticky/hover/mobile cards) — v1.5 (KBUX-01/02; Phase 33)
- ✓ Housekeeping — DeckFlow.Core XML-doc backfill + doc-gate widened to `[DeckFlow.Core/**.cs]`; retroactive VERIFICATION + artifact hygiene — v1.5 (HSK-01/03/04; Phases 28-29)
- ✓ Quality infra — Vitest+jsdom browser test runner + first GitHub Actions CI (build + xUnit + Vitest); expert-pin injection bug fixed + pin-id derivation unified — v1.5 (close)
- ✓ Packet-service family SRP split — shared `PacketTextAssembler` + `ScryfallReferenceResolver` collaborators extracted from the four packet god-services (Analysis / Comparison / MetaGap / Primer); per-variant prompt prose stays hand-authored (ADR-0001); byte-identical across ChatGPT/Claude/Gemini (flag ON/OFF) via a regression guard — 2026.07.2 (PKTSVC-01..04)
- ✓ `--accent-strong` semantic-token migration across all 27 theme forks — re-aliased onto `--link`/`--danger`/`--focus`/`--cta-border` by role; error/danger text no longer resolves to the link color in red guild themes; no non-error visual drift — 2026.07.2 (THEME-01..03)
- ✓ `chatgpt-*` → AI-agnostic naming cleanup — ~1545 CSS/TS/view identifiers + the `ChatGptSwapPrompt` C# symbol renamed with byte-identical render, grep-clean across css/ts/Views, Playwright e2e unchanged — 2026.07.2 (AICLEAN-01..03)
- ✓ Refactor-review sweep over the largest/most-duplicated files (`deck-sync.ts` 2877 LOC, `Harvest.razor.cs` 1225) with a recorded per-candidate triage; in-scope targets refactored behavior-neutral (tests), others explicitly deferred to backlog — 2026.07.2 (REVIEW-01/02)
- ✓ 6-pillar UI audit re-score 18→21/24 — theme gap fixes A/B/C/D (filled-accent-pill active step-tab, tokenized Jeskai-blue literals, analysis-bucket toggle chevron+aria, perceptible Full/Compact/Advanced layout picker) with visual-regression + interaction e2e — 2026.07.2 (UIAUDIT-01/02)
- ✓ DirectPush Stage 4 closeout — no-op success copy no longer claims a push (`AlreadyInSync` returns without pushing), phrasing unified to `origin/<branch>`, short-form commit SHA (no mobile overflow); three Stage-4 bUnit variants + operator live prod desktop+mobile eyeball — 2026.07.2 (UIAUDIT-03)
- ✓ Creator-source model hardening (Studio-local) — `creator_sources`↔`content_sources` linked by a persisted `content_source_id` at harvest with an edit-after-select provenance guard, disabled-same-URL source re-enable on both add paths, existence-aware `/creators` link status, and an additive/idempotent `content-kb.db` migration — 2026.07.2 (CREATOR-01..04)

### Active

<!-- Cycle 16 — Content-KB Prod↔Git↔Studio Sync Hardening. REQ-IDs defined in .planning/REQUIREMENTS.md. -->

- Cycle 16 (Content-KB Prod↔Git↔Studio Sync Hardening) — SYNC-01..16 per `docs/research/kb-prod-sync-roadmap.md`; scoped REQ-IDs in `.planning/REQUIREMENTS.md`.

### Out of Scope

<!-- Deferred to next milestones, with reasoning. -->

**Descoped from Cycle 15 (2026.07.2):**

- ADMIN-01 — `/Admin/Flags` sortable by on/off (enabled) state — descoped to backlog for a future cycle (user decision 2026-07-05). View-only sort, no flag key/default/persisted-semantics change; the odd net-new admin-UX addition in an otherwise byte-identical tech-debt cycle.

**Deferred from v1.1 (Admin Console):**

- UI audit re-score (`tasks/UI-REVIEW.md`, 16/24 → ≥ 20/24) — split into its own UI-audit milestone; not coupled to admin tooling
- Raw Serilog log tail / file viewer — Render dashboard already streams stdout; usage analytics gives more triage value than tail-follow
- Multi-user admin auth (session cookie, role split) — single-operator BasicAuth is sufficient for current ops volume
- Admin alerts / notifications — Render dashboard + manual stat checks cover ops; not blocking
- Cache flush button, Postgres connection test, Render restart, manual artifact cleanup — not required for v1.1; future "ops actions" tile if demand surfaces

**Carried from v1.0:**

- DeckController god-class split — too large for a polish milestone; warrants its own dedicated refactor milestone
- ChatGPT services extraction (`PromptBuilder`, `ScryfallReferenceResolver`, etc.) — too large; own refactor milestone
- DeckController test coverage uplift — own milestone (depends on the split)
- Visual regression harness for 25 themes — separate testing-infra milestone; nice-to-have, not table stakes
- Mobile polish beyond what UI-REVIEW.md flagged — current responsive layout is adequate
- ScryfallThrottle → Polly rate limiter migration — already in flight under prior plan; out of this milestone's scope
- Per-route Scryfall fairness queue — own resilience milestone
- Disk-backed Scryfall set cache — own caching milestone
- Tagger refresh `IHostedService` — own cron/jobs milestone
- DB-backed Archidekt cache job persistence — own jobs milestone
- ChatGPT artifact retention sweep — own cleanup milestone
- RestSharp abstraction (`IUpstreamHttpClient`) — own milestone
- Health/ready endpoint + correlation ID middleware — own observability milestone
- Structured API error envelope — own milestone
- Resilience pipeline behavior tests — own testing milestone
- Cloudflare upstream snapshot harness — own testing milestone
- PWA / offline-first — UX nice-to-have, not blocking
- Browser-extension test coverage gap — manifest-version protocol bumps are documented; deferred

## Context

**Technical environment**

- ASP.NET 10 (Razor Views + MVC controllers), TypeScript via `tsc` in MSBuild
- PostgreSQL 16 on Render (Basic-256mb), `dpg-d7oj8iugvqtc73fso0g0-a` instance "deckflow"
- Render web service (`srv-d7gmufkp3tds73a29m30`) on Starter plan with `/data` persistent disk for ChatGPT artifacts
- Custom domain `www.deckflow.gg`; auto-deploys from `main` on `luntc1972/DeckFlow`
- Local dev: WSL2 + .NET 10 SDK; VSTest currently broken in WSL (socket permission issue — known limitation)
- HTTP layer: RestSharp wrapping `IHttpClientFactory` HttpClient + per-call `ResiliencePipelineBuilder<RestResponse>` (NOT MS standard handler)
- Markdig pipeline hardened with `DisableHtml()` defense-in-depth XSS

**Known UI debt (basis for the 2026.07.2 milestone) — RESOLVED**

- Recent UI audit (`tasks/UI-REVIEW.md`, 2026-04-30) scored 16/24 across 6 pillars; re-baselined 18/24 in Phase 82 and re-scored **21/24** in Phase 86 (human-verified 2026-07-05, clears the ≥20/24 bar).
- Color (2/4) and Typography (2/4) were the lowest pillars; Color addressed via the `--accent-strong` semantic-token migration + theme gap fixes. Typography deepening carried forward (D3 → future cycle).
- Real bug FIXED: `--accent-strong` was overloaded (links + brand + focus + error + CTA) — error text read as link in red guild themes; migrated onto `--link`/`--danger`/`--focus`/`--cta-border` in Phase 84 (THEME-01..03).

**Recent project state**

- **Cycle 15 — Cleanup, Refactor & Visual Polish shipped 2026-07-05 (`2026.07.2`)** — a behavior-neutral cleanup cycle: packet-service SRP split, theme visual polish (`--accent-strong` migration + UI gap fixes A/B/C/D), 6-pillar UI audit re-score 18→21/24, `chatgpt-*`→prompt AI-agnostic rename shipped byte-identical, and Studio creator-source model hardening. Zero net-new user features; every paste artifact byte-identical. ADMIN-01 (Flags sorting) descoped → backlog.
- v1.0 Polish & Quality milestone shipped 2026-05-02 (5 phases, 17 plans, 63 commits, +20,284 / -5,194 LOC)
- Phase 4 abandoned mid-milestone after both fixes proved ineffective on prod despite passing static checks; rerouted to Phase 5 with surgical revert + corrective Postgres-backed throttle (see `04-ABANDONED.md` post-mortem)
- Two latent root causes surfaced during Phase 5 BUG-01: Cloudflare BIC blocks Render egress IPs without browser-shaped headers, and `AutomaticDecompression` must be enabled when advertising `Accept-Encoding`
- All 15 v1 requirements shipped; UI audit re-score against `tasks/UI-REVIEW.md` deferred to v1.1
- Tech stack pinned: ASP.NET 10 + Razor + RestSharp/Polly v8 + Postgres on Render

**User profile**

- Solo developer (Chris Lunt) with deep MTG/cEDH domain knowledge
- Communication style: terse, technical, demanding ("caveman mode" preferred)
- Prefers explainer text (`.mode-note`) over relabeling unclear UI controls
- Prefers bundled PRs to many small ones for related refactors
- README must be kept current with each commit

## Constraints

- **Tech stack**: ASP.NET 10 + Razor — pinned by deployed app; no framework migration in this milestone
- **Hosting**: Render Starter web + Basic-256mb Postgres — 512MB RAM cap on web tier, mind allocations
- **Theme system**: Guild themes are full standalone CSS forks; layout CSS must go in `site-common.css`, not `site.css` — token additions go in `:root` of each theme file
- **HTTP resilience**: Use existing RestSharp + direct Polly v8 pattern — do NOT migrate to standard handler
- **Public repo**: `luntc1972/DeckFlow` is public — no secrets in commits ever; secrets live in Render dashboard with `sync: false`
- **Testing**: VSTest unreliable in WSL; rely on `dotnet build` clean + targeted manual harness or push-and-watch CI
- **Commits**: Plain default-author commits, no Co-Authored-By trailer; README updated when behavior changes; commit per logical change

## Key Decisions

<!-- Decisions that constrain future work in this milestone and beyond. -->

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Polish & quality before refactor | Visible improvements ship faster, sustain user momentum, and the UI audit gave a concrete punch list. Refactors (DeckController, ChatGPT services) are larger and benefit from a clean baseline. | ✓ Good (v1.0 shipped on schedule, 15/15 reqs) |
| Audit re-score ≥ 20/24 as success bar | Concrete, measurable, ties milestone completion back to the same evidence that started it. Avoids "feels nicer" subjective drift. | ⚠️ Revisit (re-score not measured at close; carry to v1.1) |
| Keep `--accent-strong` for backward compat; layer new semantic tokens on top | Mass-renaming `--accent-strong` would touch 25 theme files; aliases preserve all themes while fixing the semantic collision. | ✓ Good (token block landed on all 25 themes; Rakdos `--link` override proves the seam) |
| Postgres on Render (single managed instance) | Free SQLite + persistent disk works but is fragile across deploys; managed Postgres is durable, cheap (Basic-256mb $7/mo), and the storage layer was already pluggable. | ✓ Good (deployed 2026-04-30) |
| Render Starter web tier ($7/mo) over Free | Free tier sleeps after 15min, .NET cold start ~30s gave bad UX; Starter is always-on for ~$84/yr. | ✓ Good (no cold-start UX complaints during milestone) |
| Phase 4 abandonment + rerouting BUG-01/BUG-02 to Phase 5 | Both Phase 4 fixes passed static checks but failed live on prod (Tagger still empty for cEDH staples, throttle still ineffective). Pressing forward would have buried the root causes. | ✓ Good (Phase 5 surfaced Cloudflare BIC + AutomaticDecompression as the actual blockers) |
| `CF-Connecting-IP` as the partition key for both admin throttle AND `/feedback` rate-limiter | `X-Forwarded-For` was being fragmented by multi-proxy chain; spoof-resistance comes from Render Inbound IP Rules + Cloudflare CIDR allow-list, not from header trust. | ✓ Good (live UAT 10×401 + 1×429 with monotonic Retry-After 899→879) |
| Localhost `HttpListener` integration test for Tagger cookie-replay | Static checks let `4db8b8a` ship to prod without exercising the GraphQL POST leg. Real `SocketsHttpHandler` against a stub server catches future regressions to manual cookie replay or `UseCookies=false`. | ✓ Good (2/2 pass; closes the verification gap) |
| Per-AI dispatch primitive on prompt builders (v1.2 Phase 10-01) | Single `request.TargetAiPlatform` branch at the top of every prompt builder, fanned out across all 5 builders (analysis, set-upgrade, comparison, follow-up, meta-gap). Keeps ChatGPT path unchanged; Claude/Gemini are additive. | ✓ Good (15 variants shipped 2026-05-09; AISEL-02/03 satisfied) |
| Unified `<result>...</result>` wrapper for AI responses (v1.2 Phase 10-03) | One regex extracts JSON from any AI's response with the fenced ` ```json ` block preserved as fallback. Closes AISEL-04 in a single seam rather than 3 page-specific parsers. | ✓ Good (one extractor, 3 pages, no per-page paste logic) |
| Hybrid deck text storage in session zips (v1.2 Phase 10 hardening) | Store both original (user-pasted) and canonical (BuildDecklistText output) in every zip. Original-prefers-canonical loader precedence handles the alphabetize-vs-preserve mismatch on re-upload. | ✓ Good (62ee45b; 11 new tests) |
| Hidden form field carries cEDH Step 1 state between Step 2 submits (v1.2 Phase 10-05) | Stateless server, no session-affinity required, sidesteps edhtop16 rate-limit on regenerate. ~50-200KB per form post is acceptable. | ✓ Good (T3 retest passed 2026-05-13) |
| Gemini hidden behind `DECKFLOW_GEMINI_ENABLED` flag at v1.2 close | Full packet exceeds gemini.google.com paste cap, truncating instructions. Server logic preserved; flip env var to re-enable. | ⚠️ Revisit in v1.4 (still flag-gated through v1.3; needs split-message prompt or direct API integration) |
| Cross-AI execution pattern (Codex codes, Claude reviews) established 2026-05-19 | Codex authored production edits; Claude orchestrated planning + verification + cross-AI peer review. Sustained zero friction across 6+ consecutive phase closures (999.5 → 999.8). | ✓ Good (v1.3 shipped 13 phases / 51 plans through pattern) |
| Backlog-phase numbering 999.x for in-milestone catch-up work | Allows v1.3 to absorb quality-debt phases (999.1 prose adaptation, 999.2 wrapper strip, 999.3 cache, 999.4 truncation UX, 999.5 test hardening, 999.6 ship-gate cleanup, 999.7 audit cleanup, 999.8 redirect removal) without disrupting the production phase numbering (11-15). | ✓ Good (8 backlog phases closed; v1.3 shipped 22/22 reqs) |
| Retire legacy `chatgpt-*` 301 redirects 2+ weeks after Phase 12 rename | User-decided 2026-05-22 that past links can be retired after the graceful-deprecation tier had been live since 2026-05-08. Removes 22-line middleware block, 0 added. 11 URLs now 404. | ✓ Good (Phase 999.8; net deletion, build clean, baseline preserved) |
| Cross-AI plan review (Codex reviews Claude's plans) | After `/gsd-plan-phase` produces PLAN.md, route through Codex via `/gsd-review` before execute. Reduces single-model blind spots. | ✓ Good (caught 2 BLOCKER issues in 999.7 P01 before execution) |
| `no-ship-failing-tests` rule (established 2026-05-22) | Prior milestone closures shipped with deferred failures; rule mandates Failed:0 before milestone PR + merge. Phase 999.6 created specifically to honor this. | ✓ Good (v1.3 ships with Failed:0 across 500 tests) |
| Phase 15 SC4 empirical sha256 byte-identical hash gate BYPASS (user-authorized 2026-05-18) | Substituted evidence: clean build + variant Build bodies extracted byte-for-byte + ResultContractTests + AiPlatformPhase10RoundTripTests migrated. Residual silent byte-drift risk accepted. | ⚠️ Revisit if any AI-output divergence reported in v1.4 |
| WDG-04 deferred `onsubmit` retained in AdminFeedback/Detail (override 2026-05-16) | v1.4 will replace with styled focus-trapped modal; v1.3 accepts the inline JS confirm() for admin-only single-operator surface. | ⚠️ Revisit v1.4 (closed in v1.4 MODAL-01) |
| Content KB ships dark in v1.5 (`content.kb.enabled` OFF) | Proven live at Phase 30 UAT 2026-06-07, then re-disabled. KB content/cost not ready for general availability; the integration code ships behind the flag so it can be flipped on later without a deploy. | ✓ Good (KBI-01 satisfied; no GA exposure) |
| Phases 32/33 inserted mid-milestone from UAT + dogfooding (before Phase 31) | Phase 30 UAT surfaced the need for manual expert selection (SEL); admin dogfooding surfaced the curation-list filter (KBUX). Both layered on Content KB and were higher-value to land before the Primer. | ✓ Good (both shipped; SEL traceability orphan caught + closed at milestone audit) |
| SEL-02 expert-pin root cause corrected at close | The long-suspected pin-id 3-level-vs-2-level mismatch was refuted as unreachable dead code (read invariant); real cause was `ParseRowsAsync` dropping parse-failed pin rows before tier-1. Fixed + TDD-covered (`a106c6a`); pin-id derivation unified into `ContentSiteIndexRow.PinId` (`bfe16b1`). | ✓ Good (3 regression tests; CI green; live-pin re-confirm pending next KB-enable) |
| Add Vitest+jsdom JS test runner + GitHub Actions CI at v1.5 close (user-authorized) | Browser TS (`module:none`) had no test runner; KBUX-01 filter logic was untested. Extracted a global-assignment seam (`DeckFlowKbFilter`) testable under both tsc and esbuild; first real CI runs build + xUnit + Vitest. | ✓ Good (CI green; package.json un-gitignored to track the toolchain) |
| Filled-accent-pill for the active workflow step-tab (Cycle 15 Phase 86) | The baseline UI audit flagged the active step-tab as low-contrast/ambiguous across themes; a filled per-theme accent pill makes the current step unmistakable without adding a feature. | ✓ Good (Bug A; contributed to the 21/24 re-score) |
| Empirical per-theme WCAG contrast gate for dark-theme token swaps (Cycle 15 Phase 86) | Verified dark-theme token pairings against measured WCAG contrast per theme rather than eyeballing, catching low-contrast pairings the `--accent-strong` re-alias would otherwise mask. | ✓ Good (dark-theme fixes landed under the gate) |
| Tokenize the 3 hardcoded Jeskai-blue literals to per-theme accent (Cycle 15 Phase 86) | Three hardcoded blue literals bypassed the per-theme accent token, breaking palette self-consistency; tokenizing them keeps every theme on its own accent. | ✓ Good (Bug B; no non-error visual drift) |
| UIAUDIT-03 (DirectPush Stage 4) = code-complete + operator prod eyeball (Cycle 15 Phase 86) | There is no local prod-secrets path to render Stage 4; the code + three bUnit regression variants land in-repo and the live desktop+mobile render is confirmed by an operator eyeball during a real prod publish. | ✓ Good (operator confirmed 2026-07-05; SHA short-form, no mobile overflow) |
| ADMIN-01 (`/Admin/Flags` on/off sorting) descoped from Cycle 15 → backlog | The one net-new admin-UX addition was the odd item out in a byte-identical tech-debt cycle; descoping it keeps the cycle purely behavior-neutral. | ⚠️ Deferred (backlog, user decision 2026-07-05) |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

## Shipped History

**Shipped:** v1.0 Polish & Quality (2026-05-02) — all 15 v1 requirements landed across 5 phases, 17 plans, 63 commits.
**Shipped:** v1.1 Admin Console (2026-05-08) — all 27 requirements landed across Phases 6–8 + Phase 7.1 insert.
**Shipped:** v1.2 Multi-AI Prompts (2026-05-13) — 5 requirements across Phases 9-10 (8 plans). AI target selector + Claude-optimized artifacts + cEDH Step 1 round-trip live. Gemini flag-gated.
**Shipped:** v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene (2026-05-23) — 22 REQ-IDs across Phases 11-15 + 999.1-999.8 (13 phases, 51 plans, 370 commits, +47,724 / -5,385 LOC across 386 files, 10-day timeline 2026-05-13 → 2026-05-23). Test suite Failed:0 / Passed:497 / Skipped:3. 8/8 security threats closed.

**Shipped:** v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup (2026-06-03) — 20/20 active REQ-IDs across 14 phases (16-27 + 21.1/21.2, 31 plans, 343 commits, +54,651/−4,726 LOC across 638 files, 11-day timeline 2026-05-23 → 2026-06-03). Content KB end-to-end + category cache rebuild + admin mobile + doc gate. Tests Core 257/257, Web 528/533. Audit: tech_debt, 0 critical gaps.

**Shipped:** v1.5 Deck Primer Generator + Content KB Integration + Housekeeping (2026-06-10) — 30/30 requirements across 6 phases (28-33, 25 plans, 219 commits, +56,893/−2,108 LOC across 781 files, 7-day timeline 2026-06-03 → 2026-06-09). Deck Primer fourth workflow + Content KB prompt integration + expert selection + Core doc gate. Vitest+jsdom + GitHub Actions CI added at close. Tests Core 282/282, Web 657/662 (5 PG-skip). Audit: passed. Content KB ships dark (flag OFF by design).

---
*Last updated: 2026-07-06 — Cycle 16 (Content-KB Sync Hardening) started*
