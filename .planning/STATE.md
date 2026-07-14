---
gsd_state_version: 1.0
milestone: Cycle 17
milestone_name: Creator-Style Deck Intelligence
status: executing
stopped_at: Phase 97 executed 7/7 (verifier PASS) — awaiting operator ledger checkpoint
last_updated: "2026-07-14T22:35:00.000Z"
last_activity: 2026-07-14 -- Phase 97 executed (7 plans, Codex cross-AI, foreman verified)
progress:
  total_phases: 7
  completed_phases: 4
  total_plans: 25
  completed_plans: 25
  percent: 57
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-11)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything. Cycle 17 adds a new such workflow: a $0 paste-ready packet that critiques a submitted deck in a chosen creator's deckbuilding style.
**Current focus:** Phase 95 — measured-style-extractor

## Current Position

Phase: 95 (measured-style-extractor) — EXECUTING
Plan: 1 of 7
Status: Ready to execute
Last activity: 2026-07-12 -- Phase 97 planning complete

## Roadmap Summary

| # | Phase | Requirements | Flag | Status |
|---|-------|-------------|------|--------|
| 94 | Style-Profile Foundation | CS-01, CS-02, CS-03, CS-04 | — | ✅ Complete |
| 95 | Measured-Style Extractor | CS-04a, CS-04b, CS-04c, CS-04d, CS-05, CS-06, CS-07, CS-08, CS-09, CS-10 | — | Not started |
| 96 | Stated-Rules Distiller | CS-11, CS-11a, CS-11b, CS-11c, CS-12, CS-13, CS-14, CS-15 | — | Not started |
| 97 | Profile Fusion + Conflict Ledger | CS-16, CS-16a, CS-17, CS-18, CS-19, CS-20 | — | Not started |
| 98 | Card-Grounding Guard | CS-21, CS-22, CS-23, CS-24, CS-25 | — | Not started |
| 99 | Creator-Style Artifact Engine | CS-26, CS-27, CS-28, CS-29 | — | Not started |
| 100 | Creator-Style Tool Surface | CS-30, CS-31 | `creator.style-artifact` | Not started |

**Phase ordering rationale** (locked arc from `docs/research/creator-style-roadmap.md`, Codex gpt-5.4-high plan-review adjustments folded in):

- **94 first**: pure substrate (schema + store) — every other phase reads/writes through it.
- **95 and 96 in parallel (Wave 1)**: both consume only Phase 94's schema and are independent of each other — the measured extractor (Archidekt crawl + stats) and the stated distiller (transcripts + rules) don't share code.
- **97 and 98 in parallel (Wave 2)**: Fusion needs both 95 and 96's outputs; the Card-Grounding Guard is independent (needs only the existing Scryfall client) so it can run concurrently with Fusion rather than blocking on it.
- **99**: Codex HIGH — the prompt-artifact deliverable is mandated as ≥2 phases. 99 is the "engine" half (deterministic rubric + artifact assembly), needs Fusion (97, for targets) and the Guard (98, for card safety) — nothing in 99 can start before both land.
- **100 last**: the "tool surface" half of the mandated split — page/controller/flag/cache-wiring on top of 99's engine. Only phase that ships user-visible value; flag `creator.style-artifact` seeded OFF.
- **Core-vs-Web layering (Codex HIGH, resolved without a dedicated phase)**: Phase 95 lives in `DeckFlow.Web` (needs `CommanderSpellbookService` + Scryfall Tagger) behind a narrow contract, keeping its pure logic Core-testable; Phases 94/96/97/98 stay Core-centric.

## Performance Metrics

**Velocity (Cycle 16 reference — most recently shipped):**

