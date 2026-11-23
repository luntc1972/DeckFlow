// Keep Cut Lab imports on the same response wait because Debug builds run a sim-heavy pipeline.
import { expect, type Page, type Response } from '@playwright/test';
import { expandCutLabSection } from './cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './manabase-pill';
import { resolveE2EPort } from './e2e-port';

interface FillImportPoolOptions {
  bracket?: string;
  playExperience?: string;
  scriptsDisabled?: boolean;
}

interface ImportPoolOptions extends FillImportPoolOptions {
  landing?: 'lock-pool' | 'plan-panel' | 'none';
  commander?: string;
}

export async function submitImportPool(page: Page): Promise<Response> {
  // The MDFC spec established that a 60s budget and an explicit POST wait handle the Debug pipeline.
  const [response] = await Promise.all([
    page.waitForResponse(response => {
      return new URL(response.url()).pathname === '/cut-lab' && response.request().method() === 'POST';
    }, { timeout: 60_000 }),
    page.getByRole('button', { name: 'Import pool' }).click({ timeout: 60_000 }),
  ]);
  return response;
}

export async function fillImportPool(
  page: Page,
  pool: string,
  { bracket = '4', playExperience = 'Focused', scriptsDisabled = false }: FillImportPoolOptions = {},
): Promise<void> {
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  if (scriptsDisabled) {
    // The paste panel toggle depends on JavaScript.
    await page.evaluate(() => {
      document.querySelector('[data-sync-panel="cut-lab-deck-text"]')?.classList.remove('hidden');
      document.querySelector('[data-sync-panel="cut-lab-deck-url"]')?.classList.add('hidden');
    });
  }

  await page.locator('#cut-lab-deck-text').fill(pool);
  await clickManabasePillRadio(page, 'Bracket', bracket);
  await clickManabasePillRadio(page, 'PlayExperience', playExperience);
}

export async function importPool(page: Page, pool: string, options: ImportPoolOptions = {}): Promise<void> {
  const { scriptsDisabled = false, landing = 'lock-pool', commander } = options;

  // Manually created no-JS contexts need an absolute URL.
  await page.goto(`http://localhost:${resolveE2EPort()}/cut-lab`);
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await fillImportPool(page, pool, options);
  await submitImportPool(page);

  if (landing === 'lock-pool') {
    await expandCutLabSection(page, 'cut-lab-section-lock-pool', !scriptsDisabled);
    await expect(page.getByRole('heading', { name: 'Lock your pool' }))
      .toBeVisible({ timeout: scriptsDisabled ? 60_000 : 30_000 });
  } else if (landing === 'plan-panel') {
    await expandCutLabSection(page, 'cut-lab-section-plan-panel');
    await expect(page.locator('[data-cut-lab-plan-panel]')).toBeVisible({ timeout: 30_000 });
  }

  if (commander !== undefined) {
    await expect(page.locator(`tr[data-cut-lab-card="${commander}"]`)).toHaveAttribute('data-cut-lab-commander', 'true');
  }
}
