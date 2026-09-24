import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { withToolEnabled } from './support/admin-tools';
import { assignWithKeyboard } from './support/deck-modules-assign';

const winotaDeck = readFileSync(join(__dirname, 'fixtures', 'winota-cedh.txt'), 'utf8').trim();

test.describe.configure({ mode: 'serial' });
test.setTimeout(120_000);
withToolEnabled('Deck Modules');

test('shows blank-slate guidance before import and contextual guidance after import', async ({ page }) => {
  await page.goto('/deck-modules');
  await expect(page.locator('[data-deck-modules-next]')).toHaveText('Start by importing a baseline deck below.');
  await expect(page.locator('[data-deck-modules-empty]')).toBeVisible();
  await expect(page.locator('[data-deck-modules-compile]')).toHaveAttribute('aria-disabled', 'true');
  await expect(page.locator('.deck-modules__howto')).not.toHaveAttribute('open');

  await page.locator('#deck-modules-input-source').selectOption('PasteText');
  await page.locator('#deck-modules-deck-text').fill(winotaDeck);
  const importResponse = page.waitForResponse('/deck-modules/import');
  await page.getByRole('button', { name: 'Import deck' }).click();
  expect((await importResponse).status()).toBe(200);

  await expect(page.locator('[data-deck-modules-empty]')).toBeHidden();
  await expect(page.locator('[data-deck-modules-next]')).toHaveText('Baseline imported. Name your first strategy alternative — you need 2 to 4.');
});

test('imports pasted decklist when hidden URL input contains a scheme-less URL', async ({ page }) => {
  await page.goto('/deck-modules');
  await page.locator('input[name="DeckUrl"]').fill('moxfield.com/decks/abc');
  await page.locator('#deck-modules-input-source').selectOption('PasteText');
  await page.locator('#deck-modules-deck-text').fill(winotaDeck);
  const importResponse = page.waitForResponse('/deck-modules/import');
  await page.getByRole('button', { name: 'Import deck' }).click();

  expect((await importResponse).status()).toBe(200);
});

test('uses the desktop hybrid top layout after import', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'chromium-desktop', 'Desktop-only layout check.');

  await page.goto('/deck-modules');
  await page.locator('#deck-modules-input-source').selectOption('PasteText');
  await page.locator('#deck-modules-deck-text').fill(winotaDeck);
  const importResponse = page.waitForResponse('/deck-modules/import');
  await page.getByRole('button', { name: 'Import deck' }).click();
  expect((await importResponse).status()).toBe(200);

  await expect(page.locator('.deck-modules__commander [data-deck-modules-restart]')).toHaveCount(1);
  await expect(page.locator('.deck-modules__overview [data-deck-modules-alternatives]')).toHaveCount(1);
  await expect(page.locator('.deck-modules__overview [data-deck-modules-balance]')).toHaveCount(1);

  const nameBox = await page.locator('[data-deck-modules-name]').boundingBox();
  const profileBox = await page.locator('.deck-modules__configuration .df-select__trigger').boundingBox();
  expect(nameBox).not.toBeNull();
  expect(profileBox).not.toBeNull();
  expect(Math.abs(nameBox!.y - profileBox!.y)).toBeLessThanOrEqual(4);
  await expect(page.locator('.deck-modules__unassigned')).toHaveAttribute('open', '');

  const coreBox = await page.locator('[data-deck-modules-entries="core"]').locator('xpath=ancestor::section[1]').boundingBox();
  const pathBox = await page.locator('.deck-modules__path').boundingBox();
  const howtoBox = await page.locator('.deck-modules__howto').boundingBox();
  const workspaceBox = await page.locator('.deck-modules__workspace').boundingBox();
  const actionsBox = await page.locator('.deck-modules__actions').boundingBox();
  expect(coreBox).not.toBeNull();
  expect(pathBox).not.toBeNull();
  expect(howtoBox).not.toBeNull();
  expect(workspaceBox).not.toBeNull();
  expect(actionsBox).not.toBeNull();
  expect(Math.abs(coreBox!.y - pathBox!.y)).toBeLessThanOrEqual(8);
  expect(coreBox!.x).toBeLessThan(pathBox!.x);
  expect(coreBox!.width).toBeLessThan(pathBox!.width);
  expect(Math.abs(howtoBox!.width - workspaceBox!.width)).toBeLessThanOrEqual(1);
  expect(actionsBox!.y).toBeGreaterThan(workspaceBox!.y + workspaceBox!.height - 1);
});

test('stacks workspace without horizontal overflow on mobile', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'chromium-mobile', 'Mobile-only layout check.');

  await page.goto('/deck-modules');
  await page.locator('#deck-modules-input-source').selectOption('PasteText');
  await page.locator('#deck-modules-deck-text').fill(winotaDeck);
  const importResponse = page.waitForResponse('/deck-modules/import');
  await page.getByRole('button', { name: 'Import deck' }).click();
  expect((await importResponse).status()).toBe(200);

  const coreBox = await page.locator('[data-deck-modules-entries="core"]').locator('xpath=ancestor::section[1]').boundingBox();
  const pathBox = await page.locator('.deck-modules__path').boundingBox();
  expect(coreBox).not.toBeNull();
  expect(pathBox).not.toBeNull();
  expect(pathBox!.y).toBeGreaterThan(coreBox!.y + coreBox!.height - 4);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBeTruthy();
});

