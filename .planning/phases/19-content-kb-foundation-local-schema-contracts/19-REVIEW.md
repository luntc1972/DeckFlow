---
phase: 19-content-kb-foundation-local-schema-contracts
reviewed: 2026-05-26T22:55:00Z
depth: standard
files_reviewed: 25
files_reviewed_list:
  - DeckFlow.Core/Knowledge/ContentModels.cs
  - DeckFlow.Core/Knowledge/ContentSpendModels.cs
  - DeckFlow.Core/Knowledge/ContentArtifactSpec.cs
  - DeckFlow.Core/Knowledge/ContentTagVocabulary.cs
  - DeckFlow.Core/Storage/RelationalDatabaseConnection.cs
  - DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs
  - DeckFlow.Web/Services/Content/IContentSourceStore.cs
  - DeckFlow.Web/Services/Content/ContentSourceStore.cs
  - DeckFlow.Web/Services/Content/IContentVideoStore.cs
  - DeckFlow.Web/Services/Content/ContentVideoStore.cs
  - DeckFlow.Web/Services/Content/IWhisperSpendLedger.cs
  - DeckFlow.Web/Services/Content/WhisperSpendLedger.cs
  - DeckFlow.Web/Services/Content/IContentHarvestRunStore.cs
  - DeckFlow.Web/Services/Content/ContentHarvestRunStore.cs
  - DeckFlow.Web/Services/Content/IContentSiteIndexStore.cs
  - DeckFlow.Web/Services/Content/ContentSiteIndexStore.cs
  - DeckFlow.Core.Tests/ContentTagVocabularyTests.cs
  - DeckFlow.Core.Tests/ContentArtifactSpecTests.cs
  - DeckFlow.Core.Tests/RelationalDatabaseConnectionForeignKeyTests.cs
  - DeckFlow.Web.Tests/ContentSourceStoreTests.cs
  - DeckFlow.Web.Tests/ContentVideoStoreTests.cs
  - DeckFlow.Web.Tests/WhisperSpendLedgerTests.cs
  - DeckFlow.Web.Tests/ContentHarvestRunStoreTests.cs
  - DeckFlow.Web.Tests/ContentSiteIndexStoreTests.cs
findings:
  critical: 1
  warning: 6
  info: 4
  total: 11
status: issues_found
---

# Phase 19: Code Review Report

**Reviewed:** 2026-05-26T22:55:00Z
**Depth:** standard
**Files Reviewed:** 25
**Status:** issues_found

## Summary

This phase delivers pure schema/contract code for the Content KB foundation: sealed records,
a tag-vocabulary allowlist, an artifact file-format spec, an FK-pragma connection seam, and five
dual-DDL (SQLite + Postgres) stores. The good news first: SQL is consistently parameterized
(no string-interpolated DML/DDL), every record uses `{ get; init; }` (the System.Text.Json
get-only trap is avoided), the FK pragma seam is correct, `WhisperSpendLedger.GetMonthlyTotalAsync`
correctly sums in C# decimal space rather than SQL `SUM` over TEXT, and CASCADE FKs/CHECK clauses
match across both dialects. The DDL is the strong part of this submission.

The concerns are concentrated in **schema-invariant gaps** that the DB cannot catch and that
later phases will trust silently:

1. The `content_videos` mutual-exclusivity invariant ("a video is YouTube **xor** RSS") is **not**
   enforced — the CHECK is an inclusive OR, so a row with both keys is accepted, and there is no
   matching app-side guard. This contradicts the `ContentSiteIndexStore` which **does** enforce
   exactly-one. (CR-01)

2. The path-traversal guard rejects `..` segments and rooted paths, but does **not** reject paths
   that escape via other vectors the doc-comment implies are handled (empty after the rooted check,
   and the `..` check is purely segment-equality, so `..` is the only traversal token caught).
   Adequate for the documented contract but narrower than the comment suggests. (WR-01)

The remaining items are robustness/consistency warnings and info-level notes.

## Critical Issues

### CR-01: `content_videos` CHECK allows a row with BOTH youtube_video_id and rss_guid; no xor enforcement anywhere

