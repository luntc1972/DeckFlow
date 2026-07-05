---
gsd_state_version: 1.0
milestone: Cycle 15
milestone_name: Cleanup, Refactor & Visual Polish
status: Awaiting next milestone
stopped_at: Completed 86-05-PLAN.md — UIAUDIT-02 satisfied + human-verified (6-pillar re-score 21/24, clears >=20/24). Checkpoint approved by coordinator 2026-07-05.
last_updated: "2026-07-05T23:02:52.924Z"
last_activity: 2026-07-05 — Milestone 2026.07.2 completed and archived
progress:
  total_phases: 6
  completed_phases: 5
  total_plans: 22
  completed_plans: 22
  percent: 83
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 86 — UI Audit Re-Score (theme UI gaps)

**Cycle 14 close (2026-07-03):** Phases 79-81 (Interaction & Answers Audit / Win-Condition & Combo Map / Opening-Hand Mulligan Evaluator) squash-merged to `main` (`701ec2fa`), CI green (`28694830980`), milestone audit PASSED 13/13 requirements + 5/5 integration flows. All three flags (`analysis.interaction-audit`, `analysis.wincon-map`, `analysis.mulligan-eval`) seeded OFF. Full detail archived at `.planning/milestones/cycle14-ROADMAP.md`. Operator prod-deploy + flag-flip debt for Cycles 12-14 confirmed **DONE** 2026-07-04 (see PROJECT.md Current State).

## Current Position

Phase: Milestone 2026.07.2 complete
Plan: —
Status: Awaiting next milestone
Last activity: 2026-07-05 — Milestone 2026.07.2 completed and archived

## Roadmap Summary

| # | Phase | Requirements | Status |
|---|-------|-------------|--------|
| 82 | Refactor-Review Sweep | REVIEW-01, REVIEW-02 | Complete (3/3 plans) |
| 83 | Packet-Service SRP Split | PKTSVC-01, PKTSVC-02, PKTSVC-03, PKTSVC-04 | Complete (7/7 plans) |
| 84 | Theme Semantic-Token Migration | THEME-01, THEME-02, THEME-03 | Complete (2/2 plans) |
| 85 | `chatgpt-*` Naming Cleanup | AICLEAN-01, AICLEAN-02, AICLEAN-03 | Complete (5/5 plans) |
| 86 | UI Audit Re-Score & Studio Stage 4 Closeout | UIAUDIT-01 ✅, UIAUDIT-02 ✅, UIAUDIT-03 (deferred), ADMIN-01 (descoped) | Partially open (5/5 planned plans done; UIAUDIT-01/02 satisfied. UIAUDIT-03 needs a follow-up plan; ADMIN-01 descoped from Cycle 15) |

**Phase ordering rationale:**

- **82 first**: the review sweep's triage findings can surface additional risk/candidates before the rest of the cycle's scope locks in; it operates on files (`deck-sync.ts`, `Harvest.razor.cs`) disjoint from Phases 83-86, so it carries no sequencing dependency on them.
- **83 second**: the packet-service SRP split is the largest single refactor in the cycle (4 god-services, ~5265 combined LOC) and is purely backend/C# — no CSS/theme surface, so it proceeds independently of 84/85.
- **84 third**: the `--accent-strong` semantic-token migration fixes the exact color-pillar bug the UI audit (86) will re-score against — must land before the re-score to be reflected in the new score.
- **85 fourth**: the `chatgpt-*` rename touches the same 25 theme fork files as 84 (sequenced after to avoid same-file churn for two unrelated reasons in parallel) but is a pure identifier rename with no semantic/color change, so it does not need to precede the audit re-score.
- **86 last**: re-scoring the UI audit only makes sense once the color-token fix (84) has landed; also closes the owed DirectPush Stage 4 verification.

## Performance Metrics

**Velocity (Cycle 14 reference — most recent shipped):**

- Phases 79-81; build 0/0; Core 1053 pass, Web 1158 (12 PG-skip)
- Claude (Opus 4.8) implements + reviews code; Codex (gpt-5.4 medium) reviews plans + code (delegation rule per CLAUDE.md)

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 82 P01 | 15min | 2 tasks | 3 files |
| Phase 82 P02 | 15min | 2 tasks | 1 files |
| Phase 82 P03 | 40min | 2 tasks | 32 files |
| Phase 83 P01 | 90min | 2 tasks | 9 files |
| Phase 83 P02 | 30min | 2 tasks | 4 files |
| Phase 83 P03 | 25min | 1 tasks | 2 files |
| Phase 83 P04 | 40min | 2 tasks | 2 files |
| Phase 83 P05 | 30min | 1 tasks | 2 files |
| Phase 83 P06 | 50min | 2 tasks | 2 files |
| Phase 83 P07 | 35min | 1 tasks | 2 files |
| Phase 84 P01 | 50min | 3 tasks | 16 files |
| Phase 85 P01 | 12min | 1 tasks | 1 files |
| Phase 85 P02 | 20min | 3 tasks | 25 files |
| Phase 85 P03 | 35min | 2 tasks | 15 files |
| Phase 85 P04 | 25min | 3 tasks | 13 files |
| Phase 85 P05 | 55min | 1 tasks | 1 files |
| Phase 86 P01 | 10min | 2 tasks | 6 files |
| Phase 86 P02 | 25min | 2 tasks | 15 files |
| Phase 86 P03 | 20min | 2 tasks | 15 files |
| Phase 86 P04 | 4min | 2 tasks | 13 files |
| Phase 86 P05 | 55min | 2 tasks | 6 files |

## Accumulated Context

### Resolved Decisions (Cycle 15 — do NOT re-open)

