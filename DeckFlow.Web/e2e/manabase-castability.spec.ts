import { expect, test, type Page } from '@playwright/test';

// Phase 64 — Casual/cEDH modes + castability table + the two FORMULA-01 panels.
//
// Unlike `manabase.spec.ts` (chrome only), this spec submits a real paste decklist, which
// drives a live Scryfall card-resolution round-trip. When the sandbox can't reach Scryfall the
// result panel never appears; the result-dependent blocks below are guarded so the spec still
// asserts the form-side contract (both selectors, the always-on formula panel) and the
// no-horizontal-scroll invariant rather than failing on an environment limitation.

// A tiny but real two-color shell: basics + resolvable staples (a 1-drop, a UU 2-drop, and a
// high-MV double-pip spell) so the castability ordering is meaningful. The `Commander` section
// header is the Moxfield-paste marker the parser recognizes, so the commander lands on the
// commander board and is pinned to the top of the castability table.
const PASTE_DECK = [
  'Commander',
  '1 Brago, King Eternal',
  '',
  'Deck',
  '24 Plains',
  '24 Island',
  '1 Swords to Plowshares',
  '1 Counterspell',
  '1 Cyclonic Rift',
  '1 Supreme Verdict',
  '1 Wrath of God',
].join('\n');

async function submitDeck(
  page: Page,
  mode: 'Casual' | 'Cedh',
  importance: 'Central' | 'Standard' | 'Low' = 'Standard',
): Promise<boolean> {
  await page.goto('/manabase');
  await page.locator('input[name="DeckInputSource"][value="PasteText"]').check();
  await page.locator('#manabase-deck-text').fill(PASTE_DECK);
  await page.locator(`.manabase-pill input[name="Mode"][value="${mode}"]`).check();
  await page.locator(`.manabase-pill input[name="CommanderImportance"][value="${importance}"]`).check();
  // Click Analyze specifically — the page now also has a "Load deck" run-button, so a bare
  // `button.run-button` matches two elements (strict-mode failure / would post the load step).
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  // Either a result panel or an error banner comes back. Treat a visible error (Scryfall
  // unreachable, etc.) as "could not analyze in this environment" and let callers skip.
  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  const error = page.locator('.error-banner:not(.hidden)');
  await Promise.race([
    result.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
    error.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
  ]);

  return (await result.count()) > 0 && (await result.isVisible());
}

async function assertNoHorizontalScroll(page: Page): Promise<void> {
  const overflows = await page.evaluate(
    () => document.documentElement.scrollWidth > window.innerWidth + 1,
  );
  expect(overflows, 'page must not gain a horizontal scrollbar').toBe(false);
}

test('mode + commander-importance selectors are present and persist on postback', async ({ page }) => {
  await page.goto('/manabase');

  // Both segmented radio groups render with Casual / Standard selected by default.
  await expect(page.locator('input[name="Mode"][value="Casual"]')).toBeChecked();
  await expect(page.locator('input[name="CommanderImportance"][value="Standard"]')).toBeChecked();

  // The "How the analysis works" formula panel renders even before a deck is entered.
  const howPanel = page.locator('details[data-manabase-formula="how"]');
  await expect(howPanel).toHaveCount(1);
  await expect(howPanel).toContainText(/Karsten/i);

  // Native <details> is collapsed by default and expands on click (no JS needed).
  expect(await howPanel.evaluate((el) => (el as HTMLDetailsElement).open)).toBe(false);
  await howPanel.locator('summary').click();
  expect(await howPanel.evaluate((el) => (el as HTMLDetailsElement).open)).toBe(true);

  await assertNoHorizontalScroll(page);
});

