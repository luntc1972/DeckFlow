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
      <button type="button" role="tab" id="cut-lab-step-tab-1" data-cut-lab-step="1" aria-selected="${selectedStep === 1}" tabindex="${selectedStep === 1 ? 0 : -1}">Process</button>
      <button type="button" role="tab" id="cut-lab-step-tab-2" data-cut-lab-step="2" aria-selected="${selectedStep === 2}" tabindex="${selectedStep === 2 ? 0 : -1}">Decide</button>
      <button type="button" role="tab" id="cut-lab-step-tab-3" data-cut-lab-step="3" aria-disabled="true" aria-selected="false" tabindex="-1">Plan</button>
      <button type="button" role="tab" id="cut-lab-step-tab-4" data-cut-lab-step="4" aria-selected="${selectedStep === 4}" tabindex="${selectedStep === 4 ? 0 : -1}">Goals</button>
      <button type="submit" role="tab" id="cut-lab-step-tab-5" form="cut-lab-export-form" data-cut-lab-step="5" aria-selected="${selectedStep === 5}" tabindex="${selectedStep === 5 ? 0 : -1}">Export</button>
    </div>
    <section role="tabpanel" id="cut-lab-step-panel-1" aria-labelledby="cut-lab-step-tab-1">Process</section>
    <section role="tabpanel" id="cut-lab-step-panel-2" aria-labelledby="cut-lab-step-tab-2">Decide</section>
    <section role="tabpanel" id="cut-lab-step-panel-3" aria-labelledby="cut-lab-step-tab-3">Plan</section>
    <section role="tabpanel" id="cut-lab-step-panel-4" aria-labelledby="cut-lab-step-tab-4">Goals</section>
    <section role="tabpanel" id="cut-lab-step-panel-5" aria-labelledby="cut-lab-step-tab-5">Export</section>
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
  document.body.innerHTML = '';
});

describe('cut-lab step tabs', () => {
  it('preserves the server-selected tab on load and shows one panel on click', () => {
    renderFixture();
    initialize();

    expect(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).toHaveLength(5);
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

  it('leaves every panel visible when the server selected no tab', () => {
    renderFixture(0);
    initialize();

    expect(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).toHaveLength(5);
  });
});
