import { afterEach, beforeAll, describe, expect, it } from 'vitest';
import '../wwwroot/ts/cut-lab';

beforeAll(() => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
    configurable: true,
    value(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    },
  });
});

afterEach(() => {
  document.body.innerHTML = '';
});

const buildFixture = (allProtected: boolean): void => {
  const buttons = ['Lands', 'Ramp', 'Targeted removal', 'Card draw', 'Protection']
    .map((label, index) => `
      <div class="cutlab-role-group">
        <summary>${label} · 4 cards</summary>
        <button type="button" data-cut-lab-lock-role="role-${index}" aria-pressed="${allProtected}">${allProtected ? 'Unlock' : 'Protect'}</button>
      </div>`)
    .join('');
  const rows = ['role-0', 'role-1', 'role-2', 'role-3', 'role-4']
    .map(role => `<tr data-cut-lab-card="${role}" data-cut-lab-role="${role}"><td><input type="checkbox" data-cut-lab-lock-card="${role}" ${allProtected ? 'checked' : ''} /></td></tr>`)
    .join('');

  document.body.innerHTML = `
    <form data-cache-key="cut-lab">
      <input type="hidden" name="CutLabStateJson" value="" />
      <div data-cut-lab-constraints-role-count>0/5</div>
      <span data-cut-lab-constraints-role-caption>Not yet protected:</span>
      <div data-cut-lab-constraints-role-chips></div>
      <p data-cut-lab-constraints-role-more hidden></p>
      <p data-cut-lab-constraints-role-hint hidden>Protect a role with its "Lock all" button in <a href="#cut-lab-section-competes" data-cut-lab-jump-link>How your pool competes</a>.</p>
      <table><tbody>${rows}</tbody></table>
      ${buttons}
    </form>`;
};

describe('Cut Lab constraints summary', () => {
  it('shows three unprotected roles and the remaining count', () => {
    buildFixture(false);
    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.querySelector('[data-cut-lab-constraints-role-caption]')?.hidden).toBe(false);
    expect(document.querySelector('[data-cut-lab-constraints-role-caption]')?.textContent).toBe('Not yet protected:');
    expect(document.querySelector('[data-cut-lab-constraints-role-caption]')?.compareDocumentPosition(document.querySelector('[data-cut-lab-constraints-role-chips]') ?? document.body) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(document.querySelectorAll('[data-cut-lab-constraints-role-chips] .kb-chip')).toHaveLength(3);
    expect(document.querySelector('[data-cut-lab-constraints-role-more]')?.textContent).toBe('+2 more');
    expect(document.querySelector('[data-cut-lab-constraints-role-more]')?.hidden).toBe(false);
    expect(document.querySelector('[data-cut-lab-constraints-role-hint]')?.hidden).toBe(false);
    expect(document.querySelector('[data-cut-lab-constraints-role-hint]')?.textContent).toBe('Protect a role with its "Lock all" button in How your pool competes.');
  });

  it('hides the unprotected role details when all roles are protected', () => {
    buildFixture(true);
    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.querySelector('[data-cut-lab-constraints-role-count]')?.textContent).toBe('5/5');
    expect(document.querySelector('[data-cut-lab-constraints-role-caption]')?.hidden).toBe(true);
    expect(document.querySelector('[data-cut-lab-constraints-role-more]')?.hidden).toBe(true);
    expect(document.querySelector('[data-cut-lab-constraints-role-hint]')?.hidden).toBe(true);
  });
});
