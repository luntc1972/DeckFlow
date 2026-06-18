import { afterEach, beforeEach, describe, expect, it } from 'vitest';

const creatorKey = 'deckflowAdminKbCreator';
const searchKey = 'deckflowAdminKbSearch';

const TABLE_HTML = `
  <input id="kb-filter-search" type="search" />
  <select id="kb-creator-filter">
    <option value="">All creators</option>
    <option value="Salubrious Snail">Salubrious Snail</option>
    <option value="Based Deck Department">Based Deck Department</option>
  </select>
  <p id="kb-filter-count"></p>
  <table id="kb-entries-table"><tbody>
    <tr data-kb-search="alpha snail combo" data-kb-source="Salubrious Snail"><td>a</td></tr>
    <tr data-kb-search="beta based ramp" data-kb-source="Based Deck Department"><td>b</td></tr>
    <tr class="kb-filter__empty-row hidden" id="kb-filter-empty"><td>none</td></tr>
  </tbody></table>
`;

// Re-render the page and fire DOMContentLoaded so the module re-wires the filter,
// simulating the full-page reload that a visibility-tab <a> link triggers.
const reload = (): void => {
  document.body.innerHTML = TABLE_HTML;
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

// The browser loads kb-entry-filter.js (defines the DeckFlowKbFilter global)
// before content-kb-admin.js; mirror that load order here.
await import('../wwwroot/ts/kb-entry-filter');
await import('../wwwroot/ts/content-kb-admin');

beforeEach(() => {
  window.sessionStorage.clear();
  document.body.innerHTML = '';
});

afterEach(() => {
  window.sessionStorage.clear();
  document.body.innerHTML = '';
});

describe('Admin Content KB filter persistence across tab reloads', () => {
  it('persists the creator and search selections to sessionStorage on change', () => {
    reload();
    const select = document.querySelector<HTMLSelectElement>('#kb-creator-filter')!;
    const input = document.querySelector<HTMLInputElement>('#kb-filter-search')!;

    select.value = 'Based Deck Department';
    select.dispatchEvent(new Event('change'));
    input.value = 'ramp';
    input.dispatchEvent(new Event('input'));

    expect(window.sessionStorage.getItem(creatorKey)).toBe('Based Deck Department');
    expect(window.sessionStorage.getItem(searchKey)).toBe('ramp');
  });

  it('restores the selections on load without clearing them, surviving repeated tab switches', () => {
    window.sessionStorage.setItem(creatorKey, 'Salubrious Snail');
    window.sessionStorage.setItem(searchKey, 'combo');

    reload();
    expect(document.querySelector<HTMLSelectElement>('#kb-creator-filter')!.value).toBe('Salubrious Snail');
    expect(document.querySelector<HTMLInputElement>('#kb-filter-search')!.value).toBe('combo');

    // Keys must NOT be cleared on restore — a second tab switch still applies them.
    expect(window.sessionStorage.getItem(creatorKey)).toBe('Salubrious Snail');
    expect(window.sessionStorage.getItem(searchKey)).toBe('combo');

    reload();
    expect(document.querySelector<HTMLSelectElement>('#kb-creator-filter')!.value).toBe('Salubrious Snail');
    expect(document.querySelector<HTMLInputElement>('#kb-filter-search')!.value).toBe('combo');
  });
});
