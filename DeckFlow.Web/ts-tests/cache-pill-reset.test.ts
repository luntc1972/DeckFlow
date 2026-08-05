import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
  window.localStorage.clear();
  window.sessionStorage.clear();
});

const seedCache = (key: string, state: Record<string, string[]>): void => {
  window.sessionStorage.setItem(`decksync-form-state-${key}`, JSON.stringify(state));
  window.sessionStorage.setItem(`decksync-form-state-${key}:savedAt`, Date.now().toString());
};

const bootstrap = async (): Promise<void> => {
  vi.resetModules();
  await import('../wwwroot/ts/deck-sync');
};

const resetButton = (): HTMLButtonElement | null =>
  document.querySelector<HTMLButtonElement>('[data-cache-pill] .cache-pill__reset');

const mountForm = (key: string, withClearControl = true): void => {
  document.body.innerHTML = `
    <form data-cache-key="${key}">
      <input name="DeckName" value="Repro Deck" />
      ${withClearControl
        ? '<a href="/deck-history" class="clear-cache-button" data-clear-cache>Start over</a>'
        : ''}
    </form>
  `;
};

/** Stops jsdom from warning about the unimplemented navigation the anchor would trigger. */
const swallowNavigation = (): void => {
  document.querySelector('[data-clear-cache]')
    ?.addEventListener('click', event => event.preventDefault());
};

describe('restored-from-cache pill Reset', () => {
  it('delegates to the form clear control instead of calling form.reset()', async () => {
    // Why: on a POST-rendered page the server writes the submitted values into each
    // control's HTML default, so form.reset() restores exactly what the user wanted
    // cleared. Only a navigation to the clean GET actually resets the page.
    seedCache('deck-history', { DeckName: ['Repro Deck'] });
    mountForm('deck-history');

    await bootstrap();

    const clearControl = document.querySelector<HTMLAnchorElement>('[data-clear-cache]');
    const clearClicks = vi.fn((event: Event) => event.preventDefault());
    clearControl?.addEventListener('click', clearClicks);

    const reset = resetButton();
    expect(reset).not.toBeNull();

    const formResetSpy = vi.spyOn(HTMLFormElement.prototype, 'reset');
    reset!.click();

    expect(clearClicks).toHaveBeenCalledTimes(1);
    expect(formResetSpy).not.toHaveBeenCalled();
    expect(window.sessionStorage.getItem('decksync-form-state-deck-history')).toBeNull();
    expect(window.sessionStorage.getItem('decksync-form-state-deck-history:savedAt')).toBeNull();
  });

  it('falls back to clearing state when the form has no clear control', async () => {
    seedCache('orphan-form', { DeckName: ['Repro Deck'] });
    mountForm('orphan-form', false);

    await bootstrap();
    resetButton()!.click();

    expect(window.sessionStorage.getItem('decksync-form-state-orphan-form')).toBeNull();
  });
});

describe('clear control native anchor navigation', () => {
  it('suppresses re-persistence so pagehide cannot resurrect the cleared fields', async () => {
    // Why: the anchor form of the clear control navigates natively, and pagehide fires
    // during that navigation. Without skipPersistence the still-populated form is written
    // straight back into sessionStorage and the clean GET hydrates it again.
    // Why a key of its own: each test re-imports deck-sync, and every import binds another
    // window-level pagehide listener closed over that import's form. Those listeners outlive
    // the test. Sharing a cache key would let a previous test's detached form persist over
    // this one's entry, and the failure would read as a product bug.
    seedCache('native-nav', { DeckName: ['Repro Deck'] });
    mountForm('native-nav');

    await bootstrap();

    swallowNavigation();
    document.querySelector<HTMLAnchorElement>('[data-clear-cache]')!.click();

    const form = document.querySelector<HTMLFormElement>('form[data-cache-key]')!;
    expect(form.dataset.skipPersistence).toBe('true');

    window.dispatchEvent(new Event('pagehide'));
    expect(window.sessionStorage.getItem('decksync-form-state-native-nav')).toBeNull();
  });
});
