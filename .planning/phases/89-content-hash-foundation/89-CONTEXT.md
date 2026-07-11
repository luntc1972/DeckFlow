# Phase 89: Content-Hash Foundation - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Give every `content_site_index` row a body-inclusive content hash so drift is a single indexed comparison and body corruption (e.g. the CP437 mojibake class) is detectable instead of silently served. Three locked outcomes (SYNC-01/02/03):

1. `content_site_index` gains a `body_sha256` column across SQLite + Postgres + seed JSON, computed from the `.md` body at publish time.
2. The two divergent row-signature schemes — `ContentSiteIndexContentSignature` (full column set) and `ContentSyncDiffClassifier.Fingerprint` (subset) — collapse into ONE body-inclusive unified signature shared by DirectPush, Pull, and reconcile.
3. The web app refuses-or-flags rendering of a row whose on-disk body hash ≠ stored `body_sha256`, logging the mismatch (this phase: log-only / fail-open — see D-05).

Not in this phase: DirectPush git-body serving flip + seed re-export (P90), reconciler + seed lifecycle + seed-ownership marker (P91), Pull hardening (P92), the end-to-end containerized integration test (P93/SYNC-16). Phase 89 lays the hash + unified signature those phases consume.

</domain>

<decisions>
## Implementation Decisions

### Hash input & algorithm (SYNC-01)
- **D-01:** **Hash the body only, post-`SplitHeader`** — the bytes after the YAML frontmatter, exactly what `ContentArtifactParser.SplitHeader` already returns at render (`ContentKbController.cs:119`). The header fields (title/tags/dates/url) already live in the column signature, so hashing them again is redundant drift signal; the body hash exists to catch mojibake/stale prose. The publish-compute side and the render-guard side MUST call the identical split so the two hashes are comparable.
- **D-02:** **Normalize before SHA-256:** decode as UTF-8, normalize line endings to LF, then hash the resulting bytes. Rationale: `.gitattributes` enforces LF but the git-tree `/app`, the SFTP `/data` overlay, and OS write paths can differ on EOL/encoding; without normalization the render guard would trip on byte-identical content. Directly hardens against the CP437 mojibake class that motivated this phase. Algorithm is SHA-256 (column name is contractual).

### Unified signature shape (SYNC-02)
- **D-03:** **Extend the full column signature and retire the subset.** The one canonical signature = the existing `ContentSiteIndexContentSignature` column set (source, title, video_url, artifact_path, published_utc, indexed_utc, archetype/bracket/card-category tags) **plus an appended `body_sha256` field**. `ContentSyncDiffClassifier.Fingerprint` (the divergent subset of title/artifact_path/tags) is deleted; the classifier switches to the unified signature. One signature, one home, no second scheme to drift.
- **D-04:** **Keep the `indexed_utc` timestamp-direction logic** in the classifier (ProdNewer / Diverged / equal-timestamp-different-content). The body hash becomes the tie-breaker on the equal-timestamp branch (replacing the old `Fingerprint` compare) and a component of the equal-content check — it does not remove the existing UTC-compared direction decision that guards the F-51-PG-01 timestamptz class of bug.

### Render-guard behavior (SYNC-03)
- **D-05:** **Fail-open + log on BOTH mismatch and missing-hash, this phase.** On a hash mismatch, or on a legacy row whose `body_sha256` is null/absent, the web app still serves the body but emits a structured warning (and ideally a counter/metric) naming the row. Zero risk of live content vanishing during rollout; corruption becomes visible in logs. A future phase may tighten to fail-closed once backfill (D-06) guarantees every live row is hashed.
- **D-06 (guard, unflagged):** **No feature flag for Phase 89.** Roadmap assigns 89 none; because the guard is log-only/fail-open it changes no user-visible behavior, so there is no legitimate "off" state to gate (matches P88 D-13 stance). The riskier fail-closed tightening, if/when it lands, is where a flag belongs — not here.
- **D-07 (surface scope):** The guard applies where the body is actually read and served — the Content-KB **detail render** (`ContentKbController` body path). List/browse pages don't read the `.md` body, so the guard is a no-op there; do not bolt a redundant check onto them.

### Backfill / rollout (SYNC-01)
- **D-08:** **One-time deterministic backfill, not lazy.** Compute `body_sha256` for every existing row's `.md` in a single pass: the **web app startup/seed path** for prod, and the **Studio/publish path** for local. Every row is hashed up front, which is what makes a future fail-closed guard safe (no un-hashed row gets orphaned). Rejected: lazy compute-on-next-publish, which would leave the guard permanently fail-open and drift-detection blind on old rows.

### Prod DDL rollout path (SYNC-01, carried constraint)
- **D-09:** **The `body_sha256` ALTER runs ONLY via the web app's startup/seed `EnsureSchema`** (dialect-guarded, idempotent, SQLite + Postgres). Studio prod stores stay schema-ensure OFF (P88 D-10) — Studio never issues DDL against prod. The prod DB schema owner is the web app; the seed JSON (`index-seed.json`) gains the `body_sha256` field so a reseed reconstructs it.

