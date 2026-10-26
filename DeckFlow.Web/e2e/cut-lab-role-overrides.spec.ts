import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { expandCutLabSection } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';
import { resolveE2EPort } from './support/e2e-port';

const baseUrl = `http://localhost:${resolveE2EPort()}`;

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

const fillImportForm = async (page: Page): Promise<void> => {
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
};

const importPool = async (page: Page): Promise<void> => {
  await page.goto(`${baseUrl}/cut-lab`);
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await fillImportForm(page);
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
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


test('edits card roles, preserves them through locks, and resets inferred roles', async ({ page }) => {
  await importPool(page);
  const solRing = page.locator('tr[data-cut-lab-card="Sol Ring"]');
  const roleCell = solRing.locator('[data-label="Type / role"]');
  const mainState = page.locator('form[data-cache-key="cut-lab"] input[name="CutLabStateJson"]');
  const editor = page.locator('#cut-lab-role-editor');
  const cardSelect = editor.locator('[data-cut-lab-role-override-card]');
  const roleSelect = editor.locator('[data-cut-lab-role-override-role]');

  await expect(roleCell).toContainText('Ramp');
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"] [data-cut-lab-role-edit-card]')).toHaveCount(0);
  await solRing.getByRole('link', { name: 'Edit roles for Sol Ring' }).click();
  await expect(cardSelect).toHaveValue('Sol Ring');
  await roleSelect.selectOption({ label: 'Card draw' }, { force: true });
  await editor.getByRole('button', { name: 'Add role', exact: true }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(roleCell).toContainText('Card draw');
  await expect(solRing.locator('.cutlab-role-edited-chip')).toHaveText('Edited');
  await expect(mainState).toHaveValue(/"roleOverrides":\[\{"name":"Sol Ring"/);

  await cardSelect.selectOption('Sol Ring', { force: true });
  await roleSelect.selectOption({ label: 'Ramp' }, { force: true });
  await editor.getByRole('button', { name: 'Remove role', exact: true }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(roleCell).not.toContainText('Ramp');
  await expect(roleCell).toContainText('Card draw');

  const lock = page.locator('[data-cut-lab-lock-card="Arcane Signet"]');
  await lock.setChecked(!(await lock.isChecked()));
  await expect(mainState).toHaveValue(/"roleOverrides":\[\{"name":"Sol Ring"/);

  await cardSelect.selectOption('Sol Ring', { force: true });
  await editor.getByRole('button', { name: 'Reset card roles', exact: true }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(solRing.locator('.cutlab-role-edited-chip')).toHaveCount(0);
  await expect(roleCell).toContainText('Ramp');
  await expect(roleCell).not.toContainText('Card draw');
  await cardSelect.selectOption('Plains', { force: true });
  await expect(roleSelect.locator('option[value="ramp"]')).toBeDisabled();
});