- **Granularity = coarse:** 5 phases — one per requirement family (review sweep, packet-service split, theme migration, naming cleanup, audit re-score); derived from the 15 REQ-IDs, not padded.
- **Zero net-new features / byte-identical gate:** every phase's success criteria includes a behavior-neutral or byte-identical proof (paste artifacts unchanged; theme render unchanged except THEME-02/UIAUDIT's explicit visual corrections). ADR-0001 (prompt-variant decoupling — no shared prompt-prose helper) holds through the PKTSVC split.
- **Manabase engine untouched:** `CastabilitySimulator`/`ManabaseAnalyzer`/`ManabaseClassifier` are explicitly out of scope this cycle (deferred backlog item; needs a numeric-parity harness first).
- **DeckController split already done:** verified 2026-07-04; not reopened this cycle.

### Key Pitfalls to Watch (every phase)

- **Byte-identical / behavior-neutral proof required per phase** — refactor phases (82, 83) need an automated before/after comparison (paste artifacts across ChatGPT/Claude/Gemini, flag ON/OFF where applicable); CSS phases (84, 85) need a visual/render diff proving only the intended pixels changed.
- **ADR-0001 must survive PKTSVC-01** — the shared prompt-assembly collaborator must not become a shared prompt-*prose* helper; per-AI-variant text stays hand-authored and duplicated by design.
- **Theme files are full forks** — THEME and AICLEAN both touch all ~25-27 theme CSS files; layout CSS stays in `site-common.css`, new tokens go in each theme's own `:root`. Never fork layout into `site.css`.
- **AICLEAN blast radius (~1545 refs)** — treat as a large mechanical rename with real regression risk (25 theme forks + TS + views); grep-clean verification (AICLEAN-03) plus a full Playwright e2e run before considering it done.
- **UI audit re-score depends on THEME landing first** — sequencing violation risk if Phase 86 is planned/executed before Phase 84 ships.

### Pending Todos

- Run `/gsd-verify-work` for Phase 82, then `/gsd-plan-phase 83` (Packet-Service SRP Split).
- Phase 83/85 planners: read `.planning/phases/82-refactor-review-sweep-ui-baseline-audit/REFACTOR-BACKLOG.md` before finalizing scope — row 1b (deck-sync.ts persistence/card-picker/chatgpt-packets) and row 6 (PacketArtifactStore) are explicitly flagged for their coordination.
- Operator: assign an owner for the Spacing token-scale gap, the Feedback double-submit gap, and the missing branded `Error.cshtml` (flagged `needs-assignment` in `tasks/UI-REVIEW.md` — fit neither Phase 84 nor Phase 85).

### Blockers/Concerns

- None — Phase 82 (all 3 plans) complete, no blockers surfaced.

### Carry-Forward (still open from prior cycles)

| Item | Status |
|------|--------|
| `deckflow_admin` credential deletion (password rotated) | Operator task |
| Full dual-dialect branch collapse (PG DDL parity prereq) | Backlog |
| SEO/growth lane (SEO-01..05) | Deferred |
| Scheduled/bulk harvest (AUTO-03/04) | Deferred |
| Matchup / meta-threat read (deepens cedh-meta-gap) | Deferred (separate lane) |
| Manabase engine refactor (needs numeric-parity harness first) | Deferred (own future cycle) |

### Phase 82 Plan 01 Decisions (Refactor-Review Sweep)

- **deck-sync.ts + Harvest.razor.cs triaged in-scope for Wave 2 (82-03):** both named REVIEW-01 flagships, backed by confirmed existing Vitest/Playwright (deck-sync.ts) and xUnit/bUnit (Harvest.razor.cs) coverage — satisfies the "no test harness is not a valid deferral reason" guardrail.
- **Cross-file sessionStorage form-persistence duplication (deck-sync.ts + category-suggestions.ts) triaged in-scope:** natural byproduct of the deck-sync.ts extraction; needs one new test file on the existing Vitest runner (not new tooling).
- **ContentSiteIndexStore upsert-method duplication triaged in-scope:** small, contained, single-file dedup with 5 dedicated existing Core.Tests files as regression coverage.
- **ContentKbOrchestrator split, PacketArtifactStore dedup, df-select.ts, ScryfallSetService scoring-extraction all triaged backlog** with written reasons in `REFACTOR-TRIAGE.md` (LLM-spend-cap regression risk / PKTSVC-adjacent deferred to Phase 83 / no existing test coverage / capacity-only respectively).
- See `.planning/phases/82-refactor-review-sweep-ui-baseline-audit/REFACTOR-TRIAGE.md` for the full 8-row decision table consumed by 82-03.

### Phase 82 Plan 02 Decisions (UI Baseline Audit)

- **UI baseline re-scored 18/24 (prior 16/24, +2), via live screenshot audit** (desktop 1280x900 + mobile 390x844, 6 routes, Classic theme, headless `scripts/run-web-test.sh`) plus full-file CSS grep evidence — not the code-review-only fallback. `npm ci` was needed first (Rule 3, blocking, zero new packages) since this worktree's `DeckFlow.Web/node_modules/` was never populated.
- **Color (2/4) and Typography (3/4) gaps assigned to Phase 84 (THEME):** both are CSS-token-migration work (`--accent-strong` alias substitution at 58+4 call sites; ~24 residual literal `font-size` values onto `var(--fs-*)`) touching the exact files Phase 84 already owns. Target after Phase 84 lands: 21/24.
- **Spacing gap, Feedback-form double-submit gap, and missing branded `Error.cshtml` flagged `needs-assignment`** — none fit Phase 84's theme-token scope or Phase 85's `chatgpt-*` rename scope, and per Phase 82 success-criterion 5 none were defaulted to Phase 86 (re-score only). See `tasks/UI-REVIEW.md`'s "Out of Phase 84/85 scope — needs assignment" subsection.

### Phase 82 Plan 03 Decisions (Refactor Execution + Backlog)

- **All 3 in-scope REFACTOR-TRIAGE.md targets executed under the byte-identical gate:** deck-sync.ts
  narrowed split (busy-indicator.ts + moxfield-extension-bridge.ts), Harvest.razor.cs 4-coordinator
  split, ContentSiteIndexStore.cs upsert-parameter dedup. Build 0/0; Core.Tests 1053/1053 (incl.
  CarveOutGuard); Studio.Tests 258/258; Web.Tests 1158/1170 (12 PG-skip, baseline-consistent);
  Vitest 30/30; Playwright 62/62 e2e tests across the touched pages.

