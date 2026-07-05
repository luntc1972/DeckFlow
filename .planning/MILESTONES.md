# Milestones

## 2026.07.2 Cycle 15 — Cleanup, Refactor & Visual Polish (Shipped: 2026-07-05)

**Phases completed:** 6 phases (82–87; Phase 87 Creator-Source hardening merged from a separate branch), 22 plans, 42 tasks

**Key accomplishments:**

- Code-review sweep over 22 ranked Web/Studio/Core files surfaced 8 real SRP/duplication candidates (4 triaged in-scope for Wave 2, 4 backlog with written reasons), while excluding the pre-owned PKTSVC/THEME/AICLEAN/manabase-engine families.
- Re-ran the 6-pillar UI audit against live screenshots (desktop + mobile, 6 routes) and source evidence, scoring 18/24 (+2 over the 2026-04-30 baseline of 16/24), then enumerated the gap-to-20 with Color and Typography fixes handed to Phase 84 and three residual gaps (Spacing, feedback double-submit, branded error page) flagged for explicit operator assignment rather than left for Phase 86.
- Executed all 3 in-scope REFACTOR-TRIAGE.md targets (deck-sync.ts 2-concern split, Harvest.razor.cs 4-coordinator split, ContentSiteIndexStore.cs upsert dedup) under the byte-identical gate, and recorded all 6 remaining candidates in REFACTOR-BACKLOG.md with written deferral reasons.
- 25 xUnit byte-identity tests across all 4 packet services (Analysis/Comparison/MetaGap/Primer) x 3 AI platforms, with goldens captured verbatim from real BuildAsync runs against today's unrefactored code — the safety net every Wave-2 migration in this phase must keep green.
- Two new pure static collaborators under `DeckFlow.Web/Services/Packets/` — a sectioned-decklist/key-value text assembler and a first-match commander reflag helper — each characterization-tested and NOT yet wired into any of the four packet services.
- A single reusable Scryfall batch-resolution collaborator — chunk(75) -> cards/collection -> validate -> match-back-by-original-name -> per-miss fallback-delegate — that reproduces the mechanical core of all three current copy-pasted loops (Analysis/Comparison/MetaGap) byte-for-byte, fixture-tested and NOT yet wired into any service.
- DeckComparisonService migrated onto all three Wave-1 collaborators (ScryfallReferenceResolver / PacketTextAssembler / DeckEntryReflagHelper), dropping from 1033 to 924 LOC with zero change to its comparison/follow-up paste artifacts — the 25 byte-identity tests from 83-01 remain green.
- MetaGapService migrated onto ScryfallReferenceResolver and DeckEntryReflagHelper (plus PacketTextAssembler.AppendKeyValueLine for its 2 normalized request-context fields), dropping from 956 to 909 LOC with zero change to its meta-gap paste artifact — the 25 byte-identity tests from 83-01 remain green.
- DeckAnalysisPacketService — the largest of the four packet services (2372 LOC) — migrated onto ScryfallReferenceResolver and PacketTextAssembler, dropping to 2254 LOC with zero change to its analysis/set-upgrade paste artifacts across all 3 AI platforms and every prompt-mutating flag ON/OFF; the 25 byte-identity tests from 83-01 remain green.
- DeckPrimerPacketService — the fourth and final packet service, and the only one of the four with zero Scryfall card-resolution code — migrated onto PacketTextAssembler, dropping from 905 to 866 LOC with zero change to its per-platform primer artifacts across all 3 AI variants; this closes out the phase, with all four PKTSVC requirements now marked complete.
- Re-pointed --link/--focus/--cta-border to var(--accent-strong) across site.css + 11 forks, added the missing token block to site-commander-table.css, and swapped exactly 19 genuine link/focus/cta-border affordance sites onto the semantic tokens — leaving 37+3+2 decorative --accent-strong consumers correctly unchanged.
- Extended theming.spec.ts with a permanent danger!=link structural guard across all 24 themes, then produced a full 24-theme x {light,dark} computed-style no-drift diff plus red-guild screenshot evidence — surfacing one genuine, intended-but-unplanned additional color delta in rakdos for human sign-off.
- 1. [Rule 2 - Missing Critical / plan-gate compliance] Renamed 2 capitalized "ChatGPT" comment-prose instances in site-common.css
- 1. [Rule 1 - Bug/Gate-compliance] Reworded 8 "ChatGPT"-branded strings in deck-sync.ts not explicitly enumerated in the plan's symbol/literal tables
- Re-captured the post-rename render/computed-style snapshot, proved it structurally byte-identical to the 85-01 baseline (modulo three explained, rename-unrelated noise sources), ran all grep-clean/build/xUnit/full-Playwright-e2e gates green, and is now paused at the mandatory human sign-off for the two semantic judgments (D3 keep-list intact, D5 contract lockstep) that grep cannot make.
- Replaced all 8 hardcoded `rgba(43, 108, 176, …)` literals (base + mobile + 3 forks) with inline `color-mix(in srgb, var(--accent) N%, transparent)`, so every non-Jeskai theme now tints hover/active states with its own accent instead of a fixed blue.
- Replaced the low-salience active step-tab (same bg as inactive) with a filled `var(--accent)` pill across base site.css + 12 standalone forks, and added empirically-measured `--accent-contrast` tokens to 6 themes whose white-on-accent text fails WCAG 4.5:1.
- Restyled the empty analysis-questions bucket toggle from a bordered grey pill into a borderless, higher-contrast chevron and gave it an aria-label, mirrored across site.css + all 13 duplicating theme files (12 standalone forks + site-rakdos.css).
- Full/Compact/Advanced now produce an unmistakable, measurable box delta on `.prompt-instructions` (always rendered on Step 1), and Full gets a positive accent style instead of a do-nothing default — mirrored across base `site.css` and all 12 standalone-fork themes.
- Two new Playwright specs close the test gap that let Bugs A-D ship green (visual-regression for the filled step-tab pill + WCAG + accent-leak + bucket a11y; interaction-outcome for the layout-picker mode delta), and the 6-pillar UI audit re-scores 18/24 -> 21/24 — clearing the >=20/24 UIAUDIT-02 target with 1 point of margin. The plan's blocking human-verify checkpoint is PENDING, not self-approved.

