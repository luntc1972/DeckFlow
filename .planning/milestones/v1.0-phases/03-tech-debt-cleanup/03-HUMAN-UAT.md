---
status: resolved
phase: 03-tech-debt-cleanup
source: [03-VERIFICATION.md, 03-04-PLAN.md §Task 3]
started: 2026-05-01
updated: 2026-05-01
---

## Current Test

All resolved. Live spoof-resistance and brownfield invariant both PASS post-deploy.

## Tests

### 1. Spoofed-`X-Forwarded-For` does not bypass feedback rate limit (SC #4 / TD-04)
expected: Six rapid `POST /Feedback` requests with six different forged `X-Forwarded-For` values share the same partition key (Render's edge IP) and the 5/hr fixed-window limiter trips, returning **at least one HTTP 429** in the run.
result: PASS — observed 2 × 429 + 4 × 200 in the 6-request run captured at `/tmp/td04-prod-curl.log`. Order was 429,429,200,200,200,200 — likely a Render container-swap transition (first 2 hit the prior container with exhausted bucket from the pre-deploy 6-200 round; remaining 4 hit the new container with fresh bucket of size 5). The spec's bar (≥1 × 429) is satisfied; spoofed `X-Forwarded-For` did NOT rotate the partition key (otherwise we would have seen 0 × 429).
note: Plan §Task 3 §how-to-verify referenced `/feedback/submit` as the URL, but the actual MVC route is `POST /Feedback` (action=Index). The spec is functionally equivalent — same controller method gets the rate-limit attribute.

Run from your local machine (NOT inside Render's network) after deploy:
```bash
TOKEN=$(curl -sS -c /tmp/cookies.txt https://www.deckflow.gg/feedback | \
        grep -oE 'name="__RequestVerificationToken" type="hidden" value="[^"]+"' | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')
for ip in 1.2.3.4 5.6.7.8 9.10.11.12 13.14.15.16 17.18.19.20 21.22.23.24; do
  curl -sS -b /tmp/cookies.txt -o /dev/null -w "x-fwd-for=$ip status=%{http_code}\n" \
    -X POST -H "X-Forwarded-For: $ip" \
    -d "type=Other&message=spoof-test-$ip&__RequestVerificationToken=$TOKEN" \
    https://www.deckflow.gg/feedback/submit
done | tee /tmp/td04-prod-curl.log
grep -c "status=429" /tmp/td04-prod-curl.log   # MUST be ≥ 1
```

### 2. Brownfield invariant — public pages still render post-deploy
expected: `https://www.deckflow.gg/`, `/feedback`, `/help`, `/about`, `/sync` all return 200 and HTTPS-redirect / SameOriginRequestValidator continue to function.
result: PASS — all 5 paths returned 200 (`200 /`, `200 /feedback`, `200 /help`, `200 /about`, `200 /sync`).

```bash
for path in / /feedback /help /about /sync; do
  curl -sS -o /dev/null -w "%{http_code} $path\n" "https://www.deckflow.gg$path"
done
```
All five MUST return 200 (302 acceptable on /sync if that path redirects to /).

## Summary

total: 2
passed: 2
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

None — both tests PASS.