test('segmented pill radios stay visually collapsed so labels center', async ({ page }) => {
  // Regression: the global custom radio render in site-theme-overrides.css
  // (`input[type="radio"] { appearance:none; width:1.05rem; ... position:relative }`)
  // loads after site-common.css and tied its specificity, re-inflating the
  // visually-hidden pill radio into an in-flow ~16px box (opacity:0 but
  // space-occupying), which shoved each pill's label off-center. The collapse
  // rule is now `.manabase-pill > input[type="radio"]` (higher specificity) so
  // the radio must measure ~1px and sit out of flow on every theme.
  await page.goto('/manabase');

  const pillRadios = page.locator('.manabase-pill > input[type="radio"]');
  const count = await pillRadios.count();
  expect(count).toBeGreaterThan(0);

  for (let i = 0; i < count; i++) {
    const box = await pillRadios.nth(i).evaluate((el) => ({
      width: (el as HTMLElement).offsetWidth,
      height: (el as HTMLElement).offsetHeight,
      position: getComputedStyle(el).position,
    }));
    expect(box.width, 'pill radio must stay collapsed (not the 16px custom box)').toBeLessThanOrEqual(2);
    expect(box.height).toBeLessThanOrEqual(2);
    expect(box.position, 'pill radio must be out of flow').toBe('absolute');
  }

  await assertNoHorizontalScroll(page);
});

test('Mode + CommanderImportance selections survive the postback', async ({ page }) => {
  // The radios re-render from Model.Request on BOTH the success and the error path, so this holds
  // even when Scryfall is unreachable in the sandbox — we assert the form state, not the result.

  // Casual + Low.
  await submitDeck(page, 'Casual', 'Low');
  await expect(page.locator('input[name="Mode"][value="Casual"]')).toBeChecked();
  await expect(page.locator('input[name="Mode"][value="Cedh"]')).not.toBeChecked();
  await expect(page.locator('input[name="CommanderImportance"][value="Low"]')).toBeChecked();
  await expect(page.locator('input[name="CommanderImportance"][value="Standard"]')).not.toBeChecked();

  // cEDH + Central.
  await submitDeck(page, 'Cedh', 'Central');
  await expect(page.locator('input[name="Mode"][value="Cedh"]')).toBeChecked();
  await expect(page.locator('input[name="Mode"][value="Casual"]')).not.toBeChecked();
  await expect(page.locator('input[name="CommanderImportance"][value="Central"]')).toBeChecked();
  await expect(page.locator('input[name="CommanderImportance"][value="Standard"]')).not.toBeChecked();

  await assertNoHorizontalScroll(page);
});

test('casual submit renders the castability table, worst-first, commander pinned, no rocks', async ({ page }) => {
  const ok = await submitDeck(page, 'Casual');
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  // Mode echo shows Casual.
  await expect(page.locator('.manabase-context')).toContainText(/Mode:\s*Casual/i);

  const table = page.locator('table.castability-table');
  await expect(table).toBeVisible();

  const names = await table.locator('tbody tr td.castability-name').allInnerTexts();
  // Only real payoff spells appear — no mana rocks / dorks / lands as rows.
  for (const banned of ['Sol Ring', 'Birds of Paradise', 'Plains', 'Island']) {
    expect(names.some((n) => n.includes(banned))).toBe(false);
  }
  // The real spells we pasted do appear as rows.
  expect(names.some((n) => n.includes('Counterspell'))).toBe(true);

  // The commander (carried on the `Commander` section of the paste) is pinned to the FIRST row
  // and flagged with the commander glyph.
  const firstRow = table.locator('tbody tr').first();
  await expect(firstRow).toHaveClass(/manabase-row--commander/);
  await expect(firstRow.locator('td.castability-name')).toContainText('Brago, King Eternal');
  await expect(firstRow.locator('.manabase-cmd-glyph')).toBeVisible();

  // Non-commander rows are sorted ascending by cast %.
  const chipText = await table.locator('tbody tr:not(.manabase-row--commander) .manabase-chip').allInnerTexts();
  const percents = chipText.map((t) => parseInt(t.replace('%', ''), 10)).filter((n) => !Number.isNaN(n));
  const sorted = [...percents].sort((a, b) => a - b);
  expect(percents).toEqual(sorted);

  // Both formula panels are present once a result exists, and expand.
  await expect(page.locator('details[data-manabase-formula="how"]')).toHaveCount(1);
  const numbers = page.locator('details[data-manabase-formula="numbers"]');
  await expect(numbers).toHaveCount(1);
  await numbers.locator('summary').click();
  expect(await numbers.evaluate((el) => (el as HTMLDetailsElement).open)).toBe(true);

  // Panel 2 ("show the work") surfaces the regression's deck inputs/terms, not just prose: the
  // expanded numbers panel must name the Karsten regression coefficients used to derive the target.
  // Non-brittle: assert the constant term appears, not the exact computed value.
  await expect(numbers).toContainText('19.59');

  // The aggregate color finding (COLOR-AGG "N of M under-supported") is surfaced in the verdict
  // summary whenever a weakest color exists. Guard on its presence so a perfectly-balanced shell
  // (no weak color) does not fail the spec.
  const panelText = (await page.locator('.result-panel').first().innerText()).toLowerCase();
  if (panelText.includes('under-supported')) {
    // Matches both the summary ("N of M Blue cards under-supported") and the table/list
    // ("N of M under-supported") renderings; we only assert the "N of M ... under-supported"
    // aggregate shape, never the exact counts.
    expect(panelText).toMatch(/\d+\s+of\s+\d+[\s\w()·]*?under-supported/);
  }

  await assertNoHorizontalScroll(page);
});

