import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

const interactionAuditFlagKey = 'analysis.interaction-audit';
const winConMapFlagKey = 'analysis.wincon-map';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

const deckProfileJson = `
\`\`\`json
{
  "deck_profile": {
    "format": "commander",
    "commander": "Sokka, Tenacious Tactician",
    "game_plan": "Build a board of value creatures and win through repeated combat triggers.",
    "primary_axes": ["combat", "tempo", "blink"],
    "speed": "midrange",
    "strengths": [
      { "name": "Efficient curve", "description": "The deck starts developing pressure on turns 1 through 3 without giving up interaction." },
      { "name": "Card velocity", "description": "Repeated ETB value keeps the hand stocked." }
    ],
    "weaknesses": [
      { "name": "Stack interaction", "description": "It cannot consistently stop fast combo once shields are down." },
      { "name": "Closing speed", "description": "The deck sometimes stabilizes without ending the game quickly." }
    ],
    "deck_needs": [
      { "need": "More burst draw", "description": "Add effects that reload after the first sweeper." },
      { "need": "Cheaper protection", "description": "Protect key engines without spending a full turn cycle." }
    ],
    "weak_slots": [
      { "card": "Firemantle Adept", "reason": "Too small an effect for four mana." }
    ],
    "synergy_tags": ["blink", "tokens"],
    "question_answers": [
      { "question_number": 1, "question": "How does the deck usually win?", "answer": "Combat snowballing.", "basis": "Attack-trigger shell." }
    ]
  }
}
\`\`\`
`;

const interactionAuditJson = JSON.stringify({
  TargetedRemoval: {
    Confident: [{ Name: 'Swords to Plowshares', Quantity: 1 }],
    Review: [{ Name: 'Beast Within', Quantity: 1 }],
  },
  BoardWipes: {
    Confident: [{ Name: 'Farewell', Quantity: 1 }],
    Review: [{ Name: 'Toxic Deluge', Quantity: 1 }],
  },
  Counterspells: {
    Confident: [{ Name: 'Counterspell', Quantity: 1 }],
    Review: [{ Name: 'Mana Drain', Quantity: 1 }],
  },
  ProtectionRecursion: {
    Confident: [{ Name: "Teferi's Protection", Quantity: 1 }],
    Review: [{ Name: 'Eternal Witness', Quantity: 1 }],
  },
  StaxTaxation: {
    Confident: [{ Name: 'Drannith Magistrate', Quantity: 1 }],
    Review: [{ Name: 'Thalia, Guardian of Thraben', Quantity: 1 }],
  },
  CoverageGaps: ['Counterspell count is approximately low; verify against the list.'],
});

