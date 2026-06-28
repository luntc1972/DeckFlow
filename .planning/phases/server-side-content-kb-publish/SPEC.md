# Phase: Server-Side Content-KB Publish (Design B)

**Status:** 🗒️ BACKLOG — recorded 2026-06-28 for a future milestone (deferred from
the Studio best-practice sweep; was report item #11). NOT started; no branch.
**Design fork:** B chosen (full bundle, no SCP) — options A/B in the Studio
best-practice report `scratchpad-research/studio-improvement-best-practice-report.md`.
**Plan review:** Codex (gpt-5.4 medium) reviewed this plan 2026-06-28 — design
APPROVED ("keep B"), 4 HIGH baked in (see "v2 — Codex plan-review hardening").
**Promote via:** `/gsd-review-backlog` at the next cycle open, then
`/gsd-plan-phase` (the waves below are the intended breakdown). Branch off main
(or rebase over the Studio H1 branch if that has not merged yet).

> Origin note: this is item "#11" from the Studio assessment. The companion #10
> (reconsider the Blazor Server shell) was deliberately NOT taken — H2's loopback
> guard already closed its security driver, leaving only low-payoff ergonomics.



Goal: move the DeckFlow Studio **DirectPush prod-WRITE** path off the operator
laptop. Studio stops writing prod Postgres directly and stops SCP-uploading
artifacts; instead it POSTs an approved bundle (index rows + artifact text) to an
authenticated endpoint on deckflow.gg (DeckFlow.Web), which already runs on
Render with the same prod Postgres + `/data` disk. Web writes the artifacts to
its own `/data` and performs the transactional DB upsert + stamp + visibility.

## Scope

IN:
- DirectPush WRITE path (SCP upload + direct prod DB upsert/stamp/visibility).
- The prod READ used by DirectPush's ComputeDiff (move server-side so the laptop
  holds no prod connection string at all for DirectPush).

OUT (explicit, follow-up):
- PullFromProd (read + SCP-download). It is read-only toward prod; migrating it
  is a separate phase. Until then the laptop MAY still hold read-only/SCP creds
  for PullFromProd — so "zero prod creds on laptop" is true for the WRITE path
  only, not globally. State this in the PR.
- The Publish-to-Git page (separate publish path, unaffected).

## Architecture

Two authenticated machine-to-machine endpoints on DeckFlow.Web, called by Studio
over HTTPS:

1. `GET  /api/content-kb/prod-rows`  → returns all prod ContentSiteIndexRows as
   JSON. Read-only. Studio classifies the diff locally (it already has local
   rows + ContentSyncDiffClassifier).
2. `POST /api/content-kb/publish`    → body = PublishBundleRequest. Web writes
   each artifact text to `{dataRoot}/{Row.ArtifactPath}` (containment-guarded),
   then runs the existing transactional `UpsertContentColumnsOnlyBatchAsync` +
   `StampPushedToProdAsync` + `SetVisibilityAsync`. Returns per-row result.

### Auth (NOT browser; machine-to-machine)
- Dedicated bearer secret `DECKFLOW_PUBLISH_TOKEN` (Render dashboard, sync:false),
  NOT the human Basic-auth admin password (least-privilege, independently
  rotatable). Sent by Studio as `Authorization: Bearer <token>` or
  `X-Publish-Token`.
- New minimal endpoint filter/middleware validates the token with a fixed-time
  compare; reuse `IAdminBruteForceTrackerStore` throttle keyed on CF-Connecting-IP.
- These are API endpoints with NO browser origin, so SameOrigin/antiforgery do
  NOT apply; the bearer token is the guard. Endpoints return 401 (bad/missing
  token) / 429 (throttled). If `DECKFLOW_PUBLISH_TOKEN` is unset, endpoints
  return 503 (publish disabled) — never an open endpoint.
- HTTPS only (UseForwardedHeaders already in place); reject non-HTTPS.

### Request size / resource guard (512MB RAM, 1GB disk)
- Hard cap on request body size (e.g. 8 MB) + max entries (e.g. 500) → 413 if
  exceeded. Artifact .md files are small (distilled KB entries); publish sets are
  New+Updated only. Reject oversized rather than risk the web tier.

### Artifact write safety
- Reuse the path-containment guard (reject rooted / `..` / escape-outside-data-root).
  Currently duplicated in Studio (ReviewCoordinator.ReadArtifactSafe) and the SFTP
  session base. Extract a shared `ContentArtifactPathGuard` in DeckFlow.Core and
  use it on BOTH the Web write side and the existing Studio read side. (Dedup win.)
