import { afterEach, beforeAll, describe, expect, it } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabStateSnapshot {
  pool: Array<{
    name: string;
    isLocked: boolean;
  }>;
}

interface CutLabApi {
  buildCutLabStateJson(snapshot: CutLabStateSnapshot): string;
}

let api: CutLabApi;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlowCutLab: CutLabApi }).DeckFlowCutLab;
});

afterEach(() => {
  document.body.innerHTML = '';
});

describe('DeckFlowCutLab pool filter', () => {
  it('hides non-matching pool rows without detaching them or changing whole-pool serialization/counts', () => {
    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value="" />
        <textarea name="PrimaryPlan">Keep the mana and engine intact.</textarea>
        <textarea name="SecondaryPlan"></textarea>
        <input type="radio" name="Bracket" value="3" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <p class="prompt-size-note"><span data-cut-lab-lock-count>3 cards in pool · 2 locked</span><span>(protected from any future cut)</span></p>
        <details class="cutlab-collapsible" open id="cut-lab-section-lock-pool">
          <summary class="cutlab-collapsible__summary">Lock your pool</summary>
          <div class="panel-heading">
            <div>
              <div class="cutlab-pool-filter" hidden>
                <div class="cutlab-pool-filter__group" role="radiogroup" aria-label="Show">
                  <span class="cutlab-pool-filter__label">Show:</span>
                  <label class="cutlab-pool-filter__option">
                    <input type="radio" name="CutLabPoolFilter" value="all" checked />
                    <span>All</span>
                  </label>
                  <label class="cutlab-pool-filter__option">
                    <input type="radio" name="CutLabPoolFilter" value="locked" />
                    <span>Locked</span>
                  </label>
                  <label class="cutlab-pool-filter__option">
                    <input type="radio" name="CutLabPoolFilter" value="unlocked" />
                    <span>Unlocked</span>
                  </label>
                </div>
                <input type="text" class="cutlab-pool-search" placeholder="Search card name…" aria-label="Search card name" />
                <p class="cutlab-pool-match-count">Showing 3 of 3 cards</p>
              </div>
            </div>
          </div>
          <table>
            <tbody>
              <tr data-cut-lab-card="Atraxa, Praetors' Voice" data-cut-lab-type-line="Legendary Creature — Angel Horror" data-cut-lab-role="payoffs" data-cut-lab-quantity="1" data-cut-lab-commander="true">
                <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Atraxa, Praetors' Voice" checked disabled /></td>
                <td data-label="Card"><strong>1 × Atraxa, Praetors' Voice</strong></td>
                <td data-label="Type / role">Legendary Creature — Angel Horror</td>
                <td data-label="Package"><select data-cut-lab-package-card="Atraxa, Praetors' Voice"><option value="">Unlocked pool</option><option value="__new__">+ New package…</option></select></td>
              </tr>
              <tr data-cut-lab-card="Sol Ring" data-cut-lab-type-line="Artifact" data-cut-lab-role="ramp" data-cut-lab-quantity="1" data-cut-lab-commander="false">
                <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Sol Ring" /></td>
                <td data-label="Card"><strong>1 × Sol Ring</strong></td>
                <td data-label="Type / role">Artifact</td>
                <td data-label="Package"><select data-cut-lab-package-card="Sol Ring"><option value="">Unlocked pool</option><option value="__new__">+ New package…</option></select></td>
              </tr>
              <tr data-cut-lab-card="Command Tower" data-cut-lab-type-line="Land" data-cut-lab-role="lands" data-cut-lab-quantity="1" data-cut-lab-commander="false">
                <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Command Tower" checked /></td>
                <td data-label="Card"><strong>1 × Command Tower</strong></td>
                <td data-label="Type / role">Land</td>
                <td data-label="Package"><select data-cut-lab-package-card="Command Tower"><option value="">Unlocked pool</option><option value="__new__">+ New package…</option></select></td>
              </tr>
              <tr class="cutlab-pool-empty-row" hidden>
                <td colspan="4">No cards match.</td>
              </tr>
            </tbody>
          </table>
        </details>
      </form>
    `;

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const filterContainer = document.querySelector<HTMLDivElement>('.cutlab-pool-filter');
    const matchCount = document.querySelector<HTMLElement>('.cutlab-pool-match-count');
    const searchInput = document.querySelector<HTMLInputElement>('input.cutlab-pool-search');
    const unlockedFilter = document.querySelector<HTMLInputElement>('input[name="CutLabPoolFilter"][value="unlocked"]');
    const emptyRow = document.querySelector<HTMLTableRowElement>('tr.cutlab-pool-empty-row');
    const summary = document.querySelector<HTMLElement>('[data-cut-lab-lock-count]');
    const hiddenInput = document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]');

    expect(filterContainer?.hidden).toBe(false);
    expect(matchCount?.textContent).toBe('Showing 3 of 3 cards');

    const summaryBeforeFilter = summary?.textContent ?? '';
    expect(document.querySelectorAll('tr[data-cut-lab-card]').length).toBe(3);

    if (unlockedFilter) {
      unlockedFilter.checked = true;
      unlockedFilter.dispatchEvent(new Event('change', { bubbles: true }));
    }

    expect(document.querySelectorAll('tr[data-cut-lab-card]').length).toBe(3);
    expect(document.querySelector<HTMLTableRowElement>("tr[data-cut-lab-card=\"Atraxa, Praetors' Voice\"]")?.hidden).toBe(true);
    expect(document.querySelector<HTMLTableRowElement>('tr[data-cut-lab-card="Command Tower"]')?.hidden).toBe(true);
    expect(document.querySelector<HTMLTableRowElement>('tr[data-cut-lab-card="Sol Ring"]')?.hidden).toBe(false);
    expect(matchCount?.textContent).toBe('Showing 1 of 3 cards');
    expect(summary?.textContent).toBe(summaryBeforeFilter);

    const serializedState = JSON.parse(hiddenInput?.value ?? '{}') as CutLabStateSnapshot;
    expect(api.buildCutLabStateJson(serializedState)).toContain('"Atraxa, Praetors\' Voice"');
    expect(serializedState.pool.map(card => card.name)).toEqual([
      'Atraxa, Praetors\' Voice',
      'Sol Ring',
      'Command Tower',
    ]);
    expect(serializedState.pool.map(card => card.isLocked)).toEqual([true, false, true]);

    if (searchInput) {
      searchInput.value = 'SOL';
      searchInput.dispatchEvent(new Event('input', { bubbles: true }));
    }

    expect(matchCount?.textContent).toBe('Showing 1 of 3 cards');
    expect(document.querySelector<HTMLTableRowElement>('tr[data-cut-lab-card="Sol Ring"]')?.hidden).toBe(false);

    if (searchInput) {
      searchInput.value = 'zzz';
      searchInput.dispatchEvent(new Event('input', { bubbles: true }));
    }

    expect(matchCount?.textContent).toBe('Showing 0 of 3 cards');
    expect(emptyRow?.hidden).toBe(false);
    expect(summary?.textContent).toBe(summaryBeforeFilter);
  });
});
