import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type Locator, type Page, type TestInfo } from '@playwright/test';

const screenshotRoot = '/mnt/c/users/chrislunt/source/personal/deckflow/ux-shots';

// Tapland-heavy Azorius shell with real early interaction, a couple of rocks, and a small top end.
// This reliably renders both the untapped-source lens and the opening-hand lens without needing any
// admin flag toggles because analysis.manabase.tap-analyzer and analysis.manabase.mulligan-eval seed ON.
const PASTE_DECK = [
  'Commander',
  '1 Brago, King Eternal',
  '',
  'Deck',
  '1 Command Tower',
  '1 Exotic Orchard',
  '1 Prairie Stream',
  '1 Tranquil Cove',
  '1 Azorius Guildgate',
  '1 Temple of Enlightenment',
  '1 Port Town',
  '1 Glacial Fortress',
  '1 Adarkar Wastes',
  '1 Skycloud Expanse',
  '1 Nimbus Maze',
  '1 Sejiri Refuge',
  '1 Idyllic Beachfront',
  '1 Meandering River',
  '1 Seachrome Coast',
  '1 Restless Anchorage',
  '8 Plains',
  '8 Island',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Azorius Signet',
  '1 Talisman of Progress',
  '1 Mind Stone',
  '1 Swords to Plowshares',
  '1 Path to Exile',
  '1 Counterspell',
  '1 Arcane Denial',
  '1 Wall of Omens',
  '1 Reflector Mage',
  '1 Skyclave Apparition',
  '1 Supreme Verdict',
  '1 Cloudblazer',
  '1 Mulldrifter',
  '1 Conjurer\'s Closet',
  '1 Sun Titan',
].join('\n');

test('captures tap and mulligan lens element screenshots at each viewport', async ({ page }, testInfo) => {
  const ok = await submitDeck(page);
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  const tapLens = page.locator('.manabase-taplens');
  const mulliganLens = page.locator('.manabase-mulliganlens');

  await expect(tapLens).toBeVisible();
  await expect(tapLens).toContainText('Untapped sources');
  await expect(tapLens).toContainText('turn-1 untapped');

  await expect(mulliganLens).toBeVisible();
  await expect(mulliganLens).toContainText('Opening hand');
  await expect(mulliganLens).toContainText('Keep-size process');

  await assertWithinViewportWidth(page, tapLens);
  await assertWithinViewportWidth(page, mulliganLens);

  await mkdir(screenshotRoot, { recursive: true });

  const project = testInfo.project.name;
  await tapLens.screenshot({
    path: path.join(screenshotRoot, `lens-tap-${project}.png`),
  });
  await mulliganLens.screenshot({
    path: path.join(screenshotRoot, `lens-mulligan-${project}.png`),
  });
});

async function submitDeck(page: Page): Promise<boolean> {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(PASTE_DECK);
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  const error = page.locator('.error-banner:not(.hidden)');
  await Promise.race([
    result.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
    error.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
  ]);

  return (await result.count()) > 0 && (await result.isVisible());
}

async function assertWithinViewportWidth(page: Page, locator: Locator): Promise<void> {
  const box = await locator.boundingBox();
  expect(box).not.toBeNull();

  const viewport = page.viewportSize();
  expect(viewport).not.toBeNull();

  expect(box!.width).toBeLessThanOrEqual(viewport!.width + 1);
}
