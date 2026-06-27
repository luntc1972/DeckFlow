# Phase 60: Pull-from-Prod Reconcile - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning
**Source:** Orchestrator-locked decisions + 60-RESEARCH.md (resolves its 3 open questions)

<domain>
## Phase Boundary

A NEW prod→local READ + reconcile lane in DeckFlow.Studio. The operator pulls live prod content down, sees a per-entry diff vs local, and resolves each diff locally. It is a read mirror of the existing DirectPush WRITE path. SYNC-01/02/03. NO write-back to prod. NO public-app (deckflow.gg) change.
</domain>

<decisions>
## Implementation Decisions (LOCKED)

### Read-only-against-prod is an absolute invariant
- This lane NEVER writes to prod — no INSERT/UPDATE/DELETE, no SCP upload, no DDL. It only SELECTs `content_site_index` from prod and SCP-DOWNLOADS artifacts from Render `/data`. Every "apply" in Stage 2 writes the LOCAL store / local artifact root only. (Hard DeckFlow rule: AI never writes prod; here the *app feature* is also strictly read-only toward prod.)
- The `prodStore` / prod connection obtained for the pull is used for reads only and is never captured into a field that a write path could reach. Mirror the DirectPush gating: if prod is unconfigured (`StudioConfig` presence flags false), the page disables the pull.

### Reuse the DirectPush convention exactly
- Same secrets: `Studio__ProdConnectionString` (Npgsql read) + `Studio__Scp__*` (Host/Username/KeyFile/RemoteArtifactRoot/optional Port/KeyPassphrase). Build the prod connection on-demand the SAME way `ProdStoreFactory` does — but for the prod READ use the dedicated read-only `IProdContentReader` (see R1 in Review Revisions), NOT `IProdStoreFactory`/`ContentSiteIndexStore` (whose read methods run schema-ensure DDL = a prod write). No secrets logged (sanitized literal error strings only — the D-07 rule; never `ex.Message` with creds).
- Add ONE new interface/impl pair `ISshArtifactDownloader` + `SftpArtifactDownloader` in `DeckFlow.Studio/Services/`, symmetric to `ISshArtifactUploader`/`SftpArtifactUploader`: same one-`SftpClient`-per-call pattern, same path-traversal guard, same sanitized errors, but `SftpClient.DownloadFile` (download) instead of upload. Zero new packages (SSH.NET 2025.1.0 already present).

### Diff classifier = pure Core function (testable)
- `ContentSyncDiffClassifier.Classify(prodRows, localRows)` → `IReadOnlyList<SyncDiffEntry>` in `DeckFlow.Core/Content/` beside `PublishStateDeriver`. Pure, no I/O, unit-tested in `DeckFlow.Core.Tests`.
- **Exactly the 4 SYNC-02 kinds** (Q1 resolved): `ProdNewer`, `MissingLocally`, `LocalOnly`, `Diverged`. There is NO 5th `LocalNewer` kind — when the same key exists both sides and local's timestamp is newer, classify as **`Diverged`** and carry a direction hint field (e.g. `LocalIsNewer: true`) on the entry so the UI can show which side leads. "Newer" is decided by `IndexedUtc` (the distill timestamp — non-nullable, always present). Beware the F-51-PG-01 timestamptz-vs-text gotcha when reading/comparing prod timestamps.

