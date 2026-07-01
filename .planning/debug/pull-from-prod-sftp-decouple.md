# Bug: Pull-from-Prod artifact download fails for every content-kb row ("SSH download failed — check SCP configuration")

**Status:** diagnosed, fix planned
**Area:** DeckFlow.Studio — Pull-from-Prod workflow
**Severity:** Medium (feature unusable for its main case; misleading error; no data loss)
**Branch:** `fix/pull-from-prod-git-decouple`

## Symptom

Running Pull-from-Prod, Stage 1 log shows:

```
Reading production content_site_index...
  109 row(s) read from production.
Downloading 109 artifact(s)...
  not downloaded: content-kb/salubrioussnail/kR3C_OC5BzU.md — SSH download failed — check SCP configuration and Render SSH access.
  ... (all 109 rows fail identically)
```

The DB read succeeds (109 rows). Every artifact "download" fails with the sanitized
`SSH download failed — check SCP configuration and Render SSH access.` message.

## Root cause

The error message is **misleading**: SSH auth and connectivity are fine. The real problem is
that Pull-from-Prod SFTP-downloads content-kb bodies from the prod **`/data`** persistent disk,
but content-kb is **git-managed** and never lives on `/data`.

Verified against live prod (read-only SSH):

- `Studio:Scp:RemoteArtifactRoot` = `/data`; downloader fetches `/data/content-kb/<...>.md`.
- Prod `/data` is an ext4 mount but **empty** — `ls -la /data` returns only `.`/`..`.
- The content-kb markdown actually lives at **`/app/content-kb`** (398 `.md`), baked into the
  deploy image beside `/app/DeckFlow.Web.dll`. The exact failing file
  `salubrioussnail/kR3C_OC5BzU.md` exists there (4703 bytes).
- `content-kb/` is **git-tracked** (399 files) and the `Dockerfile` `COPY . .` ships it into
  `/app` at build. `.gitignore` ignores `artifacts/` but NOT `content-kb/`.
- `ContentKbArtifactPathResolver` (Web) resolves `ContentBase` to the first candidate dir
  containing `content-kb/`; on prod that is `/app`. So prod Web reads KB from `/app/content-kb`.
  **`/data` is never used for KB.**

Independent SSH.NET probe (isolated console, SSH.NET 2025.1.0, same secrets): `SftpClient.Connect()`
succeeds and `ListDirectory("/data")` returns only `.`/`..`. So the failure is per-file
`DownloadFile` throwing (remote path absent), not a connect/auth failure. Both paths surface the
same sanitized string (D-07), which is why it reads like an SSH-config problem.

## Why this is architectural, not a config typo

content-kb sync between local and prod is **git-based**:

- **Publish-to-Git** (`PublishCoordinator`) exports the index to `repoRoot/content-kb/seed/index-seed.json`
  and copies bodies from the Studio data tree into `repoRoot/content-kb/`, then commits. Render
  deploys the commit → bodies land in `/app/content-kb`. This is the *only* real transport for KB
  bodies to prod.
- The `/data` SFTP path (`SftpArtifactDownloader` for pull, `SftpArtifactUploader` for DirectPush)
  is the wrong mechanism for content-kb: prod bodies are git-deployed to `/app`, not written to
  `/data`. Pull can never find them there; DirectPush would upload them to a location Web never
  reads (and which is wiped on the next deploy).

## Local body source of truth

- Studio operational tree: `artifacts/studio/content-kb` (`ContentKbOrchestratorOptions.ArtifactRoot`) — partial working set (~141 md).
- Git repo working tree: `repoRoot/content-kb` — the published corpus (~697 md), == prod `/app/content-kb` at HEAD.

Because prod bodies == git HEAD, the authoritative local mirror of any prod body is
`repoRoot/content-kb/<ArtifactPath>`. Pull-from-Prod should resolve bodies from there, not SFTP.

## Fix (decouple KB pull from SFTP) — scope

**In scope: Pull-from-Prod read path.**

1. `PullFromProdCoordinator.PullAndClassifyAsync`: replace the `ISshArtifactDownloader.DownloadArtifactsAsync`
   step with **local git-tree body resolution**. Resolve `repoRoot` via `IGitRepository`, and for each
   prod row check `repoRoot/content-kb/<ArtifactPath>` (== `SeedRelative` sibling tree). Build the
   "available" set from local presence — no SSH.
2. Stamp `SyncDiffEntry.ArtifactDownloaded` (or a renamed "ArtifactAvailableLocally") from that set.
3. `ApplyAdoptionsAsync`: promote the body from the git repo tree into the Studio operational tree
   (`dataRoot/content-kb/<ArtifactPath>`) instead of from `pull-staging`. The existing
   "artifact already present locally" branch already models this outcome — keep adopt's row upsert +
   approval mirror unchanged (still LOCAL-ONLY, never writes prod).
4. Replace the misleading progress/notes: when a body is missing from the local git tree, tell the
   user to `git pull` (the real KB sync), NOT "check SCP configuration". Drop all SSH/SCP wording
   from the Pull page + its gate (the Pull page should no longer require `IsScpConfigured`).
5. Remove the now-unused `ISshArtifactDownloader` dependency from the Pull path (the uploader for
   DirectPush is untouched by this change).
6. Update `PullFromProdCoordinatorTests` + `PullFromProdPageTests` to the git-tree resolution;
   remove SSH-download expectations.

**Out of scope (follow-up, note only):** DirectPush writes KB bodies to prod `/data` via SFTP — same
architectural mismatch (Web reads `/app`, and `/data` uploads are wiped each deploy). Track
separately; do not change DirectPush in this fix.

## Safety / invariants

- Pull stays **read-only toward production** (Postgres SELECT only; no SSH writes; git tree is local).
- Adopt stays **LOCAL-ONLY** (content-columns upsert + approval mirror; never `is_visible`/`is_hidden`).
- No prod deploy or secret change required by this fix.
- `.editorconfig` changed-lines gate + LF endings preserved; five carve-outs respected.

## Verification

- Build clean (Core + Web + Studio + both Studio test projects).
- New/updated unit tests: git-tree present → available + adopt promotes; git-tree absent → honest
  "run git pull" note, adopt still upserts row.
- Manual: run Pull-from-Prod against prod; expect 109 rows classified, bodies resolved from local
  git tree, zero "SSH download failed" lines, adopt applies locally.
