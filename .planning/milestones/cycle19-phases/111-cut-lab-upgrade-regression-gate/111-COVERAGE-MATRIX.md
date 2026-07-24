# CLUP-10 Coverage Matrix

Audit basis: opened each referenced Vitest, xUnit, and e2e file from `111-02-PLAN.md` and verified the named assertion instead of trusting the interface map.

| Surface | Layer | File | Test name | Status |
| --- | --- | --- | --- | --- |
| Pool filters/search | Vitest | `DeckFlow.Web/ts-tests/cut-lab-pool-filter.test.ts` | `hides non-matching pool rows without detaching them or changing whole-pool serialization/counts` | COVERED |
| Pool filters/search | e2e | `DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts` | `captures cross-theme mobile chrome coverage for Cut Lab navigation and disclosures` | COVERED |
| Collapse state | Vitest | `DeckFlow.Web/ts-tests/cut-lab-section-collapse.test.ts` | `restores stored collapsed sections on init, including pre-existing collapsibles` | COVERED |
| Collapse state | Vitest | `DeckFlow.Web/ts-tests/cut-lab-mobile-collapse.test.ts` | `removes open from an auxiliary mobile-collapse section on mobile` | COVERED |
| Collapse state | e2e | `DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts` | `proves the no-JS Cut Lab navigation and disclosure fallbacks` | COVERED |
| Anchors | Vitest | `DeckFlow.Web/ts-tests/cut-lab-jump-nav.test.ts` | `opens a collapsed target before scrolling and moves focus to it` | COVERED |
| Anchors | e2e | `DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts` | `proves the no-JS Cut Lab navigation and disclosure fallbacks` | COVERED |
| Oracle disclosures | Vitest | `DeckFlow.Web/ts-tests/cut-lab-structural-cardtext.test.ts` | `re-attaches the pool-row card text disclosure under a rebuilt structural evidence chip after decide` | COVERED |
| Oracle disclosures | e2e | `DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts` | `captures cross-theme mobile chrome coverage for Cut Lab navigation and disclosures` | COVERED |
| Oracle disclosures | e2e | `DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts` | `proves the no-JS Cut Lab navigation and disclosure fallbacks` | COVERED |
| Combo labels | Vitest | `DeckFlow.Web/ts-tests/cut-lab-structural-cardtext.test.ts` | `re-attaches the pool-row card text disclosure under a rebuilt structural evidence chip after decide` | COVERED |
| Combo labels | Vitest | `DeckFlow.Web/ts-tests/cut-lab-lock-interactions.test.ts` | `toggles an individual card pill when a combo badge span is nested inside the button` | COVERED |
| Combo labels | xUnit | `DeckFlow.Web.Tests/CutLabViewModelWordingTests.cs` | `From_AssignsComboBadgeMapForInitialRender` | COVERED |
| Combo labels | Vitest | `DeckFlow.Web/ts-tests/cut-lab-combo-package-copy.test.ts` | `renders combo badge text and package helper copy for a multi-member package` | COVERED |
| Package helper copy | Vitest | `DeckFlow.Web/ts-tests/cut-lab-lock-interactions.test.ts` | `computes package checkbox states for checked, unchecked, and mixed members` | GAP - missing a direct DOM assertion that the package helper copy string renders for a multi-member package. |
| Package helper copy | Vitest | `DeckFlow.Web/ts-tests/cut-lab-combo-package-copy.test.ts` | `renders combo badge text and package helper copy for a multi-member package` | COVERED |