test('cedh submit echoes cEDH and replaces the castability table with a note', async ({ page }) => {
  // The exact land-target drop (cEDH < casual) needs a full ~99-card deck and is covered by the
  // xUnit service test; here we assert the mode echo + the cEDH table-hidden contract, which is
  // what a user sees. A real land-target line is still rendered.
  const cedhOk = await submitDeck(page, 'Cedh');
  test.skip(!cedhOk, 'analysis result unavailable (Scryfall not reachable in this environment)');

  await expect(page.locator('.manabase-context')).toContainText(/Mode:\s*cEDH/i);
  await expect(page.locator('.result-panel p:has(strong:text-is("Lands:"))')).toContainText(/recommended/i);

  // Castability table is hidden in cEDH; the note appears instead.
  await expect(page.locator('table.castability-table')).toHaveCount(0);
  await expect(page.locator('.manabase-castability-note')).toContainText(/available in Casual mode/i);

  await assertNoHorizontalScroll(page);
});

test('biggest-fix callout never recommends a negative source count', async ({ page }) => {
  // Regression: the callout used to do ceil(weakest.Deficit) on a color picked by the composite
  // (under-supported) signal. When that color held a raw source SURPLUS, the deficit was negative
  // and it rendered "add ~-14 more Green source(s)" — contradicting the "add lands" health line.
  // The reconciled selector (ManabaseReport.PrimaryFix) must never emit a negative add amount.
  const analyzed = await submitDeck(page, 'Casual');
  test.skip(!analyzed, 'Scryfall unreachable in this environment — cannot render the result panel.');

  const note = page.locator('.result-panel p.mode-note:has(strong:text-is("Biggest fix:"))');
  if ((await note.count()) > 0) {
    await expect(note).not.toContainText('~-');
  }

  await assertNoHorizontalScroll(page);
});

test('health verdict renders a four-tier scale label', async ({ page }) => {
  // Health chip must read one of the four scale tiers (Excellent / Solid / Workable / Needs work),
  // never the old two-tier "Healthy"/"Functional" wording.
  const analyzed = await submitDeck(page, 'Casual');
  test.skip(!analyzed, 'Scryfall unreachable in this environment — cannot render the result panel.');

  const chip = page.locator('.result-panel .manabase-chip').first();
  await expect(chip).toBeVisible();
  await expect(chip).toHaveText(/^(Excellent|Solid|Workable|Needs work)$/);

  await assertNoHorizontalScroll(page);
});