---

## Cycle 14 — Deeper Deck Evaluation (Shipped: 2026-07-03, `2026.07.1`)

**Phases completed:** 3 phases (79-81), 9 plans, 31 tasks. Squash-merged to `main` `701ec2fa`; CI green `28694830980`; milestone audit PASSED (13/13 requirements, 5/5 integration). Build 0/0; Core 1053 / Web 1158 (12 PG skips). All three flags (`analysis.interaction-audit`, `analysis.wincon-map`, `analysis.mulligan-eval`) seeded OFF — operator flips ON after prod deploy. Known deferred items at close: 15 (see STATE.md Deferred Items).

**Key accomplishments:**

- Pure Core interaction audit buckets with curated stax/protection detection, classifier predicates, review tiers, and coverage-gap advisories.
- Deck-analysis prompt artifacts can now carry a flag-gated, card-backed interaction audit block across ChatGPT, Claude, and Gemini.
- Step-3 interaction audit readout with hardened hidden-field and zip round-trip, plus page/artifact byte-identity tests
- Pure-Core WinConMapAggregator ranks/bands Commander Spellbook combos (low mana-value-needed first, then high popularity, then ordinal card-name tie-break), strictly separates one-card-away near-combos, counts assembly paths, and reuses DeckStatClassifier.IsClosingPowerCard for a combo-less-deck win-condition read — all golden-tested with zero Web dependency.
- Wires the Phase 80-01 WinConMapAggregator into the /deck-analysis paste artifact behind a new `analysis.wincon-map` flag (seeded OFF), reusing the single already-fetched Commander Spellbook result (gate widened, never re-fetched) and generalizing the Phase-73 command-zone cache bypass into a shared predicate so a wincon-ON packet can never be replayed after the flag flips OFF.
- Step-3 on-page win-condition/combo map readout with hardened WinConMapJson round-trip through a hidden field and a conditional 61-wincon-map.json zip entry, proven flag-OFF byte-identical at both the artifact/zip layer and the Razor page-render layer
- Two-stage pure-observation instrumentation on the existing London-mulligan Monte-Carlo pass surfaces a keepable-hand band, keep-size distribution, and spell-attributed representative openers — no second simulation, cast% byte-identical.
- analysis.mulligan-eval flag (seeded OFF both dialects) gates a hedged "Opening Hand (mulligan)" block on the /manabase paste artifact — keepable band, keep-size process, and tracked-spell-attributed representative openers — byte-identical to today's output when off.
- The `/manabase` page now renders a flag-guarded opening-hand lens card (keepable band, keep-size process, tracked-spell-attributed representative openers) behind `ShowMulliganEval`, proven byte-identical to baseline when OFF by an `IRazorViewEngine` excision test.

---

## Cycle 13 — Deck Evaluation & Creator Output (Shipped: 2026-06-30, `2026.06.10`)

**Phases:** 75-78, developed on branch `plan/cycle-13-deck-eval` and squash-merged to `main` (per the one-branch-per-cycle convention), capped by the `2026.06.10` tag.

**Key accomplishments:**

- **Tap Analyzer surface (Phase 75):** the manabase report and paste artifact expose untapped-source frequency (overall + per color) and turn-1 untapped availability — discrete metrics read off counters already accumulated inside the existing 20k-trial castability simulation (no second pass). Flag `analysis.manabase.tap-analyzer`.
- **Bracket Classifier + Balancer (Phase 76):** auto-classify a deck into the official 5-tier Commander bracket (B1-B5) from Game Changers, two-card combos (Commander Spellbook), and mass land denial — with why-this-bracket reasons, tier-aware floor violations, and a balancer paste artifact for the cuts to reach a target bracket. Game Changers migrated to a versioned `bracket-data.json` (out of `.cs` literals). Tutors are not a bracket gate (Oct-2025 rubric). Own tool tile, flag `tool.bracket.enabled`.
- **Multi-Axis Deck Score (Phase 77):** a four-axis Power / Speed / Control / Consistency score (coarse 0-5 bands) folded into the `/deck-analysis` results panel and all three paste variants (ChatGPT/Claude/Gemini), with a bracket cross-check note. Deterministic `DeckFlow.Core` heuristics — no AI round-trip for the numbers. Flag `analysis.multi-axis-score`.
- **Auto-Refreshing Primer (Phase 78):** a stale-flag caution banner on the Deck Primer when the current deck differs from the deck a saved primer was generated against. Activates on the resume-without-rebuild path (upload a saved `.zip` while Step 1 holds a different deck) — renders the old primer verbatim, no auto-rebuild and no upstream re-fetch. Staleness is a card-name + quantity multiset hash (reorder / printing-swap stay fresh). Flag `tool.primer.stale-flag`.

