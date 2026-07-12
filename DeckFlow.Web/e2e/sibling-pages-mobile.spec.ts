import { expect, test, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const winotaDeck = readFileSync(join(__dirname, 'fixtures', 'winota-cedh.txt'), 'utf8').trim();
const winotaDeckVariant = winotaDeck
  .split('\n')
  .filter((line) => !line.includes('Aether Vial') && !line.includes('Arcane Signet') && !line.includes('Ragavan, Nimble Pilferer'))
  .join('\n');

test.describe.configure({ mode: 'serial' });
test.setTimeout(180_000);

async function expectNoHorizontalOverflow(page: Page): Promise<void> {
  expect(
    await page.evaluate(
      () => (document.scrollingElement?.scrollWidth ?? document.documentElement.scrollWidth)
        <= (document.scrollingElement?.clientWidth ?? document.documentElement.clientWidth) + 1,
    ),
  ).toBe(true);
}

async function generatePrimer(page: Page): Promise<boolean> {
  await page.setViewportSize({ width: 390, height: 844 });

  const response = await page.goto('/deck-primer');
  expect(response?.ok()).toBeTruthy();

  await page.locator('select[name="DeckInputSource"]').selectOption('PasteText');
  await page.locator('textarea[name="DeckText"]').fill(winotaDeck);
  await page.locator('select[name="TargetCommanderBracket"]').selectOption('cEDH');
  await page.getByRole('button', { name: 'Generate Primer' }).click();

  const primerOutput = page.locator('#primer-output');
  const errorBanner = page.locator('.error-banner:not(.hidden)');

  const outcome = await Promise.race([
    primerOutput.waitFor({ state: 'visible', timeout: 120_000 }).then(() => 'output').catch(() => 'timeout'),
    errorBanner.waitFor({ state: 'visible', timeout: 120_000 }).then(() => 'error').catch(() => 'timeout'),
  ]);

  return outcome === 'output';
}

async function generateComparison(page: Page): Promise<boolean> {
  await page.setViewportSize({ width: 390, height: 844 });

  const response = await page.goto('/deck-comparison');
  expect(response?.ok()).toBeTruthy();

  await page.locator('textarea[name="DeckASource"]').fill(winotaDeck);
  await page.locator('textarea[name="DeckBSource"]').fill(winotaDeckVariant);
  await page.locator('select[name="DeckABracket"]').selectOption('cEDH');
  await page.locator('select[name="DeckBBracket"]').selectOption('cEDH');
  await page.getByRole('button', { name: 'Next: Generate Packet' }).click();
  await page.getByRole('button', { name: 'Generate Comparison Packet' }).click();

  const comparisonOutput = page.locator('#comparison-prompt-output');
  const errorBanner = page.locator('.error-banner:not(.hidden)');

  const outcome = await Promise.race([
    comparisonOutput.waitFor({ state: 'visible', timeout: 120_000 }).then(() => 'output').catch(() => 'timeout'),
    errorBanner.waitFor({ state: 'visible', timeout: 120_000 }).then(() => 'error').catch(() => 'timeout'),
  ]);

  return outcome === 'output';
}

test('deck-primer result state does not overflow horizontally on mobile after generation', async ({ page }) => {
  test.skip(!test.info().project.name.includes('mobile'), 'mobile-only coverage');

  const ok = await generatePrimer(page);
  test.skip(!ok, 'primer prompt unavailable (Scryfall not reachable in this environment)');

  await expectNoHorizontalOverflow(page);
});

test('deck-comparison result state does not overflow horizontally on mobile after packet generation', async ({ page }) => {
  test.skip(!test.info().project.name.includes('mobile'), 'mobile-only coverage');

  const ok = await generateComparison(page);
  test.skip(!ok, 'comparison packet unavailable (Scryfall not reachable in this environment)');

  await expectNoHorizontalOverflow(page);
});
