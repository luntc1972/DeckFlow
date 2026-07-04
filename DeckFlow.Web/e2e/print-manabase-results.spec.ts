import { expect, test } from '@playwright/test';

// Manabase is the "separate results container" print case: its rendered result
// <section data-print-region> is a top-level sibling of the input <form>, so the
// universal @media print rule (hide every content-shell child except the marked
// region) applies with no form-scoped rules. Rendering requires a live Scryfall
// round-trip, so — like manabase-castability.spec.ts — this skips when the result
// panel never appears (Scryfall unreachable in the sandbox).
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

test('manabase print view strips chrome and keeps the result readable', async ({ page }, testInfo) => {
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
  const analyzed = (await result.count()) > 0 && (await result.isVisible());
  test.skip(!analyzed, 'Scryfall unreachable in this environment — cannot render the manabase result to print.');

  const printButton = page.locator('button[data-chatgpt-print]').first();
  await expect(printButton).toBeVisible();

  // Switch to print media — the shared @media print rules apply.
  await page.emulateMedia({ media: 'print' });

  // Site chrome and the whole input form are stripped; the in-panel Print button too.
  await expect(page.locator('.page-header')).toBeHidden();
  await expect(page.locator('form.result-panel')).toBeHidden();
  await expect(printButton).toBeHidden();

  // The rendered result section survives and stays readable.
  await expect(result).toBeVisible();
  await expect(result.getByRole('heading', { name: 'Result' })).toBeVisible();

  // Paper does not overflow horizontally.
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();

  await page.screenshot({ path: `${testInfo.outputDir}/manabase-print-${testInfo.project.name}.png`, fullPage: true });
});
