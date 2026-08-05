---
quick_id: 260805-iu9
slug: flip-feature-flag-seed-defaults-on-for-t
date: 2026-08-05
branch: feat/flags-bracket-deck-history-default-on
---

# Quick Task: seed Bracket Check and Deck History ON by default

## Goal

`tool.bracket.enabled` and `tool.deck-history.enabled` leave dark launch and seed
`TRUE`/`1` instead of `FALSE`/`0`, so a fresh database brings both tools up reachable
with no operator flip.

## Scope boundary (important)

`FeatureFlagStore`'s seed SQL ends in `ON CONFLICT (key) DO NOTHING`, which exists to
preserve operator-set values across restarts (FLAG-01). Both keys already have rows in
the production database set to `false`, so **this change does not alter production.**
Turning the tools on in prod remains a separate operator action in `/Admin/Flags`.

## Tasks

1. `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — `PostgresSeedSql`
   `FALSE` -> `TRUE` and `SqliteSeedSql` `0` -> `1` for both keys.
2. `DeckFlow.Web.Tests/Tools/ToolFlagSeedConsistencyTests.cs` — drop both keys from
   `DarkLaunchedFlags` (the set is an allow-list inversion: every tool flag must seed ON
   unless named there) and correct the explanatory comment.
3. `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` — `InlineData` `false` -> `true` for
   both keys, comments updated.
4. `DeckFlow.Web/e2e/bracket-smoke.spec.ts` — the `beforeEach` comment claimed "prod seed
   stays FALSE". The forced toggle stays (the flag-OFF test mutates shared state), only
   the now-wrong rationale is rewritten.
5. `README.md:141` — Bracket Check "default OFF" -> "default ON". The Phase-76 changelog
   entry at line 919 is history and is left alone.

## Out of scope (checked, no change needed)

- `FeatureFlagCatalog.cs` — the "Seeded OFF" prose at lines 46/127/130/135 all belongs to
  Cut Lab and `analysis.cut-lab.*` flags, not to these two keys.
- E2E flag-OFF specs — they toggle the flag through the admin API explicitly, so they are
  independent of the seed default.
- Production database rows — see scope boundary.

## Verification

- `dotnet build DeckFlow.sln` — 0 errors, warnings at the known 9 x CS8629 baseline.
- Full suite green.
- No line-ending churn (`git diff --stat` equals `git diff --ignore-all-space --stat`).
</content>
