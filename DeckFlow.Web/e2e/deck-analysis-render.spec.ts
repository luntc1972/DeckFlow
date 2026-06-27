import { expect, test, type Page } from '@playwright/test';

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

async function renderAnalysis(page: Page): Promise<void> {
  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  // The page is an ARIA tablist workflow. Step 3 accepts a saved deck_profile
  // directly, but the form's only HTML-`required` control — the Step 2 target
  // bracket select — still blocks native submission until it has a value, so
  // satisfy it first (matches the real flow where Step 2 is filled en route).
  await page.locator('[data-chatgpt-show-step="2"][role="tab"]').click();
  await page.locator('select[name="TargetCommanderBracket"]').selectOption({ index: 1 });
  await page.locator('[data-chatgpt-show-step="3"][role="tab"]').click();

  const profileTextarea = page.locator('textarea[name="DeckProfileJson"]');
  await expect(profileTextarea).toBeVisible();
  await profileTextarea.fill(deckProfileJson);
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