- **Cross-file TS calls under `module: "none"` need a `window.*` bridge, not bare identifiers, for
  Vitest compatibility:** confirmed empirically (isolated tsc probe) that tsc's unified program +
  the browser's shared `<script>` scope make bare cross-file calls compile and run fine in
  production, but Vitest's per-file dynamic-import ESM graph does not share them — the fix mirrors
  the codebase's existing `window.DeckFlow` bridge pattern. Any FUTURE `wwwroot/ts` extraction must
  budget for this the same way.

- **Harvest.razor.cs coordinators keep all Blazor UI state in the page** (not a fully-stateless
  DirectPushCoordinator-style split) — `HarvestPlanner.cs`'s own prior doc comment already explained
  why a full split was rejected (I/O interleaved with UI state); only the cleanly-separable
  injected-service I/O per concern was extracted.

- **6 remaining candidates recorded in `REFACTOR-BACKLOG.md`** with written reasons: row 1b
  (deck-sync.ts persistence/card-picker/chatgpt-packets, behavior-coupled + Phase-85-fenced), row 3
  (cross-file form-persistence "dedup", refuted — not a real duplication), row 5
  (ContentKbOrchestrator split, needs boundary tests first), row 6 (PacketArtifactStore, defer to
  Phase 83's own scope check), row 7 (df-select.ts, needs new test coverage first), row 8
  (ScryfallSetService scoring extraction, capacity-only deferral).

- See `.planning/phases/82-refactor-review-sweep-ui-baseline-audit/82-03-SUMMARY.md` for full detail.

### Phase 83 Plan 01 Decisions (Byte-Identical Regression Harness, PKTSVC-04)

- **25 xUnit byte-identity tests across all 4 packet services x 3 AI platforms**, goldens captured
  verbatim from real `BuildAsync` runs against today's unrefactored code — see
  `.planning/phases/83-packet-service-srp-split/83-01-SUMMARY.md`.

- **Fixed 3 self-inflicted flake risks in the harness itself before trusting it as a gate:** a live
  `generated_at_utc` timestamp embedded in Analysis/Comparison artifacts (normalized via regex), a
  Windows-vs-Linux CRLF/LF mismatch (`dotnet.exe` captures with `\r\n`; CI runs `ubuntu-latest` with
  `\n` — normalized both sides before every comparison), and an escaped-string golden for the one
  fixture that deliberately embeds a bare CR (H3 whitespace lock) which the LF-only format gate
  rejects inside a raw string literal.

- **Golden constants factored into separate `*Goldens.cs` files** (`AnalysisGoldens.cs`,
  `ComparisonGoldens.cs`, `MetaGapGoldens.cs`, `PrimerGoldens.cs`) rather than inlined in the
  `*ByteIdentityTests.cs` files, for readability given 5-100+ KB of captured text per service.

- **Confirmed MetaGap needs no whitespace fixture** (only free-text field is `CommanderName`,
  already covered by the shared `JsonTextFormatterService.NormalizeSingleLine`) and Comparison has
  no prompt-mutating flags — both documented in the suites' XML doc comments rather than silently
  assumed.

- All 4 mandated path-coverage requirements (versioned decklist, per-consumer collection-miss
  fallback, per-service commander reflag, flag combinations, whitespace normalization) are covered.
  Plans 83-02 through 83-07 must re-run `dotnet.exe test DeckFlow.Web.Tests --filter
  "FullyQualifiedName~ByteIdentity"` after each collaborator extraction and treat any failure as a
  regression.

### Phase 83 Plan 02 Decisions (PacketTextAssembler + DeckEntryReflagHelper, PKTSVC-01/03 groundwork)

- **Two new unconsumed static collaborators built under `DeckFlow.Web/Services/Packets/`:**
  `PacketTextAssembler` (`BuildSectionedDecklistText` Cluster D + `AppendKeyValueLine` Cluster E) and
  `DeckEntryReflagHelper` (`ReflagCommanderEntry` Cluster B) — see
  `.planning/phases/83-packet-service-srp-split/83-02-SUMMARY.md`. Per the plan's explicit scope
  boundary, no service was migrated onto them yet; that is Wave-2 (83-03 through 83-07).

- **`DeckEntryReflagHelper` verified byte-identical via direct `diff` before extraction** (0 lines
  differ between `DeckComparisonService.cs:363-383` and `MetaGapService.cs:465-485`), and
  `AppendKeyValueLine` takes the normalizer as a `Func<string?, string, string>` delegate (proven by
  a test with two different delegates producing two different outputs) rather than referencing any
  concrete `NormalizeSingleLine` — preserving the 3 non-byte-equivalent per-service normalizers per
  research H3/do_not_unify.

- **PKTSVC-01 and PKTSVC-03 requirement checkboxes intentionally NOT marked complete by this plan**,
  despite being listed in its frontmatter `requirements` field — both requirements' full text
  ("each of the four packet services delegates to it" / "each service reduced to an orchestration
  shell") describes Wave-2 migration outcomes this plan explicitly defers. Confirmed 83-03 through
  83-07 all still list PKTSVC-01/02/03/04 in their own frontmatter, so marking now would create an
  inconsistent state (checked here, still "required" in later plans). Defer `requirements
  mark-complete` for PKTSVC-01/03 to whichever later plan actually finishes the corresponding
  migration/shell-reduction work.

### Phase 83 Plan 03 Decisions (ScryfallReferenceResolver, PKTSVC-02 groundwork)

- **One new unconsumed collaborator built under `DeckFlow.Web/Services/Packets/`:**
  `ScryfallReferenceResolver` (`ResolveBatchAsync` — Cluster A batch-chunk-collect-fallback loop) —
  see `.planning/phases/83-packet-service-srp-split/83-03-SUMMARY.md`. Per the plan's explicit scope
  boundary, no service was migrated onto it yet; that is Wave-2 (83-04 through 83-07).

- **Fallback strategy is a required delegate parameter, never hardcoded:** a test proves the
  fallback delegate is never invoked on a clean collection hit (it throws if called), and separate
  tests exercise both Analysis's richer fallback shape and Comparison/MetaGap's simpler fallback
  shape through the same delegate parameter — preserving each service's intentionally-different
  miss-handling per research Cluster A.

- **Single-slash match-by-original-name fallthrough (H2) locked by a dedicated test:** a
  `normalizeForScryfall=true` request for `"A / B"` (normalized to `"A // B"` on submission) does
  NOT match its own collection response (`"A / B"` != `"A // B"`) and correctly falls through to
  the fallback delegate, keyed by the ORIGINAL `"A / B"` name with `FromFallback=true`.

- **PKTSVC-02 and PKTSVC-03 requirement checkboxes intentionally NOT marked complete by this plan**,
  for the same reason 83-02 deferred PKTSVC-01/03: the collaborator exists and is unit-tested, but
  "no duplicate resolution path remains" / "each service reduced to an orchestration shell" describe
  Wave-2 migration outcomes (83-04 through 83-07) this plan explicitly does not attempt.

### Phase 83 Plan 04 Decisions (DeckComparisonService migration, first Wave-2 service)

- **DeckComparisonService migrated onto all three Wave-1 collaborators** (`ScryfallReferenceResolver`,
  `PacketTextAssembler`, `DeckEntryReflagHelper`), dropping from 1033 to 924 LOC — see
  `.planning/phases/83-packet-service-srp-split/83-04-SUMMARY.md`. All 25 byte-identity tests from
  83-01 remain green; Cluster C/F/G methods (`BuildCanonicalDeckSourceText`, `BuildComboArtifactText`,
  `NormalizeOracleText`) were left untouched per the do-not-unify guidance.

- **[Rule 1 fix] Re-wrapped the shared resolver's generic `HttpRequestException`** with the original
  deck-labeled message text ("{deckLabel} Scryfall card reference lookup failed...") — the resolver's
  generic message ("...cards/collection returned HTTP...") would otherwise mis-route through
  `UpstreamErrorMessageBuilder` in `DeckPacketController`'s catch block (which special-cases messages
  containing "Deck A"/"Deck B" to bypass the builder) and surface the wrong "...analysis packet..."
  copy for a Comparison failure. Not caught by the byte-identity harness (happy-path only) — found via
  manual trace of the controller's error-routing logic.

- **PKTSVC-01/02/03 checkboxes still NOT marked complete** — these are phase-wide requirements
  ("each of the four packet services" / "all four packet services") and only Comparison has been
  migrated so far; Analysis and MetaGap migrations (83-05/83-06) remain. The verifier or the final
  migrating plan should flip these once all four services are done.

### Phase 83 Plan 05 Decisions (MetaGapService migration, second Wave-2 service)

- **MetaGapService migrated onto `ScryfallReferenceResolver` and `DeckEntryReflagHelper`, plus
  `PacketTextAssembler.AppendKeyValueLine` for its 2 normalized request-context fields**, dropping
  from 956 to 909 LOC — see `.planning/phases/83-packet-service-srp-split/83-05-SUMMARY.md`. All 25
  byte-identity tests from 83-01 remain green; Clusters C/D/F (`BuildCanonicalDeckSourceText`,
  `BuildCanonicalDecklistText`, `BuildCompactDecklist`/`BuildCompactRefDecklist`,
  `BuildComboReferenceText`) were left untouched per the do-not-unify guidance.

- **[Rule 1 fix] Re-wrapped the shared resolver's generic `HttpRequestException`** with MetaGap's
  ORIGINAL message text ("...building the cEDH meta-gap prompt...") — same landmine 83-04 found for
  Comparison, but MetaGap's controller (`DeckPacketController.CedhMetaGap`) has no per-caller
  message-text routing, so the fix is a single generic re-wrap (not deck-labeled). The resolver's
  generic message contains "cards/collection", which `UpstreamErrorMessageBuilder
  .BuildDetailedScryfallMessage`'s first branch matches and would surface the wrong
  "...analysis packet..." copy; the original wording matches neither "cards/collection" nor
  "analysis packet", restoring the correct fallthrough to the generic "Scryfall returned HTTP
  {code}..." copy. Not caught by the byte-identity harness (happy-path only) — found via manual
  trace of the controller's error-routing logic.

- **PKTSVC-01/02/03 checkboxes still NOT marked complete** — Analysis (83-06, largest/most
  flag-interaction-heavy) and Primer (83-07, resolver-free by design) migrations remain. The
  verifier or the final migrating plan should flip these once all four services are done.

### Phase 83 Plan 07 Decisions (DeckPrimerPacketService migration, fourth and final packet service — Phase 83 COMPLETE)

- **DeckPrimerPacketService (905 LOC, the only resolver-free packet service) migrated onto
  `PacketTextAssembler`**, dropping to 866 LOC — see
  `.planning/phases/83-packet-service-srp-split/83-07-SUMMARY.md`. All 25 byte-identity tests from
  83-01 remain green, including all 3 `DeckPrimerByteIdentityTests` cases. Do-not-unify clusters
  (char-by-char `CollapseWhitespace`, `BuildCanonicalDeckSourceText`/`EvaluateStaleness`,
  `BuildComboReferenceText`) left untouched.

- **PKTSVC-02 satisfied for Primer by VERIFIED ABSENCE, not by adding a resolver** — a new
  source-scan regression test (`DeckPrimerPacketServiceTests.SourceFile_ReferencesNoScryfallResolutionType`)
  proves the service file contains zero `IScryfallCardResolver`/`ScryfallReferenceResolver`
  references, per 83-RESEARCH.md Pitfall 3 (adding one would be a net-new feature, prohibited by
  the milestone's no-new-feature gate).

- **`DeckPrimerPacketService.NormalizeSingleLine` KEPT verbatim** — still called directly by the 3
  Primer prompt-variant files (`ChatGpt`/`Claude`/`Gemini`); build confirms they still compile.
  `BuildRequestContextText`'s 5 key:value fields now route through
  `PacketTextAssembler.AppendKeyValueLine`, passing Primer's own `NormalizeSingleLine` as the
  delegate.

- **ALL FOUR PKTSVC-01/02/03/04 requirement checkboxes now marked Complete** — this was the final
  migrating plan (Analysis 83-06, Comparison 83-04, MetaGap 83-05, Primer 83-07 all done). Marked
  via `gsd-sdk query requirements.mark-complete PKTSVC-01 PKTSVC-02 PKTSVC-03` (PKTSVC-04 was
  already complete from 83-01). Phase 83 is now fully complete; build 0/0, full
  `DeckFlow.Web.Tests` suite 1214 passed / 12 PG-skip / 0 failed.

### Phase 83 Plan 06 Decisions (DeckAnalysisPacketService migration, third and final Wave-2 Scryfall-resolving service)

- **DeckAnalysisPacketService (2372 LOC, the largest packet service) migrated onto
  `ScryfallReferenceResolver` and `PacketTextAssembler`**, dropping to 2254 LOC — see
  `.planning/phases/83-packet-service-srp-split/83-06-SUMMARY.md`. All 25 byte-identity tests from
  83-01 remain green, including all 12 `DeckAnalysisByteIdentityTests` cases (3-platform baseline,
  6 individually-toggled flags, all-4-mutating-flags-ON companion/partner fixture, versioned +
  collection-miss-fallback fixture, whitespace fixture). Do-not-unify clusters (partner-aware
  commander reflag, full `NormalizeOracleText`, `BuildComboReferenceText`,
  `BuildCanonicalDeckSourceText`, JSON-validation guards) left untouched.

- **H3 confirmed: `DeckAnalysisPacketService.NormalizeSingleLine` KEPT verbatim** — not deleted, not
  swapped for `JsonTextFormatterService.NormalizeSingleLine`. It is still called directly by the 6
  Analysis/SetUpgrade prompt-variant files (`ChatGpt`/`Claude`/`Gemini` × `Analysis`/`SetUpgrade`);
  build confirms they still compile. `BuildRequestContextText`'s 6 key:value fields now route
  through `PacketTextAssembler.AppendKeyValueLine`, passing Analysis's own `NormalizeSingleLine` as
  the delegate.

- **Investigated (no fix needed): the generic `HttpRequestException` re-wrap is NOT load-bearing for
  Analysis**, unlike Comparison (83-04) and MetaGap (83-05). `DeckPacketController`'s DeckAnalysis
  actions call `UpstreamErrorMessageBuilder.BuildScryfallMessage` unconditionally (no per-caller
  message-substring routing), and BOTH Analysis's original message AND the resolver's generic
  message contain "cards/collection", so both hit the SAME `BuildDetailedScryfallMessage` branch and
  produce byte-identical final copy either way. Re-wrapped anyway for exact `exception.Message`
  parity, matching the 83-04/83-05 precedent, at negligible cost.

- **PKTSVC-01/02/03 checkboxes still NOT marked complete** — only Primer (83-07, resolver-free by
  design) remains. The verifier or 83-07 should flip these once Primer's plan confirms it has zero
  duplicate Scryfall-resolution paths.

### Phase 84 Plan 01 Decisions (Semantic-Token Alias Re-Point + Affordance Swap, THEME-01)

- **D1 executed exactly as locked:** re-pointed `--link`/`--cta-border`/`--focus` from
  `var(--accent)` to `var(--accent-strong)` in `site.css` and its 11 duplicating forks (abzan,
  bant, esper, grixis, jeskai, jund, mardu, naya, nyx, planeswalker-dark, sultai); `--danger`
  untouched everywhere. This means the ~30 real-role call sites stay byte-identical and only the
  8 already-migrated sites (global focus-visible outline, `.cache-pill__reset`, 3 `>summary`
  help-hint links, `.run-button`/`.copy-button` border, 2 generic focus outlines in
  site-common.css) shift shade — captured pre/post in `theme-baseline-pre84.json` for Plan
  84-02's no-drift diff. See `.planning/phases/84-theme-semantic-token-migration/84-01-SUMMARY.md`.

- **D4 (commander-table gap) closed:** `site-commander-table.css` had no `@import site.css` and
  was missing all four semantic tokens; added the net-new 4-token block to its own `:root`
  immediately after `--accent-strong`, preventing an unset-custom-property regression once its
  shared call sites resolve `var(--link)`/`var(--focus)`/`var(--cta-border)`.

- **Exactly 19 genuine link/focus/cta-border affordance sites swapped** (7 link + 9 focus + 3
  cta-border across site-common.css/site-mobile.css/site-theme-overrides.css), using the
  defensive `var(--<token>, var(--accent-strong, <original-tail>))` form so `--accent-strong`
  remains a fallback. D2's exhaustive decorative classification (37 residual in site-common.css,
  3 in site-mobile.css, 2 in site-theme-overrides.css) was left unchanged and verified via the
  plan's exact `grep -c`/`grep -oP` count gates — all passed on first attempt, no auto-fixes
  needed.

- **Shared-rule swap units, not selector splits:** `.kb-chip--pinned, .kb-chip--followed` and
  `.kb-clip-origin--pinned, .kb-clip-origin--followed` share one declaration each; swapped the
  shared declaration as a unit rather than splitting the rule (splitting would have added new
  selector lines, violating the "no selector-line changes" acceptance gate).

- **site-rakdos.css and the 10 cascade-safe `@import` forks (azorius, boros, dimir, golgari,
  gruul, izzet, orzhov, selesnya, simic, temur) confirmed untouched** (`git diff --quiet`) —
  rakdos keeps its UI-VS-02 `--link:#ff9ea4` override; the 10 forks inherit the re-pointed
  aliases via cascade with zero required changes (D4 minimize-churn).

- **D3 (Typography/font-size) confirmed OUT of scope** — zero `font-size` literals touched
  (verified by gate); Phase 86 still owns closing that gap.

- **THEME-01 requirement marked complete**; THEME-02 (danger != link per theme, live
  desktop+mobile red-guild verification) and THEME-03 (no-drift proof against the baseline
  artifact) remain for Plan 84-02.

### Phase 85 Plan 02 Decisions (CSS chatgpt-* to prompt-* Rename, AICLEAN-01)

- **All 39 `.chatgpt-*` class stems + 4 `data-chatgpt-*` attribute selectors renamed to
  `prompt-*`/`data-prompt-*` across all 25 CSS files** (site.css, site-common.css,
  site-commander-table.css, site-mobile.css, site-theme-overrides.css, 20 guild theme forks) — see
  `.planning/phases/85-chatgpt-naming-cleanup/85-02-SUMMARY.md`. Zero `chatgpt` (any case) remains
  under `DeckFlow.Web/wwwroot/css`; build 0/0.

- **Phase 84's `var(--cta-border, var(--accent-strong, var(--accent, currentColor)))` fallback
  chain on the `:has()` rule at site-common.css ~1222-1225 preserved byte-for-byte** — only the
  `data-chatgpt-cedh-reference-table`/`data-chatgpt-cedh-reference-checkbox` attribute names
  changed. site-rakdos.css's Phase-84 `--link:#ff9ea4` override value also left untouched. Verified
  via a strict token-normalized (chatgpt<->prompt collapsed to one token) diff gate against the
  85-01 pre-phase baseline for every one of the 25 files.

- **[Rule 2 - gate compliance] Hand-edited 2 capitalized "ChatGPT" comment-prose instances in
  site-common.css** (line 1855, describing the sticky-download feature) to "Prompt"/"prompt" — the
  plan's acceptance criteria requires zero `chatgpt` (any case) and this plan's CSS scope has no D3
  keep-list exceptions (confirmed via 85-RESEARCH.md). Comment-only change, no selector/value drift.

- **AICLEAN-01 requirement satisfied for the CSS surface.** Razor `class="chatgpt-*"` emissions and
  `deck-sync.ts` `querySelector('.chatgpt-*')` consumers remain to be renamed in 85-03/85-04 (same
  wave 2) — a CSS-renamed-but-Razor-not intermediate transiently orphans selectors until the wave
  completes, expected per the plan's NOTE. Plan 85-05 proves render byte-identical at the wave
  boundary against the 85-01 baseline.

### Phase 85 Plan 04 Decisions (ChatGptSwapPrompt Symbol + Cosmetic Identifier Rename, AICLEAN-02/03)

- **Renamed the generic `ChatGptSwapPrompt` C# symbol to `PromptSwapPrompt` in one commit**
  (ManabaseAnalysisService record param + doc `<param>`, ManabaseViewModel property,
  ManabaseController assignment, 9 test references, Manabase.cshtml binding) — property rename
  does not change the string VALUE it holds, so the rendered swap-prompt textarea content stays
  byte-identical. See `.planning/phases/85-chatgpt-naming-cleanup/85-04-SUMMARY.md`.

- **Cosmetic `chatgpt-*` identifiers renamed to `prompt-*`** in DeckPrimer.cshtml
  (sticky-download/resume/step-heading/eyebrow/badge/actions), Manabase.cshtml (print-button
  class + `data-prompt-print`, `manabase-prompt-output` textarea id + matching
  `data-copy-target`), ContentKb/Detail.cshtml (sticky-download), and `_FormError.cshtml`'s
  doc-comment example — matches the already-renamed CSS/TS selectors from 85-02/85-03.

- **2 internal (non-visible) doc comments genericized** in DeckPacketController.cs and
  DeckAnalysisPacketService.cs; the DeckAnalysisPacketService.cs:2131 `"ChatGPT"`
  TargetAiPlatform model-key default and all user-visible "ChatGPT" copy strings (DeckPrimer,
  Manabase, ContentKb ledes/buttons) kept verbatim (D3).

- **3 pre-existing "ChatGPT" doc-comment mentions deliberately left out of scope**
  (`ManabaseAnalysisService.cs:25`, `ManabaseViewModel.cs:7`, `ManabaseController.cs:13`) — not
  enumerated in this plan's `<interfaces>` rename list; flagged in the SUMMARY as a candidate for
  a future cleanup pass rather than silently expanding scope beyond the dual-reviewed plan.

- **print-manabase-results.spec.ts + HelpContentServiceTests.cs + HelpControllerTests.cs updated**
  to `data-prompt-print` / `prompt-analysis` slug; build 0/0, Manabase (149/149) + Help (12/12)
  xUnit suites and the scoped Playwright print-manabase e2e (2/2, both viewports) all pass.

### Phase 85 Plan 05 Decisions (Post-Rename Byte-Identical Proof + Full-Suite Acceptance Gate — Task 1 automated, Task 2 PAUSED)

- **Byte-identical proof PASSES structurally** once the plan's own chatgpt/prompt placeholder
  normalization is supplemented (diff-time only, neither committed artifact mutated) with
  normalization for 3 explained, rename-unrelated noise sources: per-request CSRF antiforgery
  token value, cache-bust content-hash query strings on the renamed CSS/JS `<link>`/`<script>`
  tags (expected to change — the hash reflects changed file bytes), and a pre-existing (pre-dates
  Phase 85 by many commits, `179cb673`) async Scryfall/MTG-set-catalog searchable-dropdown widget
  on `/deck-analysis` whose exact option count is time/cache-dependent. The plan's literal
  automated one-liner reports DIFFER for these 3 reasons alone; see
  `.planning/phases/85-chatgpt-naming-cleanup/85-05-SUMMARY.md` for full root-cause detail.

- **Gate (c) (`git diff --name-only` keep-list check) flags 4 Studio files** — confirmed a false
  positive: an unrelated concurrent session's review-queue-notes feature (commits
  `28837f83`/`fcee60f4`) landed on the shared branch between 85-03 and 85-04, not a D3 keep-list
  violation. No actual keep-list `.cs` file was touched.

