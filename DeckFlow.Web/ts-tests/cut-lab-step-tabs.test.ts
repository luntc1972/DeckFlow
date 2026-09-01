import { afterEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

const stubMatchMedia = (): void => {
  vi.stubGlobal('matchMedia', vi.fn().mockImplementation((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  } satisfies MediaQueryList)));
};

const renderFixture = (selectedStep = 1): void => {
  document.body.innerHTML = `
    <div class="prompt-step-nav" role="tablist" aria-label="Cut Lab workflow steps">
      <button type="button" role="tab" id="cut-lab-step-tab-1" data-cut-lab-step="1" aria-controls="cut-lab-step-panel-1" aria-selected="${selectedStep === 1}" tabindex="${selectedStep === 1 ? 0 : -1}">Process</button>
      <button type="button" role="tab" id="cut-lab-step-tab-2" data-cut-lab-step="2" aria-controls="cut-lab-step-panel-2" aria-selected="${selectedStep === 2}" tabindex="${selectedStep === 2 ? 0 : -1}">Decide</button>
      <button type="button" role="tab" id="cut-lab-step-tab-3" data-cut-lab-step="3" aria-controls="cut-lab-step-panel-3" aria-disabled="true" aria-selected="false" tabindex="-1">Plan</button>
      <button type="button" role="tab" id="cut-lab-step-tab-4" data-cut-lab-step="4" aria-controls="cut-lab-step-panel-4" aria-selected="${selectedStep === 4}" tabindex="${selectedStep === 4 ? 0 : -1}">Goals</button>
      <button type="submit" role="tab" id="cut-lab-step-tab-5" form="cut-lab-export-form" data-cut-lab-step="5" aria-controls="cut-lab-step-panel-5" aria-selected="${selectedStep === 5}" tabindex="${selectedStep === 5 ? 0 : -1}">Export</button>
    </div>
    <section role="tabpanel" id="cut-lab-step-panel-1" aria-labelledby="cut-lab-step-tab-1">Process<details id="cut-lab-section-lock-pool" data-cutlab-mobile-collapse></details></section>
    <section role="tabpanel" id="cut-lab-step-panel-2" aria-labelledby="cut-lab-step-tab-2">Decide<details id="cut-lab-section-cut-rounds" data-cutlab-mobile-collapse open></details></section>
    <section role="tabpanel" id="cut-lab-step-panel-3" aria-labelledby="cut-lab-step-tab-3">Plan</section>
    <section role="tabpanel" id="cut-lab-step-panel-4" aria-labelledby="cut-lab-step-tab-4">Goals<details id="cut-lab-section-goals" data-cutlab-mobile-collapse></details><details id="cut-lab-section-scenarios"></details></section>
    <section role="tabpanel" id="cut-lab-step-panel-5" aria-labelledby="cut-lab-step-tab-5">Export<details id="cut-lab-section-export" data-cutlab-mobile-collapse></details></section>
    <nav class="cutlab-anchor-nav"></nav>
    <form id="cut-lab-export-form"></form>
  `;
};

const initialize = (): void => {
  stubMatchMedia();
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
    configurable: true,
    writable: true,
    value: vi.fn(),
  });
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  window.localStorage.clear();
  document.body.innerHTML = '';
});