### Stage 2 resolution — LOCAL writes only
- Two gated stages (mirror DirectPush's staged UX):
  - **Stage 1 "Pull & classify"**: read prod `content_site_index` + SCP-download artifacts into a `pull-staging/` sub-dir under the studio data dir (NOT clobbering live `content-kb/`), then run the classifier and render the diff table.
  - **Stage 2 "Resolve"**: per entry the operator picks **adopt-prod** or **keep-local**, and Studio applies it locally.
- **adopt-prod** = make local match prod: local `UpsertContentColumnsOnlyAsync` (content columns) + promote the staged artifact via `File.Move(staged → content-kb/)`, and **mirror prod's `approval_status` onto the local row** via `SetApprovalStatusAsync` (Q2 resolved: reflect prod's actual approval state so local matches live — for a `MissingLocally` adopt, that means the prod row's approval_status, not a blind `pending`). Local `is_visible`/`is_hidden` is NOT auto-flipped — local publish stays a separate gate; adopting never auto-publishes anywhere.
- **keep-local** = no local write (optionally mark reconciled in-session); discard that entry's staged artifact.
- **`UpsertContentColumnsOnlyAsync` ONLY** — never `UpsertRowAsync` (the `FakeContentSiteIndexStore` test guard enforces this). adopt-prod on `LocalOnly` is NOT offered (LocalOnly is display-only — it exists locally, not on prod; nothing to adopt). Skip artifact promotion when `ArtifactDownloaded=false` (partial pull).

### Safety / failure
- Partial pull (some artifacts fail to download) must not corrupt local state: staging isolates the download; classify/resolve tolerate `ArtifactDownloaded=false`.
- No new packages. No Dockerfile/render.yaml edits. New files respect `.gitattributes` LF. README updated (new Studio workflow). `.editorconfig` carve-outs respected (no `{get;init;}`→`{get;}`, preserve LF, changed-lines only).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Research
- `.planning/phases/60-pull-from-prod-reconcile/60-RESEARCH.md` — symmetric mirror design, file:line map, validation architecture, landmines.

### Write path to mirror (read in full)
- `DeckFlow.Studio/Pages/DirectPush.razor` — staged-gated page UX to mirror.
- `DeckFlow.Studio/Services/ISshArtifactUploader.cs` + `SftpArtifactUploader.cs` — symmetric source for the new downloader (path guard, sanitized errors, one-client-per-call).
- `DeckFlow.Studio/Services/IProdStoreFactory.cs` (+ ProdStoreFactory) — reference ONLY for how the prod Npgsql connection is built on-demand from `Studio__ProdConnectionString`; the prod READ uses the new read-only `IProdContentReader` (R1), NOT this store factory (its store runs schema-ensure DDL).
- `DeckFlow.Studio/Program.cs` — StudioConfig presence flags + DI + config keys.
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `UpsertContentColumnsOnlyAsync`, `SetApprovalStatusAsync`, `GetApprovedRowsAsync`, the row model + columns (approval_status, published_utc, indexed_utc, is_visible/is_hidden).
- `DeckFlow.Core/Content/PublishStateDeriver.cs` — pattern for a pure Core class beside which the classifier lives.

### Project rules
- `./CLAUDE.md` — prod off-limits for AI writes; operator-local secret convention; no new packages; public repo; F-51-PG-01 timestamptz lesson; README on behavior change.
</canonical_refs>

<specifics>
## Specific Ideas
- `SyncDiffEntry`: { NaturalKey, Kind (ProdNewer|MissingLocally|LocalOnly|Diverged), LocalIsNewer (bool, only meaningful for Diverged), ArtifactDownloaded (bool), prod/local field snapshots needed for the UI }.
- Staging dir: `<studio data dir>/pull-staging/` — wiped at the start of each pull; promoted entries `File.Move` into `content-kb/`.
- Page: new `DeckFlow.Studio/Pages/PullFromProd.razor` (+ nav entry), gated like DirectPush when prod unconfigured.
- Q3 resolved: 2 stages (Pull&classify, Resolve), not 3.
</specifics>

<deferred>
## Deferred Ideas
- Write-back to prod from Studio (explicitly out — prod stays read-only here).
- Bulk "adopt all prod" one-click (start per-entry; bulk is a later polish).
- Three-way / field-level merge (only whole-entry adopt/keep this phase).
- Auto-scheduled reconcile (manual-trigger only, matches the cycle's manual-curation stance).
</deferred>

<review_revisions>
## Review Revisions (Codex peer review 2026-06-20 — BLOCK resolved, now LOCKED)

Codex review found the original design (reuse `ContentSiteIndexStore` pointed at prod for the read) violates the read-only-against-prod invariant. These revisions are now LOCKED and override any conflicting earlier text:

- **R1 (was HIGH-1) — DEDICATED READ-ONLY PROD READER, no DDL.** Do NOT use `IContentSiteIndexStore`/`ContentSiteIndexStore` for the prod read. Its read methods (`GetAllRowsAsync` → `EnsureSchemaAsync`, ContentSiteIndexStore.cs:345→347) run `CREATE TABLE`/`ALTER TABLE` DDL = a prod write. Add a NEW `IProdContentReader` + `ProdContentReader` (DeckFlow.Studio/Services/) whose ONLY method is e.g. `Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(CancellationToken)`. It opens the prod Npgsql connection (Studio__ProdConnectionString, on-demand like IProdStoreFactory) and runs a plain parameterless `SELECT <columns> FROM content_site_index` via Dapper — **NO EnsureSchemaAsync, NO DDL, NO mutator methods on the interface at all.** If the SELECT fails (e.g. table absent), surface a sanitized error — never attempt to create/alter. This makes the prod side STRUCTURALLY incapable of writing.
- **R2 (was HIGH-2) — structural write-free prod side + stronger test.** Because `IProdContentReader` exposes no write method, no apply path can write prod even by mistake. The bUnit test injects a DISTINCT prod reader fake (read-only, records read calls) separate from the local store fake, and asserts the prod reader receives ZERO writes (trivially true — no write API) and that Stage 2 writes land only on the LOCAL store fake. Keep the local `FakeContentSiteIndexStore.UpsertRowAsync`-throws guard too (proves local apply uses content-only upsert, not full-row).
- **R3 (was MEDIUM-1) — classifier omits identical pairs.** `Classify` must NOT emit an entry for a key whose prod and local rows are identical (same content fingerprint). Identical = in-sync = NOT a diff. Output contains only real differences across the 4 kinds. Unit tests assert identical pairs produce ZERO entries.
- **R4 (was MEDIUM-2) — partial-pull consistency.** On `ArtifactDownloaded=false`, adopt-prod stays SELECTABLE (per locked decision — it still upserts the local row + mirrors approval_status), shows a warning badge, and skips ONLY the `File.Move` artifact promotion. Remove any plan text that says a partial-pull entry disables/forbids adopt-prod. (adopt-prod is still NOT offered on `LocalOnly` — that is unchanged.)
- **R5 (open-q) — checkpoint asserts no prod mutation.** The 60-04 operator checkpoint must confirm prod is untouched: same row count AND no schema change attempted (with R1 there is no DDL path, but the checkpoint still states "prod schema + rows unchanged before/after" as the explicit proof).
</review_revisions>

---

*Phase: 60-pull-from-prod-reconcile*
*Context locked: 2026-06-20 by orchestrator + 60-RESEARCH.md (Q1/Q2/Q3 resolved); review revisions R1-R5 added after Codex BLOCK*
</content>
