import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';

// Regression specs for Batch G — five form-correctness defects where the app did the
// wrong thing rather than the ugly thing. Sourced from the 2026-08-02 second front-end
// audit pass (.planning/todos/pending/2026-08-02-batch-g-form-correctness-defects.md).
//
// What this spec covers:
//   G1 — Enter (and mobile "Go") in a text field must run the current step's action,
//        not the sticky "Download session (.zip)" bar that happened to render first.
//   G2 — the IncludeCardVersions checkbox must never be display:none, or the browser
//        drops it from the POST and silently resets it on mobile.
//   G3 — the printing-conflict resolution form must be reachable with JavaScript on.
//   G4 — Bracket and Mana Base must persist pasted deck text like every other tool.
//
// G5 (the server-side Card Lookup line cap) is covered by DeckLookupControllerTests
// rather than here — it is a controller guard with no browser-observable surface.
//
// Run:
//   1. Start the app headless: scripts/run-web-test.sh (sets DECKFLOW_DISABLE_AUTO_BROWSER=true)
//   2. cd DeckFlow.Web && DECKFLOW_DISABLE_AUTO_BROWSER=true \
//        npx --no-install playwright test e2e/form-correctness-batch-g.spec.ts --reporter=line

const MOXFIELD_URL = 'https://moxfield.com/decks/example-deck-id';

const SAMPLE_DECK = ['1 Sol Ring', '1 Arcane Signet', '1 Command Tower'].join('\n');

/**
 * Blocks document-level form POSTs and records the path each one targeted.
 *
 * Why: these specs care about *which* action a keypress fires, not what the server
 * does with it. The POST is answered with 204 rather than aborted — a 204 leaves the
 * browser on the current page, whereas an abort navigates to chrome-error:// and
 * races any later navigation (such as an admin flag revert in afterEach).
 */
const captureNextPost = async (page: Page): Promise<() => string | null> => {
  let posted: string | null = null;
  await page.route('**/*', async route => {
    const request = route.request();
    if (request.method() === 'POST' && request.resourceType() === 'document') {
      posted = new URL(request.url()).pathname;
      await route.fulfill({ status: 204, body: '' });
      return;
    }
    await route.continue();
  });
  return () => posted;
};

/**
 * Counts clicks on the button a step marks as its intended Enter target.
 *
 * Asserting on the routed click rather than on a resulting POST keeps the spec
 * honest about what G1 actually fixed: some steps legitimately fail client-side
 * validation and never post, which says nothing about whether Enter was routed
 * to the right button.
 */
const spyDefaultAction = async (page: Page, selector: string): Promise<() => Promise<number>> => {
  await page.evaluate(sel => {
    const element = document.querySelector(sel);
    Object.assign(window, { __batchGClicks: 0 });
    element?.addEventListener('click', () => {
      Object.assign(window, { __batchGClicks: (window as unknown as { __batchGClicks: number }).__batchGClicks + 1 });
    });
  }, selector);

  return () => page.evaluate(() => (window as unknown as { __batchGClicks: number }).__batchGClicks);
};

/**
 * Picks a deck-input mode. Bracket and Mana Base render the URL and paste panels
 * as separate `hidden`-toggled fields driven by a `data-df-select` custom widget,
 * so the native select is set directly and a change event dispatched.
 */
const selectInputMode = async (page: Page, selectId: string, value: 'PublicUrl' | 'PasteText'): Promise<void> => {
  await page.evaluate(({ id, mode }) => {
    const select = document.getElementById(id) as HTMLSelectElement | null;
    if (!select) {
      return;
    }
    select.value = mode;
    select.dispatchEvent(new Event('change', { bubbles: true }));
  }, { id: selectId, mode: value });
};