- Phases 88-93; ~30 plans across 6 phases; build 0/0 at close; squash `94427ada` + tag `2026.07.3`, live via manual deploy `01487890` then autodeploy `a18ac836`
- Claude implements + reviews code; Codex (gpt-5.4 medium) reviews plans + code (delegation rule per CLAUDE.md)

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 89 P01 | 25m | 2 tasks | 3 files |
| Phase 89 P02 | 35m | 2 tasks | 4 files |
| Phase 89 P03 | 15m | 2 tasks | 3 files |
| Phase 89 P04 | 20min | 2 tasks | 5 files |
| Phase 89 P05 | ~50min | 2 tasks | 6 files |
| Phase 89 P06 | ~45min | 3 tasks | 9 files |
| Phase 90 P01 | ~25min | 3 tasks | 9 files |
| Phase 90 P02 | 25min | 2 tasks | 6 files |
| Phase 90 P03 | ~20min | 2 tasks | 5 files |
| Phase 90 P04 | ~25min | 2 tasks | 8 files |
| Phase 90 P07 | 40min | 2 tasks | 4 files |
| Phase 90 P05 | ~50min | 3 tasks | 14 files |
| Phase 90 P06 | ~30min | 2 tasks | 6 files |
| Phase 91 P01 | ~15min | 3 tasks | 7 files |
| Phase 91 P02 | ~35min | 2 tasks | 10 files |
| Phase 91 P03 | ~40min | 2 tasks | 6 files |
| Phase 91 P04 | ~25min | 2 tasks | 3 files |
| Phase 91 P05 | ~12min | 1 tasks | 4 files |
| Phase 91 P06 | ~50min | 2 tasks | 4 files |
| Phase 91 P07 | 45min | 2 tasks | 10 files |
| Phase 91 P08 | ~35min | 3 tasks | 10 files |
| Phase 93 P01 | ~25min | 3 tasks | 4 files |
| Phase 93 P03 | ~10min | 1 tasks | 1 files |
| Phase 93 P02 | ~55min | 2 tasks | 1 files |

Cycle 17 has no plans executed yet — this table will accumulate per-plan entries as Phase 94+ execution begins.

## Accumulated Context

### Decisions

Full decision log lives in PROJECT.md Key Decisions table. Decisions constraining Cycle 17:

- **Phase count = 7 (94-100), not the config's coarse 3-5 guidance** — overridden by two Codex HIGH findings: the prompt-artifact deliverable must split into ≥2 phases, and the wave-dependency chain itself yields 6 natural boundaries before that split. See ROADMAP.md "Granularity note".
- **Core-vs-Web layering resolved via framing, not a new phase** — Phase 95 (Measured-Style Extractor) is explicitly a Web-hosted phase behind a narrow Core contract, rather than inserting a dedicated host-abstraction phase. Chosen to keep the milestone at 7 phases rather than 8.
- **Archidekt-only MVP crawler** (Codex HIGH) — Moxfield blocks datacenter IPs and its importer fallback drops printings/tags/sideboards, wrong for a style corpus. Moxfield crawler deferred to a separate hardening phase.
- **Creator→profile mapping is manual, one new table, NOT `CreatorSourceStore`** (Codex MED — wrong shape for multi-platform profile identity).
- **Card grounding is woven in twice**: a minimal Scryfall pass inside the Stated-Rules Distiller (Phase 96, Codex MED — ground earlier than the artifact tool) plus the full reusable guard (Phase 98) consumed by the Artifact Engine (Phase 99).
- **Still open at plan time** (per REQUIREMENTS.md/PROJECT.md): starter creator + Archidekt profile URL (Salubrious Snail — 39 decks across 5 personal folders, no patron decks); confirm Archidekt exposes a public profile→deck-list endpoint (else manual URL-list fallback, Phase 95).
- [Cycle 16 context, retained] Full prior-milestone decision entries (Phase 89-93 implementation notes) remain below for reference; they do not constrain Cycle 17 directly but document the KB-sync substrate Cycle 17 builds on top of.
- [Phase 89]: 89-02: SetBodySha256IfNullAsync declared as a throwing default interface method (mirrors DeleteAllRowsAsync) so 12 unrelated IContentSiteIndexStore test doubles compile unchanged
- [Phase 89]: 89-03: Fingerprint deleted; classifier equal-timestamp branch now calls ContentSiteIndexContentSignature.AreContentEqual (SYNC-02/D-03), UTC-direction branches (F-51-PG-01) untouched
- [Phase 89]: 89-04: bodySha256 added to the single shared export factory ContentIndexExportRow.From() (not to CLI/DirectPush consumers) so both inherit it automatically — SYNC-02 one-signature-one-home invariant extended to seed export (D-09)
- [Phase 89]: 89-05: publish-compute and detail render-guard both call ContentSiteIndexContentSignature.ComputeBodySha256, the ONE shared hash helper (D-01); guard is fail-open + structured-log on mismatch OR null/legacy stored hash, detail-render only, no feature flag (D-05/D-06/D-07)
- [Phase 89]: 89-06: ContentBodyHashBackfill is host-agnostic (DeckFlow.Core) with an IContentArtifactBodyResolver seam, wired at startup on BOTH web (after schema-ensure + seed load) and Studio (bound only to the local content-kb.db store, never a ProdStoreFactory prod store) — D-08 dual-host backfill
- [Phase 89]: 89-06: Studio backfill runs at STARTUP (not piggybacked on publish/upsert) — symmetric with web, explicit, unit-testable; new distills already hash forward via 89-05
- [Phase 90]: 90-01: Program.cs needed no change for the new IFeatureFlagCache dependency on ContentKbArtifactPathResolver/ContentKbController - both are registered via plain AddSingleton<T>()/implicit MVC DI with no factory lambda, so the container auto-resolves the new constructor param from AddDeckFlowFeatureFlags()
- [Phase 90]: 90-02: ArtifactPathSafety root param = repoRoot (not repoRoot/content-kb) — ArtifactPath already carries the content-kb/ prefix, matching PullFromProdCoordinator's proven call shape
- [Phase 90]: 90-02: GitBodyCoverageAudit depends only on IProdContentReader (no IProdStoreFactory reference) so it is structurally incapable of writing to prod (D-11/T-90-04)
- [Phase 90]: 90-03: awaiting_confirm_utc chosen as nullable timestamp (not status-string), mirroring body_sha256/pushed_to_prod_utc precedent; excluded from all Upsert* SQL so a re-distill can never clear an in-flight marker
- [Phase 90]: 90-03: SetAwaitingConfirmAsync/ClearAwaitingConfirmAsync declared as throwing default interface methods (mirrors SetBodySha256IfNullAsync); confirmed via full solution build that existing FakeContentSiteIndexStore doubles compile unchanged
- [Phase 90]: 90-04: IProdContentReader.ReadFlagAsync fails CLOSED inside its own try/catch (never propagates a connection/query failure) - inverse of the web-side IFeatureFlagCache D-13 default-on
- [Phase 90]: 90-04: DurabilityCommitSubjectPattern's trailing [skip render] made optional - a correctness fix: without it a flag-ON commit would misclassify itself as foreign on the next ahead-of-origin check and permanently block the push
- [Phase 90]: 90-04: seed re-export runs on EVERY CommitAndPushBodiesAsync call (not gated on changedCount) so the seed always reflects the current approved set; the commit-gate and N body|bodies message wording stay BODY-ONLY
- [Phase 90]: D-09 REVISED deploy-confirm: authenticated Admin/api/contentkb/deployed-body-hash endpoint by natural key — Public detail-page 200 confirm was unsound (Codex plan-review BLOCK); natural-key + git-only + is_visible-independent hash endpoint defeats all 4 races
- [Phase 90]: 90-05: WriteContentAsync/ConfirmAndPublishAsync split preserves prod-first-then-local stamp/visibility order + ContentIndexExportRow.From key derivation across both methods (Pitfall 5)
- [Phase 90]: 90-05: DeployedBodyConfirmer is bounded (5 attempts, 3s backoff) and reads config per-call so the IsConfirmerConfigured badge/gate stays accurate without a Studio restart
- [Phase 90]: 90-05: VerifyAndPublishAsync exists and is unit-tested but is NOT wired to a DirectPush.razor UI stage yet — deferred to Plan 90-06 per this plan's files_modified scope
- [Phase 90]: 90-06: DirectPush Stage 5 (Verify Deploy & Publish) gates on _gitSuccess broadly (any of Committed/PushedExistingCommits/AlreadyInSync) - the confirm poll is the real safety net, not the git outcome variant
- [Phase 90]: 90-06: GetAwaitingConfirmRowsAsync filters in memory (never a WHERE on awaiting_confirm_utc) per Pitfall 3; added to DirectPushCoordinator despite absence from plan files_modified since the H1 split gives the page no direct store access
- [Phase 90]: 90-06: resume-bucket card stays rendered while a resume result is pending display even after the bucket empties - a bucket-gated-only visibility condition hid a fully-successful resume's own confirmation from the operator
- [Phase 91]: 91-01: seed_managed follows the awaiting_confirm_utc dialect-guarded-both-branches DDL shape (BOOLEAN NULL / INTEGER NULL, never non-nullable-with-DEFAULT) so NULL (unclassified) stays distinct from false (classified prod-owned)
- [Phase 91]: 91-01: UpsertPreservingVisibilitySql always overwrites seed_managed from EXCLUDED (unlike is_visible/is_hidden/is_evergreen which are preserved) — every row reaching this path via the seed-load call site is definitionally seed-managed
- [Phase 91]: 91-01: SeedIndexFileReader.Read is the ONLY public read API (no bare-set overload) so SeedAvailable can never be bypassed by a downstream consumer; Tasks 1+2 committed together (tightly coupled column+setter) per config.json coarse granularity
- [Phase 91]: 91-02: SeedManaged hardcoded true at all three write/export call sites (ContentKbSeedLoader.BuildRow, DirectPushCoordinator.WriteContentAsync, ContentIndexExportRow.From) rather than read from the incoming row/entry (Pitfall 4) — presence in the seed file/publish batch/export set is itself the proof of seed-managed membership
- [Phase 91]: 91-02: ContentIndexExportRow.SeedManaged changes the seed JSON byte-shape, so the CLI golden fixture (ContentIndexExportJsonGoldenTests + index-seed.golden.json) was updated in the same commit as an in-scope consequence of Task 1
- [Phase 91]: 91-02: ProdContentReader's new round-trip test uses the existing [PostgresFact] env-var-gated convention rather than adding a Testcontainers dependency to DeckFlow.Studio.Tests
- [Phase 91]: 91-03: SeedManagedBackfill.RunAsync short-circuits BEFORE calling GetAllRowsAsync when SeedAvailable==false - zero store reads/writes on an unavailable seed
- [Phase 91]: 91-03: a throwing ISeedKeyMembershipSource is caught in RunAsync and treated identically to an unavailable seed - one gate, not two divergent safety mechanisms
- [Phase 91]: 91-03: StudioSeedKeyMembershipSource resolves repoRoot via IGitRepository.ResolveRepoRootAsync(...).GetAwaiter().GetResult() inside a synchronous GetSeedMembership() - safe (no SynchronizationContext); resolution failure is treated as unavailable seed
- [Phase 91]: 91-04: Tasks 1+2 committed together (record has no independent meaning without the classifier that emits it) per 91-01 grouping precedent
- [Phase 91]: 91-04: file-orphan identity is ARTIFACT PATH ONLY - ContentNaturalKey.TryDerive never invoked file->row (no trusted metadata to infer from)
- [Phase 91]: 91-04: seed-drift gated on SeedIndexReadResult.SeedAvailable (checked once for the skip log, again per-row) - unavailable seed emits zero seed-drift, other three classes unaffected
- [Phase 91]: 91-04: IsPublishedOrphan mirrors GitBodyCoverageAudit's gate (approved && IsVisible, no IsHidden check) per the plan's read_first pointer, not ContentKbOrphanScanner's slightly different gate
- [Phase 91]: 91-05: PersistRunAsync wraps the upsert-seen + resolve-absent pass in a single DB transaction (BeginTransactionAsync/CommitAsync/RollbackAsync) for atomicity across the two statements
- [Phase 91]: 91-05: empty-seen resolution uses a dedicated no-NOT-IN query (ResolveAllInScopeSql) rather than relying on dialect-specific empty-IN-list expansion behavior
- [Phase 91]: 91-05: Kind<->text mapping (ToKindText/ParseKind) is duplicated locally in ContentKbReconcileStore since ContentKbReconcileDiscrepancy.KindToken is private - vocabulary is pinned by ContentKbReconcileKind's own XML doc comment as the single source of truth by contract
- [Phase 91]: 91-06: IConfiguration added as a 5th constructor dep beyond the plan's stated list since RunDryRunAsync(scopeTag, ct) carries no connection-string param - mirrors PullFromProdCoordinator's ephemeral-read pattern
- [Phase 91]: 91-06: D-06 report path content-kb/reconcile-report.md excluded by name from its own file-orphan *.md enumeration to prevent a self-referential flagging loop on re-run
- [Phase 91]: 91-06: Tasks 1+2 committed as two separate atomic commits (unlike 91-04/91-05 precedent) - the orchestrator's dry-run core is independently meaningful and fully tested without the report writer
- [Phase 91]: 91-07: ReconcileCoordinator omits IConfiguration — RunDryRunAsync delegates entirely to the orchestrator, which already owns the ephemeral prod connection-string read (91-06). — An unused config field would trip CS0414 and violate the 0-new-warnings gate.
- [Phase 91]: 91-07: Reconcile.razor discrepancy lists render via a shared manual RenderTreeBuilder RenderFragment with literal per-iteration sequence numbers + SetKey(item.Id). — Fixes ASP0006 (ever-incrementing seq++) while avoiding four copies of near-identical markup for the four discrepancy classes.
- [Phase 91]: 91-08: ReconcileCoordinator gained IProdStoreFactory/IProdContentReader/IConfiguration ctor deps for ApplyRemovalsAsync's flag read + prod write - DI needed no registration change (all 3 already-registered singletons)
- [Phase 91]: 91-08: ApplyRemovalsAsync re-reads prod fresh to re-check seed_managed=true per matched key rather than trusting discrepancy Kind==SeedDrift alone - defense-in-depth beyond the classifier's own gate (T-91-20)
- [Phase 91]: 91-08: stale-check + reviewed set scoped to ContentKbReconcileKind.SeedDrift only so a mixed-class dry-run never false-rejects the removal Apply as stale
- [Phase 93]: 93-01: DeployToAppAsync is a plain recursive filesystem copy of content-kb/**, not a second git invocation - keeps the test-only git bootstrap helper the sole hand-rolled ProcessStartInfo carve-out
- [Phase 93]: 93-01: AppTreeDeployedBodyConfirmer mirrors ArtifactPathSafety inline rather than adding InternalsVisibleTo to DeckFlow.Studio - preserves zero-production-code-change this phase
- [Phase 93]: 93-01: Smoke test wires the real ContentKbOrchestrator via the existing public ContentKbOrchestratorFactory.Create rather than 14 raw ctor args - matches the CLI's own construction path
- [Phase 93]: 93-03: checklist created as its own standalone file (93-PREFLIP-CHECKLIST.md) rather than appended to 90-FOLLOWUPS.md or 93-CONTEXT.md, per D-08 planner's-call
- [Phase 93]: 93-02: ContentKbOrchestratorFactory.Create's artifactRoot must already carry the content-kb/ segment (Path.Combine(dataRoot, "content-kb")) to match ContentArtifactWriter's on-disk layout - mirrors Studio's own Program.cs convention
- [Phase 93]: 93-02: explicit IGitRepository.PushAsync inserted after PublishCoordinator.CommitAsync (simulating operator manual push, since Publish itself never pushes per D-01) - otherwise DirectPush's foreign-commit guard refuses
- [Phase 93]: 93-02: forced the Pull-classify diff via an IndexedUtc bump (not approval_status), since approval_status is excluded from both the timestamp compare and the content signature

### Pending Todos

None yet — Cycle 17 roadmap just created; phase planning has not started.

### Blockers/Concerns

- **Starter creator + Archidekt profile URL not yet confirmed** — Salubrious Snail has the most artifacts (39 decks across 5 personal folders, no patron decks); needs confirmation at Phase 95 plan/discuss time.
- **Archidekt public profile→deck-list endpoint not yet verified** — Phase 95 must confirm this exists before committing to the crawler design; if absent, fall back to a manual per-creator URL list (already scoped as an acceptable fallback in CS-04a).
- Cycle 16 is shipped; its prior blockers (prod drift, `sync.*` flag plumbing) are resolved and no longer active concerns for Cycle 17.

## Deferred Items

Carried forward from prior cycles, plus Cycle-17-specific fast-follow deferrals:

| Category | Item | Status |
|----------|------|--------|
| Cycle 17 fast-follow | Tier-2 in-app LLM critique (CS-32..36) | Deferred — needs new-package approval (pgvector) + net-new web-side spend/vector infra |
| Cycle 17 fast-follow | Moxfield crawler (corpus source) | Deferred — separate hardening phase |
| Cycle 17 fast-follow | Multi-creator auto-resolution | Deferred — creator→profile stays manual per creator |
| Carry-forward | `deckflow_admin` credential deletion (password rotated) | Operator task |
| Carry-forward | Full dual-dialect branch collapse (PG DDL parity prereq) | Backlog |
| Carry-forward | SEO/growth lane (SEO-01..05) | Deferred |
| Carry-forward | Scheduled/bulk harvest (AUTO-03/04) | Deferred |
| Carry-forward | Matchup / meta-threat read (deepens cedh-meta-gap) | Deferred (separate lane) |
| Carry-forward | Manabase engine refactor (needs numeric-parity harness first) | Deferred (own future cycle) |
| Carry-forward | ADMIN-01 (`/Admin/Flags` on/off sorting) | Descoped to backlog |
| Carry-forward | KB "commander advice" content class for filtered videos | Deferred (backlog) |
| Sync follow-ons | SYNC-F1 (retire DirectPush entirely) | Deferred — later-cycle decision |
| Sync follow-ons | SYNC-F2 (scheduled/automatic reconcile runs) | Deferred — Cycle 16 shipped operator-triggered only |

## Session Continuity

Last session: 2026-07-14T22:35:00.000Z
Stopped at: Phase 97 executed 7/7; blind verifier PASS; 2 foreman review fixes (href scheme allowlist, bootstrap 5.1 badges); operator checkpoint (97-07 ledger smoke "approved") pending
Resume file: .planning/phases/97-profile-fusion-conflict-ledger/97-CONTEXT.md
