# SECURITY.md — Phase 70: Manabase Accuracy (Mana Quantity & Source Fidelity)

**Audit mode:** retroactive STRIDE. Phase 70 was executed and shipped manually (outside `gsd-execute-phase`), so it had no PLAN-time `<threat_model>` block and no SUMMARY.md `## Threat Flags`. The threat register below was built from the implementation diff (`main...HEAD`) and then verified against the code. Implementation files were not modified.

**ASVS level:** L1 · **block_on:** high
**Result:** SECURED — 11/11 threats CLOSED (10 mitigated, 1 accepted-and-documented). 0 BLOCKER.

---

## Scope of change (Phase 70 prod surface)

Per-source mana-quantity parsing from oracle text (`ManaProductionAmount`), narrowed ramp-credit + land-ramp sim source (`ManabaseClassifier`, `CastabilitySimulator`), color-aware mulligan, new `ManaSource.DeployCost` / `ManaAmount` / `IsCommander` fields, `UnsupportedInteraction` disclosure, four new feature flags (all seeded TRUE), and new view markup in `Manabase.cshtml`. Plus dev/test-only tooling: `scripts/harvest-archidekt-decks.py` and two gated baseline harnesses that make outbound HTTP.

Entry point: anonymous public `POST /manabase` and `POST /manabase/load` (`ManabaseController`). Input: a public deck URL or pasted decklist text.

---

## Threat register (STRIDE) and verification