**Quality:** build 0/0; Core suite 945 pass, Web suite 1062 (1049 pass / 12 skipped at close, the 13th gap fixed). CI green on `main` after merge. Per-phase: gsd-verifier PASS (75-77), operator live theme/mobile sign-off (75-04, 76, 77-06, 78-03). All four cycle flags (`analysis.manabase.tap-analyzer`, `tool.bracket.enabled`, `analysis.multi-axis-score`, `tool.primer.stale-flag`) seeded OFF — operator flips them in prod when ready. Manual prod deploy owed (autodeploy OFF).

## Cycle 12 — Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence (Shipped: 2026-06-27, `2026.06.9`)

**Phases:** 70-74 + flag-key namespacing, shipped as trunk/ad-hoc work on `main` (linear history, capped by the `2026.06.9` tag — not a single squash, since the phases were ff-pushed individually as they shipped).

**Key accomplishments:**

- **Plain-language manabase verdict (Phase 71):** worded advisory + metric glosses on the Mana Base analyzer; underlying Karsten/simulation surfaces unchanged. Flag `manabase.plain-language-verdict`.
- **Manabase command-zone + commander castability (Phase 72):** full command zone (partners, commander+Background, companion) threaded through deck loading; commander-castability lens in the UI. Flag `manabase.commander-castability`.
- **Deck-analysis command-zone awareness (Phase 73):** `/deck-analysis` prompt artifact names the full command zone + an optional companion; flag-gated Step-1 Companion designator (auto-detected from Moxfield; nameable for Archidekt/pasted). Awareness-only — deck text untouched, byte-identical when off. Session cache bypassed while on (Codex HIGH fix). Flag `analysis.command-zone-awareness`. Codex review APPROVE; verifier PASS 6/6.
- **Cross-tool deck-input persistence (Phase 74):** paste-once carry across single-deck tools (sessionStorage, fill-if-empty), restore notice, Start-Over clear.
- **Deck Primer output-style toggle:** Moxfield-rich and Full cEDH styles.
- **Feature-flag key namespacing:** `tool.*`/`service.*`/`analysis.*`/`manabase.*` keys with state-preserving migration; Admin Flags instant prefix filter + `tool.*` descriptions.

**Quality:** build 0/0; Web suite 929 pass. New flags seeded OFF in prod (operator flips when ready). Manual prod deploy owed (autodeploy OFF).

## Mana Base — Casual/cEDH Modes & Castability (Shipped: 2026-06-21, `2026.06.7`)

**Phase:** 64 (manabase-modes-castability), 2 waves (Core + Web)
**Requirements:** MODE-01..04, CAST-01..04, COLOR-AGG-01, REDUCE-01, GRANT-01, COMMANDER-01/02, FORMULA-01 — all satisfied.

**Key accomplishments:**

- **Two analyzer modes + commander importance:** Casual (Karsten singleton target) / cEDH (~28–32 band, floor 28); user-scaled commander importance (Central/Standard/Low), orthogonal to mode — it tightens the commander's color threshold without moving the land target.
- **Monte-Carlo castability (FINDING-3):** replaced the P_mana×P_color independence product with a seeded simulation — London mulligan, joint mana+color, in-sim ramp with summoning-sickness timing, ETB-tapped lands online next turn, deployable ramp at full value while only enabler-conditional granted sources are discounted. Validated against the Salubrious Snail calculator on a real Brago deck: mean Δ 2.8 pts, same weakest color + card ordering.
- **Aggregate color findings (COLOR-AGG):** worst-driver preserved (a lone uncastable bomb still surfaces) + a population view (mean cast% + under-supported count); tail-risk-first weakest-color composite.
- **Cost reducers, mana granters, fetch colors:** static "spells you cast cost {N} less" reducers shift effective turn; Cryptolith/Relic-style granters add conditional sources; fetchlands (empty Scryfall produced_mana) are credited to the colors they can fetch, including duals/triomes sharing a named basic type.
- **Two "show the work" formula panels (FORMULA-01):** the methodology, and the Karsten regression evaluated term-by-term for the entered deck.

**Quality:** build 0 errors; Core 601, Web 708 (11 PG-skip). 6-pillar UI audit 21/24. Karsten land math + Salubrious Snail cross-check credited in UI/help/README. Codex-reviewed across both waves; one empty-library simulator crash found by the added tests and fixed.

---

## Cycle 10 — Studio Automation, Sync & Polish (Shipped: 2026-06-21, `2026.06.6`)

**Phases completed:** 5 phases (59-63), 16 plans, 24 tasks
**Requirements:** 17/17 satisfied (AUTO, SYNC, SRC, HSEL, SUI, DIST). Audit status: tech-debt — no separate milestone audit; every phase individually verified (59 PASS 14/14, 60 operator-verified PASS, 61 executed, 62 verified 6/6 + secured 0/7, 63 verified 7/7 + operator clean-machine smoke).

**Key accomplishments:**