const winConMapJson = JSON.stringify({
  Combos: [
    {
      CardNames: ['Kiki-Jiki, Mirror Breaker', 'Restoration Angel'],
      Results: ['Infinite combat steps'],
      ManaValueNeeded: 8,
      Popularity: 42,
      Band: 1,
    },
  ],
  NearCombos: [
    {
      MissingCard: 'Splinter Twin',
      CardsInDeck: ['Deceiver Exarch'],
      Results: ['Infinite hasty tokens'],
    },
  ],
  AssemblyPathCount: 1,
  ClosingCards: [{ Name: 'Craterhoof Behemoth', Quantity: 1 }],
  ComboDataAvailable: true,
  OverallBand: 1,
});

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async ({ page }) => {
  try {
    await setFlagEnabled(page, interactionAuditFlagKey, false);
    await setFlagEnabled(page, winConMapFlagKey, false);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

async function renderAnalysis(page: Page): Promise<void> {
  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  // The page is an ARIA tablist workflow. Step 3 accepts a saved deck_profile
  // directly, but the form's only HTML-`required` control — the Step 2 target
  // bracket select — still blocks native submission until it has a value, so
  // satisfy it first (matches the real flow where Step 2 is filled en route).
  await page.locator('[data-prompt-show-step="2"][role="tab"]').click();
  await page.locator('select[name="TargetCommanderBracket"]').selectOption({ index: 1 });
  await page.locator('[data-prompt-show-step="3"][role="tab"]').click();

  const profileTextarea = page.locator('textarea[name="DeckProfileJson"]');
  await expect(profileTextarea).toBeVisible();
  await profileTextarea.fill(deckProfileJson);
  await page.getByRole('button', { name: 'Render Analysis Summary' }).click();
}

async function renderAnalysisWithInteractionAudit(page: Page): Promise<void> {
  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  await page.locator('[data-prompt-show-step="2"][role="tab"]').click();
  await page.locator('select[name="TargetCommanderBracket"]').selectOption({ index: 1 });
  await page.locator('[data-prompt-show-step="3"][role="tab"]').click();

  await page.locator('textarea[name="DeckProfileJson"]').fill(deckProfileJson);
  await page.locator('form').first().evaluate((form, value) => {
    const textarea = document.createElement('textarea');
    textarea.name = 'InteractionAuditJson';
    textarea.value = value;
    form.appendChild(textarea);
  }, interactionAuditJson);
  await page.getByRole('button', { name: 'Render Analysis Summary' }).click();
}

async function renderAnalysisWithWinConMap(page: Page): Promise<void> {
  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  await page.locator('[data-prompt-show-step="2"][role="tab"]').click();
  await page.locator('select[name="TargetCommanderBracket"]').selectOption({ index: 1 });
  await page.locator('[data-prompt-show-step="3"][role="tab"]').click();

  await page.locator('textarea[name="DeckProfileJson"]').fill(deckProfileJson);
  await page.locator('form').first().evaluate((form, value) => {
    const textarea = document.createElement('textarea');
    textarea.name = 'WinConMapJson';
    textarea.value = value;
    form.appendChild(textarea);
  }, winConMapJson);
  await page.getByRole('button', { name: 'Render Analysis Summary' }).click();
}

test('step 3 renders object-shaped deck_profile lists as analysis summary', async ({ page }) => {
  await renderAnalysis(page);

  const summary = page.locator('section.summary-panel', {
    has: page.getByRole('heading', { level: 4, name: 'Strengths' }),
  });
  await expect(summary).toBeVisible();
  await expect(summary.getByRole('heading', { level: 4, name: 'Strengths' })).toBeVisible();
  await expect(summary.getByRole('heading', { level: 4, name: 'Weaknesses' })).toBeVisible();
  await expect(summary.getByRole('heading', { level: 4, name: 'Deck Needs' })).toBeVisible();
  await expect(summary.locator('li')).toContainText([
    'Efficient curve: The deck starts developing pressure on turns 1 through 3 without giving up interaction.',
    'Stack interaction: It cannot consistently stop fast combo once shields are down.',
    'More burst draw: Add effects that reload after the first sweeper.',
  ]);
  await expect(page.locator('.error-banner')).toBeHidden();
});

test('step 3 renders object-shaped deck_profile lists on mobile', async ({ page }) => {
  test.skip(!test.info().project.name.includes('mobile'), 'mobile-only coverage');

  await renderAnalysis(page);

  const summary = page.locator('section.summary-panel', {
    has: page.getByRole('heading', { level: 4, name: 'Strengths' }),
  });
  await expect(summary).toBeVisible();
  await expect(summary.getByRole('heading', { level: 4, name: 'Strengths' })).toBeVisible();
  await expect(summary.getByRole('heading', { level: 4, name: 'Weaknesses' })).toBeVisible();
  await expect(summary.getByRole('heading', { level: 4, name: 'Deck Needs' })).toBeVisible();
  await expect(summary).toContainText('Cheaper protection: Protect key engines without spending a full turn cycle.');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
  await expect(page.locator('.error-banner')).toBeHidden();
});

test('step 3 renders interaction audit readout when the flag is ON', async ({ page }) => {
  await setFlagEnabled(page, interactionAuditFlagKey, true);
  await renderAnalysisWithInteractionAudit(page);

  const audit = page.locator('.interaction-audit');
  await expect(audit).toBeVisible();
  await expect(audit).toContainText('Targeted removal');
  await expect(audit).toContainText('Swords to Plowshares');
  await expect(audit).toContainText('Coverage gaps to verify');
});

test('step 3 omits interaction audit readout when the flag is OFF', async ({ page }) => {
  await setFlagEnabled(page, interactionAuditFlagKey, false);
  await renderAnalysisWithInteractionAudit(page);

  await expect(page.locator('.interaction-audit')).toHaveCount(0);
});

test('step 3 renders win-condition/combo map readout when the flag is ON', async ({ page }) => {
  // Runs under both the chromium-desktop (1280) and chromium-mobile (390) projects.
  await setFlagEnabled(page, winConMapFlagKey, true);
  await renderAnalysisWithWinConMap(page);

  const winConMap = page.locator('.wincon-map');
  await expect(winConMap).toBeVisible();
  await expect(winConMap).toContainText('Kiki-Jiki, Mirror Breaker');
  await expect(winConMap).toContainText('One card away (not currently a win line)');
  await expect(winConMap).toContainText('Craterhoof Behemoth');
});

test('step 3 omits win-condition/combo map readout when the flag is OFF', async ({ page }) => {
  await setFlagEnabled(page, winConMapFlagKey, false);
  await renderAnalysisWithWinConMap(page);

  await expect(page.locator('.wincon-map')).toHaveCount(0);
});

async function setFlagEnabled(page: Page, key: string, enabled: boolean): Promise<void> {
  const response = await page.goto('/Admin/Flags');
  expect(response?.ok()).toBeTruthy();

  const row = page.locator(`tr[data-flag-key="${key}"]`);
  const status = row.locator('[data-label="Status"]');
  const currentStatus = (await status.textContent())?.trim();
  const desiredStatus = enabled ? 'On' : 'Off';
  if (currentStatus === desiredStatus) {
    return;
  }

  await row.getByRole('button', { name: enabled ? 'Enable' : 'Disable', exact: true }).click();
  await expect(page.locator('.admin-banner--success')).toBeVisible();
  await expect(row.locator('[data-label="Status"]')).toHaveText(desiredStatus);
}