test.describe('G1 — Enter must not trigger the sticky download bar', () => {
  // Why: the download bar is the first submit in DOM order, which made it the form's
  // implicit default button. deck-sync.ts now demotes it to type="button" at runtime,
  // while the markup keeps type="submit" so the <noscript> download still works.
  for (const path of ['/deck-analysis', '/deck-comparison', '/cedh-meta-gap']) {
    test(`${path} demotes the download button out of implicit submission`, async ({ page }) => {
      await page.goto(path);
      const downloadButton = page.locator('button[data-prompt-download-submit]').first();
      await expect(downloadButton).toHaveAttribute('type', 'button');
    });
  }

  test('/deck-analysis Enter in the deck URL advances the workflow instead of downloading', async ({ page }) => {
    await page.goto('/deck-analysis');

    let downloadStarted = false;
    page.on('download', () => {
      downloadStarted = true;
    });
    // The demoted button fetches rather than navigating, so also watch the endpoint.
    let downloadPosted = false;
    await page.route('**/deck-analysis/download', async route => {
      downloadPosted = true;
      await route.abort();
    });

    await page.locator('input[name="DeckUrl"]').fill(MOXFIELD_URL);
    await page.locator('input[name="DeckUrl"]').press('Enter');

    // Step 1's marked default action is "Next: Analysis", so step 2 becomes visible.
    await expect(page.locator('[data-prompt-step="2"]')).toBeVisible();
    expect(downloadStarted).toBe(false);
    expect(downloadPosted).toBe(false);
  });

  test('/cedh-meta-gap Enter routes to the step action, never the download', async ({ page }) => {
    await page.goto('/cedh-meta-gap');
    const posted = await captureNextPost(page);
    const clicks = await spyDefaultAction(page, '[data-prompt-cedh-step="1"] [data-default-action]');

    await page.locator('input[name="DeckUrl"]').fill(MOXFIELD_URL);
    await page.locator('input[name="DeckUrl"]').press('Enter');

    await expect.poll(clicks).toBeGreaterThan(0);
    // Whether the step posts depends on client-side validation; what must never
    // happen is a download.
    expect(posted()).not.toBe('/cedh-meta-gap/download');
  });

  test('/manabase Enter runs the analysis, not the load-and-detect-costs path', async ({ page }) => {
    await page.goto('/manabase');
    const posted = await captureNextPost(page);

    await selectInputMode(page, 'manabase-input-source', 'PublicUrl');
    await page.locator('#manabase-deck-url').fill(MOXFIELD_URL);
    await page.locator('#manabase-deck-url').press('Enter');

    // "Load deck & detect costs" (formaction=/manabase/load) precedes "Analyze Mana Base"
    // in DOM order, so before the fix Enter ran the load path.
    await expect.poll(posted).toBe('/manabase');
  });

});

test.describe('G2 — hidden form controls are dropped from the POST', () => {
  test('IncludeCardVersions renders and binds at every viewport', async ({ page }) => {
    await page.goto('/deck-analysis');

    const checkbox = page.locator('input[name="IncludeCardVersions"]');
    await expect(checkbox).toHaveCount(1);

    // A display:none control is omitted from form submission entirely, which is how
    // a mobile post silently reset this option to false. Assert the control itself is
    // laid out at the current project viewport (mobile project runs at 390px).
    const isRendered = await checkbox.evaluate(node => {
      const label = node.closest('label');
      return label !== null && window.getComputedStyle(label).display !== 'none';
    });
    expect(isRendered).toBe(true);

    // FormData is the ground truth: it reflects exactly what the browser would send.
    await checkbox.evaluate(node => {
      (node as HTMLInputElement).checked = true;
    });
    const submitted = await page.locator('form[data-prompt-packets-form]').evaluate(
      form => new FormData(form as HTMLFormElement).get('IncludeCardVersions')
    );
    expect(submitted).toBe('true');
  });
});

test.describe('G3 — printing-conflict resolution must be reachable with JS on', () => {
  // /deck-sync is flag-gated (tool.deck-sync.enabled) and ships off, so the flag is
  // toggled transiently for this run and reverted in afterEach — no prod seed change.
  let heldLock: Awaited<ReturnType<typeof acquireAdminLockForTest>> | null = null;

  test.beforeEach(async ({ page }) => {
    heldLock = await acquireAdminLockForTest(page);
    await setToolEnabled(page, 'Deck Sync', true);
  });

  test.afterEach(async ({ page }) => {
    if (heldLock) {
      await setToolEnabled(page, 'Deck Sync', false);
      await releaseAdminLockForTest(heldLock);
      heldLock = null;
    }
  });

  test('the JS-path conflicts panel is a form that posts to /resolve', async ({ page }) => {
    await page.goto('/sync');

    const resolveForm = page.locator('#deck-sync-conflicts-js');
    await expect(resolveForm).toHaveJSProperty('tagName', 'FORM');
    await expect(resolveForm).toHaveAttribute('data-deck-sync-resolve-form', '');

    const action = await resolveForm.evaluate(form => new URL((form as HTMLFormElement).action).pathname);
    expect(action).toBe('/resolve');

    // The "Use" column is what makes /resolve reachable; before the fix the JS path
    // rendered three read-only cells and the resolution form existed only in <noscript>.
    await expect(resolveForm.locator('thead th', { hasText: 'Use' })).toHaveCount(1);
    await expect(resolveForm.locator('button[type="submit"]')).toHaveCount(1);
  });
});