- **All other gates green:** grep-clean (a)/(b)/(d) pass; build 0/0; full xUnit 2603 passed/0
  failed/12 skipped; full Playwright e2e 256 passed/0 failed/14 skipped.

- **AICLEAN-01/02/03 marked COMPLETE after human sign-off (2026-07-05).** The Task 2
  `checkpoint:human-verify` was approved by the coordinator: the 4 reworded client-side validation
  strings are RATIFIED to keep (they align with the site's existing "your AI" convention; ChatGPT
  branding on titles/ledes/descriptions is preserved), D3 keep-list confirmed intact, and D5
  cache-key lockstep confirmed. AICLEAN-01 was flipped via
  `gsd-sdk query requirements.mark-complete` (AICLEAN-02/03 were already complete from 85-03/85-04).
  Phase 85 is closed. NOTE: the pre-existing step-tab/theme/layout-picker/checklist styling UI bugs
  are explicitly out of scope here and handled as separate work — the byte-identical proof confirms
  the rename did not touch them.

### Phase 86 Plan 02 Decisions (Filled-Accent-Pill Active Step-Tab + Dark-Theme Contrast, Bug A)

- **Base `.prompt-step-tab.is-active` in `site.css` is now a filled `var(--accent)` pill**
  (`border-color`/`background: var(--accent)`, `color: var(--accent-contrast, #fff)`, 20%
  color-mix box-shadow ring) — reuses the proven `site-mobile.css:353` template. Inherited free
  by all 11 `@import` themes; mirrored into all 12 standalone forks that duplicate the base rule
  (`site-rakdos.css` intentionally left untouched — its existing filled pill already renders
  acceptably).

