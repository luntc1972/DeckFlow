# Phase 92: Pull Hardening - Context

**Gathered:** 2026-07-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Harden the **Pull-from-Prod** workflow (`DeckFlow.Studio` `PullFromProdCoordinator` + `PullFromProd` page) so that adopting production's state:

1. Sources each field from its correct master — body+content from the git tree, DB-only operator fields (`is_visible`/`is_hidden`/`approval_status`) from prod — without either side clobbering the other's authoritative data (SYNC-13 / M7).
2. Refuses or warns before reading a **stale local checkout**, rather than silently classifying against an out-of-date git tree (SYNC-14). Pull NEVER SFTP-downloads prod bodies (already true by design — prod `/data` is empty per `0dd49f19`; the Codex "download bodies from prod" rebuild was rejected in cycle planning).
3. Surfaces any **body-vs-index divergence** (a git body whose hash disagrees with prod's `body_sha256`) to the operator for a decision instead of silently adopting it (SYNC-15).

Pull writes to the **LOCAL Studio store only** and never mutates production. This phase adds guards and a divergence-detection path on top of the existing, already-extracted coordinator — it does not re-architect Pull.

**In scope:** field-authority split ratification, a stale-checkout guard, body-hash-vs-`body_sha256` divergence detection + operator-gated adoption.
**Out of scope:** any prod write, SFTP/remote body fetch, re-architecture of the coordinator, changes to DirectPush (push direction) or the P91 reconcile Apply.

</domain>

<decisions>
## Implementation Decisions

### Body-vs-index divergence (SYNC-15)
- **D-01:** When a git body's computed hash != prod's `body_sha256` for an entry being adopted, that entry is **blocked from silent adoption**. It is classified as a **distinct divergence class** in the Pull diff and **excluded from the default adopt set**; the operator must **explicitly opt-in per entry** to adopt a divergent row. This honors SYNC-15 ("surfaced to the operator instead of silently adopted"). Reuse of the P91 reconcile discrepancy vocabulary was considered and rejected — keep the operator flow on the single Pull page rather than splitting it across the reconcile page.
- **D-01a:** Divergence detection uses the shared `ContentSiteIndexContentSignature.ComputeBodySha256` helper (the same UTF-8 + LF-normalized, body-only hash shipped in P89) against the resolved git-tree body, compared to the prod row's `body_sha256`. When prod's `body_sha256` is null/absent (legacy unbackfilled row), treat as **indeterminate → surface, do not auto-adopt** (fail-safe, mirrors P90/P91 fail-safe posture).

### Field authority (SYNC-13)
- **D-02:** **Ratify the current adopt field split.** Body FILE ← git tree (copied into the live tree, local only). Content-index columns (title/tags/artifact path/`body_sha256`) ← the prod row via `UpsertContentColumnsOnlyAsync`. `approval_status` ← prod-mirror via `SetApprovalStatusAsync` (Pull is the *adopt-prod* direction, so taking prod's operator decision is correct — this does **not** conflict with P90 D-03's "approval is LOCAL-authoritative + mirror," which governs the **push** direction). `is_visible` / `is_hidden` are **ALWAYS preserved-local** — the content-only upsert never touches them, so adopting never auto-publishes or auto-hides.
- **D-02a:** The D-01 divergence guard is what keeps D-02 coherent: prod's `body_sha256` is only adopted into the local index when it **matches** the git body being copied, so the local index row and local body file never fall out of sync as a result of Pull. "content ← git tree" (SYNC-13) resolves to: the **body file** is the git tree's; the index columns that *describe* it are prod's, and are only adopted when the two agree.

### Flag gating
- **D-03:** **Always-on, no `sync.*` flag.** Pull writes LOCAL-only and never has a destructive-prod blast radius (unlike P90 DirectPush and P91 reconcile Apply, which are flag-gated because they mutate prod). The staleness guard and divergence surfacing are strictly *protective*, so shipping them always-on is strictly safer than gating them. No `FeatureFlagCatalog` entry, no Studio flag read for this phase.

### Claude's Discretion
- **SYNC-14 staleness guard — mechanism and warn-vs-refuse left to the planner/research.** The gray area was intentionally not deep-dived. Guidance for the planner: `IGitRepository` today has `GetCurrentBranchAsync`, `CountWorkingChangesAsync` (dirty-tree), and `GetSubjectsAheadOfRemoteAsync` (local-ahead) but **no fetch and no behind-detection** — a new capability is needed to know the checkout is behind its remote. Preferred direction (not locked): add a behind-detection git seam (e.g., `git fetch` + behind-count, a network op) and **WARN + let the operator proceed** rather than hard-refuse, consistent with this phase's "surface to the operator" theme; a hard-refuse option is acceptable if research shows the fetch is reliable and cheap in the Studio host. Keep the guard's git work behind the existing `IGitRepository` abstraction (testable seam) and honor `ArtifactPathSafety` for any path use. Must never SFTP or touch prod.
- Exact UI treatment of the divergence class (badge, section, per-entry opt-in control) and progress-log copy — follow the existing `PullFromProd` page conventions and the P91 reconcile page's class-grouping precedent.
- Test seams and doubles follow the established `Fake*`/`IProdContentReader`/`FakeGitRepository` patterns from P90/P91.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` §SYNC-13/14/15 — the three locked requirements for this phase
- `.planning/ROADMAP.md` §"Phase 92: Pull Hardening" — goal + 3 success criteria
- `.planning/STATE.md` — cycle status, prior-phase decisions

### Pull-from-Prod code (the thing being hardened)
- `DeckFlow.Studio/ViewModels/PullFromProdCoordinator.cs` — `PullAndClassifyAsync` (read prod → resolve git bodies → classify) + `ApplyAdoptionsAsync` (local content upsert + approval mirror + body copy). The primary edit target.
- `DeckFlow.Studio/Pages/PullFromProd.razor.cs` — page code-behind: progress log, resolution map, adopt pre-filter, busy/cancel state.
- `DeckFlow.Studio.Tests/ViewModels/PullFromProdCoordinatorTests.cs`, `DeckFlow.Studio.Tests/PullFromProdPageTests.cs` — existing coverage to extend.

### Shared building blocks (reuse, do not re-hand-roll)
- `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` — `ComputeBodySha256` (P89 body-hash helper) — the divergence detector's hash source.
- `DeckFlow.Core/Integration/IGitRepository.cs` — git seam; needs a new behind-detection member for SYNC-14.
- `DeckFlow.Studio/Services/ArtifactPathSafety.cs` — contained-path guard used on every artifact path.
- `DeckFlow.Core/Orchestration/ContentSyncDiffClassifier.cs` — the classifier that produces `SyncDiffEntry`; divergence class likely extends `SyncDiffKind`.
- `DeckFlow.Core/Content/IProdContentReader.cs` — read-only prod reader (already round-trips `body_sha256` + `seed_managed` after P90/P91).

### Cycle design docs
- `docs/research/kb-prod-sync-roadmap.md`, `docs/research/kb-prod-sync-fix-design.md` — M7 (Pull clobber/stale) origin; the git-SoT stance and the rejected SFTP rebuild.
- `.planning/phases/90-directpush-correctness-seed-sync/90-FOLLOWUPS.md` — P90 fail-safe posture precedent (tri-state flag reads, fail-to-verify).
- `.planning/phases/91-reconcile-seed-lifecycle/` — P91 reconcile classifier/orchestrator/page as the closest analog for a new diff class + operator-gated action.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PullFromProdCoordinator` — already extracted from the page (H1 god-component split), holds the read-only prod pull + local adopt. New guards slot into `PullAndClassifyAsync` (divergence stamp, staleness pre-check) and `ApplyAdoptionsAsync` (skip divergent-not-acked).
- `ComputeBodySha256` (P89) — single source of body-hash truth; divergence detection must use it, not a second hand-rolled hash (repeat of the P89 "one signature surface" rule).
- `SyncDiffEntry` already carries `ArtifactDownloaded` (per-entry git-body-present flag); a parallel divergence flag/kind fits the same stamping pattern (line ~119-121 of the coordinator).
- `ArtifactPathSafety.TryBuildContainedPath` — already used on every body path; reuse for any new path work.

### Established Patterns
- Read prod via `IProdContentReader.ReadAllAsync(connStr)` — ephemeral conn string, never materialized into DI (D-03/D-07 of prior phases). NO DDL on prod.
- `is_visible`/`is_hidden` are never written by adopt (content-only upsert) — the phase must preserve this invariant.
- Fail-safe on indeterminate signal (null hash, unreadable) → surface, never silently proceed — the P90/P91 house style.
- git work behind `IGitRepository` so it is unit-testable with `FakeGitRepository`.

### Integration Points
- `IGitRepository` gains a behind-detection member (SYNC-14) — new interface member ⇒ use the **throwing default-interface-method** escape hatch so the ~N existing doubles don't hit CS0535 (the P89/P90/P91 pattern).
- Divergence class likely a new `SyncDiffKind` value + classifier/coordinator plumbing + page rendering + adopt pre-filter exclusion.

</code_context>

<specifics>
## Specific Ideas

- Divergence = a *distinct diff class*, excluded from the default adopt set, per-entry explicit operator opt-in (D-01). Keep it on the single Pull page, not the reconcile page.
- Null prod `body_sha256` ⇒ indeterminate ⇒ surface, do not auto-adopt (D-01a).
- Approval mirrors prod on pull (adopt direction) — explicitly NOT a conflict with the push-direction P90 D-03 local-authority rule (D-02).

</specifics>

<deferred>
## Deferred Ideas

- Auto-`git pull` / auto-remediation of a stale checkout — SYNC-14 lands as a *guard* (warn/refuse), not an auto-fixer; automatic pulling is out of scope for this phase.
- Any prod-side write or SFTP body fetch — permanently rejected for Pull by the cycle's git-SoT stance.
- Merging the Pull divergence view into the P91 reconcile page — considered and rejected (D-01); could be revisited if a unified "sync health" operator surface is ever scoped.

None outstanding beyond the above — discussion stayed within phase scope.

</deferred>

---

*Phase: 92-pull-hardening*
*Context gathered: 2026-07-10*
