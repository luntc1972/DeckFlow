import { expect, test, type Locator, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';

const baseUrl = 'http://localhost:5173';
const screenshotDir = resolve(__dirname, '../../.planning/ui-design/cut-lab/screenshots');

const themes = [
  { name: 'classic', cookie: 'site.css' },
  { name: 'azorius', cookie: 'site-azorius.css' },
  { name: 'nyx', cookie: 'site-nyx.css' },
] as const;

const viewports = [
  { name: 'desktop', width: 1440, height: 1000 },
  { name: 'mobile', width: 430, height: 932 },
] as const;

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
  await page.locator('input[name="Bracket"][value="4"]').check();
  await page.locator('input[name="PlayExperience"][value="Focused"]').check();
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
};

const getRoleFloorRow = (page: Page, roleKey: string): Locator =>
  page.locator(`tr[data-cut-lab-floor-row="${roleKey}"]`);

const getFloorValue = async (row: Locator): Promise<number> => {
  const rawValue = await row.locator('input[data-cut-lab-floor]').inputValue();
  return Number.parseInt(rawValue, 10);
};

const getFloorCount = async (row: Locator): Promise<number> => {
  const rawCount = await row.getAttribute('data-cut-lab-floor-count');
  return Number.parseInt(rawCount ?? '0', 10);
};

const getFloorDefault = async (row: Locator): Promise<number> => {
  const rawDefault = await row.getAttribute('data-cut-lab-floor-default');
  return Number.parseInt(rawDefault ?? '0', 10);
};

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async ({ page }) => {
  try {
    await setToolEnabled(page, 'Cut Lab', false);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

test('renders the three structure sections with 8 collapsed role groups and 8 floor inputs', async ({ page }) => {
  await importPool(page);

  await expect(page.getByRole('heading', { name: 'How your pool competes' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Structural findings' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Role floors' })).toBeVisible();

  const roleGroups = page.locator('details.cutlab-role-group');
  await expect(roleGroups).toHaveCount(8);
  await expect(page.locator('details.cutlab-role-group[open]')).toHaveCount(0);
  await expect(page.locator('input[data-cut-lab-floor]')).toHaveCount(8);
});

test('opens the Lands group, shows member chips, and locks land rows from the group pill', async ({ page }) => {
  await importPool(page);

  const landsGroup = page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' });
  await landsGroup.locator('summary').click();

  await expect(landsGroup.locator('[data-cut-lab-chip-card="Plains"]')).toBeVisible();
  await expect(landsGroup.locator('[data-cut-lab-chip-card="Island"]')).toBeVisible();
  await expect(landsGroup.locator('[data-cut-lab-lock-role="lands"]')).toContainText('Lock all lands');

  await landsGroup.locator('[data-cut-lab-lock-role="lands"]').click();
  await expect(page.locator('tr[data-cut-lab-card="Plains"] input[data-cut-lab-lock-card]')).toBeChecked();
  await expect(page.locator('tr[data-cut-lab-card="Island"] input[data-cut-lab-lock-card]')).toBeChecked();
  await expect(page.locator('tr[data-cut-lab-card="Command Tower"] input[data-cut-lab-lock-card]')).toBeChecked();
});

test('marks interaction as adjusted after floor edits and writes roleFloors into hidden state', async ({ page }) => {
  await importPool(page);

  const interactionRow = getRoleFloorRow(page, 'interaction');
  const interactionInput = interactionRow.locator('input[data-cut-lab-floor="interaction"]');
  const interactionCount = await getFloorCount(interactionRow);
  const interactionDefault = await getFloorDefault(interactionRow);
  const validHighValue = Math.max(interactionDefault + 1, interactionCount - 1);

  await interactionInput.fill('99');
  await interactionInput.blur();
  await interactionInput.fill(`${validHighValue}`);
  await interactionInput.blur();

  await expect(interactionRow.locator('[data-cut-lab-floor-adjusted-badge]')).toBeVisible();
  await expect(page.locator('input[name="CutLabStateJson"]')).toHaveValue(
    /"roleFloors":\[.*"role":"interaction".*"isUserSet":true/,
  );
});

test('preserves the adjusted interaction floor and badge across Recalculate', async ({ page }) => {
  await importPool(page);

  const interactionRow = getRoleFloorRow(page, 'interaction');
  const interactionInput = interactionRow.locator('input[data-cut-lab-floor="interaction"]');
  const interactionCount = await getFloorCount(interactionRow);
  const persistedValue = Math.max((await getFloorDefault(interactionRow)) + 1, interactionCount - 1);

  await interactionInput.fill(`${persistedValue}`);
  await interactionInput.blur();
  await page.locator('[data-cut-lab-recalculate]').click();

  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expect(getRoleFloorRow(page, 'interaction').locator('input[data-cut-lab-floor="interaction"]')).toHaveValue(`${persistedValue}`);
  await expect(getRoleFloorRow(page, 'interaction').locator('[data-cut-lab-floor-adjusted-badge]')).toBeVisible();
});

test('shows the at floor marker when a floor is raised to within 1 of the role count', async ({ page }) => {
  await importPool(page);

  const interactionRow = getRoleFloorRow(page, 'interaction');
  const interactionInput = interactionRow.locator('input[data-cut-lab-floor="interaction"]');
  const interactionCount = await getFloorCount(interactionRow);
  const atFloorValue = Math.max(0, interactionCount - 1);

  await interactionInput.fill(`${atFloorValue}`);
  await interactionInput.blur();

  await expect(interactionRow.locator('[data-cut-lab-floor-at-marker]')).toContainText('at floor');
});

test('resets an adjusted role floor back to its default value', async ({ page }) => {
  await importPool(page);

  const interactionRow = getRoleFloorRow(page, 'interaction');
  const interactionInput = interactionRow.locator('input[data-cut-lab-floor="interaction"]');
  const defaultValue = await getFloorDefault(interactionRow);
  const interactionCount = await getFloorCount(interactionRow);
  const adjustedValue = Math.max(defaultValue + 1, interactionCount - 1);

  await interactionInput.fill(`${adjustedValue}`);
  await interactionInput.blur();
  await expect(interactionRow.locator('[data-cut-lab-floor-adjusted-badge]')).toBeVisible();

  await interactionRow.locator('[data-cut-lab-floor-reset="interaction"]').click();

  await expect(interactionInput).toHaveValue(`${defaultValue}`);
  await expect(interactionRow.locator('[data-cut-lab-floor-adjusted-badge]')).toBeHidden();
});

test('captures the structure screenshot matrix across themes and viewports', async ({ page }) => {
  mkdirSync(screenshotDir, { recursive: true });

  for (const viewport of viewports) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height });

    for (const theme of themes) {
      await page.context().clearCookies();
      await page.context().addCookies([{ name: 'deckflow-theme', value: theme.cookie, url: baseUrl }]);
      await importPool(page);
      await page.locator('details.cutlab-role-group').filter({ hasText: 'Interaction' }).locator('summary').click();
      await page.locator('input[data-cut-lab-floor="interaction"]').scrollIntoViewIfNeeded();

      await page.screenshot({
        path: join(screenshotDir, `structure-${theme.name}-${viewport.name}.png`),
        fullPage: true,
      });
    }
  }
});
