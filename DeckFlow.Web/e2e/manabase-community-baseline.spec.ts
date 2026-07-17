import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type Page, type TestInfo } from '@playwright/test';

// Screenshot sink for the manual/CI visual sweep; when unset the shots go to the test output dir.
const screenshotRoot = process.env.DECKFLOW_BASELINE_SHOT_DIR;

// A real Azorius shell that resolves cleanly on Scryfall, so a full analysis (and therefore the
// community-baseline line) renders. Mirrors the deck used by manabase-lens-visual.spec.ts.
const PASTE_DECK = [
  'Commander',
  '1 Brago, King Eternal',
  '',
  'Deck',
  '1 Command Tower',
  '1 Exotic Orchard',
  '1 Prairie Stream',
  '1 Tranquil Cove',
  '1 Glacial Fortress',
  '1 Adarkar Wastes',
  '1 Nimbus Maze',
  '1 Seachrome Coast',
  '10 Plains',
  '10 Island',
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
  '1 Sun Titan',
].join('\n');

async function enableBaselineFlag(page: Page): Promise<void> {
  await page.goto('/Admin/Flags');
  const row = page.locator('tr[data-flag-key="analysis.manabase.baseline"]');
  await expect(row).toHaveCount(1);
  if ((await row.getAttribute('data-flag-enabled')) !== 'true') {
    await row.locator('form.admin-action-form button[type="submit"]').click();
    await page.waitForLoadState('networkidle');
  }
  const refreshed = page.locator('tr[data-flag-key="analysis.manabase.baseline"]');
  expect(await refreshed.getAttribute('data-flag-enabled')).toBe('true');
}

async function screenshotPath(testInfo: TestInfo, defaultName: string, prefix: string): Promise<string> {
  if (!screenshotRoot) {
    return testInfo.outputPath(defaultName);
  }

  await mkdir(screenshotRoot, { recursive: true });
  return path.join(screenshotRoot, `${prefix}-${testInfo.project.name}.png`);
}

test('community baseline selector and line render when the flag is on', async ({ page }, testInfo) => {
  test.skip(!process.env.DECKFLOW_LIVE_E2E, 'live-only: needs Scryfall + the admin flag toggle');

  await enableBaselineFlag(page);

  // The B2-B5 selector renders pre-analysis (flag on).
  await page.goto('/manabase');
  const selector = page.locator(
    'fieldset.manabase-segmented:has(legend:has-text("Community baseline bracket"))');
  await expect(selector).toBeVisible();
  // No Exhibition/B1 pill; the four supported brackets + Auto are present.
  await expect(selector).toContainText('B2 Core');
  await expect(selector).toContainText('B5 cEDH');
  await expect(selector).not.toContainText('Exhibition');
  await selector.screenshot({ path: await screenshotPath(testInfo, 'selector.png', 'baseline-selector') });

  // Full analysis -> the community-baseline line renders beside the Karsten line.
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(PASTE_DECK);
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  const error = page.locator('.error-banner:not(.hidden)');
  await Promise.race([
    result.waitFor({ state: 'visible', timeout: 40_000 }).catch(() => undefined),
    error.waitFor({ state: 'visible', timeout: 40_000 }).catch(() => undefined),
  ]);
  test.skip((await result.count()) === 0 || !(await result.isVisible()),
    'analysis result unavailable (Scryfall not reachable in this environment)');

  const line = page.locator('.manabase-community-baseline');
  await expect(line).toBeVisible();
  await expect(line).toContainText('Community baseline');
  await line.screenshot({ path: await screenshotPath(testInfo, 'line.png', 'baseline-line') });

  // Whole result panel for layout context at this viewport.
  await result.screenshot({ path: await screenshotPath(testInfo, 'result.png', 'baseline-result') });
});