test('imports, assigns, compiles, and remains usable at the current viewport', async ({ page }, testInfo) => {
  const response = await page.goto('/deck-modules');
  expect(response?.ok(), '/deck-modules should return 200 with flag ON').toBeTruthy();

  await expect(page.getByRole('heading', { name: 'Deck Modules' })).toBeVisible();
  await expect(page.locator('.tool-nav__link.is-active')).toHaveText('Deck Modules');
  await expect(page.locator('.deck-modules__report')).toBeHidden();
  await expect(page.locator('[data-deck-modules-copy]')).toBeDisabled();
  await expect(page.locator('[data-deck-modules-export]')).toBeDisabled();
  await expect(page.locator('[data-deck-modules]').getByRole('button', { name: /share|save|project/i })).toHaveCount(0);

  await page.locator('#deck-modules-input-source').selectOption('PasteText');
  await page.locator('#deck-modules-deck-text').fill(winotaDeck);
  const importResponse = page.waitForResponse('/deck-modules/import');
  await page.getByRole('button', { name: 'Import deck' }).click();
  const imported = await importResponse;
  expect(imported.status()).toBe(200);
  await expect(page.locator('[data-deck-modules-entries="unassigned"] [data-deck-modules-select]')).not.toHaveCount(0);

  await page.locator('[data-deck-modules-name]').fill('Winota Combat');
  await page.locator('[data-deck-modules-profile]').selectOption('Cedh');
  await page.locator('[data-deck-modules-plan]').fill('Trigger Winota early and pressure every combat.');
  await page.locator('[data-deck-modules-add-alternative]').click();
  await expect(page.locator('[data-deck-modules-alternative]')).toHaveCount(1);
  await expect(page.locator('[data-deck-modules-summary-profile]')).toHaveText('cEDH');
  await expect(page.locator('[data-deck-modules-summary-plan]')).toContainText('Trigger Winota early');

  await assignWithKeyboard(page, 'unassigned', 'core');
  await assignWithKeyboard(page, 'unassigned', 'strategy');
  await assignWithKeyboard(page, 'unassigned', 'mana');
  await expect(page.locator('[data-deck-modules-live]')).toContainText('Moved 1 card entries');

  await page.locator('[data-deck-modules-name]').fill('Winota Stax');
  await page.locator('[data-deck-modules-profile]').selectOption('Cedh');
  await page.locator('[data-deck-modules-plan]').fill('Lock opponents while Winota supplies pressure.');
  await page.locator('[data-deck-modules-add-alternative]').click();
  await expect(page.locator('[data-deck-modules-alternative]')).toHaveCount(2);
  await expect(page.locator('[data-deck-modules-active-name]')).toHaveText('Winota Stax');

  await assignWithKeyboard(page, 'unassigned', 'strategy');
  await assignWithKeyboard(page, 'unassigned', 'mana');
  await expect(page.locator('[data-deck-modules-balance]')).toHaveText('Alternatives are balanced.');
  await expect(page.locator('[data-deck-modules-compile]')).toBeEnabled();

  const outline = await page.locator('[data-deck-modules-move="unassigned:core"]').evaluate(element => {
    const style = getComputedStyle(element);
    return `${style.outlineStyle}:${style.outlineWidth}`;
  });
  expect(outline).not.toBe('none:0px');

  await page.locator('[data-deck-modules-compile]').click();
  await expect(page.locator('.deck-modules__report')).toBeVisible();
  await expect(page.locator('[data-deck-modules-report-total]')).toHaveText('3');
  await expect(page.locator('[data-deck-modules-diagnostics] li')).toContainText(['The imported command zone is empty.', 'Compiled deck has 3 cards; a Commander deck needs exactly 100.']);
  await expect(page.locator('[data-deck-modules-diagnostics]')).not.toContainText('[object Object]');
  await expect(page.locator('[data-deck-modules-report-strategy]')).toHaveText('Winota Stax');
  await expect(page.locator('[data-deck-modules-report-mana]')).toHaveText('Winota Stax Mana Support');
  await expect(page.locator('[data-deck-modules-swap="add"]')).toBeAttached();
  await expect(page.locator('[data-deck-modules-swap="reset"]')).toBeAttached();
  await expect(page.locator('[data-deck-modules-swap="remove"] li')).not.toHaveCount(0);
  await expect(page.locator('[data-deck-modules-copy]')).toBeEnabled();
  await expect(page.locator('[data-deck-modules-export]')).toBeEnabled();

  await page.screenshot({ path: testInfo.outputPath(`deck-modules-${testInfo.project.name}.png`), fullPage: true });
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth);
  expect(overflow).toBeTruthy();

  if (testInfo.project.name === 'chromium-mobile') {
    await expect(page.locator('.deck-modules__assignment').first()).toBeVisible();
    await expect(page.locator('[data-deck-modules-active-name]')).toBeVisible();
    await expect(page.locator('[data-deck-modules-compile]')).toBeVisible();
  }
});