- **Pipeline automation (P59, AUTO-01/02):** one-action harvest → auto-distill → auto-approve in Studio — no separate Distill click, no rubber-stamping high-confidence distills. A swappable Core auto-approve signal (clip-count threshold, default ON/5) routes low-confidence distills to the review queue; operator can retune the cutoff or turn auto-approval off entirely. The existing spend dry-run/cap gate and distill provider are unchanged (no bypass, no model swap).
- **Pull-from-Prod reconcile (P60, SYNC-01/02/03):** a read-only inverse of DirectPush — Studio pulls live prod `content_site_index` rows (Postgres read) + their artifacts (SSH.NET SCP **download** from Render `/data`), classifies each entry as prod-newer / missing-locally / local-only / diverged, and lets the operator resolve each diff (adopt prod / keep local) without touching the CLI or DB. Read-only against prod; operator live-verified (60-04). Surfaced the prod-artifact-gap backlog item (86/109 rows missing `.md` on `/data`).
- **Creator sources & selection (P61, SRC-01/02, HSEL-01/02/03):** a persisted curated creator list + dropdown picker (paste-URL still available as a one-off), an unharvested-only default browse with a show-all toggle, and a lightweight skip/un-skip lane distinct from the heavyweight Block path (no hard-delete, no blocklist entry) — with a viewable skipped list.
- **Studio UI polish (P62, SUI-01..06):** a single shared `StatusBadge.razor` + `VideoStatusResolver.FromContentRow` pure mapper used on Harvest + Review (one rule, one place); creator filtering on Harvest browse and the Review queue via a pure `CreatorNameResolver` (Select-All/harvest scoped to visible rows); a live streaming **Pull Log** panel on Pull-from-Prod (stage + per-artifact `IProgress` lines, sanitized — no local paths or exception text); grouped Pipeline/Support nav; a Review → "Go to Publish (N approved)" shortcut; and the MainLayout About link fixed to `https://www.deckflow.gg`.
- **Self-contained Studio executable (P63, DIST-01):** a single-file self-contained `win-x64` `DeckFlow.Studio.exe` (~116 MB) produced by re-runnable publish scripts (ps1/sh) with a pinned Kestrel port, crash logging, and browser auto-open — the operator runs Studio on a clean Windows box with no .NET install. Operator clean-machine smoke passed.

**Quality:** build 0 errors; Studio 140/140, Core 524/524.

**Known deferred items at close:** 3 acknowledged (see STATE.md Deferred Items) — Phase 62 live-UI operator smoke (creator filters, Pull-from-Prod streaming, grouped nav/About — backed by bUnit + automated verification), and two stale Cycle-8 Phase-51 UAT artifacts (0 pending scenarios). Carry-forward backlog: prod-artifact gap (86/109 rows missing `.md`), KB-value A/B gating experiment (KBVAL-01/02, Cycle 10 v2), scheduled/bulk harvest (AUTO-03/04, Cycle 10 v2).

---

## Cycle 9 — Content Pipeline & Publish-Tracking (Shipped: 2026-06-19, `2026.06.5`)

**Phases completed:** 4 phases (55-58), 11 plans
**Code delta vs `main`:** 20 files (+706/−20) across Core/Web/Studio — pure additive Cycle-9 work (plus planning-doc reorg).
**Requirements:** 12/12 satisfied. Audit status: tech-debt — no separate milestone audit; every phase individually verified (55 SECURED, 56 verified 7/7, 57 verified + SC2→Phase 58, 58 all 4 SCs PASS + SECURED 9/9).

**Key accomplishments:**

- **Publish-state foundation (P55, PUB-01/02):** new `pushed_to_prod_utc` column (kept distinct from the seed-contract `published_utc`) via an idempotent dual-dialect migration, stamped by both publish paths; a single pure `PublishStateDeriver` in Core returns one of {Never published / Pushed-hidden / Published / Local-newer} — no duplicate status logic.
- **Studio surfaces (P56, BROWSE/REM/ADD/PUB-03):** 6-state per-video status badges at channel-browse, multi-select harvest, Block (hard-delete + blocklist) and a Blocked-list/unblock page, single-URL add, and the derived publish-state on Review + Publish — all without dropping to the CLI.
- **Admin surface + distill quality (P57, SITE-01/DIST-01):** publish-state column on `/Admin/ContentKb` reading the shared deriver; reworked the four distill system prompts for paste-ready summaries, on-topic clips, and tag parsimony (JSON contract unchanged).
- **Dogfooded the whole pipeline on real content (P58, DOGFOOD-01):** harvested + distilled a new video (real spend, $0 subscription), judged higher-quality than the pre-Cycle-9 baseline (tag discipline 3 vs 12), published it to prod, and confirmed `Published` on both surfaces — within the spend cap, no corpus regression (108→109 additive).
- **Found + fixed a real cross-surface gap (SC2):** dogfood exposed that DirectPush stamped `pushed_to_prod_utc` but never set `is_visible`, so Studio stayed Pushed-hidden while prod /Admin showed Published. Fixed (`4cb333e`): keyed `SetVisibilityAsync` + DirectPush publishes visible (prod-then-local); Codex-reviewed (1 HIGH + 1 MED fixed); secured (T-58-09, 9/9 SECURED).

**Quality:** build 0 errors; Studio 49/49, Core 475/475.

**Known carry-forward at close:** prod harvest green-run not yet observed since the F-51-PG-01 deploy (fix live 2026-06-17 21:19Z on `d0bb913`); `e3qGnuupp8U` durability (in prod DB, not the git seed — a future reset+reseed omits it until a full git-Publish); backlog seeded — Studio "Pull from Prod" (prod→local sync) + Validate-KB-value A/B gating experiment. Carry-forward ops from Cycle 8 still open (`deckflow_admin` deletion, Gemini prod flip).

## Cycle 8 — Hardening & Backlog Burn-down (Shipped: 2026-06-17, `2026.06.4`)

**Phases completed:** 4 phases (51-54), 11 plans
**Git range:** 46 commits, 184 files (+7,627/−1,656), all 2026-06-17
**Requirements:** 8/8 satisfied (FEAT-01 PASS-WITH-NOTES). Audit status: tech-debt — no milestone audit run; every phase individually verified (P51 PASS, P52 PASS, P53 PASS 8/8 + SECURED 4/4, P54 PASS-with-notes). First CalVer release.