**File:** `DeckFlow.Web/Services/Content/ContentVideoStore.cs:332` (Postgres) and `:381` (SQLite); insert path `:76-104`

**Issue:** The natural-key invariant for a content video is "exactly one of YouTube id / RSS guid."
The schema enforces only an inclusive OR:

```sql
CHECK (youtube_video_id IS NOT NULL OR rss_guid IS NOT NULL)
```

A row supplying **both** keys passes the CHECK and both `UNIQUE` constraints. `InsertVideoAsync`
performs no app-side guard either — it forwards `youtubeVideoId` and `rssGuid` straight through.
This is a genuine data-integrity defect because the **downstream** `ContentSiteIndexStore.GetNaturalKey`
(`ContentSiteIndexStore.cs:160-174`) treats both-set as an error (`hasYoutubeVideoId == hasRssGuid`
throws). So a `content_videos` row that the video store happily persists cannot be represented in
the site index — the two stores disagree on the same invariant. Phase-20 harvest code that builds a
site-index row from a both-keys video row will throw at index time, after the (local-only, possibly
paid Whisper) work has already been done and committed.

It is classified Critical rather than Warning because it is a silent data-shape divergence between
two stores that share the same conceptual key, and the failure surfaces only later, after spend has
been incurred — exactly the data-loss/late-failure profile the harvest cap machinery exists to avoid.

**Fix:** Make the invariant explicit and identical in both layers. Tighten the CHECK to an exclusive
or, and add an app-side guard mirroring `GetNaturalKey`:

```sql
-- both dialects
CHECK (
  (youtube_video_id IS NOT NULL AND rss_guid IS NULL)
  OR (youtube_video_id IS NULL AND rss_guid IS NOT NULL)
)
```

```csharp
// InsertVideoAsync, before EnsureSchemaAsync
var hasYoutube = !string.IsNullOrWhiteSpace(youtubeVideoId);
var hasRss = !string.IsNullOrWhiteSpace(rssGuid);
if (hasYoutube == hasRss)
{
    throw new ArgumentException(
        "Exactly one of youtubeVideoId or rssGuid must be supplied for a content video.",
        nameof(youtubeVideoId));
}
```

Add a regression test asserting both-keys and neither-key inserts throw (the existing
`InsertVideoAsync_RejectsVideoWithoutNaturalKey` covers neither-key via SQLite only; add a both-keys case).

## Warnings

### WR-01: Path-traversal guard is narrower than its doc-comment and the `ContentSiteIndexRow` summary claim

**File:** `DeckFlow.Web/Services/Content/ContentSiteIndexStore.cs:176-200`; contract doc `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs:121-127`