- Write order: all artifacts first, then the DB transaction (matches current
  SCP-then-DB ordering; a DB rollback leaves orphan artifact files exactly as the
  current SCP-then-DB-fail path does — acceptable, documented).

## Shared contracts (DeckFlow.Core)
- `PublishBundleRequest { IReadOnlyList<PublishBundleEntry> Entries }`
- `PublishBundleEntry { ContentSiteIndexRow Row; string ArtifactText }`
- `PublishResult { IReadOnlyList<PublishRowResult> Rows; bool Success }`,
  `PublishRowResult { Title, KeyType, KeyValue, bool Success, string? Reason }`
  (Reason sanitized — never DB exception text; D-07.)
- `ContentArtifactPathGuard.ResolveSafe(dataRoot, relativeArtifactPath)` →
  absolute path or null (containment).

## Web (DeckFlow.Web)
- `Controllers/Api/ContentKbPublishController.cs` — the two endpoints, token guard,
  size cap, `[FromBody]`.
- `Services/ContentPublishService.cs` (IContentPublishService) — writes artifacts
  via guard + calls the already-registered IContentSiteIndexStore batch methods.
  Web currently only READS/curates content-kb; this adds a guarded WRITE surface —
  the central new risk to review.
- `Infrastructure/PublishTokenAuth` — token validation + throttle.
- Program.cs: register the service + map endpoints; read `DECKFLOW_PUBLISH_TOKEN`.

## Studio (DeckFlow.Studio)
- DirectPushCoordinator: replace `ComputeDiffAsync` prod read with a call to the
  `prod-rows` endpoint (HttpClient); replace `UploadArtifactsAsync` (SCP) +
  `WritePublishAsync` (direct prod DB) with one `PublishToServerAsync` that builds
  the bundle (reads local artifact text) and POSTs to `/publish`.
- Remove from DirectPush path: ISshArtifactUploader, IProdStoreFactory,
  `Studio:ProdConnectionString`, `Studio:Scp:*` (KEEP these for PullFromProd until
  that is migrated — verify no shared-only removal breaks PullFromProd).
- New Studio config: `Studio:PublishEndpointBaseUrl`, `Studio:PublishToken`
  (user-secrets). The token is the ONLY new secret on the laptop; the prod DB
  write string is gone.
- DirectPush.razor.cs/UI copy unchanged in shape; the "see logs" + sanitized error
  mapping stays.

## Tests
- Core: PublishBundleRequest round-trip; ContentArtifactPathGuard (rooted/.. /escape).
- Web: ContentPublishService (happy path writes artifacts + calls batch upsert;
  rollback leaves UI-safe result; oversize → reject; bad token → 401; missing
  token env → 503). xUnit + MockHttp pattern.
- Studio: DirectPushCoordinator bundle build + endpoint call via the existing
  HttpClient test seam (no live HTTP); bUnit DirectPush page stays green.

## Rollout
- New flag `directpush.server-side-publish` (default OFF) gating the Studio path:
  OFF = legacy SCP+direct-write (until verified), ON = new endpoint path. Lets us
  ship dark and flip after prod verification. (Web endpoints can ship live; they
  do nothing until Studio calls them with a valid token.)
- Seed `DECKFLOW_PUBLISH_TOKEN` in Render BEFORE flipping the flag.

## Definition of Done
- Studio publishes a real approved set through the endpoint to prod (manual e2e).
- No prod DB write string + no SCP-upload key on the laptop for DirectPush.
- Web build 0/0; Web.Tests + Core.Tests + Studio.Tests green; format-gate clean.
- README + STUDIO-SETUP/PROD-CONNECTION docs updated (new token, removed keys).
- Codex code-review per commit; address HIGH before merge.
- Public repo: no secrets committed; token only in Render + local user-secrets.

## Side Effects Report
**Files (direct):** new Web controller+service+token-auth+DI; new Core DTOs +
path guard; Studio DirectPushCoordinator rewrite + config; docs.
**Transitive:** Web now exposes a prod-WRITE API (new attack surface) — auth +
size cap + containment are the mitigations to review hardest. Studio HttpClient
gains a new upstream (the publish endpoint) — needs the resilience pattern.
**Shared state / external:** prod Postgres (same DB Web already reads); `/data`
disk (Web already mounts); new Render secret `DECKFLOW_PUBLISH_TOKEN`.
**Contract changes:** new API contract (versioned path `/api/content-kb/*`);
new Core DTOs; ContentArtifactPathGuard extracted (Studio read side switches to
it — verify identical behavior).
**Backward-compat:** flag-gated; legacy path intact until flipped. Web endpoints
inert without a token. PullFromProd untouched.
**Risks / open Qs:**
- R1: Web getting a write path to prod content-kb is the core risk. Token +
  fixed-time compare + throttle + size cap + containment guard + 503-when-unset.
