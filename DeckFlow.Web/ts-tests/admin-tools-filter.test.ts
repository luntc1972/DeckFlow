import { afterEach, beforeEach, expect, test } from 'vitest';
import '../wwwroot/ts/flag-filter';
import '../wwwroot/ts/admin-tools';

const fixture = `
<div class="tools-filter">
  <label for="tools-filter-search">Filter by tool name or flag key prefix</label>
  <input id="tools-filter-search" type="search" />
  <div class="tools-filter__chips" role="group" aria-label="Status filter">
    <button type="button" class="tools-filter__chip is-active" data-tools-status="" aria-pressed="true">All statuses</button>
    <button type="button" class="tools-filter__chip" data-tools-status="on" aria-pressed="false">Enabled</button>
    <button type="button" class="tools-filter__chip" data-tools-status="off" aria-pressed="false">Disabled</button>
  </div>
  <p id="tools-filter-count"></p>
</div>
<div id="tools-sections">
  <section class="admin-tools__section"><table><tbody>
    <tr data-tool-label="Deck Analysis" data-tool-flag-key="tool.deck-analysis.enabled" data-tool-enabled="true"><td>Deck Analysis</td></tr>
    <tr data-tool-label="Mana Base" data-tool-flag-key="tool.manabase.enabled" data-tool-enabled="false"><td>Mana Base</td></tr>
  </tbody></table></section>
  <section class="admin-tools__section"><table><tbody>
    <tr data-tool-label="Cut Lab" data-tool-flag-key="tool.cut-lab.enabled" data-tool-enabled="true"><td>Cut Lab</td></tr>
    <tr data-tool-label="Convert Deck" data-tool-flag-key="tool.convert.enabled" data-tool-enabled="false"><td>Convert Deck</td></tr>
  </tbody></table></section>
  <section class="admin-tools__section"><h2>Empty section</h2><table><tbody></tbody></table></section>
  <p id="tools-filter-empty" class="tools-filter__empty hidden">No tools match the current filter.</p>
</div>`;

const reload = (): void => {
  document.body.innerHTML = fixture;
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

const input = (): HTMLInputElement => document.querySelector<HTMLInputElement>('#tools-filter-search')!;
const labels = (): string[] => Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-tool-label]:not(.hidden)')).map((row) => row.dataset.toolLabel!);
const click = (name: string): void => (Array.from(document.querySelectorAll<HTMLButtonElement>('button[data-tools-status]')).find((chip) => chip.textContent === name)!).click();
const search = (value: string): void => { input().value = value; input().dispatchEvent(new Event('input')); };

beforeEach(() => { sessionStorage.clear(); reload(); });
afterEach(() => { sessionStorage.clear(); document.body.innerHTML = ''; });

test('shows all rows and defaults to all statuses', () => { expect(labels()).toHaveLength(4); expect(document.querySelector('#tools-filter-count')!.textContent).toBe('4 of 4 tools shown'); expect(document.querySelector('#tools-filter-empty')!.classList.contains('hidden')).toBe(true); expect(document.querySelector('[data-tools-status=""]')!.getAttribute('aria-pressed')).toBe('true'); });
test('search cut shows Cut Lab', () => { search('cut'); expect(labels()).toEqual(['Cut Lab']); expect(document.querySelector('#tools-filter-count')!.textContent).toBe('1 of 4 tools shown'); });
test('hides empty sections only while filtered', () => { const emptySection = document.querySelectorAll<HTMLElement>('.admin-tools__section')[2]; expect(emptySection.classList.contains('hidden')).toBe(false); search('cut'); expect(emptySection.classList.contains('hidden')).toBe(true); search(''); expect(emptySection.classList.contains('hidden')).toBe(false); click('Enabled'); expect(emptySection.classList.contains('hidden')).toBe(true); });
test('search CUT is case insensitive', () => { search('CUT'); expect(labels()).toEqual(['Cut Lab']); });
test('search lab is prefix not substring', () => { search('lab'); expect(labels()).toEqual([]); });
test('search tool.man uses flag key prefix', () => { search('tool.man'); expect(labels()).toEqual(['Mana Base']); });
test('whitespace search is trimmed', () => { search('   '); expect(labels()).toHaveLength(4); });
test('enabled chip filters enabled tools', () => { click('Enabled'); expect(labels()).toEqual(['Deck Analysis', 'Cut Lab']); expect(document.querySelector('[data-tools-status="on"]')!.classList.contains('is-active')).toBe(true); expect(document.querySelector('[data-tools-status=""]')!.getAttribute('aria-pressed')).toBe('false'); });
test('disabled chip filters disabled tools', () => { click('Disabled'); expect(labels()).toEqual(['Mana Base', 'Convert Deck']); });
test('disabled chip composes with search', () => { click('Disabled'); search('c'); expect(labels()).toEqual(['Convert Deck']); });
test('search hides empty sections and restores them', () => { search('mana'); expect(document.querySelectorAll('.admin-tools__section')[1].classList.contains('hidden')).toBe(true); search(''); expect(Array.from(document.querySelectorAll('.admin-tools__section')).every((section) => !section.classList.contains('hidden'))).toBe(true); });
test('empty search result shows empty state', () => { search('zzz'); expect(document.querySelector('#tools-filter-empty')!.classList.contains('hidden')).toBe(false); expect(document.querySelector('#tools-filter-count')!.textContent).toBe('0 of 4 tools shown'); search(''); expect(document.querySelector('#tools-filter-empty')!.classList.contains('hidden')).toBe(true); });
test('persists search and status', () => { search('c'); click('Disabled'); expect(sessionStorage.getItem('deckflowAdminToolsSearch')).toBe('c'); expect(sessionStorage.getItem('deckflowAdminToolsStatus')).toBe('off'); });
test('restores search and status', () => { sessionStorage.setItem('deckflowAdminToolsSearch', 'c'); sessionStorage.setItem('deckflowAdminToolsStatus', 'off'); reload(); expect(input().value).toBe('c'); expect(labels()).toEqual(['Convert Deck']); });
test('stale status falls back to all statuses', () => { sessionStorage.setItem('deckflowAdminToolsStatus', 'maybe'); reload(); expect(labels()).toHaveLength(4); expect(document.querySelector('[data-tools-status=""]')!.getAttribute('aria-pressed')).toBe('true'); });
test('tools filter writes only its own storage keys', () => { search('cut'); click('Disabled'); expect(sessionStorage.getItem('deckflowAdminToolsSearch')).toBe('cut'); expect(sessionStorage.getItem('deckflowAdminToolsStatus')).toBe('off'); expect(sessionStorage.getItem('deckflowAdminFlagSearch')).toBeNull(); expect(sessionStorage.getItem('deckflowAdminFlagPrefix')).toBeNull(); expect(sessionStorage.getItem('deckflowAdminFlagStatus')).toBeNull(); });
test('missing markup does not throw', () => { document.body.innerHTML = ''; expect(() => document.dispatchEvent(new Event('DOMContentLoaded'))).not.toThrow(); });
