---
status: partial
phase: 04-security-bug-fixes
source: [04-01-PLAN.md §Task 4, 04-02-PLAN.md §Task 3]
started: 2026-05-01
updated: 2026-05-01
---

## Current Test

Awaiting Render auto-deploy of phase 04 commits + human walk on deckflow.gg.

## Tests

### 1. Admin brute-force throttle (BUG-02 / SC #1)

expected: 11th failed admin auth attempt from one IP returns HTTP 429 with non-empty `Retry-After` header; first 10 still return 401; existing per-challenge warn log keeps firing.

result: pending

procedure (run from a workstation, NOT Render-internal):

```bash
for i in $(seq 1 11); do
  curl -sS -o /dev/null \
    -w "attempt=%{http_code} retry=%header{retry-after}\n" \
    -u admin:WRONGPASSWORD \
    https://www.deckflow.gg/Admin/Feedback
done | tee /tmp/04-01-prod-curl.log
```

verification:
- `grep -c "attempt=401" /tmp/04-01-prod-curl.log` MUST be 10
- `grep -c "attempt=429" /tmp/04-01-prod-curl.log` MUST be ≥1
- `grep "attempt=429" /tmp/04-01-prod-curl.log | grep -v "retry=$" | wc -l` MUST be ≥1 (Retry-After non-empty)

evidence: paste matched lines or attach `/tmp/04-01-prod-curl.log` once run.

window-reset check: wait 15 minutes (or trigger benign Render redeploy) and re-run a single curl — must return 401 again, not stuck on 429.

### 2. Sol Ring returns real Tagger data (BUG-01 / SC #2)

expected: `/suggest-categories` mode = `ScryfallTagger`, card = `Sol Ring`, returns a non-empty oracle tag list within ~6s. No "tagger returned empty" / fallback copy. No `LogWarning("Tagger has no indexed printing for Sol Ring after 5 probes")` in Render logs.

result: pending

procedure (browser walk):
1. Open https://www.deckflow.gg/suggest-categories
2. Mode = `ScryfallTagger`
3. Card = `Sol Ring`
4. Submit; capture rendered tag list

evidence: paste or screenshot the returned tag list (expected: tags like "Ramp", "Mana Rock", or similar functional Tagger oracle tags).

repeat: run once more with another known cEDH staple of choice (e.g. `Counterspell`, `Mana Crypt`, `Cyclonic Rift`) to reduce single-card-lucky-case risk. Both must PASS.

### 3. SC #3 regression matrix — legacy flows still produce same artifacts

#### 3a. /sync deck reconcile

expected: DeckSync diff page renders for a pair of known Moxfield deck URLs; no exception page; diff structure unchanged from pre-deploy.

result: pending

procedure: visit https://www.deckflow.gg/sync; submit two known Moxfield deck URLs (use the Phase 03 UAT pair for direct A/B comparability).

evidence: paste diff summary or attach screenshot; flag any structural change vs prior capture.

#### 3b. /chatgpt-packets artifact

expected: ChatGPT-paste artifact renders with intact header line + per-card sections; format unchanged from pre-deploy.

result: pending

procedure: visit https://www.deckflow.gg/chatgpt-packets (or the deck-comparison flavor used in past UAT); submit a known deck URL.

evidence: paste artifact header line + first card section.

#### 3c. /suggest-categories mode=All for Sol Ring

expected: Cached + EDHREC + Tagger sections all render; Tagger section is now non-empty (BUG-01 fix surfaces in the All-mode aggregator).

result: pending

procedure: same form as Test 2 but mode = `All`.

evidence: paste presence/non-emptiness of all three sections.

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps

(populated after live walk — list each FAIL with which plan/task to revisit)
