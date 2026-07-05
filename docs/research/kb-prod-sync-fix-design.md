# Keeping the Content-KB in sync: prod ↔ git ↔ Studio — fix design

*Merges (a) a codebase+prod audit of DeckFlow's actual sync path with (b) a deep-research pass on git-body/DB-index sync patterns (8 findings, adversarially verified 3-0). 2026-07-05.*

## The one principle that fixes most of it

**Git is the single source of truth for BODIES; the prod DB index row is strictly subordinate and reconstructable from git.** This is the established Git-based CMS model (Decap/TinaCMS: "file system as the source of truth… the DB is an ephemeral cache/index") [1][2]. DeckFlow already half-implements this — bodies in `content-kb/*.md`, prod DB holds only `content_site_index`, and `ContentKbSeedLoader` rebuilds the index from `index-seed.json` on deploy. The sync bugs are all places where a path **violates** the subordination: DirectPush writes prod state that git can't reconstruct, and reads bodies from a store git doesn't own.

## Weakness → pattern → fix (each fix cites the audit file:line + research finding)

### 1. DirectPush body/index channel mismatch → git must own the body
- **Now:** DirectPush writes the prod row + uploads the body to `/data` (SFTP), but prod reads bodies **git-`/app` first**, `/data` only if `MTG_DATA_DIR` set (`ContentKbArtifactPathResolver.cs:107-116` vs `DirectPushCoordinator.cs:167-173`). Unset env ⇒ live+visible row with an unreachable body.
- **Pattern:** git/body is master; the DB row is subordinate; ship the body **before** the row goes visible (expand-contract) [1][8].
- **Fix:** Stop depending on the `/data` overlay for prod bodies. Bodies reach prod **only via git deploy to `/app`**. DirectPush's "immediate" write becomes: commit body → deploy (or verify already-deployed) → then flip visible. If an out-of-band fast path is kept, gate it on a **body-hash present check** (see #5) so a row can never be visible without its body.

### 2. DirectPush omits the seed → git can't reconstruct prod
- **Now:** DirectPush deliberately never re-exports `index-seed.json` (`DirectPushCoordinator.cs:214-217`). A prod DB rebuild-from-seed (any deploy/reset) silently drops DirectPush-only rows. This is the literal cause of "orphans not in git seed."
- **Pattern:** the DB is reconstructable from git; every prod state change is reflected in the authoritative git artifact [1].
- **Fix:** **DirectPush must re-export the seed** exactly like Publish does (`PublishCoordinator.cs:97`). Every prod write ⇒ seed updated + committed. Then git alone fully reconstructs the prod index.