### Claude's Discretion
- Exact home/name of the shared body-hash helper (new static class in `DeckFlow.Core/Content` vs a method on the existing signature class). Prefer folding into `ContentSiteIndexContentSignature` so there is one signature surface.
- Whether the render-guard emits a metric/counter in addition to the structured log — encouraged for observability but not required to satisfy SYNC-03.
- Backfill mechanics (idempotent UPDATE-where-null pass vs recompute-all) — pick the smaller-surface option that stays safe on re-run.
- Whether the local (Studio) backfill piggybacks an existing publish/upsert path or is a discrete one-shot command.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Cycle 16 design + audit (source of the SYNC requirements)
- `docs/research/kb-prod-sync-fix-design.md` §34 — the body-hash column + unified-signature fix (the "add `body_sha256`, unify DirectPush+Pull+reconcile, refuse-to-render" design that this phase implements); §48/§57 note it is the foundational item everything else uses.
- `docs/research/kb-prod-sync-roadmap.md` §27–30 — SYNC-01/02/03 exact goal text (body-inclusive hash, one signature, refuse-to-render + log).
- `.planning/REQUIREMENTS.md` — SYNC-01/02/03 exact text + out-of-scope table (public-surface changes limited to the SYNC-03 render guard + the SYNC-04 visibility fix).
- `.planning/ROADMAP.md` — Phase 89 goal + 3 success criteria; dependency chain 88 → 89 → 90 (P90 needs this body hash to hash-gate ordering).

### Prior-phase decisions this phase inherits
- `.planning/phases/88-index-row-integrity-hotfix/88-CONTEXT.md` — D-09/D-10 (schema-ensure OFF switch + all-prod-stores-always), D-05/06/07 (shared natural-key helper + stored vocabulary `youtube_channel`/`podcast_rss`), D-04 (serve-side approval filter already in the public KB query). D-09 here builds directly on P88 D-10.

### Primary code under change
- `DeckFlow.Core/Content/ContentSiteIndexContentSignature.cs` — the full column signature to extend with `body_sha256` (D-03).
- `DeckFlow.Core/Content/ContentSyncDiffClassifier.cs` — `Fingerprint` (lines ~136-143) to delete; `Classify` equal-timestamp branch (~63) to switch onto the unified signature (D-03/D-04).
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `content_site_index` schema/DDL + `ContentSiteIndexRow` model + upsert column list; add `body_sha256` (D-09).
- `DeckFlow.Studio/ViewModels/DirectPushCoordinator.cs` + `PullFromProdCoordinator.cs` — consumers of both old schemes; move onto the unified signature.
- `DeckFlow.Web/Controllers/ContentKbController.cs` (~118-120, `ReadAllText` → `SplitHeader` → `Markdown.ToHtml`) — insert the render guard here (D-05/D-07).
- `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs` / `ContentArtifactSpec.cs` — publish-time body write; publish-compute of `body_sha256` (D-01) plugs in here.
- Seed JSON (`index-seed.json`) export/load path — add the `body_sha256` field (D-09).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ContentArtifactParser.SplitHeader` — already used at render (`ContentKbController.cs:119`) to strip frontmatter; reuse verbatim on the publish-compute side so both hashes see identical body bytes (D-01).
- `ContentSiteIndexContentSignature.BuildSignature` — the canonical signature surface; extend it rather than adding a third scheme (D-03).
- Dialect-guarded idempotent DDL pattern in `ContentSiteIndexStore` + the P88 schema-ensure OFF switch — the ALTER rides the existing web-startup `EnsureSchema` (D-09).
- Shared `ContentNaturalKey` helper (P88 D-05) — the classifier already keys on it; the unified signature slots into the same Classify flow.

### Established Patterns
- Two divergent signatures existing side-by-side is the exact bug SYNC-02 kills — one signature, one home is the invariant to enforce (a test should assert only one signature surface remains).
- git = body source-of-truth, prod DB subordinate/reconstructable (cycle stance) — the body hash is computed FROM the `.md`, never authored independently in the DB.
- Fail-open-with-log defense-in-depth (mirrors P88 D-04's serve-side filter) rather than hard-refusal, until backfill guarantees coverage.

### Integration Points
- Publish/DirectPush write path → computes `body_sha256` (D-01) → stored on the row + in seed JSON.
- Web detail render → guard compares on-disk body hash vs stored, logs mismatch (D-05/D-07).
- Classifier / DirectPush / Pull → all read the one unified signature (D-03).

</code_context>

<specifics>
## Specific Ideas

- The CP437 mojibake incident (`project_distill_mojibake_cp437`, already fixed) is the concrete corruption class D-02's LF/UTF-8 normalization and D-05's render guard are built to make detectable next time — the phase's reason for existing.
- Every decision was chosen at the recommended default; the user consciously ratified "fail-open this phase, tighten later" and "backfill up front so tightening is safe" as a two-step rollout across 89 → a later phase.

</specifics>

<deferred>
## Deferred Ideas

- **Fail-closed render guard** (hard-refuse mismatched/un-hashed rows) — deferred; unlocked by D-08's backfill, belongs in a later phase with a rollout flag. Not Phase 89.
- **Reconciler body-hash-mismatch discrepancy type** — SYNC-11, explicitly Phase 91; consumes this phase's `body_sha256` but is not built here.
- **End-to-end distill→publish→prod→reseed→pull integration test** asserting `body_sha256` matches end to end — SYNC-16, Phase 93.

None outside the KB-sync domain surfaced — discussion stayed within phase scope.

</deferred>

---

*Phase: 89-content-hash-foundation*
*Context gathered: 2026-07-06*