**Key accomplishments:**

- **Verified the shipped v1.7 Studio/publish pipeline end-to-end (P51/P52):** deferred non-prod operator-UAT smokes (Studio runtime render, `/Admin/Harvest` no-jump grid, re-distill/cap/cancel, Review/Publish git+LF) plus a **live prod publish run** — DirectPush SCP'd artifacts to the Render `/data` disk and ran a content-columns-only Postgres upsert, proving admin flags (`is_visible`/`is_evergreen`/`is_hidden`/`approval_status`) on all 86 pre-existing rows were preserved while 8 new rows landed `pending`/not-visible.
- **Fixed F-51-PG-01:** `AddDeckIdsAsync` compared TEXT `last_checked_utc` to a `timestamptz` param → Npgsql `42883` on Postgres (SQLite tolerated). Dialect-guarded `::timestamptz` cast (PG-only, no migration); PG 19/19 + SQLite 20/20. Surfaced by the `DECKFLOW_POSTGRES_TESTS=1` gate (HARD-03).
- **Burned down the Phase 39 architecture backlog (Phase 53, ARCH-01/02):** facade-then-extract split of the `CategoryKnowledgeRepository` god-file (1272→274 LOC + Schema/DeckQueue/CardCategory collaborators); `Program.cs` DI extracted into `AddDeckFlowXxx()` (553→354 LOC) + finished `Services/` foldering (Scryfall/Persistence/Content); deck-stat classifiers relocated to `DeckFlow.Core.Analysis` (+64 tests); `Feedback*` layering leak removed from the Core `IRelationalDialect`. Zero user-visible change; verifier PASS 8/8; SECURED 4/4. Finding C dropped (already addressed by the Core orchestrator slices); full dialect-branch collapse deferred (PG DDL parity prereq). The DI ValidateOnBuild smoke test caught a latent missing `IFeatureFlagCache` registration.
- **Resolved feature debt (Phase 54, FEAT-01/02):** captured the `SpellbookCombo` ranking fields (`popularity`/`manaValueNeeded`/`uses`) the parser previously dropped + priority-ranked Deck Primer combos (popularity DESC, manaValueNeeded ASC); verified Gemini artifacts fit the ~30,000-char paste ceiling across all 4 workflows (analysis 24,994 / comparison 23,830 / meta-gap 18,026 / primer 5,553) — flag stays default-off.
- **Merged v1.7 to `main` + confirmed Render deploys from `main` (Phase 51, OPS-01).**

**Quality:** build 0 errors; Core 447/447, Web 633/644 (11 PG-skip).

**Known carry-forward at close:** `deckflow_admin` credential deletion (P52 — password already rotated, deletion owed by operator); operator live Gemini paste before flipping `DECKFLOW_GEMINI_ENABLED` in prod (P54); full dual-dialect branch collapse (gated on a Postgres DDL parity test); Cycle 9 = Studio/content-pipeline expansion + SEO/growth + SEED-001 (KB add/remove + publish-tracking).

Full archive: `.planning/milestones/cycle8-ROADMAP.md` · `.planning/milestones/cycle8-REQUIREMENTS.md`

---

## v1.7 Local Harvest & Publish Studio (Shipped: 2026-06-17)

**Phases completed:** 10 phases (41-50), 35 plans, 36 tasks
**Requirements:** 23/23 satisfied (STU, ORCH, REVQ, PUB, GRID, HARV, UIR). Audit status: tech_debt — integration clean, deferred operator-UAT tracked (see `.planning/milestones/v1.7-MILESTONE-AUDIT.md`).

**Key accomplishments:**

- **DeckFlow.Studio (new standalone Blazor Server app, Phases 41/45/46/47):** local operator console to harvest YouTube captions → distill to KB entries via LLM (with a spend dry-run gate) → review/approve in a queue → publish to production via TWO paths: git commit-publish of the LF-normalized seed, and direct prod-DB push (SSH.NET SCP of artifacts to Render `/data` then a safe content-columns-only Postgres upsert). Prod connection lives in user-secrets only; StudioConfig is presence-only (never carries the secret).
- **Orchestrator extraction (Phase 42, ORCH-01/02):** harvest/distill/export domain logic moved DeckFlow.CLI → DeckFlow.Core as `IContentKbOrchestrator` (facade over 5 slice interfaces); CLI reduced to thin adapters; closes the v1.6 god-class backlog item. Pure refactor, golden-fixture pinned.
- **Approval status + safe upsert (Phase 43, REVQ-01/PUB-01/02):** `approval_status` self-healing migration + `UpsertContentColumnsOnlyAsync` (preserves admin `is_visible`/`is_evergreen`) + approved-only filtered export — the prerequisite both publish paths depend on.
- **Admin grid lazy paging (Phase 44, GRID-01/02):** `/Admin/Harvest` initial load goes synchronous-count → AJAX on-demand; `LOWER(commander_name)` partial index fixes the slow query at the source.
- **Dapper data-access adoption (Phase 49):** raw ADO.NET boilerplate in 13 dual-provider stores replaced with Dapper behind the existing `IRelationalDialect`/`RelationalDatabaseConnection` abstraction; 5 provider-aware type handlers preserve Sqlite+Postgres parity; DDL/introspection + unnest-batch stay raw.
- **Code-style enforcement (Phase 50):** operator ReSharper style reconciled into `.editorconfig` (5 bug-driven carve-outs win); changed-lines-only pre-commit hook + CI `format-gate`; existing files never reflowed; CLAUDE.md made the source of truth. CI behavioral proof both directions.
- **UI audit remediation (Phase 48, UIR-01/02/03):** 6-pillar visual audit of the deployed deckflow.gg re-scored from v1.0 16/24 → **20/24** — inline-SVG iconography + resting elevation, widened surface/bg delta, darker muted, raised `--fs-xs` floor, typography hierarchy, short-form empty-state panels; every finding browser-verified at mobile + desktop across light/dark/commander-table themes.

