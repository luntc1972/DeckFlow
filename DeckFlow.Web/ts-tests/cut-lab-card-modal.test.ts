import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabStateSnapshot {
  pool: Array<{
    name: string;
    isLocked: boolean;
  }>;
}

let showModalSpy: ReturnType<typeof vi.fn>;
let closeSpy: ReturnType<typeof vi.fn>;

beforeAll(() => {
  showModalSpy = vi.fn(function showModal(this: HTMLDialogElement) {
    this.setAttribute('open', '');
  });
  closeSpy = vi.fn(function close(this: HTMLDialogElement) {
    this.removeAttribute('open');
  });

  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
    configurable: true,
    value: showModalSpy,
  });

  Object.defineProperty(HTMLDialogElement.prototype, 'close', {
    configurable: true,
    value: closeSpy,
  });
});

afterEach(() => {
  document.body.innerHTML = '';
  showModalSpy.mockClear();
  closeSpy.mockClear();
});

const buildFixture = (): void => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab">
      <input type="hidden" name="CutLabStateJson" value="" />
      <input type="hidden" name="CutLabStateJson" value="" />
      <textarea name="PrimaryPlan">Keep the interaction dense.</textarea>
      <textarea name="SecondaryPlan"></textarea>
      <input type="radio" name="Bracket" value="3" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
      <table>
        <tbody>
          <tr data-cut-lab-card="Counterspell" data-cut-lab-type-line="Instant" data-cut-lab-role="interaction" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Lock"><input type="checkbox" data-cut-lab-lock-card="Counterspell" /></td>
            <td data-label="Card">
              <button type="button" data-cutlab-card-open="Counterspell">Counterspell</button>
            </td>
            <td data-label="Package">
              <select data-cut-lab-package-card="Counterspell"><option value="">Unlocked pool</option></select>
            </td>
          </tr>
          <tr data-cut-lab-card="Atraxa, Praetors' Voice" data-cut-lab-type-line="Legendary Creature — Angel Horror" data-cut-lab-role="commander" data-cut-lab-quantity="1" data-cut-lab-commander="true">
            <td data-label="Lock"><input type="checkbox" data-cut-lab-lock-card="Atraxa, Praetors' Voice" checked disabled /></td>
            <td data-label="Card">
              <button type="button" data-cutlab-card-open="Atraxa, Praetors' Voice">Atraxa, Praetors' Voice</button>
            </td>
            <td data-label="Package">
              <select data-cut-lab-package-card="Atraxa, Praetors' Voice"><option value="">Unlocked pool</option></select>
            </td>
          </tr>
          <tr data-cut-lab-card="Mystery Card" data-cut-lab-type-line="Artifact" data-cut-lab-role="" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Lock"><input type="checkbox" data-cut-lab-lock-card="Mystery Card" /></td>
            <td data-label="Card">
              <button type="button" data-cutlab-card-open="Mystery Card">Mystery Card</button>
            </td>
            <td data-label="Package">
              <select data-cut-lab-package-card="Mystery Card"><option value="">Unlocked pool</option></select>
            </td>
          </tr>
        </tbody>
      </table>
      <div class="kb-chip-area__chips">
        <button type="button" class="kb-chip cutlab-role-chip" data-cut-lab-chip-card="Counterspell" data-cutlab-card-open="Counterspell" aria-pressed="false">
          Counterspell
        </button>
      </div>
    </form>
    <script type="application/json" id="cutlab-card-text-data">{"Counterspell":{"typeLine":"Instant","manaCost":"{U}{U}","setCode":"TMP","collectorNumber":"55","oracleText":"Counter target spell.","comboContext":"Infinite cards"},"Atraxa, Praetors' Voice":{"typeLine":"Legendary Creature — Angel Horror","manaCost":"{G}{W}{U}{B}","oracleText":"Flying, vigilance, deathtouch, lifelink","power":"4","toughness":"4"}}</script>
    <dialog id="cutlab-card-modal" class="cutlab-card-modal" aria-labelledby="cutlab-card-modal-title">
      <div class="cutlab-card-modal__panel">
        <h2 id="cutlab-card-modal-title"></h2>
        <div class="cutlab-card-modal__body">
          <p data-cutlab-modal-meta></p>
          <p data-cutlab-modal-oracle></p>
          <p data-cutlab-modal-combo></p>
        </div>
        <div class="cutlab-card-modal__actions">
          <button type="button" data-cutlab-modal-lock></button>
          <button type="button" data-cutlab-modal-close>Close</button>
        </div>
      </div>
    </dialog>
  `;

  document.dispatchEvent(new Event('DOMContentLoaded'));
};

describe('cut-lab card modal', () => {
  it('opens from a trigger, populates the dialog, and toggles the canonical pool lock state', () => {
    buildFixture();

    const trigger = document.querySelector<HTMLButtonElement>('button[data-cut-lab-chip-card="Counterspell"]');
    const title = document.getElementById('cutlab-card-modal-title');
    const meta = document.querySelector<HTMLElement>('[data-cutlab-modal-meta]');
    const oracle = document.querySelector<HTMLElement>('[data-cutlab-modal-oracle]');
    const lockButton = document.querySelector<HTMLButtonElement>('[data-cutlab-modal-lock]');
    const closeButton = document.querySelector<HTMLButtonElement>('[data-cutlab-modal-close]');
    const checkbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Counterspell"]');

    trigger?.click();

    expect(showModalSpy).toHaveBeenCalledTimes(1);
    expect(title?.textContent).toBe('Counterspell');
    expect(meta?.textContent).toBe('Instant · {U}{U} · TMP #55');
    expect(oracle?.textContent).toContain('Counter target spell.');
    expect(lockButton?.textContent).toBe('Lock');
    expect(checkbox?.checked).toBe(false);

    lockButton?.click();

    expect(checkbox?.checked).toBe(true);
    expect(lockButton?.textContent).toBe('Unlock');

    Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).forEach(input => {
      const parsed = JSON.parse(input.value) as CutLabStateSnapshot;
      expect(parsed.pool.find(card => card.name === 'Counterspell')?.isLocked).toBe(true);
    });

    closeButton?.click();

    expect(closeSpy).toHaveBeenCalledTimes(1);
  });

  it('renders the missing-data fallback and disables the modal lock button for commander cards', () => {
    buildFixture();

    const mysteryTrigger = document.querySelector<HTMLButtonElement>('button[data-cutlab-card-open="Mystery Card"]');
    const commanderTrigger = document.querySelector<HTMLButtonElement>('button[data-cutlab-card-open="Atraxa, Praetors\\\' Voice"]');
    const meta = document.querySelector<HTMLElement>('[data-cutlab-modal-meta]');
    const oracle = document.querySelector<HTMLElement>('[data-cutlab-modal-oracle]');
    const lockButton = document.querySelector<HTMLButtonElement>('[data-cutlab-modal-lock]');

    mysteryTrigger?.click();

    expect(meta?.textContent).not.toContain('/');
    expect(oracle?.textContent).toBe('No card text available.');
    expect(lockButton?.disabled).toBe(false);

    commanderTrigger?.click();

    expect(meta?.textContent).toBe('Legendary Creature — Angel Horror · {G}{W}{U}{B} · 4/4');
    expect(lockButton?.disabled).toBe(true);
    expect(lockButton?.textContent).toBe('Locked');
  });
});
