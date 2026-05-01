---
status: partial
phase: 03-tech-debt-cleanup
source: [03-VERIFICATION.md, 03-04-PLAN.md §Task 3]
started: 2026-05-01
updated: 2026-05-01
---

## Current Test

Awaiting human testing post-deploy (gated on pushing the 13 phase-03 commits to `origin/main` and Render auto-redeploy).

## Tests

### 1. Spoofed-`X-Forwarded-For` does not bypass feedback rate limit (SC #4 / TD-04)
expected: Six rapid `POST /feedback/submit` requests with six different forged `X-Forwarded-For` values share the same partition key (Render's edge IP) and the 5/hr fixed-window limiter trips, returning **at least one HTTP 429** in the run.
result: pending

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
result: pending

```bash
for path in / /feedback /help /about /sync; do
  curl -sS -o /dev/null -w "%{http_code} $path\n" "https://www.deckflow.gg$path"
done
```
All five MUST return 200 (302 acceptable on /sync if that path redirects to /).

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps

(none yet — pending live verification)
