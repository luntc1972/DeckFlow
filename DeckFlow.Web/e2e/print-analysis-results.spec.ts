import { expect, test, type Page } from '@playwright/test';

// A saved deck_profile pasted straight into Step 3 renders the Analysis Summary
// with no live network (no Scryfall / combo calls), giving a deterministic
// results panel to print. Mirrors deck-analysis-render.spec.ts.
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
      { "name": "Efficient curve", "description": "Starts developing pressure on turns 1 through 3 without giving up interaction." },
      { "name": "Card velocity", "description": "Repeated ETB value keeps the hand stocked." }
    ],
    "weaknesses": [
      { "name": "Stack interaction", "description": "Cannot consistently stop fast combo once shields are down." },
      { "name": "Closing speed", "description": "Sometimes stabilizes without ending the game quickly." }
    ],
    "deck_needs": [
      { "need": "More burst draw", "description": "Add effects that reload after the first sweeper." }
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

  // Satisfy the Step 2 required bracket select (blocks native submit), then
  // paste the saved profile into Step 3 and render.
  await page.locator('[data-chatgpt-show-step="2"][role="tab"]').click();
  await page.locator('select[name="TargetCommanderBracket"]').selectOption({ index: 1 });
  await page.locator('[data-chatgpt-show-step="3"][role="tab"]').click();

  const profileTextarea = page.locator('textarea[name="DeckProfileJson"]');
  await expect(profileTextarea).toBeVisible();
  await profileTextarea.fill(deckProfileJson);
  await page.getByRole('button', { name: 'Render Analysis Summary' }).click();

  await expect(page.locator('section.summary-panel[data-chatgpt-result-anchor]')).toBeVisible();
}

test('print view strips chrome and keeps the analysis results readable', async ({ page }, testInfo) => {
  await renderAnalysis(page);

  const printButton = page.locator('button[data-chatgpt-print]').first();
  const summary = page.locator('section.summary-panel', {
    has: page.getByRole('heading', { level: 4, name: 'Strengths' }),
  });

  // On screen: the Print button is a visible, discoverable affordance.
  await expect(printButton).toBeVisible();
  await expect(summary).toBeVisible();

  // Switch to print media — the @media print rules in site-common.css apply.
  await page.emulateMedia({ media: 'print' });

  // Site chrome, page intro, and the in-panel toolbar are all stripped from paper.
  await expect(page.locator('.page-header')).toBeHidden();
  await expect(page.locator('.hero')).toBeHidden();
  await expect(page.locator('.timing-summary')).toBeHidden();
  await expect(page.locator('.chatgpt-page-toolbar')).toBeHidden();
  await expect(page.locator('.chatgpt-sticky-download')).toBeHidden();
  await expect(printButton).toBeHidden();

  // The rendered results survive and stay readable: headings + list content present.
  await expect(summary).toBeVisible();
  await expect(summary.getByRole('heading', { level: 4, name: 'Strengths' })).toBeVisible();
  await expect(summary.getByRole('heading', { level: 4, name: 'Weaknesses' })).toBeVisible();
  await expect(summary.getByRole('heading', { level: 4, name: 'Deck Needs' })).toBeVisible();
  await expect(summary.locator('li')).toContainText([
    'Efficient curve: Starts developing pressure on turns 1 through 3 without giving up interaction.',
    'Stack interaction: Cannot consistently stop fast combo once shields are down.',
  ]);

  // Paper does not overflow horizontally (content fits the page width).
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();

  // Capture the print layout for visual review (desktop + mobile projects).
  await page.screenshot({ path: `${testInfo.outputDir}/print-${testInfo.project.name}.png`, fullPage: true });
});

test('an inactive result step is not printed (only the visible step prints)', async ({ page }) => {
  await renderAnalysis(page);

  const summary = page.locator('section.summary-panel', {
    has: page.getByRole('heading', { level: 4, name: 'Strengths' }),
  });
  await expect(summary).toBeVisible();

  // Navigate away from Step 3 — its result panel is now an inactive (.hidden) tab.
  await page.locator('[data-chatgpt-show-step="1"][role="tab"]').click();
  await expect(summary).toBeHidden();

  // Under print media the inactive result step must stay hidden: the CSS reveal
  // is scoped to :not(.hidden), so a populated-but-inactive step never prints.
  await page.emulateMedia({ media: 'print' });
  await expect(summary).toBeHidden();
});
