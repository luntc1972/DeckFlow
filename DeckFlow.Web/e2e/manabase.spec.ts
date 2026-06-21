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

test('mana base nav link is wired in the Analyze group when the flag is on', async ({ page }) => {
  await page.goto('/deck-analysis');

  // Flag defaults ON, so the link must be present in the deck-tool nav and
  // point at the tool. The dropdown is hover-revealed (and collapses to a menu
  // on mobile), so assert DOM wiring rather than driving the hover interaction.
  const link = page.locator('#deck-tool-nav a[href$="/manabase"]');
  await expect(link).toHaveCount(1);
  await expect(link).toHaveText(/Mana Base/i);
});
