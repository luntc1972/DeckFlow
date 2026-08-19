# User feedback

DeckFlow user-feedback and moderation guidance.

## User Feedback

A public **Feedback** form is linked in the site footer (`/feedback`). Submissions are stored through DeckFlow's relational storage provider. SQLite is the default and stores `feedback.db` at `$MTG_DATA_DIR/feedback.db` (falling back to `./artifacts/feedback.db` in development). Postgres can be enabled with the database environment variables below.

An admin page at `/Admin/Feedback` displays submissions with filters for status and type, and lets you mark items Read, Archive, or Delete them.

### Admin configuration

Set these environment variables (via the Render env var UI):

- `FEEDBACK_ADMIN_USER` — basic auth username for all `/Admin/*` pages.
- `FEEDBACK_ADMIN_PASSWORD` — basic auth password.
- `FEEDBACK_IP_SALT` (optional) — salt for hashing submitter IPs. If unset, a random 32-byte salt is generated on first run and persisted in the feedback metadata table.

Basic auth covers the whole admin shell: Dashboard (`/Admin`), Feedback, Flags, Harvest, Analytics, Content KB curation, and YouTube Export. If `FEEDBACK_ADMIN_USER` or `FEEDBACK_ADMIN_PASSWORD` are not set, `/Admin/*` returns **503 Service Unavailable**. The public `/feedback` form continues to accept submissions.

On `/Admin/Flags`, operators can narrow the table instantly in-browser with a starts-with key filter, namespace chips for `service.` and `analysis.`, and status chips (**All statuses / Enabled / Disabled**) that filter rows by their current on/off state; all three compose, and the current filter is kept in `sessionStorage` across admin page reloads within the session. Public-tool visibility flags (`tool.*`) are **not** listed here — they are administered on `/Admin/Tools` (which cascades to the home tile, nav, help, and route), so a tool flag is toggled in exactly one place.

Public submissions are rate-limited to 5 per hour per IP.

### Feedback rate-limit identity (CF-Connecting-IP, Phase 5)

The feedback-submit rate-limit policy in `DeckFlow.Web/Program.cs` derives its
partition key from the `CF-Connecting-IP` request header (set by Cloudflare to
the originating client IP). The same helper, `Program.DeriveCloudflareClientIp`,
also drives the admin brute-force throttle — single source of truth for both
surfaces.

Spoofing `X-Forwarded-For` cannot rotate the partition key (the helper does not
read that header). The Phase 03 immediate-peer-IP shape (`peer:<RemoteIpAddress>`)
was rewritten in Phase 5 because Render's edge fans inbound traffic across
multiple proxy IPs, fragmenting per-client buckets — see Phase 5 Plan 05-02.

This trust-the-header model requires that the Render container origin be
reachable only via Cloudflare; otherwise `CF-Connecting-IP` is spoofable by a
direct-to-origin attacker. See "Admin throttle" below for the Render Inbound IP
Rules prerequisite — it covers both surfaces.

If `CF-Connecting-IP` is missing on a request, the partition falls back to
`feedback:unknown` (or `admin:unknown` for /Admin/* requests) and a warning is
logged. All unidentifiable traffic shares one bucket, fail-closed.

### Admin throttle (Phase 5, BUG-02)

The `/Admin/*` routes (feedback console) are protected against basic-auth
brute-force by an application-layer throttle:

- **Lockout window:** 10 failed authentication attempts per client IP within a
  15-minute fixed window. The 11th attempt returns `429 Too Many Requests` with
  a `Retry-After` header value (seconds until window reset, in the range 1..900).
- **Persistence:** the throttle state is stored in Postgres
  (`admin_brute_force_buckets` table), so a deploy or container restart does NOT
  reset accumulated failure counts. There is no brute-force amnesty window on
  redeploy.
- **Client IP source:** the throttle partitions on the `CF-Connecting-IP`
  request header (same helper as the feedback rate-limit). Cloudflare always
  sets this to the originating client IP, so the partition key is stable per
  real client (not fragmented across the Render edge's multi-proxy IP fan-out).
- **Successful auth does NOT increment the bucket.** Only `Challenge`-emitted
  401s (missing/malformed/invalid credentials) count toward the throttle.

#### Spoof-prevention prerequisite (REQUIRED for production)

The `CF-Connecting-IP` header is trusted only because Cloudflare proxies all
inbound traffic. To prevent an attacker from reaching Render's container origin
directly and supplying a fake `CF-Connecting-IP` header, configure **Render Inbound IP Rules**
to allow only Cloudflare's published CIDR ranges:

- Render docs: https://render.com/docs/inbound-ip-rules
- Cloudflare IPv4 CIDRs: https://www.cloudflare.com/ips-v4/
- Cloudflare IPv6 CIDRs: https://www.cloudflare.com/ips-v6/

Render dashboard: deckflow service → Settings → Inbound IP Rules → add the full
Cloudflare list. Cloudflare publishes ~22 IPv4 + ~7 IPv6 CIDRs and announces
changes on the same pages. Refresh the Render allow-list manually if Cloudflare
publishes a CIDR change announcement.

Without this configuration, `CF-Connecting-IP` is spoofable by direct-to-origin
hits and the throttle can be evaded by rotating the header value per request.

#### Operational notes

- Both the admin throttle (`/Admin/*`) and the feedback-submit rate-limiter
  (`POST /feedback`) read from the same `CF-Connecting-IP`-derived partition
  function (`Program.DeriveCloudflareClientIp`), so the spoof-prevention
  requirement covers both surfaces.
- The throttle table grows lazily — one row per distinct partition key. Stale
  rows reset themselves on the next `RecordFailureAsync` after their 15-minute
  window has elapsed. No periodic cleanup job is required.

### Database storage

Feedback and category knowledge/cache storage can use either SQLite or Postgres.

SQLite is the zero-config default:

- unset `DECKFLOW_DATABASE_PROVIDER`, or set `DECKFLOW_DATABASE_PROVIDER=Sqlite`
- optional `DECKFLOW_DATABASE_CONNECTION_STRING`
- if no SQLite connection string is set, DeckFlow stores `feedback.db` and `category-knowledge.db` under `MTG_DATA_DIR`, falling back to `../artifacts`

Postgres is intended for hosted deployments where local files should not be the source of truth:

- `DECKFLOW_DATABASE_PROVIDER=Postgres`
- `DECKFLOW_DATABASE_CONNECTION_STRING=<Postgres connection string>`

DeckFlow creates its feedback and category/cache tables and indexes automatically on first use. You only need to provide the Postgres database, user, and connection string.

`DECKFLOW_DATABASE_CONNECTION_STRING` accepts either Npgsql key=value form (`Host=...;Username=...;Password=...;Database=...`) or a libpq URI (`postgresql://user:pass@host:port/db`, the default format Render and most managed Postgres providers hand out). URIs are normalized internally; URL-encoded passwords and `?sslmode=require` query params are honored.

### Postgres integration tests

By default, `dotnet test` skips Postgres integration tests because they require Docker.

To run them:

1. Ensure Docker (Desktop on Windows/macOS, daemon on Linux) is running and reachable from the test process. On WSL, enable Docker Desktop's WSL integration.
2. Set the env var: `DECKFLOW_POSTGRES_TESTS=1`
3. Run: `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~PostgresStorageTests"`

Testcontainers.PostgreSql will start a `postgres:16-alpine` container, run the tests against the live database, and dispose the container at the end.

