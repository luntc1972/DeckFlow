import { afterEach, expect, it } from 'vitest';
import '../wwwroot/ts/cut-lab';

afterEach(() => { document.body.innerHTML = ''; });

it('keeps alphabetical subtype results unpinned and original state order through lock and package toggles', () => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab">
      <input name="CutLabStateJson" value="" />
      <input data-cut-lab-subtype-search value="Wizard" />
      <div data-cut-lab-subtype-results></div>
      <div data-cut-lab-package-id="p" data-cut-lab-package-name="Package">
        <input type="checkbox" data-cut-lab-package-toggle="p" />
      </div>
      <table><tbody>
        <tr data-cut-lab-card="Zulu Commander" data-cut-lab-pool-index="1" data-cut-lab-sort-order="1" data-cut-lab-commander="true" data-cut-lab-quantity="1" data-cut-lab-type-line="Creature — Wizard">
          <td><input type="checkbox" data-cut-lab-lock-card="Zulu Commander" checked disabled /></td>
        </tr>
        <tr data-cut-lab-card="Alpha" data-cut-lab-pool-index="0" data-cut-lab-sort-order="0" data-cut-lab-quantity="1" data-cut-lab-type-line="Creature — Wizard">
          <td><input type="checkbox" data-cut-lab-lock-card="Alpha" /><select data-cut-lab-package-card="Alpha"><option value="p" selected>Package</option></select></td>
        </tr>
      </tbody></table>
    </form>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
  expect(Array.from(document.querySelectorAll('[data-cut-lab-subtype-results] [data-cut-lab-chip-card]'))
    .map(chip => (chip as HTMLElement).dataset.cutLabChipCard)).toEqual(['Alpha', 'Zulu Commander']);
  const state = () => JSON.parse(document.querySelector<HTMLInputElement>('[name="CutLabStateJson"]')!.value);
  const lock = document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card="Alpha"]')!;
  lock.click();
  expect(state().pool.map((card: { name: string }) => card.name)).toEqual(['Alpha', 'Zulu Commander']);
  expect(state().pool[0].isLocked).toBe(true);
  lock.click();
  expect(state().pool[0].isLocked).toBe(false);
  document.querySelector<HTMLInputElement>('[data-cut-lab-package-toggle]')!.click();
  expect(state().pool.map((card: { name: string }) => card.name)).toEqual(['Alpha', 'Zulu Commander']);
  expect(state().pool[0].isLocked).toBe(true);
});
