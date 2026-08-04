import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { clickManabasePillRadio } from './support/manabase-pill';

const oversizedPool = [
  'Commander',
  '1 Zur the Enchanter',
  '',
  'Deck',
  '36 Plains',
  '36 Island',
  '20 Swamp',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Fellwar Stone',
  '1 Mystic Remora',
  '1 Rhystic Study',
  '1 Swords to Plowshares',
  '1 Path to Exile',
  '1 Counterspell',
  '1 Dovin\'s Veto',
  '1 Demonic Tutor',
  '1 Enlightened Tutor',
  '1 Command Tower',
  '1 Exotic Orchard',
].join('\n');

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

const importPool = async (page: Page): Promise<void> => {
  await page.goto('/cut-lab');
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await page.locator('#cut-lab-primary-plan').fill('Protect the control shell, then trim to the cleanest Zur line.');
  await page.locator('#cut-lab-secondary-plan').fill('Keep the fast mana package intact.');
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' }).locator(':scope > summary').click();
  await expect(page.locator('[data-cut-lab-lock-role="lands"]')).toBeVisible();
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
};

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('G-1 preserves the five-slot wizard contract in document order', async ({ page }) => {
  await importPool(page);

  await expect(page.locator('[role="tabpanel"]').evaluateAll((panels) => panels.map((panel) => panel.id))).resolves.toEqual([
    'cut-lab-step-panel-1',
    'cut-lab-step-panel-2',
    'cut-lab-step-panel-3',
    'cut-lab-step-panel-4',
    'cut-lab-step-panel-5',
  ]);
});

test('G-2 activates exactly one panel through native tab dispatch', async ({ page }) => {
  await importPool(page);

  const tabs = page.getByRole('tab').filter({ hasNot: page.locator('[disabled]') });
  const tabCount = await tabs.count();
  expect(tabCount).toBeGreaterThan(0);

  for (let index = 0; index < tabCount; index++) {
    await page.evaluate(() => window.scrollTo(0, 0));
    const tab = tabs.nth(index);
    const before = await page.locator('[role="tab"]').evaluateAll((buttons) => buttons.map((button) => button.getAttribute('aria-selected')));
    const beforePanels = await page.locator('[role="tabpanel"]').evaluateAll((panels) => panels.map((panel) => getComputedStyle(panel).display));
    await tab.evaluate((button) => {
      button.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    });

    const after = await page.locator('[role="tab"]').evaluateAll((buttons) => buttons.map((button) => button.getAttribute('aria-selected')));
    const afterPanels = await page.locator('[role="tabpanel"]').evaluateAll((panels) => panels.map((panel) => getComputedStyle(panel).display));
    expect(after).not.toEqual(before);
    expect(afterPanels).not.toEqual(beforePanels);
    await expect(tab).toHaveAttribute('aria-selected', 'true');
    await expect(page.locator('[role="tabpanel"]').evaluateAll((panels) => panels.filter((panel) => getComputedStyle(panel).display !== 'none').length)).resolves.toBe(1);
    await expect(page.locator('[role="tab"].is-active')).toHaveCount(1);
  }
});

test('G-3 keeps Decide page bulk below the desktop and mobile headroom thresholds', async ({ page }) => {
  await importPool(page);

  await page.evaluate(() => {
    const decideTab = Array.from(document.querySelectorAll<HTMLElement>('[role="tab"]'))
      .find((button) => button.textContent?.trim() === 'Decide');
    if (!decideTab || decideTab.matches('[disabled]')) return;
    decideTab.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
  });

  const height = await page.evaluate(() => document.documentElement.scrollHeight);
  const limit = page.viewportSize()?.width === 390 ? 4_000 : 3_000;
  expect(height, `Decide page height should be below ${limit}px`).toBeLessThan(limit);
});

test('G-4 collapses intake after import and exposes the commander summary', async ({ page }) => {
  await importPool(page);

  const deckTextarea = page.locator('#cut-lab-deck-text');
  const intakeIsCollapsed = await page.evaluate(() => {
    const textarea = document.querySelector('#cut-lab-deck-text');
    if (!textarea) return true;
    const disclosure = textarea.closest('details');
    return Boolean(disclosure && !disclosure.open);
  });
  expect(intakeIsCollapsed).toBe(true);
  const commanderSummary = page.locator('xpath=//*[self::summary or @data-cut-lab-intake-summary or @data-cut-lab-summary][contains(normalize-space(), "Zur the Enchanter") and not(ancestor::tr) and not(ancestor::table)]');
  await expect(commanderSummary).toBeVisible();
  await expect(commanderSummary).toBeInViewport();
});