test.describe('G4 — deck text must survive navigating away and back', () => {
  // Bracket is covered separately below — it is flag-gated and needs an admin toggle.
  for (const tool of [
    { path: '/manabase', selectId: 'manabase-input-source', textarea: '#manabase-deck-text', cacheKey: 'manabase' },
  ]) {
    test(`${tool.path} restores pasted deck text`, async ({ page }) => {
      await page.goto(tool.path);

      await expect(page.locator(`form[data-cache-key="${tool.cacheKey}"]`)).toHaveCount(1);

      await selectInputMode(page, tool.selectId, 'PasteText');
      await page.locator(tool.textarea).fill(SAMPLE_DECK);
      // Persistence is wired to input/change, so blur to be sure the handler ran.
      await page.locator(tool.textarea).blur();

      await page.goto('/');
      await page.goto(tool.path);

      await expect(page.locator(tool.textarea)).toHaveValue(SAMPLE_DECK);
    });

    test(`${tool.path} "Start over" clears the restored text`, async ({ page }) => {
      await page.goto(tool.path);
      await selectInputMode(page, tool.selectId, 'PasteText');
      await page.locator(tool.textarea).fill(SAMPLE_DECK);
      await page.locator(tool.textarea).blur();

      await page.locator('[data-clear-cache]').click();
      await page.waitForURL(`**${tool.path}`);

      await expect(page.locator(tool.textarea)).toHaveValue('');
    });
  }
});

// Bracket Check is flag-gated (tool.bracket.enabled) and ships off, so its Batch G
// coverage lives here behind a transient admin toggle that afterEach reverts.
test.describe('Bracket — G1 and G4 behind the tool flag', () => {
  let heldLock: Awaited<ReturnType<typeof acquireAdminLockForTest>> | null = null;

  test.beforeEach(async ({ page }) => {
    heldLock = await acquireAdminLockForTest(page);
    await setToolEnabled(page, 'Bracket Check', true);
  });

  test.afterEach(async ({ page }) => {
    if (heldLock) {
      await setToolEnabled(page, 'Bracket Check', false);
      await releaseAdminLockForTest(heldLock);
      heldLock = null;
    }
  });

  test('Enter classifies the deck instead of doing nothing useful', async ({ page }) => {
    await page.goto('/bracket');
    const posted = await captureNextPost(page);

    await selectInputMode(page, 'bracket-input-source', 'PublicUrl');
    await page.locator('#bracket-deck-url').fill(MOXFIELD_URL);
    await page.locator('#bracket-deck-url').press('Enter');

    await expect.poll(posted).toBe('/bracket');

    // Drop the interceptor so afterEach's admin navigation is not routed.
    await page.unroute('**/*');
  });

  test('restores pasted deck text after navigating away and back', async ({ page }) => {
    await page.goto('/bracket');
    await expect(page.locator('form[data-cache-key="bracket"]')).toHaveCount(1);

    await selectInputMode(page, 'bracket-input-source', 'PasteText');
    await page.locator('#bracket-deck-text').fill(SAMPLE_DECK);
    await page.locator('#bracket-deck-text').blur();

    await page.goto('/');
    await page.goto('/bracket');

    await expect(page.locator('#bracket-deck-text')).toHaveValue(SAMPLE_DECK);
  });

  test('"Start over" clears the restored text', async ({ page }) => {
    await page.goto('/bracket');
    await selectInputMode(page, 'bracket-input-source', 'PasteText');
    await page.locator('#bracket-deck-text').fill(SAMPLE_DECK);
    await page.locator('#bracket-deck-text').blur();

    await page.locator('[data-clear-cache]').click();
    await page.waitForURL('**/bracket');

    await expect(page.locator('#bracket-deck-text')).toHaveValue('');
  });
});
