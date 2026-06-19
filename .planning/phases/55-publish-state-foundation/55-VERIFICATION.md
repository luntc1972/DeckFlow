---
phase: 55-publish-state-foundation
verified: 2026-06-18T18:05:00Z
status: verified
score: 4/4 ROADMAP success criteria verified (gap closed by fix ed68afa)
overrides_applied: 0
gap_resolution: >-
  The DirectPush-stamp BLOCKER below was fixed in commit ed68afa: DirectPush.razor now builds
  the StampPushedToProdAsync key set from the row's canonical NaturalKeyType/NaturalKeyValue
  (via ContentIndexExportRow.From(row)) instead of the literal "youtube"/"podcast" strings, so
  the stamp UPDATE matches the stored rows. The Studio FakeContentSiteIndexStore.StampPushedToProdAsync
  now applies the stamp by natural key. DeckFlow.Studio.Tests = 36/36 pass, including the previously
  FAILING DirectPush_Success_StampsLocalAndProd_WithSameInstant. Full DeckFlow.sln build 0/0;
  DeckFlow.Core.Tests 467/467. SC3 now fully met. Manual PG-gated DirectPush stamp still pending
  (DECKFLOW_POSTGRES_TESTS / live prod run) per the WSL VSTest caveat.
gaps_resolved:
  - truth: "Both publish paths (git Publish + DirectPush) stamp pushed_to_prod_utc on every row they touch (ROADMAP SC3; PUB-01)."
    status: partial
    reason: >-
      The DirectPush path builds the StampPushedToProdAsync key set with the literal
      natural-key-type strings "youtube"/"podcast", but the rows are persisted with
      natural_key_type = ContentSourceType.Youtube ("youtube_channel") / ContentSourceType.Podcast
      ("podcast_rss"). The stamp UPDATE's `WHERE natural_key_type = @type` therefore matches
      ZERO rows in both the local AND prod stores — DirectPush records no pushed_to_prod_utc at all.
      The git Publish path is correct (it uses r.NaturalKeyType from the export rows). The
      executor-authored bUnit test DirectPush_Success_StampsLocalAndProd_WithSameInstant FAILS
      on this exact defect; the 55-01-SUMMARY's "all tests pass" / "executed exactly as written"
      claim is contradicted by a live test run.
    artifacts:
      - path: "DeckFlow.Studio/Pages/DirectPush.razor"
        issue: >-
          Lines 689 and 724 use literal "youtube"/"podcast" for the natural-key type instead of
          ContentSourceType.Youtube ("youtube_channel") / ContentSourceType.Podcast ("podcast_rss").
          Line 724 feeds StampPushedToProdAsync, so the stamp WHERE clause never matches stored rows.
    missing:
      - "Build DirectPush stamp keys using ContentSourceType.Youtube / ContentSourceType.Podcast (matching the natural_key_type the store actually persists) so the local + prod stamp UPDATE matches the rows it just upserted."
      - "Re-run DeckFlow.Studio.Tests — DirectPush_Success_StampsLocalAndProd_WithSameInstant must pass (currently FAILS: rows show PushedToProdUtc = null after a successful DirectPush)."
deferred: []
---

# Phase 55: Publish-State Foundation Verification Report

**Phase Goal:** The system records when content was pushed to production and can derive a single authoritative status for each entry.
**Verified:** 2026-06-18T18:05:00Z
**Status:** gaps_found
**Re-verification:** No — initial verification
**Verdict:** PARTIAL — PUB-02 fully delivered; PUB-01 schema/migration/git-path correct, but the DirectPush stamp is wired with the wrong natural-key type and writes zero rows. SC3 not fully met.

## Goal Achievement

### Observable Truths

| #  | Truth (ROADMAP Success Criterion / must-have) | Status | Evidence |
|----|-----------------------------------------------|--------|----------|
| 1  | SC1: Fresh SQLite DB auto-gains `pushed_to_prod_utc` (idempotent self-healing migration, no manual SQL). | ✓ VERIFIED | `ContentSiteIndexStore.cs:94-101` guarded ADD COLUMN; both CREATE TABLE constants carry the column (`:900` PG TIMESTAMPTZ, `:923` SQLite TEXT). Test `EnsureSchemaAsync_AddsPushedToProdColumn_ToFreshAndLegacySchema` + `..._Twice_IsIdempotent...` PASS (live run, 20/20 targeted). |
| 2  | SC2: Same migration applies cleanly to Render Postgres without data loss. | ✓ VERIFIED (code) / ⚠ manual-gate for live PG | Dialect-branched ADD COLUMN (`:97-99`), nullable, NO DEFAULT, NO grandfather UPDATE → cannot rewrite/lose existing rows. Mirrors proven approval_status/is_hidden precedent. Live Postgres ALTER only exercised when `DECKFLOW_POSTGRES_TESTS` is set (CLAUDE.md WSL VSTest caveat) — flagged as manual-verify, per plan. |
| 3  | SC3 / PUB-01: BOTH publish paths stamp `pushed_to_prod_utc` on every row they touch; never-pushed stays NULL. | ✗ FAILED (PARTIAL) | git Publish: CORRECT — `Publish.razor:518-521` stamps after `_commitSuccess = true` using `r.NaturalKeyType` from export rows (`:413-414`); bUnit `PublishPageTests` 11/11 PASS. **DirectPush: BROKEN** — `DirectPush.razor:724` builds keys with `"youtube"/"podcast"` but rows persist as `"youtube_channel"/"podcast_rss"` (`ContentSourceType`), so `StampPushedToProdAsync`'s `WHERE natural_key_type=@type` matches 0 rows. bUnit `DirectPush_Success_StampsLocalAndProd_WithSameInstant` FAILS (rows read PushedToProdUtc=null). HIGH-1 (column absent from all 3 upserts) IS verified — re-distill preserves the stamp. |
| 4  | SC4 / PUB-02: A single `PublishStateDeriver` returns one of the 4 locked states; no duplicate logic. | ✓ VERIFIED | `PublishStateDeriver.cs` pure, store-free, locked precedence (null→NeverPublished, !visible→PushedHidden, local>push UTC→LocalNewer, else Published); `.ToUniversalTime().UtcDateTime` on both operands; equal⇒Published. Display strings sole-sourced in `PublishState.cs`. grep finds the 4 strings ONLY in PublishState.cs + its test. Deriver tests PASS (incl. equal-boundary + cross-offset same-instant + cross-offset strictly-later). |

