import { expect, test } from '@playwright/test';

// The Mana Base analyzer is a public deck tool gated by the
// `tool.manabase.enabled` flag (default ON). These tests cover the page
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

  // Form posts back to /manabase. The input method defaults to Public URL, so the
  // URL field and deck-name show while the paste field stays toggled off (present
  // in the DOM but hidden until the user picks Paste text).
  await expect(page.locator('form[action="/manabase"]')).toBeVisible();
  await expect(page.locator('#manabase-input-source')).toBeVisible();
  await expect(page.locator('#manabase-deck-url')).toBeVisible();
  await expect(page.locator('#manabase-deck-name')).toBeVisible();
  await expect(page.locator('#manabase-deck-text')).toBeAttached();
  await expect(page.locator('#manabase-deck-text')).toBeHidden();

  expect(consoleErrors).toEqual([]);
});

test('Moxfield Bridge hint renders under the URL field like Deck Analysis', async ({ page }) => {
  await page.goto('/manabase');

  const bridgeHint = page.locator('details.deckflow-bridge-hint');
  await expect(bridgeHint).toBeAttached();
  await expect(bridgeHint.locator('summary')).toContainText('DeckFlow Bridge extension');
});

test('reduced/alternative cost overrides box is present and posts its value', async ({ page }) => {
  await page.goto('/manabase');

  // The overrides field exists inside its collapsible section and binds to CostOverridesText.
  const box = page.locator('#manabase-cost-overrides');
  await expect(box).toHaveAttribute('name', 'CostOverridesText');

  // Expand the section (closed on a fresh load with no detected suggestions), then it is editable.
  await page.locator('.manabase-overrides > summary').click();
  await expect(box).toBeVisible();
  await box.fill('Force of Will: 0');
  await expect(box).toHaveValue('Force of Will: 0');
});

test('Start over link resets the form to a fresh empty page', async ({ page }) => {
  await page.goto('/manabase');

  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill('1 Sol Ring');
  await expect(page.locator('#manabase-deck-text')).toHaveValue('1 Sol Ring');

  // The Start over control navigates to a clean GET /manabase (no persisted input).
  const startOver = page.locator('.manabase-start-over');
  await expect(startOver).toHaveAttribute('href', /\/manabase$/);
  await startOver.click();

  await expect(page).toHaveURL(/\/manabase$/);
  await expect(page.locator('#manabase-deck-text')).toHaveValue('');
});

test('analysis form is wired to the shared busy indicator', async ({ page }) => {
  await page.goto('/manabase');

  // The submit form carries the data-busy-* contract that deck-sync.js auto-wires on submit,
  // and the busy-indicator element it drives is present on the page.
  await expect(page.locator('form[action="/manabase"]')).toHaveAttribute('data-busy-title', /Analyzing mana base/);
  await expect(page.locator('form[action="/manabase"]')).toHaveAttribute('data-busy-min-ms', '500');
  await expect(page.locator('#busy-indicator')).toBeAttached();
});

test('clicking Analyze shows the busy overlay (min-display floor)', async ({ page }) => {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill('1 Sol Ring');

  // The data-busy-min-ms floor holds the navigation briefly, so the overlay is reliably visible right
  // after the click instead of flashing past on a fast response. (No Scryfall needed: the overlay is
  // shown before the request is even sent.)
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();
  await expect(page.locator('#busy-indicator')).toBeVisible();
  await expect(page.locator('#busy-indicator-title')).toHaveText(/Analyzing mana base/);
});

test('input-method dropdown toggles URL vs paste field like Deck Analysis', async ({ page }) => {
  await page.goto('/manabase');

  const urlPanel = page.locator('[data-sync-panel="manabase-deck-url"]');
  const textPanel = page.locator('[data-sync-panel="manabase-deck-text"]');

  // Default is PublicUrl: URL field shown, paste field hidden.
  await expect(urlPanel).toBeVisible();
  await expect(textPanel).toBeHidden();

  // Switching to Paste text shows the textarea and hides the URL field.
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await expect(textPanel).toBeVisible();
  await expect(urlPanel).toBeHidden();

  // Switching back to URL reverses it.
  await page.locator('#manabase-input-source').selectOption('PublicUrl');
  await expect(urlPanel).toBeVisible();
  await expect(textPanel).toBeHidden();
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