describe('cut-lab step tabs', () => {
  // Killing mutation: M4 - skip the initialization hide
  it('preserves the server-selected tab on load and shows one panel on click', () => {
    renderFixture();
    initialize();

    const visiblePanels = document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])');
    expect(visiblePanels).toHaveLength(1);
    expect(visiblePanels[0]?.id).toBe('cut-lab-step-panel-1');
    expect(document.getElementById('cut-lab-step-tab-1')?.classList.contains('is-active')).toBe(true);

    const goals = document.getElementById('cut-lab-step-tab-4') as HTMLButtonElement;
    goals.click();

    expect(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).toHaveLength(1);
    expect(document.getElementById('cut-lab-step-panel-4')?.hasAttribute('hidden')).toBe(false);
    expect(goals.getAttribute('aria-selected')).toBe('true');
    expect(goals.getAttribute('tabindex')).toBe('0');
    expect(goals.classList.contains('is-active')).toBe(true);
    expect(document.getElementById('cut-lab-step-tab-1')?.getAttribute('aria-selected')).toBe('false');
  });

  it('allows a submit-type step tab to submit its export form natively', () => {
    renderFixture();
    initialize();
    const submit = vi.fn((event: SubmitEvent) => event.preventDefault());
    document.getElementById('cut-lab-export-form')?.addEventListener('submit', submit);

    (document.getElementById('cut-lab-step-tab-5') as HTMLButtonElement).click();

    expect(submit).toHaveBeenCalled();
    expect(document.getElementById('cut-lab-step-tab-5')?.getAttribute('aria-selected')).toBe('false');
  });

  it('moves scenarios outside hidden step panels on initialization', () => {
    renderFixture(2);
    initialize();

    const scenarios = document.getElementById('cut-lab-section-scenarios');
    expect(scenarios?.closest('[role="tabpanel"]')).toBeNull();
    expect(scenarios?.closest('[hidden]')).toBeNull();
    expect(scenarios?.nextElementSibling?.classList.contains('cutlab-anchor-nav')).toBe(true);
  });

  // Killing mutation: M4 - skip the initialization hide
  it('hides every panel except the server-selected one for each selected step', () => {
    for (const step of [1, 2, 4, 5]) {
      renderFixture(step);
      initialize();
      expect(Array.from(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).map(panel => panel.id)).toEqual([`cut-lab-step-panel-${step}`]);
    }
  });

  it('requires selecting Process before the Lock your pool panel is visible after a Decide load', () => {
    renderFixture(2);
    initialize();

    expect(document.getElementById('cut-lab-step-panel-1')?.hidden).toBe(true);
    document.getElementById('cut-lab-step-tab-1')?.click();
    expect(document.getElementById('cut-lab-step-panel-1')?.hidden).toBe(false);
  });

  // Killing mutation: M5 - route initialization through tab activation
  it('does not apply the default section collapse state on first load', () => {
    renderFixture();
    initialize();
    expect(document.getElementById('cut-lab-section-cut-rounds')?.hasAttribute('open')).toBe(true);
    expect(document.getElementById('cut-lab-section-lock-pool')?.hasAttribute('open')).toBe(false);
    expect(window.localStorage.getItem('deckflow.cutlab.sections')).toBeNull();
  });

  // Killing mutation: M6 - delete the collapse default from activation too
  it('still applies the default section collapse state when a tab is clicked', () => {
    renderFixture();
    initialize();
    (document.getElementById('cut-lab-step-tab-1') as HTMLButtonElement).click();
    expect(document.getElementById('cut-lab-section-lock-pool')?.hasAttribute('open')).toBe(true);
    expect(document.getElementById('cut-lab-section-cut-rounds')?.hasAttribute('open')).toBe(false);
  });

  // Killing mutation: M18 - no-op activation on click
  it('every entry path leaves exactly one panel visible when the server selected a tab', () => {
    const assertSelectedPanelVisible = (): void => {
      const visiblePanelIds = Array.from(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).map(panel => panel.id);
      const selectedTab = document.querySelector<HTMLButtonElement>('[role="tab"][aria-selected="true"]');
      expect(visiblePanelIds).toHaveLength(1);
      expect(visiblePanelIds[0]).toBe(selectedTab?.getAttribute('aria-controls'));
    };

    renderFixture(2);
    initialize();
    assertSelectedPanelVisible();

    renderFixture(2);
    initialize();
    (document.getElementById('cut-lab-step-tab-4') as HTMLButtonElement).click();
    assertSelectedPanelVisible();
    expect(document.getElementById('cut-lab-step-tab-4')?.getAttribute('aria-selected')).toBe('true');
    expect(document.getElementById('cut-lab-step-tab-2')?.getAttribute('aria-selected')).toBe('false');
  });

  // Killing mutation: M1 - remove the activation guard
  // Killing mutation: M2 - skip aria-disabled tabs during traversal
  it('keeps aria-disabled tabs focusable and reachable by arrow navigation', () => {
    renderFixture(2);
    initialize();

    const decide = document.getElementById('cut-lab-step-tab-2') as HTMLButtonElement;
    const plan = document.getElementById('cut-lab-step-tab-3') as HTMLButtonElement;
    expect(plan.disabled).toBe(false);

    const visiblePanelIdsBefore = Array.from(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).map(panel => panel.id);
    decide.focus();
    decide.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(document.activeElement).toBe(plan);
    expect(plan.getAttribute('aria-selected')).toBe('false');
    expect(Array.from(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).map(panel => panel.id)).toEqual(visiblePanelIdsBefore);
    expect(decide.getAttribute('aria-selected')).toBe('true');
  });

  // Killing mutation: M21 - drop Home/End boundary navigation
  it('moves focus to the first and last tabs via Home and End', () => {
    renderFixture();
    initialize();

    const process = document.getElementById('cut-lab-step-tab-1') as HTMLButtonElement;
    const exportTab = document.getElementById('cut-lab-step-tab-5') as HTMLButtonElement;
    exportTab.focus();
    exportTab.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true }));
    expect(document.activeElement).toBe(process);
    expect(Array.from(document.querySelectorAll('[aria-disabled="true"]')).every(tab => tab.getAttribute('aria-selected') !== 'true')).toBe(true);

    process.focus();
    process.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));
    expect(document.activeElement).toBe(exportTab);
    expect(Array.from(document.querySelectorAll('[aria-disabled="true"]')).every(tab => tab.getAttribute('aria-selected') !== 'true')).toBe(true);
  });

  // Killing mutation: M1 - remove the activation guard
  it('traverses the reserved tab across two ArrowRight presses then one ArrowLeft without ever selecting it', () => {
    renderFixture(2);
    initialize();

    const decide = document.getElementById('cut-lab-step-tab-2') as HTMLButtonElement;
    const plan = document.getElementById('cut-lab-step-tab-3') as HTMLButtonElement;
    const goals = document.getElementById('cut-lab-step-tab-4') as HTMLButtonElement;
    decide.focus();
    decide.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(document.activeElement).toBe(plan);
    expect(plan.getAttribute('aria-selected')).toBe('false');
    plan.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(document.activeElement).toBe(goals);
    expect(plan.getAttribute('aria-selected')).toBe('false');

    const visiblePanelIdsBefore = Array.from(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).map(panel => panel.id);
    goals.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
    expect(document.activeElement, 'arrowLeft: focus returns to Plan').toBe(plan);
    expect(plan.getAttribute('aria-selected'), 'arrowLeft: aria-selected stays false').toBe('false');
    expect(Array.from(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).map(panel => panel.id), 'arrowLeft: visible-panel set unchanged').toEqual(visiblePanelIdsBefore);
  });

  // Killing mutation: M1 - remove the activation guard
  it('refuses to select an aria-disabled tab on a direct click', () => {
    renderFixture(2);
    initialize();

    const plan = document.getElementById('cut-lab-step-tab-3') as HTMLButtonElement;
    const visiblePanelIdsBefore = Array.from(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).map(panel => panel.id);
    plan.click();
    expect(plan.getAttribute('aria-selected')).toBe('false');
    expect(Array.from(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).map(panel => panel.id)).toEqual(visiblePanelIdsBefore);
  });

  // Why: D-4 keeps all content available when no tab is selected.
  // Killing mutation: M7 - hide panels on the no-selection path too
  it('leaves every panel visible when the server selected no tab, so content is never stranded behind an unselected tablist', () => {
    renderFixture(0);
    initialize();

    expect(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).toHaveLength(5);
  });
});
