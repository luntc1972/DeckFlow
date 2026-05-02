---
phase: 04-security-bug-fixes
plan: 04
status: complete
requirements: [BUG-02]
commits:
  - 7286bd3: feat(04-04) trust Render RFC1918 proxy network in ForwardedHeaders
---

# Plan 04-04 Summary — BUG-02 v2: trust Render proxy network

## What Was Wrong With Plan 04-01

Plan 04-01 added `AdminBruteForceTracker` (singleton, in-memory `ConcurrentDictionary` keyed by `"admin:" + Connection.RemoteIpAddress`) wired into `BasicAuthMiddleware` with a 10/15min throttle. Static verification passed 13/13. Live UAT revealed a fragmented bucket on prod:

```
$ for i in $(seq 1 11); do curl ... -u admin:WRONG https://www.deckflow.gg/Admin/Feedback; done
n=1 code=429 retry=253
n=2 code=429 retry=253
n=3 code=429 retry=253
n=4 code=401 retry=
n=5 code=429 retry=251
n=6 code=401 retry=
n=7 code=429 retry=251
n=8 code=401 retry=
n=9 code=401 retry=
n=10 code=401 retry=
n=11 code=429 retry=252
```

5×401 + 6×429 with **Retry-After ascending** (251 → 252 within 1-second burst). A single bucket with monotonic time would only show monotonically *decreasing* Retry-After. Mixed pattern proves multiple distinct partition keys for the same client.

## Root Cause

Per Render docs research (2026-05-01):
- Render Starter + persistent disk = guaranteed single-instance (no horizontal scaling).
- BUT Render's edge load balancer fronts that single instance with **multiple internal proxy IPs** in the RFC1918 10.x.x.x range.
- Each proxy IP appears as a distinct `Connection.RemoteIpAddress` → distinct partition key → fragmented bucket → ~50% effective throttle.

Phase 03 TD-04's "Path B-rawpeer" assumption (Render proxy = stable peer IP) was wrong. Phase 03's local smoke test happened to hit a single bucket and missed the multi-proxy-IP fanout.

## What Was Fixed

`Program.cs` `Configure<ForwardedHeadersOptions>`:
- Added `options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8))`
- Added `172.16.0.0/12` and `192.168.0.0/16` for full RFC1918 coverage
- Set `options.ForwardLimit = 1` explicitly (default; documented to prevent silent regression)
- Loopback (127.0.0.1, ::1) trust is preserved (not cleared) — required for Kestrel internal health checks

After `UseForwardedHeaders` runs, `Connection.RemoteIpAddress` is peeled to the X-Forwarded-For value Render's proxy appended (= real client IP). All N proxy IPs collapse to one client bucket.

### Why This Is Spoof-Safe

`ForwardLimit=1` instructs ASP.NET to consume only the **rightmost** X-Forwarded-For entry — the one written by the trusted Render proxy. An attacker sending `X-Forwarded-For: 1.1.1.1, 2.2.2.2, 3.3.3.3` will have those values prepended; Render's edge appends the real client IP. ASP.NET takes only the rightmost = real client IP. Spoofed entries are never consumed.

`KnownIPNetworks` (RFC1918) gates this entire mechanism: if the immediate TCP peer is NOT in a trusted network, ASP.NET refuses to honor any X-Forwarded-For at all. So even if an attacker reached Kestrel directly somehow, header injection would silently no-op.

### Carry-Over Effect on Phase 03 TD-04

The feedback-submit rate-limit also reads `Connection.RemoteIpAddress` via `DeriveFeedbackPartitionKey`. With the trust list expanded, that policy now correctly partitions by real client IP cross-proxy — closing a latent fragmentation bug that was never load-tested in Phase 03. Path B-rawpeer is no longer load-bearing; same code path, but the underlying property now resolves to the real client IP. The unit test `DeriveFeedbackPartitionKey_IgnoresForwardedForHeader` still passes (it tests the helper, not the middleware pipeline).

See `.planning/phases/03-tech-debt-cleanup/03-04-SUMMARY.md` Phase 04 addendum for the cross-link.

## Commits

```
7286bd3 feat(04-04): trust Render RFC1918 proxy network in ForwardedHeaders (BUG-02 v2)
```

(Single feat commit; no test changes — existing AdminBruteForceTrackerTests use `TestServer` with loopback peer IP, which remains correctly trusted.)

## Verification

- Local: `dotnet build DeckFlow.sln -m:1 -p:BuildInParallel=false` clean (0 errors, 0 warnings)
- Live: Pending push + Render redeploy ~10 min, then re-run BUG-02 11-burst. Expected clean 10×401 + 1×429, monotonically decreasing Retry-After. After 15-min wait, single curl returns 401 (window reset).

## Files Modified

- `DeckFlow.Web/Program.cs` (+8, -1)
- `.planning/phases/03-tech-debt-cleanup/03-04-SUMMARY.md` (Phase 04 addendum appended)

## Sources Consulted

- [Render Inbound IP Rules](https://render.com/docs/inbound-ip-rules)
- [Render Scaling](https://render.com/docs/scaling)
- [Render Zero-Downtime Deploys](https://render.com/articles/how-render-handles-zero-downtime-deploys)
- [Render X-Forwarded-For feedback ticket](https://feedback.render.com/features/p/send-the-correct-xforwardedfor)
- [adam-p: perils of "real" client IP](https://adam-p.ca/blog/2022/03/x-forwarded-for/)
- [Anthony Simmon: securely reverse-proxy ASP.NET Core](https://anthonysimmon.com/securely-reverse-proxy-aspnet-core-web-apps/)