**Issue:** `ValidateArtifactPath` rejects rooted paths (POSIX + Windows `X:\`) and any segment exactly
equal to `..`. That is correct for the two tested vectors. However the `ContentSiteIndexRow.ArtifactPath`
doc states "slugs and natural keys must be sanitized before becoming paths," implying sanitization the
store does not perform — the store only blocks traversal, it does not sanitize embedded control chars,
NUL bytes, or leading-backslash filenames (`\foo` is not rooted on Linux and contains no `..`, so it is
stored verbatim and later `Path.Combine`d as an ordinary filename). No injection occurs, but the comment
over-promises relative to the implementation, and a later phase reading the comment may skip its own
sanitization. Also note: there is no defensive guard against an empty-after-split path (e.g. `"/"`),
though `IsPathRooted` happens to catch that case today.

**Fix:** Either narrow the doc-comment to state exactly what the guard does ("rejects rooted and `..`
paths; callers remain responsible for slug sanitization"), or add the missing checks (reject control
chars / `Path.GetInvalidPathChars`, normalize separators). Prefer the doc fix unless later phases will
rely on this method as the sole sanitizer.

### WR-02: SQLite `cost_usd`/`spend_usd` stored as TEXT can silently lose ordering/precision contracts if a future query ever does range filtering

**File:** `DeckFlow.Web/Services/Content/WhisperSpendLedger.cs:218-228`; `ContentHarvestRunStore.cs:228-240`

**Issue:** Money is stored as `TEXT` in SQLite and `DECIMAL(10,6)` in Postgres. Today the only
aggregation (`GetMonthlyTotalAsync`) correctly sums in C# decimal space, so this is safe. But the two
backends now hold money in non-comparable column types: any future SQLite-side `WHERE cost_usd > ...`
or `ORDER BY cost_usd` would compare lexicographically (`"0.9" > "0.10"`), producing wrong results that
Postgres would get right. This is a latent dual-DDL divergence the phase is introducing.

**Fix:** Add a one-line comment at both SQLite DDL sites documenting that `cost_usd` is intentionally TEXT
and MUST only be aggregated/compared in app code (never in SQL), so a future maintainer does not add a
SQL comparison. Optionally store as a zero-padded fixed-width string if SQL comparison is ever needed.

### WR-03: `ReadDecimal` / `ReadDateTimeOffset` / `ReadBool` helpers are duplicated across four stores

**File:** `ContentSourceStore.cs:163-187`, `WhisperSpendLedger.cs:193-204`, `ContentHarvestRunStore.cs:189-212`, `ContentSiteIndexStore.cs:231-241`

**Issue:** `ReadDecimal`, `ReadDateTimeOffset`, and the `FormatTimestamp`/`FormatDecimal` helpers are
copy-pasted across the stores with identical bodies. Per CLAUDE.md ("Logic duplicated more than twice —
extract to a utility") this exceeds the threshold. Divergent edits to one copy (e.g. a parse-style fix)
will silently not apply to the others, which is a real correctness hazard for dual-DDL type coercion code.

**Fix:** Extract a `static` helper class (e.g. `RelationalValueReader` / extension methods on
`DbDataReader` + provider-aware formatters) in `DeckFlow.Core/Storage` and have all four stores call it.

### WR-04: Per-store `_schemaReady` cache means schema-create races across independently-constructed store instances

**File:** all stores, e.g. `ContentVideoStore.cs:14-15, 49-73`; cross-store bootstrap `WhisperSpendLedger.cs:71-72`

**Issue:** `_schemaReady` + `_schemaGate` correctly serialize schema creation **within one instance**.
But each store keeps its own gate, and `ContentVideoStore.EnsureSchemaAsync` constructs a brand-new
`ContentSourceStore` each call (`:59`) whose `_schemaReady` is always false, so it re-issues the
`CREATE TABLE IF NOT EXISTS` for sources on every video-schema bootstrap. With multiple store instances
(DI scoped + the test pattern that news up `ContentSourceStore`, `ContentVideoStore`, and
`WhisperSpendLedger` over the same file) two threads can run `CREATE TABLE IF NOT EXISTS` concurrently.
`IF NOT EXISTS` makes this idempotent and non-corrupting, so it is not Critical — but it is wasted work
on every call and relies entirely on `IF NOT EXISTS` for safety, which should be stated.

**Fix:** Either register the stores as singletons in DI (so the `_schemaReady` cache is effective), or
note in a comment that schema creation is deliberately idempotent and re-run is acceptable. At minimum,
avoid constructing a throwaway `ContentSourceStore` on every `EnsureSchemaAsync` call — inline the
parent DDL into the child's create batch, or pass the already-constructed source store in.

### WR-05: `Convert.ToInt64(ExecuteScalarAsync result)` will throw an opaque `NullReferenceException`/`InvalidCastException` if `RETURNING` yields no row

**File:** `ContentSourceStore.cs:93-94`, `ContentVideoStore.cs:102-103` (and the other `Insert*` methods), `ContentHarvestRunStore.cs:85-86`

**Issue:** Every insert does `Convert.ToInt64(await command.ExecuteScalarAsync(...), ...)`. If the insert
were ever silently skipped (e.g. an `ON CONFLICT DO NOTHING` added later, or a provider returning null),
`ExecuteScalarAsync` returns `null` and `Convert.ToInt64(null)` returns `0` — masking the failure as a
valid id `0`. Today the inserts have no conflict clause so this cannot fire, but the pattern is fragile
for a foundation layer that later phases will extend.

**Fix:** Guard the scalar: `var id = await command.ExecuteScalarAsync(...) ?? throw new InvalidOperationException("INSERT ... RETURNING produced no id.");` before converting. This turns a future silent-zero-id bug into a loud failure.

### WR-06: `WhisperSpendLedger.ReadMonthlyCapUsd` reads a process-wide environment variable as a fallback, defeating per-instance configuration and test isolation

**File:** `DeckFlow.Web/Services/Content/WhisperSpendLedger.cs:175-191`

**Issue:** When `IConfiguration` does not supply `DECKFLOW_WHISPER_MONTHLY_CAP_USD`, the method falls
back to `Environment.GetEnvironmentVariable(...)`. This is a hidden global-state read: a value set in
the host environment overrides the injected configuration's *absence*, which makes the cap behavior
depend on ambient process state. In tests that do not set the env var this is benign, but in production
the precedence (injected config wins, env only as fallback) is the reverse of the usual ASP.NET
expectation that environment variables are already folded into `IConfiguration`. It can produce a
surprising cap if the env var is set but the config provider is not wired to read it.

**Fix:** Rely solely on `IConfiguration` (env vars are already an ASP.NET config source via
`AddEnvironmentVariables`), or document explicitly why a direct env read is needed and that injected
config takes precedence. Removing the direct env read also removes the global-state coupling flagged by
the side-effects checklist.

## Info

### IN-01: `ContentClip` / `ContentTranscript` / `ContentSummary` / `ContentTag` records are defined but never read back by any store method

**File:** `DeckFlow.Core/Knowledge/ContentModels.cs:66-139`

**Issue:** `ContentVideoStore` inserts transcript/summary/clip/tag rows and counts them, but never
materializes them into the `ContentTranscript`/`ContentSummary`/`ContentClip`/`ContentTag` records —
those records currently have no consuming read path in this phase. This is expected for a foundation
phase (later phases consume them), so it is informational, not dead code. Worth confirming the next
phase actually wires `SELECT`s for these so the records do not drift from the schema.

**Fix:** None required this phase; track that read paths land in the consuming phase.

### IN-02: `content_clips` has no UNIQUE / ordering guarantee beyond `sort_order` default 0

**File:** `ContentVideoStore.cs:347-353` / `:396-402`

**Issue:** Clips default `sort_order = 0`; multiple clips for one video with default sort order have no
deterministic read ordering (no `ORDER BY` exists yet because there is no read method). Fine for a
foundation phase but a latent "stable sort order" gap — the `ContentClip.SortOrder` XML doc promises
"stable sort order" that the schema does not currently guarantee for ties.

**Fix:** When the read path lands, `ORDER BY sort_order, id` to make ties deterministic.

### IN-03: `ArtifactFileFormat` constant embeds example tag values that must stay in lockstep with `ContentTagVocabulary`

**File:** `ContentArtifactSpec.cs:13-41`

**Issue:** The example front matter hardcodes `["combo","control"]`, `["cEDH","Optimized"]`,
`["win-cons","counter"]`. These currently all exist in the allowlist, but there is no test asserting the
example values are valid vocabulary members, so the spec example could drift from the allowlist over time.

**Fix:** Add a small test that parses the example tag tokens and asserts each is `ContentTagVocabulary.IsValid`
for its dimension, keeping the canonical example honest.

### IN-04: `IWhisperSpendLedger` lacks an `EnsureSchemaAsync` member though the implementation exposes one publicly

**File:** `IWhisperSpendLedger.cs:1-43` vs `WhisperSpendLedger.cs:61`

**Issue:** The other four store interfaces declare `EnsureSchemaAsync`; `IWhisperSpendLedger` does not,
yet the concrete class exposes a public `EnsureSchemaAsync` (and the test calls it via the concrete type).
This is a minor interface-segregation inconsistency — callers depending on the interface cannot pre-create
the schema, while callers depending on the concrete type can. Not a bug (record/get-init OK, schema is
auto-ensured on first write), just an inconsistency across the five sibling contracts.

**Fix:** Either add `EnsureSchemaAsync` to `IWhisperSpendLedger` for parity, or drop it from the public
surface of the implementation if schema bootstrap should only be implicit.

---

_Reviewed: 2026-05-26T22:55:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