### 3. `[skip render]` staleness / `/app` shadows `/data`
- **Now:** committed bodies carry `[skip render]` (`DirectPushCoordinator.cs:34,314`) so they only reach `/app` on a future unrelated deploy; meanwhile git resolution wins, so a stale `/app` body **shadows** the fresh `/data` one → updates invisible until redeploy.
- **Pattern:** expand-contract ordering (ship file, verify renders, then flip state) [8]; the refuted framing [R1] doesn't change this — ordering discipline stands on its own.
- **Fix:** drop `/data` for prod entirely (folds into #1). Order = commit body → deploy → verify body-hash at `/app` → flip `is_visible`+`pushed_to_prod_utc`. Visibility is the **last, verified** step.

### 4. No prod-side / cross-store reconciliation → build one
- **Now:** `ContentKbOrphanScanner` is local-only, CLI-only, manual (`ContentKbCommandRunners.cs:372`). Nothing checks prod published-orphans, orphan `.md` files, or seed↔DB drift.
- **Pattern:** a per-row content-hash fingerprint join surfaces all three orphan classes (row-no-file, file-no-row, stale/unpublished) cheaply; the reconciliation run must itself be **idempotent** — deterministic discrepancy IDs upserted, resolution inferred by absence, partial runs scope-tagged so they don't false-resolve [4][5].
- **Fix:** a **reconcile command that runs against PROD** joining prod `content_site_index` ↔ git tree ↔ `index-seed.json`, emitting: published-orphans (visible row, no body), file-orphans (`.md`, no row), seed-drift (prod row absent from seed), and body-hash mismatches. Idempotent discrepancy store, resolution-by-absence, scope tags. Extend the existing scanner rather than new-build. **This is exactly what would have flagged the 63 unpublished rows and the CP437 mojibake before they hit prod.**

### 5. Two body-blind checksums → one body-inclusive signature
- **Now:** DirectPush uses `ContentSiteIndexContentSignature` (index cols); Pull uses the weaker `ContentSyncDiffClassifier` fingerprint (title+path+tags). **Neither hashes the `.md` body** → mojibake / stale body with an unchanged index row is invisible.
- **Pattern:** one per-row content-hash over **body + index fields**, cryptographic (not CHECKSUM); join on id, compare the hash column [4]. Postgres equivalent = `digest()`/pgcrypto or app-side SHA (the HASHBYTES examples are SQL-Server-specific) [caveat].
- **Fix:** add a **`body_sha256` column** on `content_site_index`, computed from the `.md` at publish. Unify DirectPush + Pull + reconcile on ONE signature that includes it. Now drift = one indexed comparison, and it catches body corruption. (Also lets the app **refuse to render** a row whose file hash ≠ stored hash — closes #1's residual window.)

### 6. Two publish paths, opposite consistency, premature stamp → converge them
- **Now:** Publish stamps "Published" locally at commit time before deploy (`PublishCoordinator.cs:217`); DirectPush is immediate-but-seed-stale. Operators can mix them into inconsistent states.
- **Pattern:** idempotent one-way keyed UPSERT (`ON CONFLICT DO UPDATE` on the natural key), stamp only after the authoritative store confirms [2][3][6].
- **Fix:** make DirectPush converge to the **same end-state** Publish produces (seed-writing + body-committing + hash-gated, #2/#3/#5), so the two paths differ only in speed, not consistency. **Stamp `pushed_to_prod_utc` only after prod confirms** the deployed body — never at local commit time. (This also fixes the "23 rows Never-published badge but live" symptom: seeded rows never got a real pushed stamp because stamping and true prod state were decoupled.)

### 7. Pull clobbers/misreads → master-per-field + current checkout
- **Now:** Pull mirrors `approval_status` but not `is_visible`/`is_hidden` (`PullFromProdCoordinator.cs:165-169`) and resolves bodies from the local git working tree (`:103`) — correct only if the checkout is current.
- **Pattern:** designate a master; for DB-only operator fields with no git counterpart, preserve them rather than clobber; ignore self-originated writes to avoid echo loops [6][7]. (Research open-question #4 is exactly this.)
- **Fix:** Pull does `git pull` first (or warns if behind). **Per-field master:** body+content ← git; DB-only operator fields (`is_visible`/`is_hidden`/`approval_status`) ← prod, preserved. Surface any body-vs-index divergence to the operator instead of silently adopting.

### 8. No round-trip test → add one
- **Now:** every mover is unit-tested with fakes; nothing asserts DirectPush→prod→Pull or that served-body resolution matches the upload target.
- **Fix:** an integration test spanning DirectPush → prod store → PullFromProd (containerized Postgres + real git tree), asserting served-body resolution == published body and `body_sha256` matches end to end.

## How this ties together every KB incident from this session
- **63 unpublished prod rows we deleted** = stale/unpublished orphans with no reconcile (#4).
- **CP437 mojibake live on prod** = body drift invisible to index-only checksums (#5).
- **23 rows "Never published" badge but live** = `pushed_to_prod_utc` decoupled from true prod state (#6).
- **Pull-from-Prod git-decouple fix** (`0dd49f19`) = already the right direction (#7); DirectPush's twin `/data` bug is #1, still open.

## Recommended sequencing (a small fix cycle)
1. **#5 body-hash column + unified signature** (foundational — everything else uses it).
2. **#2 + #3 DirectPush writes seed + drops `/data`, hash-gated ordering** (kills the live-broken-body + seed-drift classes).
3. **#4 prod-side idempotent reconcile command** (visibility into remaining drift; catches regressions).
4. **#6 converge publish stamping** + **#7 Pull per-field master**.
5. **#8 round-trip integration test** locking it in.

Scope-realistic for the 512MB/Render/Postgres tier — no CDC/Kafka; all patterns are upsert + hash + ordering [caveat]. Prereq: DirectPush stays the only out-of-band prod writer, so fixing it fixes the blast radius.

## Codex cross-check (independent gpt-5.4 pass) — 4 additional bugs + 1 adjudicated disagreement

An independent Codex research pass corroborated all 8 weaknesses above and surfaced **4 real bugs this audit missed** (now folded into the roadmap):

- **C1 — approval hardcoded `pending`.** `UpsertContentColumnsOnlyBatchAsync` insert path forces `approval_status='pending'` (`ContentSiteIndexStore.cs:991-1027`) and `WritePublishAsync` never mirrors local approval to prod (`DirectPushCoordinator.cs:193-205`) → DirectPush can make a row **visible while still 'pending'**, and Pull mirrors that drift back locally. Live-drift bug.
- **C2 — seed reload is additive-only, never deletes.** `ContentKbSeedLoader` only upserts present entries (`:58-62`); a row removed from the seed **lingers in prod forever**. Publish computes `removedCount` (`PublishCoordinator.cs:173-183`) but nothing acts on it. Extends weakness #4.
- **C3 — sharper #2: routine every-deploy revert.** Because DirectPush leaves the seed stale, the next normal deploy re-upserts the committed seed and `UpsertRowPreservingVisibilityAsync` **overwrites DirectPush'd content columns back to stale** (`ContentKbSeedLoader.cs:58-62`, `ContentSiteIndexStore.cs:944-988`). Not just a rebuild risk — a standing revert on every deploy.
- **C4 — PinId key collision + DDL-on-prod comment lie.** `ContentSyncDiffClassifier` keys by `PinId` only, not `(natural_key_type, natural_key_value)` (`:76-93`) → a YouTube ID and podcast GUID sharing text collide. Minor: DirectPush's "no DDL against prod" claim is false — its diff read calls `EnsureSchemaAsync` → runs DDL on prod (`DirectPushCoordinator.cs:88-97`).

**Adjudicated disagreement — Pull bodies.** Codex flagged "PullFromProd doesn't download prod bodies" as a weakness and recommended rebuilding it to SFTP-download prod bodies into staging. **Rejected.** Under git-as-body-source-of-truth [1], the correct body source for a Pull IS the git tree (after `git pull`), not prod — and prod `/data` is empty by design (the deliberate `0dd49f19` decouple fix). Codex lacked that context. The real fix stays #7: ensure the local checkout is current; do NOT fetch bodies from prod. This is a case where the external pattern breaks the tie in git-SoT's favor.

## References
[1] Decap/TinaCMS git-as-source-of-truth — tina.io, decapcms.org, cloudcannon.com, red-gate.com/simple-talk
[2] Keyed upsert idempotency — Postgres `ON CONFLICT DO UPDATE`; medium/@danthelion; dev.to/137foundry
[3] Client idempotency key (Stripe / IETF Idempotency-Key draft) — servicenow.com dev article
[4] Row content-hash fingerprint drift detection — greglow.com, sqlserverplanet.com, datafold.com
[5] Idempotent reconciliation (deterministic discrepancy IDs, resolution-by-absence, scope tags) — dev.to/137foundry
[6] One-way > bidirectional; silent lost-updates — zigiwave.com, stacksync.com
[7] Master-source + origin-tagging to prevent echo loops — zigiwave.com, stacksync.com
[8] Expand-contract deploy ordering + Flyway checksum validation — oneuptime.com, medium/@matheus.nougueira
[R1] REFUTED (0-3): schema-version-race framing mapping to prod joining a new row against a not-yet-deployed body — do not rely on this framing; expand-contract ordering stands independently.
[caveat] Most sources are vendor/tech blogs; claims are timeless engineering consensus corroborated by primaries (Postgres docs, RFC 9110, Flyway, DDIA). HASHBYTES is SQL-Server-specific → use Postgres `digest()`/app-side SHA. Cryptographic hash avoids CHECKSUM collision risk.
