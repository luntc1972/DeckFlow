---
milestone: Cycle 16
milestone_name: Content-KB Prod↔Git↔Studio Sync Hardening
audited: 2026-07-11
status: gaps_found
scores:
  requirements: 17/17 code-satisfied+verified
  phases: 6/6 verified (P91 code-passed; live operator gate outstanding)
  integration: PASS (0 blockers, 2 warnings)
  flows: E2E wired; real Render deploy round-trip is an operator live-gate (unrun)
gaps:
  operator_gate:
    - id: "SYNC-11 / SYNC-12 / SYNC-17 (Phase 91)"
      status: "code-verified; live-unverified (human_needed)"
      phase: "91-reconcile-seed-lifecycle"
      reason: >
        All 8 autonomous code plans verified against source. The phase's own manual
        operator-gate plan (91-09) has two checkpoint:human-verify tasks unrun: a live
        dry-run against a real fixture checkout, and a gated re-validated Apply with
        prod-owned safety. This IS the FU-3 pre-flip walk in 93-PREFLIP-CHECKLIST.md.
        Not a code blocker — inherently operator-owned; cannot be closed by AI.
    - id: "SYNC-16 real deploy round-trip (Phase 93)"
      status: "in-process seams wired; real git→Render→confirm round-trip unrun"
      phase: "93-round-trip-integration-test"
      reason: >
        The [PostgresFact] exercises every in-process seam against real Testcontainers
        Postgres + a real git tree (passed live 91/0 this session), faking only LLM,
        SFTP, and the deploy step. The actual git-push → Render redeploy → /app confirm
        HTTP round-trip is a LOCAL/manual Docker gate (D-07), correctly tracked in
        93-PREFLIP-CHECKLIST.md, unchecked. Operator precondition, not a silent break.
tech_debt:
  - phase: 90-directpush-correctness-seed-sync
    items:
      - "GitBodyCoverageAudit / IGitBodyCoverageAudit built as the SYNC-07 flip-precondition observability tool but NOT DI-registered, no Blazor page / CLI, and absent from 93-PREFLIP-CHECKLIST.md — 6 green unit tests, zero production consumers. Operator can't run it without ad-hoc code. Wire it (page/CLI) or reference it in the checklist, or delete it."
  - phase: cross-cutting
    items:
      - "Path-safety guard duplicated across assemblies: Web ContentKbArtifactPathResolver vs Studio ArtifactPathSafety are divergent copies of a security-critical path-traversal guard (Studio has a Windows-drive check Web lacks). Promote one guard to DeckFlow.Core, both delegate (security review). Surfaced by /simplify area 2."
      - "Seed path is one shared const in Studio (ContentKbSeedPaths.SeedRelativePath) but an inline string literal in Web ContentKbArtifactPathResolver.SeedFilePath — correct by discipline, not compile-time linkage. Promote to a Core constant."
      - "ProdContentReader.ReadAllAsync re-implements ContentSiteIndexStore.GetAllRowsAsync (copied SELECT+DTO+mapper) and has ALREADY DRIFTED (missing AwaitingConfirmUtc). Route Pull through the store, or share a Core row-DTO/mapper. Surfaced by /simplify area 3 (not yet applied)."
      - "ReconcileCoordinator.ApplyRemovalsAsync does a second full prod-table read to build an in-memory seed_managed pre-filter duplicating the store's atomic AND seed_managed=TRUE SQL gate; the shortfall-accounting branch exists only to reconcile the two. Destructive-path, behavior-affecting — deferred pending the FU-3 walk. Surfaced by /simplify area 1."
      - "CreateProdStore / TryReadFlag / 'Studio:ProdConnectionString' idiom copy-pasted across DirectPush/Reconcile/PullFromProd coordinators — no shared seam. Extract a prod-access helper."
  - phase: simplify-area-3
    items:
      - "/simplify final area (PullFromProd + ProdContentReader + seed-membership) reviewed but NOT yet applied. Clean apply set gathered: dead stagingRoot removal; ReadFlagAsync => TryReadFlagAsync ?? false; OpenProdConnectionAsync 3x dedup; double dict-lookup (PullFromProdCoordinator:128-129)."