**Quality:** DeckFlow.sln builds 0/0 (Studio included); Core.Tests ~346, Web.Tests 622 + DeckFlow.Studio.Tests (bUnit 34); cross-phase integration check clean (0 hard breaks, all 5 E2E flows wired); changed-lines format gate green.

**Known deferred at close (operator-UAT, non-blocking, tracked in v1.7-MILESTONE-AUDIT.md):** Studio runtime render smoke (P41), admin grid no-jump browser smoke (P44), re-distill E2E + cap-persist + cancel-on-dispose (P45), Review/Publish browser + real-git smoke (P46), live SCP+prod-Postgres publish (P47, needs operator secrets), Postgres parity tests (P49, `DECKFLOW_POSTGRES_TESTS=1`). `/gsd-secure-phase` not run for 48 + 50.

---

## v1.5 Deck Primer Generator + Content KB Integration + Housekeeping (Shipped: 2026-06-10)

**Phases completed:** 6 phases (28-33), 25 plans, 29 tasks
**Git range:** 219 commits, 781 files (+56,893/-2,108), 2026-06-03 → 2026-06-09
**Requirements:** 30/30 satisfied (HSK-02 descoped to backlog by design)

**Key accomplishments:**

- **Deck Primer Generator (Phase 31, PRM-01..12):** fourth paste-ready workflow at `/deck-primer` — 31-section catalog in 5 collapsible groups, Commander Spellbook combo grounding (null-graceful), bracket routing (EdhTop16 archetypes / generic fallback), category-knowledge distribution, per-AI artifact variants (ChatGPT/Claude/Gemini) via PacketArtifactStore zip round-trip.
- **Content KB Integration (Phase 30, KBI-01..06):** Expert Context block of top-K relevant curated clips injected into all three analysis prompt variants as attributed pull-quotes; "What Experts Say" panel on the DeckAnalysis result page; admin per-clip relevance score preview. Prod UAT passed 2026-06-07 (ships flag-gated/dark).
- **Expert Context Selection (Phase 32, SEL-01..06):** pin videos / follow creators / evergreen flag layered over auto-relevance via a 4-tier fill merge (pinned→followed→auto→evergreen); `is_evergreen` self-healing SQLite+Postgres migration; typeahead selection endpoints.
- **Admin Content KB Curation UX (Phase 33, KBUX-01..02):** instant client-side filter/search over the entries list + readability sweep (zebra, sticky header, hover/focus, mobile cards).
- **Housekeeping (Phases 28-29, HSK-01/03/04):** DeckFlow.Core XML-doc backfill + compiler doc-gate widened to `[DeckFlow.Core/**.cs]`; retroactive VERIFICATION.md + artifact hygiene.
- **Quality infra (milestone close):** added Vitest+jsdom browser test runner + first GitHub Actions CI (build + xUnit + Vitest); diagnosed and fixed the expert-pin injection bug (ParseRowsAsync dropped parse-failed pin rows — `a106c6a`) and unified pin-id derivation (`bfe16b1`).

**Known deferred items at close:** 15 (pre-v1.5 carryover — stale 999.x/v13 debug sessions, May quick-task references, empty todos; see STATE.md Deferred Items). SEL-02 live-pin re-confirm pending next KB-enable window.

---

## v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup (Shipped: 2026-06-03)

**Phases completed:** 14 phases (16-27 + inserted 21.1/21.2), 31 plans

**Stats:**

- Commits: 343 (`v1.3..HEAD` on `v1.4` branch)
- Files: 638 changed, +54,651 / −4,726 LOC
- Timeline: 2026-05-23 → 2026-06-03 (11 days)
- Requirements: 20/20 active v1.4 REQ-IDs shipped (KB-12 → backlog; DBO-01/CAT-02 ROADMAP-tracked inserts also shipped)
- Final tests: Core 257/257, Web 528 pass / 5 PG-integration skips

**Key accomplishments:**

1. **Content Knowledge Base end-to-end (KB-01..09, Phases 19-22)** — local CLI pipeline (YouTube captions via YoutubeExplode + Whisper fallback with monthly spend caps → LLM distillation into ≤200-word summaries, 3-8 timestamped clips, controlled-vocabulary tags → markdown prompt artifacts) feeding a slim Postgres site index with flag-gated public browse/filter, per-entry admin publish curation, and CSRF+SameOrigin-guarded mutations. Mid-milestone re-architecture from server-harvest to local-CLI model (2026-05-26) preserved all REQ-IDs.
2. **Pluggable LLM distill backends (KB-10/11, Phase 21.2)** — `LlmDistillationProviderFactory` selects openai|claude via env; claude-CLI backend runs the full 10-video distill at $0 subscription cost (cleared the Phase 21.1 gate after OpenAI 429 insufficient_quota), cross-platform WSL/Windows incl. the dotnet.exe-from-WSL hard case; codex backend deferred to backlog with documented untrusted-input read-boundary rationale.
3. **Category cache rebuilt (DBO-01/CAT-01/CAT-02, Phases 26/24/27)** — integer-keyed star schema with prod full-reset + re-harvest (hot commander query 69s timeout → 0.66ms index-only), Sol Ring/colorless-staple empty-categories fixed via read-time `CategoryFilter`, and content-hash dedup + 5-day refresh on deck writes.
4. **Admin mobile + tooling (AMOB-01..04/AHD-01/MODAL-01, Phases 18/25/16)** — `admin.css` factored into `admin-common.css` + `admin-mobile.css` scoped to `.admin-shell` (≥320px viewports, ≥44px touch targets), server-side paged harvested-decks commander grid, native `<dialog>` focus-trapped confirm modal reused across admin pages.
5. **Doc-warning gate live (DOC-01/02, Phases 17+23)** — every public type AND member in DeckFlow.Web XML-documented (475 compiler-derived sites in Phase 23 alone); `NoWarn 1591;1573;1587` stripped from the csproj and the editorconfig gate flipped to warning severity scoped to `[DeckFlow.Web/**.cs]`, probe-proven real; DeckFlow.Core (186 sites) deliberately deferred.
6. **Cross-AI delivery pattern sustained** — Codex implemented / Claude planned+reviewed across the milestone, with scope-fenced dispatches; plus a post-ship /simplify pass (SpendLedgerBase extraction, distillation validator dedup, dialect collapse) and ADR 0001 (prompt variants intentionally decoupled).

