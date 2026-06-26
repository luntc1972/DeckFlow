import { expect, test } from '@playwright/test';

// Deck Primer is now wired into the Moxfield Bridge like the other deck tools:
// it loads deck-sync.js (which owns the bridge submit interception + the input
// toggle + the copy buttons), and its public-URL field carries the shared
// _DeckFlowBridgeHint. These tests assert the page-side wiring without driving
// the actual extension (which is not present in CI).

test('Deck Primer renders the Moxfield Bridge hint under the URL field', async ({ page }) => {
  await page.goto('/deck-primer');

  // Default input method is Paste text, so switch to public URL to reveal the field.
  await page.locator('select[name="DeckInputSource"]').selectOption('PublicUrl');

  const bridgeHint = page.locator('details.deckflow-bridge-hint');
  await expect(bridgeHint).toBeAttached();
  await expect(bridgeHint.locator('summary')).toContainText('DeckFlow Bridge extension');
});

test('Deck Primer input-method toggle is driven by deck-sync (URL vs paste)', async ({ page }) => {
  await page.goto('/deck-primer');

  const urlPanel = page.locator('[data-sync-panel="primer-deck-url"]');
  const textPanel = page.locator('[data-sync-panel="primer-deck-text"]');

  // Default is Paste text: text field shown, URL field hidden. The toggle only
  // works if deck-sync.js is active on the page (it owns initializeSyncInputModeUi).
  await expect(textPanel).toBeVisible();
  await expect(urlPanel).toBeHidden();

  await page.locator('select[name="DeckInputSource"]').selectOption('PublicUrl');
  await expect(urlPanel).toBeVisible();
  await expect(textPanel).toBeHidden();

  await page.locator('select[name="DeckInputSource"]').selectOption('PasteText');
  await expect(textPanel).toBeVisible();
  await expect(urlPanel).toBeHidden();
});
