import { afterEach, beforeEach, expect, test } from 'vitest';
import '../wwwroot/ts/flag-filter';
import '../wwwroot/ts/admin-flags';

const fixture = `<input id="flag-filter-search"><div><button data-flag-prefix="" aria-pressed="true">All</button><button data-flag-prefix="analysis." aria-pressed="false">analysis <span class="admin-filter-chips__count">3</span></button><button data-flag-prefix="service." aria-pressed="false">service <span class="admin-filter-chips__count">1</span></button><button data-flag-prefix="services." aria-pressed="false">services <span class="admin-filter-chips__count">1</span></button><button data-flag-prefix="sync." aria-pressed="false">sync <span class="admin-filter-chips__count">1</span></button></div><div><button data-flag-status="" aria-pressed="true">All statuses</button><button data-flag-status="on" aria-pressed="false">Enabled</button><button data-flag-status="off" aria-pressed="false">Disabled</button></div><p id="flag-filter-count"></p><table><tbody><tr data-flag-key="analysis.manabase.accuracy" data-flag-enabled="true"></tr><tr data-flag-key="analysis.manabase.baseline" data-flag-enabled="false"></tr><tr data-flag-key="analysis.wincon-map" data-flag-enabled="true"></tr><tr data-flag-key="service.scryfall-tagger.enabled" data-flag-enabled="true"></tr><tr data-flag-key="services.legacy" data-flag-enabled="true"></tr><tr data-flag-key="sync.reconcile" data-flag-enabled="false"></tr><tr id="flag-filter-empty" class="hidden"></tr></tbody></table>`;
const reload = (): void => { document.body.innerHTML = fixture; document.dispatchEvent(new Event('DOMContentLoaded')); };
const keys = (): string[] => Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-flag-key]:not(.hidden)')).map(x => x.dataset.flagKey!);
const click = (name: string): void => Array.from(document.querySelectorAll<HTMLButtonElement>('button')).find(x => x.textContent?.trim() === name || x.textContent?.trim().startsWith(`${name} `))!.click();
const search = (value: string): void => { const input = document.querySelector<HTMLInputElement>('#flag-filter-search')!; input.value = value; input.dispatchEvent(new Event('input')); };
beforeEach(() => { sessionStorage.clear(); reload(); });
afterEach(() => { sessionStorage.clear(); document.body.innerHTML = ''; });
test('analysis chip shows analysis rows', () => { click('analysis'); expect(keys()).toHaveLength(3); expect(document.querySelector('#flag-filter-count')!.textContent).toBe('3 of 6 flags shown'); });
test('service chip excludes services adjacency', () => { click('service'); expect(keys()).toEqual(['service.scryfall-tagger.enabled']); });
test('analysis plus disabled status composes', () => { click('analysis'); click('Disabled'); expect(keys()).toEqual(['analysis.manabase.baseline']); });
test('longer search narrows namespace', () => { click('analysis'); search('analysis.manabase.'); expect(keys()).toHaveLength(2); });
test('saved sync prefix restores', () => { click('sync'); reload(); expect(keys()).toEqual(['sync.reconcile']); });
test('stale second-level prefix falls back to all', () => { sessionStorage.setItem('deckflowAdminFlagPrefix', 'analysis.manabase.'); reload(); expect(keys()).toHaveLength(6); expect(document.querySelector('[data-flag-prefix=""]')!.getAttribute('aria-pressed')).toBe('true'); });
