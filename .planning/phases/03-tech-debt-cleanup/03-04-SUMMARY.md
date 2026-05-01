---
phase: 03-tech-debt-cleanup
plan: 03-04
subsystem: security
tags: [forwarded-headers, rate-limit, tests, documentation, tech-debt]
requires:
  - phase: 03-tech-debt-cleanup
    provides: TD-04 forwarded-headers hardening decision
provides:
  - Path B-rawpeer feedback-submit partition key helper in DeckFlow.Web/Program.cs
  - New ForwardedHeadersOptionsTests coverage for spoofed X-Forwarded-For independence
  - README operational note for Render edge/global feedback partition behavior
affects:
  - DeckFlow.Web/Program.cs
  - DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs
  - README.md
key-files:
  created:
    - DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs
    - .planning/phases/03-tech-debt-cleanup/03-04-SUMMARY.md
  modified:
    - DeckFlow.Web/Program.cs
    - README.md
key-decisions:
  - "Task 1 research outcome: CIDR NOT FOUND; Render docs checked on 2026-04-30 and do not enumerate inbound edge CIDRs"
  - "User disposition: option-b-rawpeer"
  - "Kept forwarded-header middleware order unchanged"
  - "Used immediate-peer IP for feedback-submit partitioning instead of X-Forwarded-For-derived identity"
patterns-established:
  - "Forwarded-header spoofing cannot rotate the feedback-submit rate-limit key"
requirements-completed:
  - TD-04
metrics:
  duration: ~1h
  completed: 2026-05-01
---

# Phase 03 Plan 04 Summary

**Path B-rawpeer was implemented in `Program.cs`, the spoof-resistance invariant is covered by a unit test, and the Render operational note was added to the README.**

## Task 1 Research

- Outcome: **CIDR NOT FOUND**
- Sources checked on 2026-04-30:
  - `https://render.com/docs/inbound-ip-rules`
  - `https://feedback.render.com/features/p/send-the-correct-xforwardedfor`
- Conclusion: Render documents customer allowlists and X-Forwarded-For behavior, but not a published enumerable inbound proxy CIDR list.
- User-confirmed disposition: `option-b-rawpeer`

## Program.cs Diff Excerpt

```diff
- // Render assigns dynamic proxy IPs we can't enumerate; clear the defaults so forwarded
- // headers from any upstream are honored.
- options.KnownIPNetworks.Clear();
- options.KnownProxies.Clear();
- options.AddPolicy("feedback-submit", httpContext =>
- {
-     var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
-     return RateLimitPartition.GetFixedWindowLimiter(...);
- });
+ // Note (TD-04, Phase 03 SC #4, retrieved 2026-04-30): Render does not publish enumerable
+ // inbound proxy CIDR ranges ...
+ // Default loopback entries (127.0.0.1, ::1) preserved - do NOT call Clear().
+ options.AddPolicy("feedback-submit", httpContext =>
+     RateLimitPartition.GetFixedWindowLimiter(
+         DeriveFeedbackPartitionKey(httpContext),
+         _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0, AutoReplenishment = true }));
```

## Test File Diff Excerpt

```csharp
public sealed class ForwardedHeadersOptionsTests
{
    [Fact]
    public void DeriveFeedbackPartitionKey_IgnoresForwardedForHeader()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Forwarded-For"] = "1.2.3.4";
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        var key = Program.DeriveFeedbackPartitionKey(ctx);

        Assert.DoesNotContain("1.2.3.4", key);
        Assert.Contains("10.0.0.1", key);
        Assert.StartsWith("peer:", key);
    }
}
```

## README Diff Excerpt

```markdown
### Feedback rate-limit identity (forwarded-headers hardening)

The feedback-submit rate-limit policy in `DeckFlow.Web/Program.cs` derives its
partition key from the immediate-peer IP rather than the `X-Forwarded-For`-derived
value. Render does not publish enumerable inbound proxy CIDRs ...
```

## Build Notes

- Local `dotnet build DeckFlow.sln` from this orchestrator's WSL2 shell — Build succeeded, **0 errors, 0 warnings**.
- New `DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs` compiles and is part of `dotnet test` discovery (executed by CI per project convention; VSTest is unreliable in WSL).

## Local curl smoke (Task 3 step 2)

Captured at `/tmp/td04-local-curl.log`:
```
=== local curl smoke ===
home 200
x-fwd=1.2.3.4 status=200
x-fwd=5.6.7.8 status=200
x-fwd=9.10.11.12 status=200
x-fwd=13.14.15.16 status=200
x-fwd=17.18.19.20 status=200
x-fwd=21.22.23.24 status=200
```
- Home page renders.
- 6 forged `X-Forwarded-For` GETs to `/feedback` return 200 (expected — rate limit is on `POST /feedback/submit`, not `GET /feedback`).

## Task Commits

- Implementation: `70e01d2` (`tech-debt(03-04): partition feedback rate-limit by immediate-peer IP — Path B-rawpeer (TD-04)`)
- Summary commit: separate docs commit (next).

## Follow-Up

- **Post-deploy human verification (Task 3 §how-to-verify steps 3-5) — PENDING.** After this commit reaches `origin/main` and Render redeploys, the user must run the spoofed-`X-Forwarded-For` curl loop against `https://www.deckflow.gg/feedback/submit` and confirm at least one `429`.