- R2: orphan artifacts on DB rollback (same as today's SCP path) — accept+document.
- R3: request-size cap value (8MB?) — confirm against real publish-set sizes.
- R4: token transport — Bearer header over HTTPS only; reject HTTP.
- R5: should `prod-rows` read endpoint page/limit for large indexes? (Index is
  small today; add a cap + note.)

## v2 — Codex plan-review hardening (gpt-5.4, design APPROVED, 4 HIGH baked in)

HIGH-1 Server-derives the artifact path. Do NOT trust client `ArtifactPath`.
Resolve against a dedicated `content-kb/` root, require `content-kb/<source-slug>/<key>.md`,
and derive the final path server-side from the row's natural key + source slug;
reject anything else (not just containment). Guard = `ContentArtifactPathGuard`
but the canonical path is computed, not client-supplied.

HIGH-2 Exactly-once. Add `PublishOperationId` (client-generated GUID) to the
bundle; Web persists (operation id → result) and a duplicate publish returns the
ORIGINAL result without re-running. Needed because `StampPushedToProdAsync`
mutates state → naive retry is not a no-op. New tiny store table
`publish_operations(operation_id PK, result_json, created_utc)`.

HIGH-3 Single server-side transaction. Add ONE Core store method
`PublishBatchAsync(rows, pushedUtc, CancellationToken)` that does upsert + stamp
+ visibility in a SINGLE DB transaction (today they are 3 separate calls with a
partial-failure window). The endpoint calls only this. Improves on current behavior.

HIGH-4 Valid-token abuse limits. Bad-token throttle (IAdminBruteForceTrackerStore)
is not enough. Add: per-token/per-IP request rate limit, a single in-flight
publish lock (reject concurrent publishes), and a hard server-side publish timeout.

MED deltas folded in:
- `/prod-rows` returns a MINIMAL diff DTO (natural key + content-signature columns
  only) — never full row (no IsVisible/IsHidden/IsEvergreen/ApprovalStatus/PushedToProdUtc).
- Keep `SameOriginRequestValidator` as defense-in-depth (it passes headerless
  machine callers) IN ADDITION to the bearer token.
- Temp-dir staging: write artifacts to a temp op dir, run the DB transaction,
  then atomically move into final `content-kb/` paths; clean temp on failure
  (protects the shared 1GB disk from orphan/partial writes).
- Rollout split into 3 distinct stages (dark-ship endpoints / flip Studio flag /
  remove old creds+legacy path) — laptop keeps legacy write creds ONLY until the
  bake window closes, then they are removed.
- NO web feature flag for the endpoint (FeatureFlagCache fails OPEN on missing
  key). Gate purely on `DECKFLOW_PUBLISH_TOKEN` unset => 503. The Studio-side flag
  `directpush.server-side-publish` is fine (it fails safe to legacy).
- Verify the PullFromProd SSH key is genuinely read-only; do not claim SCP-write
  removal until it is.
- Publish POST must NOT auto-retry (dedicated client, explicit timeout). On an
  unknown outcome the UI tells the operator to check the server audit by op id.

LOW: single `Authorization: Bearer` header; no `/Admin` Basic coupling; no CORS;
audit-log {op id, caller IP, row count, byte count, natural keys, outcome,
duration} — never artifact text or token.

## Revised wave/commit sequence (Codex-endorsed)
1. Core: contracts (PublishBundleRequest/Entry, minimal ProdRowDiffDto,
   PublishResult), `ContentArtifactPathGuard` (server-derived path),
   `PublishBatchAsync` single-tx store method, publish_operations store. + tests.
2. Web: token auth + rate-limit + in-flight lock + audit + 503-when-unset; deploy
   with token UNSET (endpoint returns 503). + tests.
3. Web: ContentPublishService (temp-stage artifacts → PublishBatchAsync →
   commit move) + idempotency + the two endpoints. + tests (timeout/replay/partial).
4. Studio: DirectPushCoordinator behind `directpush.server-side-publish` flag;
   local rows updated only after acknowledged server success. + tests.
5. Prod canary: deploy Web, verify 503-when-unset, seed token, one small publish,
   KEEP legacy creds.
6. After bake: flip Studio flag, then remove old prod write string + SCP-upload +
   legacy direct-write path; docs + secret rotation.
