---
phase: 55-publish-state-foundation
audited: 2026-06-18
auditor: gsd-security-auditor
asvs_level: standard
block_on: critical
threats_total: 12
threats_closed: 12
threats_open: 0
status: SECURED
---

# Phase 55: Publish-State Foundation — Security Audit

**Verdict:** SECURED — all 12 declared threat mitigations verified present in committed code (cycle9 @ 48ed4e4).
**Scope:** Verification of the declared threat register only (Plans 55-01 + 55-02). No blind scan for new vulnerabilities, per role.

## Threat Verification

### Plan 55-01 (schema / migration / publish-boundary stamping)

| Threat ID | Category | Disposition | Evidence (file:line) |
|-----------|----------|-------------|----------------------|
| T-55-01-01 | Tampering | mitigate | Idempotent guarded ADD COLUMN: `ContentSiteIndexStore.cs:94` `if (!columns.Contains("pushed_to_prod_utc"))` → `:97-99` `ADD COLUMN ... TIMESTAMPTZ NULL` (PG) / `TEXT NULL` (SQLite). Nullable, no DEFAULT, no backfill UPDATE. Mirrors is_hidden/approval_status precedent (`:80,86`). Cannot rewrite/lose existing rows. |
| T-55-01-02 | Tampering | mitigate | Column name is a hardcoded literal in the DDL string; never interpolated. Dialect selection is the boolean `_connectionInfo.IsPostgres` ternary (`ContentSiteIndexStore.cs:97`). No injection surface. |
| T-55-01-03 | Information Disclosure | mitigate | New column additive + distinct from `published_utc`. `content-kb/seed/index-seed.json` has 0 `pushedToProdUtc`/`pushed_to_prod` tokens (grep). `ContentIndexExportRow.cs` has no `PushedToProdUtc` field. `PublishedUtc` unchanged (`ContentArtifactSpec.cs:130`). Seed JSON contract byte-stable; no Phase-55 commit touches the seed file. |
| T-55-01-04 | Tampering | mitigate | `pushed_to_prod_utc` ABSENT from all 3 upserts — `UpsertSql` (`:763-804`), `UpsertPreservingVisibilitySql` (`:806-850`), `UpsertContentColumnsOnlySql` (the distill path, `:852-890`): not in INSERT col list, VALUES, ON CONFLICT, or any anonymous param. ON CONFLICT never touches it → re-distill preserves the stamp. |
| T-55-01-05 | Repudiation | mitigate | Exactly ONE writer: `StampPushedToProdAsync` (`IContentSiteIndexStore.cs:179`; impl `ContentSiteIndexStore.cs:592-625`, transactional, null/empty-guarded). git path stamps LOCAL only AFTER `_commitSuccess = true` (`Publish.razor:519` → stamp `:526-529`). DirectPush stamps LOCAL + prod with one shared `pushedUtc` ONLY when `allOk` (`DirectPush.razor:720,732-733`). Distill never stamps (column absent from its upsert). |
| T-55-01-06 | Tampering | mitigate | Stamp is server-side `DateTimeOffset.UtcNow`, never operator-supplied (`Publish.razor:528`, `DirectPush.razor:728`). Dialect typing: `pushed_to_prod_utc TIMESTAMPTZ NULL` (PG, `:900`) / `TEXT NULL` (SQLite, `:923`), mirroring `published_utc`; bound via Dapper `@pushed` as `DateTimeOffset` (`:618`), avoiding the F-51-PG-01 TEXT-vs-timestamptz mismatch. |
| T-55-01-SC | Tampering | mitigate | No `PackageReference` changes across any Phase-55 commit (`git diff 1e259d7..HEAD -- '*.csproj'` empty). Dapper/Npgsql/Sqlite already in solution. |

### Plan 55-02 (PublishStateDeriver engine)

| Threat ID | Category | Disposition | Evidence (file:line) |
|-----------|----------|-------------|----------------------|
| T-55-02-01 | Information Disclosure | mitigate | `!isVisible → PushedHidden` (`PublishStateDeriver.cs:22-25`) evaluated BEFORE the Published branch. A non-visible pushed entry can never derive Published. |
| T-55-02-02 | Information Disclosure | mitigate | `!pushedToProdUtc.HasValue → NeverPublished` (`PublishStateDeriver.cs:17-20`) is the first check; short-circuits regardless of visibility/local time. |
| T-55-02-03 | Tampering | mitigate | Both operands normalized: `.ToUniversalTime().UtcDateTime` (`PublishStateDeriver.cs:27-28`). LocalNewer requires strictly-greater (`:30`); equal instant falls through to Published (`:35`). Cross-offset same-instant ⇒ Published. |
| T-55-02-04 | Repudiation | mitigate | Single deriver in Core; display strings centralized in `PublishState.cs:31-39` `ToDisplayString()`. Rival-string grep (`Never published`/`Pushed-hidden`/`Local-newer`, *.cs ex-obj) returns ONLY `PublishState.cs` + its test. No duplicate logic. |
| T-55-02-SC | Tampering | mitigate | No new packages; pure C# in existing DeckFlow.Core / DeckFlow.Core.Tests (csproj diff empty). |

## WR-03 Information-Disclosure Cross-Check (public-repo hygiene)

Confirmed the publish pages no longer surface raw exception text in the publish-stamp paths:
- `Publish.razor:533-535` — post-commit stamp-failure catch sets a sanitized literal (`"Commit succeeded, but the local pushed-to-prod stamp did not complete — re-export and publish again to record it."`); no `ex.Message`. Matches the b929bde sanitization noted in 55-REVIEW.md.
- `DirectPush.razor:730-733` — stamp path carries no exception-message UI surface.

This folds into the T-55-01-03 / Information-Disclosure assessment: no filesystem paths or git stderr leak through the publish-stamp surfaces audited here. (REVIEW info items IN-01..04 are quality/robustness, not declared threats — out of audit scope.)

## Unregistered Flags

None. Neither 55-01-SUMMARY.md nor 55-02-SUMMARY.md declares a `## Threat Flags` section; no new attack surface appeared during implementation without a mapped threat ID.

## Accepted Risks Log

None for this phase. (Documented manual gate, not an accepted security risk: the Postgres `TIMESTAMPTZ` ALTER + prod stamp are only exercised under `DECKFLOW_POSTGRES_TESTS` / a live DirectPush — code path is dual-dialect and matches the proven approval_status/is_hidden precedent; SQLite TEXT round-trip is automated.)

---
_Audited: 2026-06-18 — gsd-security-auditor. Implementation files unmodified._
