import { afterEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/admin-harvest';

const gridMarkup = (): string => '<table><thead><tr><th><button type="button" data-sort-column="commander_name" data-sort-next-dir="asc">Commander</button></th></tr></thead></table><a href="#" data-page="2" data-search="tef" data-sort-by="commander_name" data-sort-dir="asc">Next</a>';

const renderFixture = (includeExportForm = true): void => {
  vi.stubGlobal('matchMedia', vi.fn().mockReturnValue({ matches: false }));
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
  const exportForm = includeExportForm ? '<form><input data-export-search /><input data-export-sort-by /><input data-export-sort-dir /></form>' : '';
  document.body.innerHTML = `<div class="admin-harvest__tabs" role="tablist"><button id="harvest-tab-overview" data-harvest-tab="overview" role="tab" aria-controls="harvest-panel-overview" aria-selected="true">Overview</button><button id="harvest-tab-commanders" data-harvest-tab="commanders" role="tab" aria-controls="harvest-panel-commanders" aria-selected="false">Commanders</button></div><section id="harvest-panel-overview" data-harvest-panel="overview"></section><section id="harvest-panel-commanders" data-harvest-panel="commanders">${exportForm}<form id="commanders-search-form"><input id="commanders-search" /><button id="commanders-search-clear" type="button">Clear</button></form><div id="commanders-grid-container">${gridMarkup()}</div></section>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

const activateCommanders = (): void => document.querySelector<HTMLButtonElement>('#harvest-tab-commanders')!.click();

describe('admin harvest commanders export', () => {
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ''; });

  it('syncs search and non-default sort after a grid load', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() }));
    renderFixture();
    activateCommanders();
    await vi.waitFor(() => expect(document.querySelector<HTMLElement>('[data-sort-column]')).not.toBeNull());
    document.querySelector<HTMLElement>('[data-sort-column]')!.click();
    document.querySelector<HTMLInputElement>('#commanders-search')!.value = 'tef';
    document.querySelector<HTMLFormElement>('#commanders-search-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(document.querySelector<HTMLInputElement>('[data-export-search]')!.value).toBe('tef'));
    expect(document.querySelector<HTMLInputElement>('[data-export-sort-by]')!.value).toBe('commander_name');
    expect(document.querySelector<HTMLInputElement>('[data-export-sort-dir]')!.value).toBe('asc');
  });

  it('clearing search empties export search input', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() }));
    renderFixture();
    activateCommanders();
    document.querySelector<HTMLInputElement>('#commanders-search')!.value = 'tef';
    document.querySelector<HTMLFormElement>('#commanders-search-form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await vi.waitFor(() => expect(document.querySelector<HTMLInputElement>('[data-export-search]')!.value).toBe('tef'));
    document.querySelector<HTMLButtonElement>('#commanders-search-clear')!.click();
    await vi.waitFor(() => expect(document.querySelector<HTMLInputElement>('[data-export-search]')!.value).toBe(''));
  });

  it('changing pages leaves export state page-independent', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() }));
    renderFixture();
    activateCommanders();
    await vi.waitFor(() => expect(document.querySelector<HTMLElement>('[data-page]')).not.toBeNull());
    document.querySelector<HTMLElement>('[data-page]')!.click();
    await vi.waitFor(() => expect(document.querySelector<HTMLInputElement>('[data-export-search]')!.value).toBe('tef'));
    expect(document.querySelector<HTMLInputElement>('[data-export-sort-by]')!.value).toBe('commander_name');
  });

  it('loads grid without an export form', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, text: async () => gridMarkup() }));
    renderFixture(false);
    activateCommanders();
    await vi.waitFor(() => expect(document.querySelector<HTMLElement>('[data-page]')).not.toBeNull());
  });
});
