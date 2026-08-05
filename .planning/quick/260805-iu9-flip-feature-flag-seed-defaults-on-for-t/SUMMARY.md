---
quick_id: 260805-iu9
slug: flip-feature-flag-seed-defaults-on-for-t
date: 2026-08-05
status: complete
branch: feat/flags-bracket-deck-history-default-on
commit: a59fd80f
---

# Summary

`tool.bracket.enabled` and `tool.deck-history.enabled` now seed ON. Five files changed,
one commit, `a59fd80f`.

## What changed

| File | Change |
| --- | --- |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` | `PostgresSeedSql` `FALSE` -> `TRUE`, `SqliteSeedSql` `0` -> `1`, both keys |
| `DeckFlow.Web.Tests/Tools/ToolFlagSeedConsistencyTests.cs` | both keys removed from `DarkLaunchedFlags`; comment corrected |
| `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` | `InlineData` expectations `false` -> `true` |
| `DeckFlow.Web/e2e/bracket-smoke.spec.ts` | stale "prod seed stays FALSE" rationale rewritten; forced toggle kept |
| `README.md` | Bracket Check "default OFF" -> "default ON" (line 141; the Phase-76 changelog entry is history, untouched) |

## Verification

- `dotnet build DeckFlow.sln`: 0 errors, 9 warnings — all `CS8629` in
  `ManabaseBaselineWeightingTests.cs`, matching the known pre-existing baseline.
- Full suite: Core 2011/0, Web 2273/0 (16 skipped), Studio 426/0 (4 skipped). 4710 passed,
  0 failed.
- `scripts/format-check-changed.sh staged`: exit 0.
- No line-ending churn: `git diff --stat` and `git diff --ignore-all-space --stat` both
  report 12 insertions / 13 deletions.

## Production state

**Production is unchanged by this commit and needs no flip — both flags are already ON.**
The seed's `ON CONFLICT (key) DO NOTHING` clause preserves operator values (FLAG-01), so
the seed never touches an existing row either way. Read-only query against the Render
Postgres instance `dpg-d7oj8iugvqtc73fso0g0-a` on 2026-08-05:

| key | enabled | updated_at |
| --- | --- | --- |
| `tool.bracket.enabled` | `true` | 2026-07-01 12:08 MDT |
| `tool.deck-history.enabled` | `true` | 2026-07-17 15:29 MDT |

Both were flipped by an operator weeks ago. Any planning note still listing a Deck History
"owed prod flip" is stale as of 2026-07-17.

What this commit actually buys: fresh databases — local dev, CI, tests, and any future
environment bootstrapped from scratch — now come up with both tools reachable, matching
production instead of diverging from it.
</content>