- **Duplicate-late-override hazard fixed:** `site-abzan.css`, `site-jund.css`, `site-sultai.css`
  each declared `.prompt-step-tab.is-active` twice (the later block wins the cascade). Both
  occurrences in each file were replaced with the identical canonical block so the terminal
  block always resolves the filled pill regardless of cascade order.

- **`site-grixis.css`/`site-jeskai.css` switched off legacy `--grixis-blue`/`--jeskai-blue`
  secondary tokens onto the guild's own `--accent`** for the active-tab border/color, matching
  the canonical rule (these forks previously keyed the active-tab text/border to a different
  color than their guild's main accent).

- **Re-measured WCAG contrast empirically for all 24 themes rather than trusting the plan's
  checklist** (the plan explicitly flagged the checklist as non-authoritative). Confirmed
  checklist failures (dimir 3.80:1, golgari 2.88:1, planeswalker-dark 3.43:1, nyx 4.19:1) and
  found two the checklist omitted: **abzan (2.84:1) and esper (2.78:1)**. Added
  `--accent-contrast` tokens to all six themes' `:root`, using each theme's own dark palette
  (usually `--bg`) except nyx, whose own `--bg` (4.46:1) fell just short so a custom slightly
  darker value was used (4.73:1). jund (6.63:1), sultai (4.89:1), naya (4.57:1), grixis (5.20:1),
  mardu (5.50:1), bant (5.35:1), commander-table (5.24:1), and jeskai (5.70:1) all pass with
  white and got no token.

- **Live spot-checked** (headless Chromium via Playwright against the running dev server;
  ad-hoc script, not committed) computed `background-color`/`color` for golgari, dimir,
  planeswalker-dark, abzan, esper, and nyx active tabs — all six matched the intended CSS
  exactly. Formal Playwright visual-regression + WCAG e2e coverage (`theme-active-affordance.spec.ts`)
  is created in plan 86-05 per the phase's validation strategy; this plan's grep/contrast gates
  plus the live spot-check are the interim proof.

### Phase 86 Plan 03 Decisions (Bucket-Toggle Chevron + Accessible Name, Bug C)

- **`DeckAnalysis.cshtml:306` bucket toggle button now has `aria-label="Toggle {bucket.Label}
  questions"`**; base `site.css` `.prompt-question-bucket__toggle` restyled from a bordered
  1px/`--line`/4px-radius box with a faint `--fs-xs`/`--muted` caret to a borderless
  (`border: 0`) plain chevron at `var(--fs-base)`/`var(--ink)` — see
  `.planning/phases/86-ui-audit-re-score-studio-stage-4-admin-flags-closeout/86-03-SUMMARY.md`.

- **Mirrored identically into all 12 standalone forks** (abzan, bant, commander-table, esper,
  grixis, jeskai, jund, mardu, naya, nyx, planeswalker-dark, sultai), replacing each fork's
  hardcoded `font-size: 0.65rem` with the same `var(--fs-base)` token rather than another
  literal. The 10 bare `@import` themes (azorius, boros, dimir, golgari, gruul, izzet, orzhov,
  selesnya, simic, temur) inherit the fix for free.

- **`site-rakdos.css` handled via a same-specificity follow-up rule, not by editing the shared
  selector list:** the toggle's border/background previously came from a rule shared with
  `.tool-nav__trigger`/`.prompt-step-tab`/`.clear-cache-button`/`.swap-direction-button`.
  Removing the toggle from that list would have visually worked but dropped the literal string
  `prompt-question-bucket__toggle` from the file, failing the plan's own Task 2 automated grep
  gate (all 13 files must still match). Kept the class in the shared list and added
  `.prompt-question-bucket__toggle { border: 0; background: none; }` immediately after it,
  which wins by source order — satisfies both the gate and the "no filled pill" visual intent.

- **Environment note (not a code issue):** first build attempt failed with `MSB3027` because a
  stale `DeckFlow.Web.exe` from an earlier dev-server session held a file lock; killed the
  stale process and rebuilt clean (0/0). No plan files were affected.

- **UIAUDIT-02 requirement checkbox intentionally NOT marked complete by this plan** — same
  precedent as Phase 83's PKTSVC deferrals: all 5 of Phase 86's plans list `[UIAUDIT-02]` in
  frontmatter, but the requirement's actual text ("the enumerated gaps are fixed AND a final
  re-score confirms ≥20/24") is only satisfied once Bug D (86-04) lands and the re-score
  (86-05) confirms the threshold. Marking now would be premature.

- **Phase 86 Plan 04 (Bug D) landed 2026-07-05:** layout picker Full/Compact/Advanced now
  produces a guaranteed, measurable delta keyed to the always-rendered `.prompt-instructions`
  element (expert collapses it, focused shrinks it, guided gets a positive accent style),
  mirrored into base `site.css` + all 12 standalone forks. All four bugs (A/B/C/D) are now
  fixed; only 86-05 (visual-regression e2e + 6-pillar re-score ≥20/24) remains before
  UIAUDIT-02 can be marked complete.

### Phase 86 Plan 05 Decisions (Acceptance Gate — new e2e + 6-pillar re-score, UIAUDIT-02) — COMPLETE + human-verified

- **Two new Playwright specs close the structural test gap that let Bugs A-D ship green** (the
  prior suite asserted DOM/selectors exist, never visual STATE or interaction OUTCOME):
  `DeckFlow.Web/e2e/theme-active-affordance.spec.ts` (active-tab computed bg differs from
  inactive AND from `--panel-soft-bg` AND equals `--accent`; WCAG >=4.5:1 for the failing
  darks; 3 accent-leak checks incl `.clear-cache-button:hover`; bucket-toggle aria-label +
  zero border-width; Classic uses an EXPLICIT `site.css` cookie) and
  `DeckFlow.Web/e2e/layout-mode-interaction.spec.ts` (guided positive accent left-border,
  focused measurable shrink, expert full collapse). 26/26 + 4/4 green on both projects.

- **[Rule 1 fix ×2 in the new specs] (a)** `.prompt-instructions` locator was ambiguous — Step 2
  and Step 4 both render one; scoped to `#prompt-step-panel-2`. **(b)** the guided-mode
  assertion was viewport-dependent — `deck-sync.ts`'s `getDefaultPromptUiMode()` defaults to
  `focused` on narrow/mobile viewports (pre-existing mobile feature, unrelated to Bug D), so the
  spec now explicitly clicks the guided button before measuring. Both fixes are in the new spec
  files only — no CSS/Razor from prior plans changed.

- **6-pillar UI re-score: 18/24 -> 21/24** (clears >=20/24 with 1pt margin). Color 2->4 (Phase
  84's already-landed `--link`/`--focus`/`--cta-border`/`--danger` migration, verified live via
  `theming.spec.ts`'s permanent guards, + this phase's Bug B literal elimination; the ~60
  remaining raw `--accent-strong` refs are Phase-84-audited dispositioned-KEEP decorative uses,
  not debt); Experience Design 3->4 (Bug C aria-label + Bug D perceptible/positive modes).
  Typography/Spacing/Copywriting unchanged (no owning phase this cycle). Full evidence +
  supersession note in `tasks/UI-REVIEW.md`'s new "Phase 86 Re-Score" section; 4 headless
  screenshots (Classic+Dimir, desktop+mobile) under the phase `evidence/` dir.

- **Full-suite acceptance gate green:** build 0/0; xUnit Core 1095/1095, Web 1218/1218 (12
  PG-skip), Studio 290/290; full Playwright e2e 286 passed / 14 skipped / 0 failed; accent-leak
  grep gate clean.

- **UIAUDIT-02 marked COMPLETE after coordinator human sign-off (2026-07-05)** — the filled-pill
  after-shots (jund/dimir/gruul/azorius/planeswalker-dark) confirmed correct at both viewports.
  **UIAUDIT-03 (DirectPush Stage 4) remains PENDING** — being finished in a follow-up plan.
  **ADMIN-01 (`/Admin/Flags` on/off sorting) has been DESCOPED from Cycle 15** — not marked
  complete, will not be planned this cycle. Phase 86 stays partially open until UIAUDIT-03 lands.

## Session Continuity

Last session: 2026-07-05T22:26:28Z
Stopped at: Completed 86-05-PLAN.md — UIAUDIT-02 satisfied + human-verified (6-pillar re-score 21/24, clears >=20/24). Checkpoint approved by coordinator 2026-07-05.
Resume: Phase 86 is PARTIALLY OPEN. Next: plan the UIAUDIT-03 follow-up (DirectPush Stage 4 live verify + `DirectPush.razor:441` no-op success-copy fix). ADMIN-01 has been DESCOPED from Cycle 15 (do not plan it this cycle). Once UIAUDIT-03 lands, run `/gsd-verify-work` for Phase 86 and close the cycle.

## Deferred Items

Carried from Cycle 14 close (2026-07-03) — none are Cycle-15 gaps, all pre-existing:

| Category | Item | Status |
|----------|------|--------|
| debug | calibration-measurement | unknown |
| debug | health-band-before-baseline | unknown |
| debug | health-band-flag-on-measurement | unknown |
| debug | health-band-headline-floor-spec | unknown |
| debug | moxfield-bridge-busy-stuck | root-cause-found |
| debug | pull-from-prod-sftp-decouple | unknown |
| quick_task | manabase-load-step | missing |
| quick_task | 260624-kpg-fix-dfc-transform-cards-excluded-from-se | missing |
| quick_task | 260624-opb-be-able-to-download-the-manabase-analysi | missing |
| quick_task | 260627-flag-key-namespacing | missing |
| quick_task | 260627-p55-deckflow-studio-safety-wins-loopback-bin | missing |
| quick_task | 260627-qyc-deckflow-studio-directpush-prod-write-in | missing |

## Operator Next Steps

- Start the next milestone with /gsd-new-milestone
