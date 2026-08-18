import { expect, test, type Page } from '@playwright/test';
import { clickManabasePillRadio } from './support/manabase-pill';

// Regression for the "manabase did not find a commander" bug: a Moxfield plaintext export has
// no "Commander" section header — the commander is simply the leading card and every line
// parses as mainboard. The analyzer must infer the leading one-of as the commander so the
// castability table marks it and the color findings weight its colors.
//
// Live-only (needs Scryfall to resolve real cards). No feature flag required — base commander
// detection, not the flagged command-zone castability callout.
// Run from DeckFlow.Web/:
//   DECKFLOW_LIVE_E2E=1 npx --no-install playwright test manabase-headerless-commander

// Header-less: commander first, then an alphabetically-sorted body. The third-entry guard
// (Cultivate < Lightning Bolt) trims the inference to Bello alone — no false partner.
const HEADERLESS_DECK = [
  '1 Bello, Bard of the Brambles',
  '1 Cultivate',
  '1 Lightning Bolt',
  '20 Forest',
  '16 Mountain',
  '1 Sol Ring',
].join('\n');

async function analyze(page: Page) {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(HEADERLESS_DECK);
  await clickManabasePillRadio(page, 'Mode', 'Casual');
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  await expect(result).toBeVisible({ timeout: 60_000 });
  return result;
}

test('infers the leading commander from a header-less Moxfield paste', async ({ page }) => {
  test.skip(!process.env.DECKFLOW_LIVE_E2E, 'live-only: needs Scryfall');

  const result = await analyze(page);

  // With analysis.manabase.commander-castability default-on, the inferred commander is surfaced in
  // the command-zone callout (not as a castability-table row) and excluded from the visible rows.
  const callout = result.locator('.manabase-cmd-castability');
  await expect(callout).toBeVisible();
  await expect(callout).toContainText('Bello, Bard of the Brambles');
  await expect(result.locator('.castability-table')).not.toContainText('Bello, Bard of the Brambles');

  // Bello is R/G, so at least one color finding is marked as a commander color (★).
  await expect(result.locator('.manabase-cmd-glyph[title="commander color"]').first()).toBeVisible();
});