**Verification:**

- Milestone audit: `tech_debt` — 20/20 requirements satisfied, 4/4 E2E flows wired (integration-checker), 0 critical gaps (`.planning/milestones/v1.4-MILESTONE-AUDIT.md`)
- Live UAT: Phase 20 5-channel harvest (10/10 captions), Phase 21.2 10/10 claude distill at $0, Phase 22 both checkpoints, Phase 24 live smoke, Phase 26 prod reset verified via information_schema/pg_indexes

**Known deferred items at close:** 18 audit-open items acknowledged (11 stale scanner re-flags; see STATE.md Deferred Items) + audit tech debt: 7 phases missing VERIFICATION.md, P26 SUMMARYs, prod flag `content.kb.enabled` OFF (user flip pending), dual artifact trees, Core doc backfill, KB-12.

---

## v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene (Shipped: 2026-05-23)

**Phases completed:** 13 phases, 51 plans, 37 tasks

**Key accomplishments:**

- Five accessibility primitives (color-scheme, reduced-motion gate, touch-action, tabular-nums utility, scroll-margin-top) added to site-common.css so all 22 guild theme forks inherit them via cascade without per-fork edit.
- Universal keyboard-focus indicator + color-scheme + tabular-nums added to admin.css, mirroring site.css:109-118 so the admin shell renders the same visible focus ring as the main shell.
- One-liner:
- One-liner:
- 1. [Rule 3 - Blocking] Plan listed `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml`; the file on disk is `ChatGptCedhMetaGap.cshtml`
- Sweep-applied autocomplete="url" + inputmode="url" + ellipsis placeholder to 6 URL inputs and autocomplete="off" to 49 user-paste textareas across 8 Razor views — WDG-09 closed.
- One-liner:
- Renamed the three Razor view files (`ChatGptPackets.cshtml` / `ChatGptDeckComparison.cshtml` / `ChatGptCedhMetaGap.cshtml`) to AI-agnostic names (`DeckAnalysis.cshtml` / `DeckComparison.cshtml` / `CedhMetaGap.cshtml`) using `git mv` for history preservation, and updated all 39 `return View("ChatGpt…", …)` literal strings in DeckController.cs so view lookup resolves to the renamed files. Closes the Phase 11 verification note flagging the CedhMetaGap.cshtml filename mismatch.
- Closed the user-visible label half of RENAME-02 — added the Mock A `<p class="page-lede">` explainer paragraph under the H1 on all three AI workflow pages with exact D-07 copy, rebranded Page 1's H1 + browser title + nav label + hub-card title from `ChatGPT Analysis` to `Deck Analysis` per D-06 + D-09, swung all six remaining `~/chatgpt-` hrefs in `_DeckToolTabs.cshtml` (3) and `Home.cshtml` (4 including the hub-hero) to the new slugs, and added a cross-cutting `.page-lede` CSS rule to `site-common.css` (CLAUDE.md D-07 invariant — single source, NOT forked across any of the 22 guild themes). Pages 2 and 3 H1s left unchanged per D-06 (already AI-agnostic). Build clean against both `DeckFlow.Web.csproj` (Debug) and `DeckFlow.sln` (Release) with 0 warnings, 0 errors.
- Ctor parameters + private fields (3 of each):
- DeckControllerTests.cs (d7510da):
- One-liner:
- One-liner:
- Commit 1
- Gate 1 (warning count):
- 1. [Rule 1 - Bug] Missing `using DeckFlow.Web.Services;` in Analysis family
- AllForTesting internal seam added to AiPlatform.cs + 7-fact AiPlatformExtensionTests proving 4th-platform OCP extension with zero production edits (SC5 diff gate: PASS)
- 1. chatgpt-step-eyebrow baseline count
- One-liner:
- Hybrid prose pattern applied to CedhMetaGap.cshtml (6 ChatGPT hits rewritten) and DeckComparison hub-card in Home.cshtml generalized to "AI-authored breakdown"
- 10 InvalidOperationException strings across 3 C# service files rewritten to AI-agnostic phrasing, blocking page error banner from naming ChatGPT for Claude/Gemini users
- One-liner:
- README brand-neutralized (29 ChatGPT → AI generalizations), full-phase invariants grep gate PASS, `dotnet build Release` clean, human UAT approved.
- Found during:
- One-liner:
- One-liner:
- One-liner:
- STATE.md progress frontmatter now reports the SC1 literal v1.3 post-completion target: 11/11 phases, 46/46 plans, 100 percent.

