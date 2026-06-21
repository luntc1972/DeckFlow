import { expect, test } from '@playwright/test';

// The Mana Base analyzer is a public deck tool gated by the
// `feature.manabase.enabled` flag (default ON). These tests cover the page
// chrome and form without submitting a real analysis (which would call
// Scryfall) — the analysis pipeline itself is covered by xUnit
// ManabaseAnalysisServiceTests.

test('manabase page renders the deck-input form', async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') {
      consoleErrors.push(message.text());
    }
  });

  const response = await page.goto('/manabase');
  expect(response?.ok()).toBeTruthy();

  // Form posts back to /manabase with the three deck-input fields.
  await expect(page.locator('form[action="/manabase"]')).toBeVisible();
  await expect(page.locator('#manabase-deck-url')).toBeVisible();
  await expect(page.locator('#manabase-deck-name')).toBeVisible();
  await expect(page.locator('#manabase-deck-text')).toBeVisible();

  expect(consoleErrors).toEqual([]);
});

test('input-source radios sit adjacent, not pushed to opposite page edges', async ({ page }) => {
  await page.goto('/manabase');

  // Regression: the source toggle shares the .toolbar shell, which themes fork
  // as justify-content: space-between. Without the .manabase-source-toggle
  // override the two radios get shoved to opposite edges. Assert they stay
  // within a tight gap of each other.
  const labels = page.locator('.manabase-source-toggle label');
  await expect(labels).toHaveCount(2);
  const first = await labels.nth(0).boundingBox();
  const second = await labels.nth(1).boundingBox();
  expect(first).not.toBeNull();
  expect(second).not.toBeNull();
  const gap = second!.x - (first!.x + first!.width);
  expect(gap).toBeLessThan(60);
});

test('clicking a deck-type pill moves the selected highlight (exactly one lit)', async ({ page }) => {
  await page.goto('/manabase');

  const ACCENT_LIT = (handle: string) =>
    page.locator(handle).evaluate(
      (el) => getComputedStyle(el as HTMLElement).backgroundColor,
    );

  const casual = page.locator('.manabase-pill:has(input[value="Casual"])');
  const cedh = page.locator('.manabase-pill:has(input[value="Cedh"])');

  // Casual is the default selected mode, so it is the only lit pill on load.
  const casualBg = await ACCENT_LIT('.manabase-pill:has(input[value="Casual"])');
  const cedhBgBefore = await ACCENT_LIT('.manabase-pill:has(input[value="Cedh"])');
  expect(casualBg).not.toEqual(cedhBgBefore);

  // Clicking cEDH must move the highlight with no POST roundtrip: cEDH lights,
  // Casual goes dark. Regression for the stale .is-selected double-highlight.
  await cedh.click();
  expect(await ACCENT_LIT('.manabase-pill:has(input[value="Cedh"])')).toEqual(casualBg);
  expect(await ACCENT_LIT('.manabase-pill:has(input[value="Casual"])')).toEqual(cedhBgBefore);
  await expect(casual.locator('input')).not.toBeChecked();
  await expect(cedh.locator('input')).toBeChecked();
});

test('mana base nav link is wired in the Analyze group when the flag is on', async ({ page }) => {
  await page.goto('/deck-analysis');

  // Flag defaults ON, so the link must be present in the deck-tool nav and
  // point at the tool. The dropdown is hover-revealed (and collapses to a menu
  // on mobile), so assert DOM wiring rather than driving the hover interaction.
  const link = page.locator('#deck-tool-nav a[href$="/manabase"]');
  await expect(link).toHaveCount(1);
  await expect(link).toHaveText(/Mana Base/i);
});

test('home hub shows a Mana Base tile in the Analyze group when the flag is on', async ({ page }) => {
  await page.goto('/');

  // Flag defaults ON, so a hub-card tile linking to /manabase must render.
  const tile = page.locator('.hub-card[href$="/manabase"]');
  await expect(tile).toHaveCount(1);
  await expect(tile.locator('.hub-card__title')).toHaveText(/Mana Base/i);
});
