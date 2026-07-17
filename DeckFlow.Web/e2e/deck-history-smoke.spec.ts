import { expect, test } from '@playwright/test';
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

const baseUrl = 'http://localhost:5173';
const adminUser = process.env.FEEDBACK_ADMIN_USER ?? 'admin';
const adminPassword = process.env.FEEDBACK_ADMIN_PASSWORD ?? 'changeme-local';
const adminToolsUrl = `http://${adminUser}:${adminPassword}@localhost:5173/Admin/Tools`;
const screenshotDir = resolve(__dirname, '../../.planning/ui-design/deck-history/screenshots');

const DECK_V1 = [
  'Commander',
  '1 Zur the Enchanter',
  '',
  'Deck',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Brainstorm',
  '10 Plains',
  '10 Island',
  '10 Swamp',
].join('\n');

const DECK_V2 = [
  'Commander',
  '1 Zur the Enchanter',
  '',
  'Deck',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Mystic Remora',
  '10 Plains',
  '10 Island',
  '10 Swamp',
].join('\n');

const themes = [
  { name: 'classic', cookie: 'site.css' },
  { name: 'azorius', cookie: 'site-azorius.css' },
  { name: 'nyx', cookie: 'site-nyx.css' },
] as const;

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Deck History', true);
});

test.afterEach(async ({ page }) => {
  try {
    await setToolEnabled(page, 'Deck History', false);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

test('/deck-history renders the form when the flag is ON', async ({ page }) => {
  const response = await page.goto('/deck-history');
  expect(response?.ok(), '/deck-history should return 200 with flag ON').toBeTruthy();

  await expect(page.locator('h1')).toHaveText('Deck History');
  await expect(page.locator('input[type="file"][name="historyFile"]')).toBeVisible();
  await expect(page.locator('#deck-history-input-source')).toBeVisible();
  await expect(page.locator('#deck-history-notes')).toBeVisible();
  await expect(page.locator('form[action="/deck-history"]')).toBeVisible();
  await expect(page.locator('.history-timeline')).toHaveCount(0);
});

test('creates history, intercepts download, appends a second version, and captures screenshots across themes', async ({
  page,
}) => {
  mkdirSync(screenshotDir, { recursive: true });

  const projectName = test.info().project.name;
  const downloadDir = mkdtempSync(join(tmpdir(), 'deck-history-smoke-'));
  const uploadJsonPath = join(downloadDir, `${projectName}-download.json`);
  const finalHistoryJsonPath = join(downloadDir, `${projectName}-final-history.json`);

  await page.goto('/deck-history');
  await expect(page.locator('h1')).toHaveText('Deck History');
  await page.locator('#deck-history-input-source').selectOption('PasteText');
  await page.locator('#deck-history-deck-text').fill(DECK_V1);
  await page.locator('#deck-history-deck-name').fill('Zur Logbook');
  await page.locator('#deck-history-notes').fill('Initial list.');
  await page.getByRole('button', { name: 'Update history' }).click();

  await expect(page.locator('.history-timeline')).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('.history-warnings')).toContainText('Started a new history — version 1 saved.');
  await expect(page.locator('.history-warnings')).toContainText(
    'Deck has 34 cards — Commander decks run 100. Snapshot saved anyway.',
  );
  await expect(page.locator('.history-timeline tbody tr').first()).toContainText('Initial list.');

  const promptTextarea = page.locator('#deck-history-prompt');
  await expect(promptTextarea).toBeVisible();
  expect((await promptTextarea.inputValue()).trim()).not.toBe('');

  const downloadResponsePromise = page.waitForResponse((response) =>
    response.url().includes('/deck-history/download') && response.request().method() === 'POST',
  );
  await page.locator('form[action="/deck-history/download"]').evaluate(async (form) => {
    const response = await fetch((form as HTMLFormElement).action, {
      method: 'POST',
      body: new FormData(form as HTMLFormElement),
      credentials: 'same-origin',
      headers: { Accept: 'application/zip,*/*' },
    });
    await response.arrayBuffer();
  });
  const downloadResponse = await downloadResponsePromise;
  expect(downloadResponse.ok(), 'download response should succeed').toBeTruthy();
  expect(downloadResponse.headers()['x-deckflow-filename']).toMatch(
    /^deck-history-zur-logbook-\d{8}\.json$/,
  );

  const downloadJson = await downloadResponse.text();
  writeFileSync(uploadJsonPath, downloadJson, 'utf8');

  await page.goto('/deck-history');
  await page.locator('#deck-history-input-source').selectOption('PasteText');
  await page.locator('input[type="file"][name="historyFile"]').setInputFiles(uploadJsonPath);
  await page.locator('#deck-history-deck-text').fill(DECK_V2);
  await page.locator('#deck-history-deck-name').fill('Zur Logbook');
  await page.locator('#deck-history-notes').fill('Swapped Brainstorm for Mystic Remora.');
  await page.getByRole('button', { name: 'Update history' }).click();

  await expect(page.locator('.history-timeline tbody tr')).toHaveCount(2, { timeout: 30_000 });
  await expect(page.locator('.history-warnings')).toContainText('Version 2 added.');
  await expect(page.locator('.history-diff')).toBeVisible();

  const addsPanel = page.locator('.history-diff__panel').filter({
    has: page.getByRole('heading', { name: 'Adds' }),
  });
  const cutsPanel = page.locator('.history-diff__panel').filter({
    has: page.getByRole('heading', { name: 'Cuts' }),
  });
  await expect(addsPanel).toContainText('Mystic Remora');
  await expect(cutsPanel).toContainText('Brainstorm');

  const finalHistoryJson = await page
    .locator('form[action="/deck-history"].result-panel input[name="HistoryJson"]')
    .inputValue();
  writeFileSync(finalHistoryJsonPath, finalHistoryJson, 'utf8');

  for (const theme of themes) {
    await page.context().clearCookies();
    await page.context().addCookies([{ name: 'deckflow-theme', value: theme.cookie, url: baseUrl }]);

    await page.goto('/deck-history');
    await expect(page.locator('h1')).toHaveText('Deck History');

    const formScreenshotPath = join(
      screenshotDir,
      `deck-history-form-${theme.name}-${projectName}.png`,
    );
    await page.screenshot({ path: formScreenshotPath, fullPage: true });

    await page.locator('input[type="file"][name="historyFile"]').setInputFiles(finalHistoryJsonPath);
    await page.getByRole('button', { name: 'Update history' }).click();
    await expect(page.locator('.history-timeline tbody tr')).toHaveCount(2, { timeout: 30_000 });

    const resultsScreenshotPath = join(
      screenshotDir,
      `deck-history-results-${theme.name}-${projectName}.png`,
    );
    await page.screenshot({ path: resultsScreenshotPath, fullPage: true });
  }
});

test('with tool.deck-history.enabled OFF, /deck-history returns 404 and the Home tile is absent', async ({
  page,
}) => {
  await setToolEnabled(page, 'Deck History', false);

  const response = await page.goto('/deck-history');
  expect(response?.status(), '/deck-history should be 404 with flag OFF').toBe(404);

  await page.goto('/');
  await expect(page.locator('.hub-card[href$="/deck-history"]')).toHaveCount(0);
});

async function gotoAdminTools(page: import('@playwright/test').Page): Promise<void> {
  const response = await page.goto(adminToolsUrl);
  expect(response?.ok(), '/Admin/Tools must return 200').toBeTruthy();
}

async function setToolEnabled(
  page: import('@playwright/test').Page,
  label: string,
  enabled: boolean,
): Promise<void> {
  await gotoAdminTools(page);

  const row = page.locator('tbody tr').filter({
    has: page.locator('td[data-label="Tool"] span', { hasText: label }),
  });

  const status = row.locator('[data-label="Status"]');
  const currentStatus = (await status.textContent())?.trim();
  const desiredStatus = enabled ? 'On' : 'Off';

  if (currentStatus === desiredStatus) {
    return;
  }

  const actionButton = row.getByRole('button', {
    name: enabled ? 'Enable' : 'Disable',
    exact: true,
  });
  await actionButton.click();
  await expect(page.locator('.admin-banner--success')).toContainText(
    `Tool '${label}' is now ${enabled ? 'enabled' : 'disabled'}.`,
  );
  await expect(
    page
      .locator('tbody tr')
      .filter({ has: page.locator('td[data-label="Tool"] span', { hasText: label }) })
      .locator('[data-label="Status"]'),
  ).toHaveText(desiredStatus);
}