---

## v1.2 Multi-AI Prompts (Shipped: 2026-05-13)

**Phases completed:** 2 phases, 8 plans, 22 tasks

**Key accomplishments:**

- One-liner:
- [Rule 3 - Blocking] Symlinked node_modules to unblock TypeScript build in worktree
- One-liner:
- Per-AI dispatch on request.TargetAiPlatform proven on BuildAnalysisPrompt with full Claude XML skeleton and full Gemini markdown+tweaks variants; ChatGPT path unchanged except for the new <result> wrap directive.
- Per-AI dispatch primitive proven in 10-01 fanned out across all four remaining prompt builders. AISEL-02 and AISEL-03 content-complete; every prompt the user generates is now keyed off request.TargetAiPlatform.
- AISEL-04 fully closed across all three ChatGPT pages. The unified <result>...</result> response extractor lives at the top of ExtractJsonPayload — one regex covers every response parser path with the existing fenced-JSON detection preserved as fallback.
- Magic 3000ms literal lifted to a documented module-scope constant; skipPersistence flag now self-clears 30s after set so a transient upload failure cannot silently disable form-state persistence for the rest of the page lifetime.
- Session zip for /chatgpt-cedh-meta-gap now round-trips full Step 1 state — fetched EDH Top 16 entries, selected reference indexes, and all four filter scalars — so a re-uploaded session regenerates the prompt without re-hitting edhtop16. Closes the v1.2 milestone-close blocker surfaced by integration test T3.

---

## v1.0 Polish & Quality (Shipped: 2026-05-02)

**Delivered:** Lifted DeckFlow's design-token layer, hardened tech-debt and security surfaces, and fixed the Scryfall Tagger empty-tag bug for cEDH staples — without breaking the live deckflow.gg ChatGPT-paste pipeline.

**Stats:**

- Phases: 5 (Phase 4 abandoned 2026-05-02; rerouted to Phase 5 — see `04-ABANDONED.md` in the archived phase dir)
- Plans: 17 total summaries
- Files modified: 136 | LOC: +20,284 / -5,194
- Commits: 63 (`5c11b00..5a36d42`)
- Timeline: 2026-04-30 → 2026-05-02 (3 days)
- Requirements: 15/15 v1 requirements shipped

**Key accomplishments:**

1. **Visual-system token layer (Phase 1, UI-VS-01..04)** — 6-step type scale (`--fs-xs/sm/base/lg/xl/2xl`) and semantic color tokens (`--link`, `--danger`, `--cta-border`, `--focus`) added to `site.css` `:root`; standalone hex literals hoisted to named tokens; token block propagated to all 25 guild themes with per-theme overrides (e.g. Rakdos `--link` `#ff9ea4`) so error vs link color can finally diverge.

2. **Layout, hierarchy & UX copy (Phase 2, UI-LH-01..02 / UX-01..03)** — Hub primary-CTA promoted, all inline `style=` attributes in Feedback / AdminFeedback views moved to named CSS classes, page `<title>` / `<h1>` voice aligned, and a non-blocking submit busy-state shipped on `/feedback`.

3. **Tech-debt cleanup (Phase 3, TD-01..04)** — Test-only `Null*` factories relocated to the test assembly via `[InternalsVisibleTo]`, services collapsed to a single ctor with test-helper factories, generated browser JS untracked from git (TypeScript-only source of truth), and `ForwardedHeadersOptions` tightened to Path B-rawpeer with `CF-Connecting-IP` + Render Inbound IP Rules.

4. **Scryfall Tagger restored for cEDH staples (Phase 5, BUG-01)** — Surgical revert of manual cookie replay back to auto-cookie management, plus two follow-up root causes that Phase 4 had masked: Cloudflare BIC blocks Render egress IPs without browser-shaped headers, and `AutomaticDecompression` must be enabled when advertising `Accept-Encoding`. Sol Ring / Counterspell / Mana Crypt all return 5+ oracle tags from production.

5. **Postgres-backed admin brute-force throttle (Phase 5, BUG-02 + TD-04 patch)** — `admin_brute_force_buckets` table with lazy expiry, 10/15-min window, monotonic `Retry-After`; gated on Render Inbound IP Rules with Cloudflare CIDR allow-list; same `CF-Connecting-IP` partition fix propagated to the `/feedback` rate-limiter so multi-proxy fragmentation can't dilute the bucket.

6. **Tagger cookie-replay regression guard (Phase 5)** — In-process integration test runs the full Tagger flow against a real `SocketsHttpHandler` via a localhost `HttpListener` stub; would catch a regression to manual cookie replay or `UseCookies=false` before it ships, closing the verification gap that let commit `4db8b8a` reach production untested.

**Verification:**

- Phase 5 verifier: passed 27/27 must-haves (7 ROADMAP SCs + 20 plan-frontmatter truths)
- Live UAT: Sol Ring 7 tags, Counterspell 5 tags, Mana Crypt 9 tags; admin throttle 10×401 + 1×429 with `Retry-After` 899→879 monotonic decrement
- UI audit re-score against `tasks/UI-REVIEW.md`: not yet measured (deferred to v1.1)

**Known deferred items at close:** 2 (see STATE.md Deferred Items)

- Phase 04 UAT: 5 stale pending scenarios (phase abandoned; work re-shipped under Phase 5)
- Phase 04 VERIFICATION: human_needed (phase abandoned; superseded by Phase 5 verification)

---
