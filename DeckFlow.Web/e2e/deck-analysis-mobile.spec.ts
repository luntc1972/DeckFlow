import { expect, test, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const winotaDeck = readFileSync(join(__dirname, 'fixtures', 'winota-cedh.txt'), 'utf8').trim();

test.describe.configure({ mode: 'serial' });
test.setTimeout(120_000);

async function generateAnalysisPacket(page: Page): Promise<boolean> {
  await page.setViewportSize({ width: 390, height: 844 });

  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  await page.locator('select[name="DeckInputSource"]').selectOption('PasteText');
  await page.locator('textarea[name="DeckText"]').fill(winotaDeck);
  await page.getByRole('button', { name: 'Next: Analysis' }).click();

  const contextDetails = page.locator('details', {
    has: page.locator('summary', { hasText: 'Analysis context' }),
  });
  await expect(contextDetails).toBeVisible();
  await expect(contextDetails).toHaveJSProperty('open', true);

  await page.locator('select[name="TargetCommanderBracket"]').selectOption({ index: 1 });
  await page.locator('[data-question-bucket]').first().check();
  await page.getByRole('button', { name: 'Generate Analysis Packet' }).click();

  const analysisOutput = page.locator('#analysis-output');
  const errorBanner = page.locator('.error-banner:not(.hidden)');

  await Promise.race([
    analysisOutput.waitFor({ state: 'visible', timeout: 120_000 }).catch(() => undefined),
    errorBanner.waitFor({ state: 'visible', timeout: 120_000 }).catch(() => undefined),
  ]);

  return (await analysisOutput.count()) > 0 && (await analysisOutput.isVisible());
}

test('deck-analysis result state does not overflow horizontally on mobile after packet generation', async ({ page }) => {
  test.skip(!test.info().project.name.includes('mobile'), 'mobile-only coverage');

  const ok = await generateAnalysisPacket(page);
  test.skip(!ok, 'analysis packet unavailable (Scryfall not reachable in this environment)');

  expect(
    await page.evaluate(
      () => (document.scrollingElement?.scrollWidth ?? document.documentElement.scrollWidth)
        <= (document.scrollingElement?.clientWidth ?? document.documentElement.clientWidth) + 1,
    ),
  ).toBe(true);
});
