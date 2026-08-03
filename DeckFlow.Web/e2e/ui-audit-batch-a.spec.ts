import { test, expect } from '@playwright/test';
import { boxOf } from './support/admin-tools';

// Batch A of the 2026-08-02 site UI audit. Each assertion below locks one verified defect so a
// future edit that reintroduces it fails here rather than in a manual sweep.

const isMobile = (): boolean => test.info().project.name.includes('mobile');

test.describe('UI audit batch A', () => {
  test('landing page has exactly one h1 and no fallback tile icons', async ({ page }) => {
    await page.goto('/');

    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page.locator('h1')).toContainText('DeckFlow');

    // The default: arm of _ToolTileIcon renders a question-mark glyph. Its distinguishing mark is
    // the r="7" circle paired with the vertical stroke; a real icon never emits that pair.
    const fallbackIcons = await page.locator('.hub-card svg circle[r="7"] + line[x1="10"][y1="6"]').count();
    expect(fallbackIcons, 'no tile should fall through to the "?" icon').toBe(0);

    // Every tile must render some icon.
    const tiles = await page.locator('.hub-card').count();
    const icons = await page.locator('.hub-card svg').count();
    expect(icons).toBeGreaterThanOrEqual(tiles);
  });

  test('unknown URLs render the branded 404 page', async ({ page }) => {
    const response = await page.goto('/definitely-not-a-real-deckflow-route');

    expect(response?.status()).toBe(404);
    await expect(page.locator('h1')).toHaveText('Page not found');
    await expect(page.locator('.error-page__panel')).toBeVisible();
  });

  test('api 404s stay empty rather than returning the HTML error page', async ({ request }) => {
    const response = await request.get('/api/definitely-not-a-real-endpoint');

    expect(response.status()).toBe(404);
    expect((await response.body()).length).toBe(0);
  });

  test('feedback form leaves native constraint validation enabled', async ({ page }) => {
    await page.goto('/feedback');

    await expect(page.locator('form.feedback-form')).not.toHaveAttribute('novalidate', /.*/);

    const messageValid = await page.locator('form.feedback-form textarea').evaluate(
      element => (element as HTMLTextAreaElement).checkValidity(),
    );
    expect(messageValid, 'an empty required message must fail constraint validation').toBe(false);
  });

  test('workflow step tabs are named and focusable regardless of completion', async ({ page }) => {
    await page.goto('/deck-primer');

    const tabs = page.locator('.prompt-step-tab');
    expect(await tabs.count()).toBeGreaterThan(0);

    // No tab may carry the real disabled attribute — it would drop out of arrow traversal.
    expect(await page.locator('.prompt-step-tab[disabled]').count()).toBe(0);

    // Accessible name survives the <=600px rule that hides .prompt-step-tab__label.
    for (const tab of await tabs.all()) {
      const name = await tab.getAttribute('aria-label');
      expect(name?.trim()).toBeTruthy();
    }
  });

  test('activating an aria-disabled step tab does not submit the form', async ({ page }) => {
    await page.goto('/deck-analysis');

    // No page ships a not-yet-reachable step in its initial state — Cut Lab reaches that state only
    // after a pool import. Rather than duplicate that flow, put a real submit-bound tab into the
    // aria-disabled state the server would render, which is exactly what the site.ts guard keys on.
    const blockedTab = page.locator('.prompt-step-tab').last();
    await blockedTab.evaluate(element => element.setAttribute('aria-disabled', 'true'));

    const urlBefore = page.url();
    // force: Playwright's own actionability check already refuses aria-disabled elements, which
    // would mask the thing under test — the site.ts guard, not the harness.
    await blockedTab.click({ force: true });
    await page.waitForTimeout(300);

    expect(page.url(), 'a blocked step tab must not navigate or post').toBe(urlBefore);
    await expect(blockedTab, 'the page step handler must not run either').toHaveAttribute(
      'aria-selected',
      'false',
    );

    // Clearing the state must hand the very same element back to the page's own handler — this is
    // the path Cut Lab's client-side enable depends on (regression-locked by
    // cut-lab-export.spec.ts:141, which drives the real submit-bound tab).
    await blockedTab.evaluate(element => element.setAttribute('aria-disabled', 'false'));
    await blockedTab.click();
    await expect(blockedTab).toHaveAttribute('aria-selected', 'true');
  });

  test('tool nav menu toggle is hidden on desktop and shown on mobile', async ({ page }) => {
    await page.goto('/');

    const toggle = page.locator('.tool-nav__menu-toggle');
    await expect(toggle).toHaveAttribute('aria-controls', 'deck-tool-nav-groups');

    // It must not point at an ancestor of itself.
    const controlsSelf = await toggle.evaluate(element => {
      const target = document.getElementById(element.getAttribute('aria-controls') ?? '');
      return target === null || target.contains(element);
    });
    expect(controlsSelf, 'aria-controls must not resolve to an ancestor of the toggle').toBe(false);

    if (isMobile()) {
      await expect(toggle).toBeVisible();
    } else {
      await expect(toggle).toBeHidden();
    }
  });

  test('mobile menu toggle still reveals the tool nav groups', async ({ page }) => {
    test.skip(!isMobile(), 'the tool nav only collapses to a menu on the mobile viewport');

    await page.goto('/');

    const toggle = page.locator('.tool-nav__menu-toggle');
    const firstGroup = page.locator('.tool-nav__group').first();

    await expect(firstGroup).toBeHidden();

    await toggle.click();
    await expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await expect(firstGroup).toBeVisible();

    await toggle.click();
    await expect(firstGroup).toBeHidden();
  });

  test('tool nav groups still lay out as direct children of the nav row', async ({ page }) => {
    await page.goto('/');

    const groups = page.locator('.tool-nav__group');
    expect(await groups.count()).toBeGreaterThan(0);

    // display: contents on the wrapper means the groups keep the nav as their layout parent.
    const wrapperDisplay = await page
      .locator('#deck-tool-nav-groups')
      .evaluate(element => getComputedStyle(element).display);
    expect(wrapperDisplay).toBe('contents');
  });

  test('mobile tap targets clear 44px', async ({ page }) => {
    test.skip(!isMobile(), 'tap-target floor applies to the mobile viewport');

    await page.goto('/deck-primer');

    const stepTab = await boxOf(page, '.prompt-step-tab');
    expect(stepTab.height).toBeGreaterThanOrEqual(44);
    expect(stepTab.width).toBeGreaterThanOrEqual(44);

    const runButton = await boxOf(page, '.run-button');
    expect(runButton.height).toBeGreaterThanOrEqual(44);
  });

  test('feedback controls avoid the iOS auto-zoom threshold', async ({ page }) => {
    await page.goto('/feedback');

    const fontSize = await page
      .locator('form.feedback-form textarea')
      .evaluate(element => parseFloat(getComputedStyle(element).fontSize));
    expect(fontSize, 'controls under 16px trigger iOS Safari zoom-on-focus').toBeGreaterThanOrEqual(16);

    const submit = await boxOf(page, '.feedback-submit');
    if (isMobile()) {
      expect(submit.height).toBeGreaterThanOrEqual(44);
    }
  });
});
