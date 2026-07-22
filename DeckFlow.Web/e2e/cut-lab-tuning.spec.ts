import { expect, test, type Locator, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';

const baseUrl = 'http://localhost:5173';
const screenshotDir = resolve(__dirname, '../../.planning/ui-design/cut-lab/screenshots');

const guildThemes = [
  { name: 'azorius', cookie: 'site-azorius.css' },
  { name: 'nyx', cookie: 'site-nyx.css' },
] as const;

const viewports = [
  { name: 'desktop', width: 1440, height: 1100 },
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

const getExportTab = (page: Page): Locator => page.locator('#cut-lab-step-tab-4');

const getExportPanel = (page: Page): Locator => page.locator('#cut-lab-step-panel-4');

const getMainStateInput = (page: Page): Locator =>
  page.locator('form[data-cache-key="cut-lab"] input[name="CutLabStateJson"]');

const getScenarioRow = (page: Page, scenarioName: string): Locator =>
  page.locator('.cutlab-scenarios__item').filter({
    has: page.locator('strong.cutlab-scenarios__name', { hasText: scenarioName }),
  });

const fillImportForm = async (page: Page, primaryPlan: string): Promise<void> => {
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await page.locator('#cut-lab-primary-plan').fill(primaryPlan);
  await page.locator('#cut-lab-secondary-plan').fill('Keep the fast mana package intact.');
  await page.locator('input[name="Bracket"][value="4"]').check();
  await page.locator('input[name="PlayExperience"][value="Focused"]').check();
};

const importPool = async (page: Page, primaryPlan: string): Promise<void> => {
  await page.goto(`${baseUrl}/cut-lab`);
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await fillImportForm(page, primaryPlan);
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
};

const lockLands = async (page: Page): Promise<void> => {
  const landsGroup = page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' });
  await landsGroup.locator('summary').click();
  const lockAllButton = landsGroup.locator('[data-cut-lab-lock-role="lands"]');
  await expect(lockAllButton).toHaveAttribute('aria-pressed', 'false');
  await lockAllButton.click();
  await expect(lockAllButton).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('tr[data-cut-lab-card="Island"] input[data-cut-lab-lock-card]')).toBeChecked();
};

const waitForCutRounds = async (page: Page): Promise<void> => {
  await expect(page.getByRole('heading', { name: 'Cut rounds' })).toBeVisible();
  await expect(page.locator('.cutlab-round-banner .cutlab-finding__heading')).toBeVisible();
  await expect(page.locator('.cutlab-proposal')).toBeVisible();
};

const getStickyRemainingCount = async (page: Page): Promise<number> => {
  const stickyText = await page.locator('[data-cut-lab-sticky-remaining]').textContent();
  const match = stickyText?.match(/^(\d+) to cut$/);
  return Number.parseInt(match?.[1] ?? '0', 10);
};

const acceptUntilRemaining = async (page: Page, targetRemaining: number): Promise<void> => {
  const headingLocator = page.locator('.cutlab-proposal__heading');
  for (let guard = 0; guard < 12; guard += 1) {
    await page.waitForLoadState('networkidle');
    const remaining = await getStickyRemainingCount(page);
    if (remaining <= targetRemaining) {
      return;
    }

    const heading = (await headingLocator.textContent())?.trim() ?? '';
    await page.locator('.cutlab-proposal .cutlab-decision-btn--accept').first().click();
    await expect(headingLocator).not.toHaveText(heading, { timeout: 15_000 });
  }

  throw new Error(`Expected to reach ${targetRemaining} cards remaining to cut within 12 accepts.`);
};

const tunerRow = (page: Page, cardName: string): Locator =>
  page.locator(`tr[data-cut-lab-tuner-row="${cardName}"]`);

const tunerQuantity = async (page: Page, cardName: string): Promise<number> => {
  const value = await tunerRow(page, cardName).locator('[data-cut-lab-quantity-value]').textContent();
  return Number.parseInt(value?.trim() ?? '0', 10);
};

const clickStepper = async (page: Page, cardName: string, delta: -1 | 1, times: number): Promise<void> => {
  for (let click = 0; click < times; click += 1) {
    const row = tunerRow(page, cardName);
    const quantityBefore = await tunerQuantity(page, cardName);
    await row.locator(`[data-cut-lab-adjust][data-cut-lab-delta="${delta}"]`).click();
    await expect(row.locator('[data-cut-lab-quantity-value]')).toHaveText(`${quantityBefore + delta}`, { timeout: 15_000 });
  }
};

const addBasic = async (page: Page, basicName: string): Promise<void> => {
  await page.locator('[data-cut-lab-add-basic-select]').selectOption(basicName);
  await page.locator('button[data-cut-lab-add-basic]').click();
  await expect(tunerRow(page, basicName)).toBeVisible({ timeout: 15_000 });
};

const clearScenarioStorage = async (page: Page): Promise<void> => {
  if (page.isClosed()) {
    return;
  }

  try {
    if (!page.url().startsWith(baseUrl)) {
      await page.goto('/cut-lab');
    }

    await page.evaluate(() => {
      for (const key of Object.keys(window.localStorage)) {
        if (key === 'deckflow.cutlab.scenario-index' || key.startsWith('deckflow.cutlab.scenario.')) {
          window.localStorage.removeItem(key);
        }
      }
    });
  } catch {
    // Best-effort cleanup only.
  }
};

const clearCutLabSessionCache = async (page: Page): Promise<void> => {
  await page.evaluate(() => {
    window.sessionStorage.removeItem('decksync-form-state-cut-lab');
    window.sessionStorage.removeItem('decksync-form-state-cut-lab:savedAt');
    window.sessionStorage.removeItem('deckflow.last-deck');
  });
};

const tuneToExactHundred = async (page: Page): Promise<void> => {
  await importPool(page, 'Protect the control shell, then trim to the cleanest Zur line.');
  await lockLands(page);
  await waitForCutRounds(page);
  await acceptUntilRemaining(page, 2);

  await expect(page.locator('[data-cut-lab-sticky-remaining]')).toHaveText('2 to cut');
  await expect(tunerRow(page, 'Island')).toBeVisible();
  await expect(tunerRow(page, 'Island').locator('td[data-label="Role"]')).toContainText('Lands');

  await clickStepper(page, 'Island', -1, 3);
  await expect(page.locator('[data-cut-lab-sticky-remaining]')).toHaveText('0 to cut');
  await expect(getExportTab(page)).toBeDisabled();

  await addBasic(page, 'Wastes');
  await expect(tunerRow(page, 'Wastes')).toContainText('Added');
  await expect(tunerRow(page, 'Wastes').locator('td[data-label="Role"]')).toContainText('Lands');
  await expect(page.locator('[data-cut-lab-sticky-remaining]')).toHaveText('0 to cut');
};

const setTheme = async (page: Page, themeCookie: string): Promise<void> => {
  await page.context().clearCookies();
  await page.context().addCookies([{ name: 'deckflow-theme', value: themeCookie, url: baseUrl }]);
};

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async ({ page }) => {
  try {
    await clearScenarioStorage(page);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

test('tunes to exactly 100, exports tuned counts, and reloads a saved scenario with adjustments intact', async ({ page }) => {
  const scenarioName = 'Exact 100 tuned';
  const freshPrimaryPlan = 'Fresh import only: favor mana density before trimming.';

  await tuneToExactHundred(page);

  await expect(tunerRow(page, 'Island').locator('[data-cut-lab-quantity-value]')).toHaveText('33');
  await expect(tunerRow(page, 'Wastes').locator('[data-cut-lab-quantity-value]')).toHaveText('1');
  await expect(getExportTab(page)).toBeEnabled();
  await expect(getExportTab(page)).toHaveAttribute('aria-disabled', 'false');

  const stateInput = getMainStateInput(page);
  await expect(stateInput).toHaveValue(/"name":"Island","delta":-3,"isAddedBasic":false/);
  await expect(stateInput).toHaveValue(/"name":"Wastes","delta":1,"isAddedBasic":true/);

  await getExportTab(page).click();

  const exportPanel = getExportPanel(page);
  const moxfieldPatch = exportPanel.locator('#cut-lab-export-moxfield-patch');
  await expect(moxfieldPatch).toContainText('CUT');
  await expect(moxfieldPatch).toContainText('3 Island');
  await expect(moxfieldPatch).toContainText('ADD');
  await expect(moxfieldPatch).toContainText('1 Wastes');

  await page.locator('input[data-cut-lab-scenario-name]').fill(scenarioName);
  await page.locator('[data-cut-lab-scenario-save]').click();
  await expect(page.locator('[data-cut-lab-scenario-status]')).toHaveText('Scenario saved.');
  await expect(getScenarioRow(page, scenarioName)).toBeVisible();

  await page.locator('[data-clear-cache]').click();
  await expect(page.getByRole('heading', { name: 'No pool imported yet' })).toBeVisible({ timeout: 30_000 });
  await clearCutLabSessionCache(page);
  await page.evaluate(() => {
    const stateField = document.querySelector<HTMLInputElement>('form[data-cache-key="cut-lab"] input[name="CutLabStateJson"]');
    if (stateField) {
      stateField.value = '';
    }
  });

  await fillImportForm(page, freshPrimaryPlan);
  await page.getByRole('button', { name: 'Import pool' }).click();
  await waitForCutRounds(page);

  await expect(page.locator('#cut-lab-primary-plan')).toHaveValue(freshPrimaryPlan);
  await expect(page.locator('[data-cut-lab-sticky-remaining]')).not.toHaveText('0 to cut');

  await getScenarioRow(page, scenarioName).getByRole('button', { name: 'Load' }).click();

  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('[data-cut-lab-sticky-remaining]')).toHaveText('0 to cut');
  await expect(page.locator('#cut-lab-primary-plan')).toHaveValue('Protect the control shell, then trim to the cleanest Zur line.');
  await expect(tunerRow(page, 'Island').locator('[data-cut-lab-quantity-value]')).toHaveText('33');
  await expect(tunerRow(page, 'Wastes').locator('[data-cut-lab-quantity-value]')).toHaveText('1');
  await expect(getExportTab(page)).toBeEnabled();
  await expect(getMainStateInput(page)).toHaveValue(/"name":"Island","delta":-3,"isAddedBasic":false/);
  await expect(getMainStateInput(page)).toHaveValue(/"name":"Wastes","delta":1,"isAddedBasic":true/);
});

test('captures the tuner screenshot matrix across guild themes and desktop/mobile viewports', async ({ page }) => {
  mkdirSync(screenshotDir, { recursive: true });

  for (const viewport of viewports) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height });

    for (const theme of guildThemes) {
      await setTheme(page, theme.cookie);
      await tuneToExactHundred(page);

      const tuner = page.locator('section.cutlab-tuner');
      await tuner.scrollIntoViewIfNeeded();
      await tuner.screenshot({
        path: join(screenshotDir, `tuning-${theme.name}-${viewport.name}-${test.info().project.name}.png`),
      });
    }
  }
});
