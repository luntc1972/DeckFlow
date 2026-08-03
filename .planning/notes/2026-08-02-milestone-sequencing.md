# Milestone Sequencing: Cycle 21 → Cycle 20 → Cycle 22 (AI Enrichment)

Written 2026-08-02. Orders the completion of the two in-flight milestones and the
new AI-enrichment milestone decided today (see
`~/Downloads/deckflow-ai-feature-decisions-2026-08-02.md`).

## Why this order

- **Cycle 21 first** — it is the active workstream, mid-phase (Phase 4 at 3/4
  waves), with the most user-facing value queued (Phase 7 workflow UX fixes the
  measured 10,453px-tall page and dead tabs).
- **Cycle 20 second** — paused, admin-only surface, no user-facing pressure.
  One real cross-dependency forces part of it after Cycle 21: **Phase 113
  re-derives Scryfall shared infra (`ScryfallCollectionResolver`,
  `ScryfallLimits`, `archidekt` pipeline) line-by-line against current `main`,
  and Cycle 21 Phase 6 (Scryfall Throughput) edits that same infra.** Run 113
  after c21 P6 lands or the re-derivation is done twice.
- **Cycle 22 last** — AI features share only trivial touchpoints with Cycle 20
  (`Program.cs` DI region, CLI command registration, `ProgramStartupTests` —
  all additive), but R2 copies the `StatedRulesExtraction` distill pattern that
  Cycle 20 finishes exercising, and R4 touches the Cut Lab UI that Phase 7
  reorders. Sequencing avoids all rework.

## Stage 0 — clear the decks (in-flight branches, days)

1. **Land `feat/ui-audit-batch-a`** (pushed) and **`feat/ui-audit-batch-g`**
   (committed in worktree, awaiting user test) to `main`. Both touch views
   Phase 7/8 will edit (`CutLab.cshtml`, Bracket, DeckAnalysis forms) — landing
   first avoids conflicts. The original note that Phase 8's plan set existed only
   on `feat/ui-audit-batch-a` (`be421acd`) was true when written; after the
   2026-08-03 rebase, the plans are on this `gsd/cycle21-cut-lab` branch.
2. **Discharge owed Codex reviews**: plan 04-03 code review; Phase 8 plan
   review (user deferred 2026-08-02 — required before `/gsd-execute-phase 8`).

## Stage 1 — complete Cycle 21 (cut-lab workstream)

Order within the remaining phases (01.2, 04-04, 5, 6, 7, 8):

1. **Plan 04-04** — last Functional-Twins wave; has a human UI checkpoint.
   Gates Phase 7.
2. **Phase 7 — Cut Lab Workflow UX** (6 plans, already planned). Reserves the
   wizard slot Phase 8's plan panel needs.
3. **Phase 8 engine plans (08-01..08-06)** — independent, can run parallel with
   7 (separate files); **plan-panel UI (08-07, 08-08) after Phase 7** (same two
   files). Codex plan review owed first (Stage 0.2).
4. **Phase 6 — Scryfall Throughput** — no plans yet; plan + execute. Must land
   **before Cycle 20 Phase 113** (see cross-dep above).
5. **Phase 01.2 — Protection-Vocabulary Widening** — small classifier fix.
   ⚠ Easy to miss; Phase 2 already ran without it, so its SUMMARY must record
   the measurement caveat or trigger a targeted re-measure.
6. **Phase 5 — Archidekt Bracket Capture** — planned, one open HIGH;
   ⚠ needs a Postgres test host decision (Docker WSL integration off;
   Testcontainers unavailable) before execution.
7. **Closeout** — `deckflow-milestone-closeout` skill: release tag, README/help
   sweep, archive, and the Cut Lab prod-flag decision (flag currently OFF).

## Stage 2 — complete Cycle 20 (personal tools, `gsd/cycle20-personal-tools`)

Resume paused state (112 at 3/6 plans; waves 4-6 = the web layer: 20
`Web/Services` files, 13 test files, `CreatorStyleController`, view, e2e,
`CLI/CreatorStyleCommandRunners.cs`, 2 `Program.cs` edits).

1. **Phase 112 waves 4-6** — port the remaining 49 files (~10.1k LOC).
   Landmines already recorded in memory/plans: artifacts path is
   `ContentRootPath/../artifacts`; DI test must resolve the REAL
   `ArchidektOwnerClient` (D-20); `archidekt` Polly pipeline registers at 112
   (D-17).
2. **Phase 113 — Shared-Infra Re-derivation** — ONLY after Cycle 21 Phase 6 is
   on `main`. Re-derive against the post-P6 Scryfall code.
3. **Phase 114 — Port verification + BasicAuth `/Admin` surface.**
4. **Phase 115 — Operator run** (stated-rules seed, `fuse-profile`, real
   critique on `/Admin/CreatorStyle`).
5. Closeout + ROADMAP correction (line ~164 stale: still says 112 "0/0 Not
   started").

## Stage 3 — new milestone: Cycle 22 "AI Enrichment" (ranks 1, 2, 4)

New milestone, own branch (per standing branch rule). All $0-API-spend except
Phase 4.

| Phase | Content | Depends on |
|---|---|---|
| 1 | **Card embeddings** — local Ollama (`nomic-embed-text` on DESKTOP-PCTHMKM) embeds ~30k cards; pgvector on existing Render Postgres, exact scan (no HNSW — RAM); similarity service in Core; surfaces in Cut Lab what-if replacements, Meta Gap gap-fill, Category Suggestions fallback | — |
| 2 | **Batch category enrichment** — extend `LlmDistillationProviderFactory`/`CliLlmDistillationService` seam (`DECKFLOW_LLM_PROVIDER=claude\|codex`, flat-rate CLI); copy `StatedRulesExtraction` chunk→distill→validate pattern; fill thin Archidekt-crawl category knowledge; spend ledgers already exist | — (parallel with 1) |
| 3 | **Runtime guardrails** — per-user/daily token budget via `SpendLedgerBase` pattern, endpoint rate cap, bot-probe protection (daily POST scans observed in Render logs). Prereq for any metered runtime feature | — |
| 4 | **Cut Lab cut-rationale one-liners** — deterministic engine still decides; Haiku narrates (~$1–3/mo). Anthropic chosen for hard workspace spend cap (OpenAI cap is notification-only) | 1, 2 (rationale quality), 3 (guardrails), c21 P7 (UI settled) |
| backlog | **Judge retrieval-only** (R3 revised — NO LLM): verbatim Comprehensive Rules sections via FTS/hybrid retrieval; check overlap with existing Mechanic Rules tool; fix `/judge-questions` styling debt (unstyled on 23 themes) in same pass | optional; embeddings from Phase 1 reusable |

**Sequencing inside the milestone:** (1 ∥ 2) → 3 → 4. Phases 1+2 could start
during Stage 2 on a separate branch if desired — only trivial
`Program.cs`/CLI-registration rebase friction — but the clean path is after
Cycle 20 closes.

## Standing decisions this plan encodes

- R3 judge feature ships **without** LLM generation (owner veto 2026-08-02,
  hallucination risk) — retrieval-only, verbatim rules text.
- In-app Deck Analysis LLM answers: **skipped** — competes with the
  prompt-artifact core value.
- Deterministic tools (Manabase, Bracket, Sync, Convert, Lookups) stay
  AI-free — positioning.
