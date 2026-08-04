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
      <button type="button" role="tab" id="cut-lab-step-tab-3" data-cut-lab-step="3" disabled aria-selected="false" tabindex="-1">Plan</button>
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
  it('shows only the server-selected panel on load and moves state together on click', () => {
    renderFixture();
    initialize();

    expect(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).toHaveLength(1);
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

  it('does not activate disabled tabs and skips them during keyboard navigation', () => {
    renderFixture();
    initialize();

    const decide = document.getElementById('cut-lab-step-tab-2') as HTMLButtonElement;
    const plan = document.getElementById('cut-lab-step-tab-3') as HTMLButtonElement;
    decide.focus();
    decide.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(document.activeElement).toBe(document.getElementById('cut-lab-step-tab-4'));

    plan.click();
    expect(plan.getAttribute('aria-selected')).toBe('false');
    expect(document.getElementById('cut-lab-step-panel-4')?.hasAttribute('hidden')).toBe(false);
  });

  it('leaves every panel visible when the server selected no tab', () => {
    renderFixture(0);
    initialize();

    expect(document.querySelectorAll<HTMLElement>('[role="tabpanel"]:not([hidden])')).toHaveLength(5);
  });
});