| ID | STRIDE | Threat | Disposition | Status | Evidence |
|----|--------|--------|-------------|--------|----------|
| MB-T1 | Tampering / Injection | SQL injection via the feature-flag seed/upsert SQL (4 new manabase flags) | mitigate | CLOSED | `FeatureFlagStore.cs:150-200` — seed is a static string literal with no interpolation; flag values are hard-coded `TRUE/1`. Upsert is fully parameterized (`@key,@enabled,@now`, `FeatureFlagStore.cs:87-90,186-200`). No user input reaches SQL. |
| MB-T2 | DoS | ReDoS / catastrophic backtracking in oracle-text regex parsing (`ManaProductionAmount`, `ManabaseClassifier` reducer/evoke/suspend regexes) | mitigate | CLOSED | `ManaProductionAmount.cs:20-31` and `ManabaseClassifier.cs:16-34` — all patterns are linear with non-overlapping atoms (`\{[^}]+\}`, fixed alternations); no nested unbounded quantifiers. Regex input is Scryfall oracle text (server-controlled), not direct user input, and the request payload is capped (MB-T3). `RegexOptions.Compiled`, single `Match`/`Matches` pass. |
| MB-T3 | DoS | Unbounded input — huge pasted decklist forces unbounded allocation / Scryfall calls / 20k-trial sims | mitigate | CLOSED | `ManabaseAnalysisService.cs:103-104,232-261` — `MaxDeckSourceChars=100_000` and `MaxDeckCards=500` reject oversized input as a user-facing validation error; Scryfall batched at 75 (`:98,354`). Sim trials fixed at 20k (`CastabilitySimulator.cs:34`); `CoverPips` DFS bounded by tiny pip/source counts. Request timeout via `CreateTimeoutScope(LookupTimeout)` (`ManabaseController.cs:62,137`). |
| MB-T4 | Information disclosure / XSS | Reflected XSS via card names / oracle-derived strings / unsupported-interaction names / demanding-card names rendered in the result view | mitigate | CLOSED | `Manabase.cshtml` — every card/oracle-derived value renders through Razor `@`-expressions (`@u.Name`, `@u.Reason`, `@f.DrivingSpell`, `@c.Name`, `@d.Name`, `@Model.Unresolved`, `@Model.ErrorMessage`), which HTML-encode by default. No `Html.Raw`, `MarkupString`, or `IHtmlContent` anywhere in the view or `ManabaseDisplay.cs` (grep-confirmed). |
| MB-T5 | Tampering | Feature-flag tampering / fail-open — a missing/unseeded experimental flag silently turns a safety-gated path ON | mitigate | CLOSED | `ManabaseAnalysisService.cs:216-219` — `IsFlagOn` reads `Snapshot().TryGetValue(...) && enabled`, returning false when the key is absent (fail-safe OFF), deliberately bypassing `IFeatureFlagCache.IsEnabled` which defaults missing keys ON (documented `:178-180`). All four MQ flags read through `IsFlagOn` (`:171-184`). |
| MB-T6 | EoP / Tampering | Hand-crafted POST carries out-of-range enum (Mode / CommanderImportance) to drive invalid analysis state | mitigate | CLOSED | `ManabaseController.cs:57-60,132-135` (MEDIUM-1) — both enums coerced via `Enum.IsDefined(...) ? value : default` on both actions before use. |
| MB-T7 | Spoofing / CSRF | Cross-site forged POST to the analysis endpoints | mitigate | CLOSED | `ManabaseController.cs:48,119` — `[ValidateAntiForgeryToken]` on `POST /manabase/load` and `POST /manabase`; `@Html.AntiForgeryToken()` in the form (`Manabase.cshtml:34`). |
| MB-T8 | SSRF | Deck-URL input (`DeckInputSource.PublicUrl`) coerces the server into fetching an attacker-chosen host | mitigate | CLOSED | `DeckEntryLoader.cs:119-155` — only absolute URLs whose host contains `moxfield.com` or `archidekt.com` are fetched; any other URL falls through to text-parsing (no fetch) and ultimately a validation error. Phase 70 added **no** new outbound-URL surface in the prod path. NOTE (pre-existing, out of Phase-70 scope): the host check is a substring `Contains`, not a suffix/exact match — tracked below as a non-blocking observation, not introduced by this phase. |
| MB-T9 | Information disclosure | Internal exception detail leaked to the anonymous user on parser/runtime fault | mitigate | CLOSED | `ManabaseController.cs:87-200` — only author-written validation copy (`InvalidOperationException.Message`) and the curated `UpstreamErrorMessageBuilder.BuildScryfallMessage` are surfaced; the catch-all returns a generic "Something went wrong" string and logs the detail server-side. |
| MB-T10 | SSRF / Spoofing | Dev/test tooling (`harvest-archidekt-decks.py`, baseline harnesses) makes outbound HTTP to unpinned/attacker-influenced hosts, or runs unintentionally in CI/prod | accept | CLOSED | Dev/test-only, not in the prod request path. Harnesses gated on `DECKFLOW_MANABASE_HARNESS=1` or a `.manabase-harness-on` sentinel, default-off, "never runs in CI" (`ManabaseFlagBaselineHarness.cs:23,184-185`; `ManaQuantityBaselineHarness.cs:13,21-22`). Hosts are hard-coded literals (`api.scryfall.com`, `archidekt.com`). Cache/sentinel/harvested-deck artifacts are gitignored so CI can't trip the harness (`.gitignore` Phase-70 block). Accepted: standalone operator-run tooling against fixed public MTG endpoints; no untrusted host input. |
| MB-T11 | Information disclosure | Secrets committed to the public repo via new scripts/artifacts | mitigate | CLOSED | Diff secret-scan clean. `scripts/run-web-test.{sh,ps1}` use documented local-only PLACEHOLDER admin creds (`admin` / `changeme-local`), set only if unset, with an explicit "NEVER put a real/prod password here (public repo)" warning. `.gitignore` excludes `.manabase-*.json`, `.manabase-harness-on`, `archidekt-baseline-decks.json`. |

---

## Unregistered flags

None. No SUMMARY.md `## Threat Flags` section existed (phase shipped outside the standard executor). All new attack surface was enumerated directly from the diff and is covered by MB-T1..MB-T11 above.

---

## Accepted risks log

- **MB-T10 — dev/test outbound-HTTP tooling.** The Archidekt harvester and the two baseline harnesses make outbound HTTP to fixed public MTG endpoints (`api.scryfall.com`, `archidekt.com`). They are operator-run, gated off by default (env var or sentinel file), excluded from CI, and produce only gitignored local artifacts. They are not reachable from the deployed web app's request path. Accepted as low-risk dev tooling.

---

## Non-blocking observations (pre-existing, NOT introduced by Phase 70)

- **OBS-1 — substring host match in `DeckEntryLoader` (MB-T8).** `uri.Host.Contains("archidekt.com")` / `Contains("moxfield.com")` (`DeckEntryLoader.cs:121,127`) would also match a hostile host such as `archidekt.com.attacker.example` or `evilmoxfield.com`. Combined with the fetch happening server-side, that is a latent SSRF widening. It predates Phase 70 (this phase added no outbound-URL code) and applies to every deck-loading tool, so it is out of scope for this audit. Recommend a follow-up to tighten to a suffix/registrable-domain check across all callers of `IDeckEntryLoader`.