nyquist:
  compliant_phases: []
  partial_phases: [91, 92]
  missing_phases: [88, 89, 90, 93]
  overall: "PARTIAL — VALIDATION.md present only for P91/P92. All phases have VERIFICATION.md (primary goal-backward gate); Nyquist test-coverage validation is secondary. Discovery-only; consider /gsd-validate-phase for 88/89/90/93 if formal coverage sign-off is wanted."
---

# Cycle 16 — Content-KB Prod↔Git↔Studio Sync Hardening — Milestone Audit

**Audited:** 2026-07-11 · **Branch:** plan/cycle-16-kb-sync · **Status:** gaps_found (no code blockers; operator live-gate + tech debt)

## Verdict

All 17 SYNC requirements are code-satisfied and verified; cross-phase integration passes with zero blockers. The milestone's *code* work is complete. It is classified **gaps_found** solely because two requirement groups (SYNC-11/12/17 and SYNC-16's real-deploy leg) carry an **operator live-verification gate** — the FU-3 pre-flip walk — that is inherently human and correctly tracked in `93-PREFLIP-CHECKLIST.md`, not a code defect. Both feature flags (`sync.directpush-gitbody`, `sync.reconcile`) ship **OFF** by design; the live walk gates flipping them ON, which happens after milestone completion.

## Requirements coverage (3-source cross-reference)

| REQ | Phase | REQUIREMENTS | VERIFICATION | Final |
|-----|-------|--------------|--------------|-------|
| SYNC-01/02 | 89 | ✅ Complete | passed | **satisfied** |
| SYNC-03 | 89 | ✅ Complete | passed (gap resolved — doc reword already landed) | **satisfied** |
| SYNC-04/05/06 | 88 | ✅ Complete | **passed** (verified this session; VERIFICATION.md was missing, now written) | **satisfied** |
| SYNC-07/08/09/10 | 90 | ✅ Complete | passed | **satisfied** |
| SYNC-17/11/12 | 91 | ✅ Complete | code passed; **human_needed** (91-09 live gate = FU-3) | **satisfied (code); live-verify pending** |
| SYNC-13/14/15 | 92 | ✅ Complete | passed | **satisfied** |
| SYNC-16 | 93 | ✅ Complete | passed (harness green live 91/0; real-deploy leg = operator gate) | **satisfied (harness); live round-trip pending** |

No orphaned requirements — every SYNC-ID has ≥1 verified cross-phase consumer.

## Cross-phase integration — PASS (0 blockers)

Single body-hash surface (`ComputeBodySha256`) locked by a regression guard; composite natural key everywhere (no PinId cross-match); both flags registered + default-OFF + read through coherent accessors; DI correct in both hosts with proper lifetimes; single seed exporter/loader; deploy-confirm endpoint ↔ confirmer ↔ Stage-5 wired. **2 warnings** (see tech_debt): orphaned `GitBodyCoverageAudit`; SYNC-16 real-deploy leg unrun.

## Remaining before "done"

1. **Operator: run the FU-3 live walk** (`93-PREFLIP-CHECKLIST.md`) — closes the P91 human gate + the SYNC-16 real-deploy leg. Gates flipping `sync.reconcile` / `sync.directpush-gitbody` ON. Cannot be done by AI.
2. **Optional pre-completion cleanups** (tech debt above) — none blocking: finish /simplify area-3; decide on `GitBodyCoverageAudit`; the Core path-guard consolidation (security review).
3. **Then** `/gsd-complete-milestone "Cycle 16"` (archive + CalVer tag, e.g. `2026.07.3`) → squash/ff branch → main. Flags remain OFF; flip later per the walk.
