# DATA-01 Decision — Content-KB Body Serving Path

**Phase:** 65 — Prod Content Artifact Reconcile
**Requirement:** DATA-01
**Decided:** 2026-06-22
**Status:** Confirmed by code inspection (resolver-base live-log confirmation = operator follow-up, Task 2)

---

## Decision

The live site serves content-KB body from **`/data` `.md` artifact files**, read from the
filesystem at request time via `System.IO.File.ReadAllTextAsync`. There is **no DB content/body
column** in `content_site_index` — the database stores only metadata plus a relative
`artifact_path` pointer.

Therefore the 86 orphaned rows are rows whose `.md` artifact is missing from the prod `/data`
disk. Severity depends entirely on visibility: only a row that is **published**
(`is_visible = TRUE AND is_hidden = FALSE`) produces a user-visible defect.

---

## Evidence (code citations)

### Serving path reads the file, not a DB column

`DeckFlow.Web/Controllers/ContentKbController.cs` — `Detail` action:

- `:109` — `var resolved = _resolver.ResolveArtifactFullPath(row.ArtifactPath);`
- `:115` — `if (!System.IO.File.Exists(resolved)) { _logger.LogWarning(...); return View("Detail", BuildDetailModel(row, new HtmlString(string.Empty), string.Empty, artifactUnavailable: true)); }`
- `:121` — `var raw = await System.IO.File.ReadAllTextAsync(resolved, cancellationToken).ConfigureAwait(false);` then `ContentArtifactParser.SplitHeader(raw)` → Markdown render.

The body string comes from the filesystem. The DB row (`_store.GetByIdAsync`) supplies only
metadata (title, source, tags, visibility, `artifact_path`). No content/body column is consulted.

### No content/body column exists in the schema

`DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `EnsureSchemaAsync` DDL (both Postgres and
SQLite variants). The `content_site_index` columns are metadata only:

```
id, source, title, video_url, artifact_path, published_utc, pushed_to_prod_utc,
indexed_utc, archetype_tags, bracket_tags, card_category_tags,
natural_key_type, natural_key_value, is_visible, is_hidden, is_evergreen, approval_status
```

There is **no `content`, `body`, or `full_text` column**. A DB content column cannot be a serving
source because it does not exist.

---

## Failure modes for a missing artifact

| Row state | Detail page behavior |
|-----------|----------------------|
| `is_visible = TRUE` (and not hidden), artifact missing | Detail page renders metadata only with `ArtifactUnavailable: true` — **user-visible defect** (no body) |
| `is_visible = FALSE` | `Detail` returns `NotFound()` (404) — not reachable from public browse; artifact absence is invisible to users |

The public browse page (`Index`, `GetPublishedRowsAsync`) lists `is_visible = TRUE` rows
regardless of artifact presence; artifact absence only surfaces on the individual detail page.

---

## Resolver-base confirmation procedure

The prod artifact base is resolved by `ContentKbArtifactPathResolver`
(`DeckFlow.Web/Services/Content/ContentKbArtifactPathResolver.cs`). On startup it logs (`:31`):

```
Content KB content base resolved to {ContentBase}; content-kb exists: {ContentKbExists}.
```

**Operator check (Task 2):** Open the Render dashboard for the `mtg-deck-studio` web service →
Logs → search recent startup for `Content KB content base resolved to`. Record `{ContentBase}` and
the `content-kb exists` boolean.

- If base is `/data` and `content-kb exists: True` → DATA-01 resolver path fully confirmed live.
- If base is NOT `/data` (e.g. `/app`) → remediation: set `ContentKb__ContentBase=/data` in Render
  env vars (operator action). Per RESEARCH Pitfall 4 / Open Question 1: a wrong base means even a
  re-uploaded artifact at `/data/content-kb/...` would not be read.

### Resolver-base confirmation (live) — CONFIRMED 2026-06-22

Render web service `DeckFlow` (`srv-d7gmufkp3tds73a29m30`, deploys from **`main`**) startup log
(via Render MCP `list_logs`, two most recent boots):

```
[18:17:14 INF] Content KB content base resolved to /app; content-kb exists: True.
[19:02:00 INF] Content KB content base resolved to /app; content-kb exists: True.
```

**The resolver base is `/app`, NOT `/data`.** `content-kb exists: True` at `/app/content-kb`.

### Correction to the serving source — IMPORTANT

The live site serves content-KB body from **`/app/content-kb/{slug}/{id}.md`** — the repo's
committed `content-kb/` tree, baked into the Docker image at build (`Dockerfile` COPYs the repo into
`/app`; `content-kb/` is git-tracked, 397 `.md` files, not gitignored). The resolver candidate walk
(`ContentKbArtifactPathResolver.EnumerateCandidates`) returns the **first** candidate whose
`content-kb/` subdir exists; `ContentRootPath` = `/app` and `/app/content-kb` exists, so it wins
**before `/data` is ever considered**.

**Consequence:** the SFTP `/data/content-kb` uploads (Studio DirectPush Stage 2) are NOT the serving
source while `/app/content-kb` exists. The Phase-60 "86 rows missing from `/data`" finding was
checking the wrong location — `/data` is not what users see. The authoritative orphan set is
**visible rows whose `.md` is absent from the committed repo `content-kb/` tree** (= what's baked at
`/app`). See `65-PROBE-RESULTS.md` for that count (10).

No `ContentKb__ContentBase` env var is set on Render; the `/app` fallback is what's live. (Setting
`ContentKb__ContentBase=/data` would actually REDIRECT serving to the disk and is NOT recommended
unless the publish model is intentionally moved to `/data`.)

---

## Consequence for DATA-02

The reconcile path (Plan 02) is gated on the **published-orphan count** — the number of
`is_visible = TRUE AND is_hidden = FALSE` rows whose artifact is missing from `/data`. That count
is computed by the read-only prod probe (Plan 01 Task 3, `65-PROBE-RESULTS.md`).