**Score:** 3/4 success criteria verified (PUB-02 complete; PUB-01 partially — git path works, DirectPush path is a no-op stamp).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` | `PushedToProdUtc` nullable init prop | ✓ VERIFIED | Line 133, mirrors `PublishedUtc` (init, nullable). PublishedUtc unchanged (line 130). |
| `DeckFlow.Core/Content/IContentSiteIndexStore.cs` | `StampPushedToProdAsync` declared | ✓ VERIFIED | Line 179, the sole declared writer. |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | Migration + CREATE + 5 SELECTs + sole-writer; absent from upserts | ✓ VERIFIED | ADD COLUMN `:94`; SELECTs `:257,291,325,358,390`; StampPushedToProdAsync `:592` (transactional, guards null/empty); CREATE `:900,923`; ABSENT from UpsertSql/UpsertPreservingVisibilitySql/UpsertContentColumnsOnlySql (`:763,806,852`) — HIGH-1 confirmed. |
| `DeckFlow.Studio/Pages/Publish.razor` | Stamp local index after commit | ✓ VERIFIED | `:518-521` after `_commitSuccess=true`, non-empty keys, non-fatal error handling. Correct key type. |
| `DeckFlow.Studio/Pages/DirectPush.razor` | Stamp local + prod after allOk, same instant | ⚠ ORPHANED (calls present, keys wrong) | `:731-732` two calls, one shared `pushedUtc`, gated on `allOk`. BUT keys at `:724` use wrong type → stamp matches 0 rows. |
| `DeckFlow.Core/Content/PublishState.cs` | enum (4) + display map | ✓ VERIFIED | 4 members + `ToDisplayString()` locked strings. |
| `DeckFlow.Core/Content/PublishStateDeriver.cs` | Pure deriver | ✓ VERIFIED | Sealed, 3-scalar pure `Derive`. |
| `DeckFlow.Core.Tests/Content/ContentSiteIndexStorePushedToProdTests.cs` | Migration/round-trip/preserve coverage | ✓ VERIFIED | 6 facts incl. HIGH-1 DDL-vs-upsert assertion; all PASS. |
| `DeckFlow.Core.Tests/Content/PublishStateDeriverTests.cs` | 4 states + boundary + TZ | ✓ VERIFIED | [MemberData] covers all cases; PASS. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Publish.razor | content_site_index.pushed_to_prod_utc (local) | StampPushedToProdAsync after commit | ✓ WIRED | Correct natural-key type from export rows; bUnit asserts a single stamp call + row updated. |
| DirectPush.razor | content_site_index.pushed_to_prod_utc (local + prod) | StampPushedToProdAsync after allOk | ✗ NOT_WIRED (effectively) | Calls fire, but key type "youtube"/"podcast" ≠ stored "youtube_channel"/"podcast_rss" → UPDATE affects 0 rows. bUnit FAILS. |
| PublishStateDeriver.cs | ContentSiteIndexRow.PushedToProdUtc | consumes Plan-01 column | ✓ WIRED | Deriver takes the value as a parameter; round-trips proven by store tests. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| Publish.razor stamp | _exportedKeys | newRows.NaturalKeyType/Value (`:413`) | Yes — matches stored rows | ✓ FLOWING |
| DirectPush.razor stamp | keys (`:722-726`) | literal "youtube"/"podcast" | No — never matches stored natural_key_type | ✗ DISCONNECTED |
| PublishStateDeriver | pushedToProdUtc param | caller reads row column | Yes (round-trip test green) | ✓ FLOWING |

### Behavioral Spot-Checks / Test Execution

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Core build | `dotnet build DeckFlow.Core.csproj` | 0 warn / 0 err | ✓ PASS |
| Core.Tests build | `dotnet build DeckFlow.Core.Tests.csproj` | 0 warn / 0 err | ✓ PASS |
| Full solution build | `dotnet build DeckFlow.sln` | 0 warn / 0 err | ✓ PASS |
| Phase-55 targeted Core tests | `dotnet test --filter PublishStateDeriver\|ContentSiteIndexStorePushedToProd\|ContentPublishStamp` | 20 passed / 0 failed | ✓ PASS |
| Full Core.Tests suite | `dotnet test DeckFlow.Core.Tests.csproj` | 467 passed / 0 failed | ✓ PASS |
| Studio git-Publish bUnit | `dotnet test --filter PublishPageTests` | 11 passed / 0 failed | ✓ PASS |
| Studio DirectPush bUnit | `dotnet test --filter DirectPushPageTests` | **1 FAILED** / 24 passed | ✗ FAIL |

Failing test: `DirectPush_Success_StampsLocalAndProd_WithSameInstant` (DirectPushPageTests.cs:390). After a successful DirectPush, `localStore.Rows` and `prodStore.Rows` both read `PushedToProdUtc = null` — Expected the shared push instant. (`StampCalls` is recorded, so the call fires; the keys simply don't match.)

### Probe Execution

No conventional `scripts/*/tests/probe-*.sh` declared for this phase; verification used the test suites above. (Studio is a Blazor app, not a CLI/migration probe phase.)

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PUB-01 | 55-01 | `pushed_to_prod_utc` column, dual-dialect idempotent migration, stamped by BOTH publish paths | ✗ BLOCKED (partial) | Schema/migration/sole-writer/git-path all correct; DirectPush stamp writes 0 rows (wrong key type). "Both publish paths stamp" not met. |
| PUB-02 | 55-02 | Single PublishStateDeriver, 4 states, no duplicate logic | ✓ SATISFIED | Verified above; tests green; no rival logic. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| DeckFlow.Studio/Pages/DirectPush.razor | 689, 724 | Hardcoded natural-key literal "youtube"/"podcast" instead of `ContentSourceType.Youtube`/`Podcast` | 🛑 Blocker | Stamp keys never match stored `youtube_channel`/`podcast_rss`; DirectPush pushed_to_prod_utc is silently a no-op on local + prod. (Note: `VideoStatusResolver.cs:65` explicitly warns "use ContentSourceType.Youtube constant — never the raw string literal (LOW-1)" — the same pitfall recurred here.) |

No debt markers (TBD/FIXME/XXX) introduced. `published_utc`/seed-JSON contract honored (see below).

### Seed JSON / Contract Check

- `content-kb/seed/index-seed.json` shows ` M` in git, but the diff is unrelated content-kb cleanup (entry removals), NOT a Phase-55 change. NO Phase-55 commit (727b977, d4a138d, 0632863, 9f09806, 4cedf4d, c0dd941) touches the seed file; the field shape is byte-stable (no `pushedToProdUtc` added). `ContentIndexExportRow.cs` has NO `PushedToProdUtc`. Contract preserved. (ℹ️ Info: the working-tree seed edit is pre-existing session state, not this phase.)

### Human Verification Required

(Deferred to after the gap is closed.)

1. **Manual Postgres migration gate** — *Test:* run the gated suite with `DECKFLOW_POSTGRES_TESTS` set (or a real DirectPush against the Render Postgres DB). *Expected:* `pushed_to_prod_utc` ALTER applies with no error/data loss AND (after the fix) DirectPush'd rows carry a non-null stamp on prod. *Why human:* WSL VSTest cannot exercise live Postgres per CLAUDE.md; no automated coverage of the PG ALTER or prod stamp.

### Gaps Summary

PUB-02 is fully delivered and verified end-to-end. PUB-01's schema, idempotent dual-dialect migration, single-writer design (HIGH-1: column absent from all upserts → re-distill preserves the stamp), and the **git Publish** stamp are all correct and test-green. The single defect is in **DirectPush.razor**: it constructs the `StampPushedToProdAsync` key set with the literal type strings `"youtube"`/`"podcast"`, while the store persists rows under `natural_key_type = "youtube_channel"`/`"podcast_rss"` (`ContentSourceType` constants). The stamp UPDATE's `WHERE natural_key_type = @type` therefore matches zero rows, so DirectPush records nothing to `pushed_to_prod_utc` on either the local OR the prod store. This silently breaks ROADMAP SC3 ("both publish paths stamp ... on every row they touch") for the DirectPush path and would make every DirectPush'd entry derive as `Never published` forever.

The executor's own bUnit test `DirectPush_Success_StampsLocalAndProd_WithSameInstant` catches this exactly and is currently FAILING — contradicting the 55-01-SUMMARY's "plan executed exactly as written / no deviations" and implied all-green claim. Fix: build the DirectPush keys from `ContentSourceType.Youtube`/`ContentSourceType.Podcast` (matching what the upsert stores), then re-run `DeckFlow.Studio.Tests` to green.

---

_Verified: 2026-06-18T18:05:00Z_
_Verifier: Claude (gsd-verifier)_
