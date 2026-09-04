import { afterEach, describe, expect, it } from 'vitest';
import '../wwwroot/ts/cut-lab';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('cut-lab combo label and package helper copy', () => {
  it('renders combo badge text and package helper copy for a multi-member package', () => {
    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value="" />
        <input type="radio" name="Bracket" value="4" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <table>
          <tbody>
            <tr data-cut-lab-card="Sol Ring" data-cut-lab-type-line="Artifact" data-cut-lab-role="ramp combo" data-cut-lab-quantity="1" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Sol Ring" /></td>
              <td data-label="Card"><strong>1 × Sol Ring</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Sol Ring">
                  <option value="">Unlocked pool</option>
                  <option value="pkg-fast-mana" selected>Fast mana package</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
            <tr data-cut-lab-card="Mana Crypt" data-cut-lab-type-line="Artifact" data-cut-lab-role="ramp combo" data-cut-lab-quantity="1" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Mana Crypt" checked /></td>
              <td data-label="Card"><strong>1 × Mana Crypt</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Mana Crypt">
                  <option value="">Unlocked pool</option>
                  <option value="pkg-fast-mana" selected>Fast mana package</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
          </tbody>
        </table>
        <section class="result-panel" data-cut-lab-structural-findings>
          <div class="panel-heading">
            <div><h2>Structural findings</h2></div>
            <div data-cut-lab-findings-count-slot><span class="cutlab-findings-count">1 structural finding</span></div>
          </div>
          <div data-cut-lab-structural-findings-body>
            <div class="cutlab-finding">
              <p class="cutlab-finding__heading">Combo-protected cards</p>
              <div class="cutlab-finding__item">
                <p class="cutlab-finding__lead">Fast mana is part of an active combo line.</p>
                <div class="kb-chip-area__chips">
                  <button type="button" class="kb-chip cutlab-role-chip" data-cut-lab-chip-card="Sol Ring" aria-pressed="false">
                    <span>Sol Ring</span>
                    <span class="cutlab-combo-badge cutlab-combo-badge--complete">Combo piece</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
          <p class="cutlab-degradation-note hidden" data-cut-lab-degradation="combo">Combo data unavailable right now.</p>
          <p class="cutlab-degradation-note hidden" data-cut-lab-degradation="category">Community category data unavailable.</p>
        </section>
      </form>
      <details class="cutlab-collapsible" data-cutlab-mobile-collapse open id="cut-lab-section-packages">
        <summary class="cutlab-collapsible__summary">Packages</summary>
        <div class="cutlab-package-help">
          <p class="cutlab-package-help__heading">How packages work</p>
          <p>Grouping a card into a package doesn't remove it from the pool — it's still counted and still cuttable unless the package itself is locked. Packages let you lock or unlock several cards together instead of one at a time.</p>
        </div>
        <div class="result-panel nested-panel" data-cut-lab-package-id="pkg-fast-mana" data-cut-lab-package-name="Fast mana package">
          <div class="panel-heading">
            <div>
              <h2>Fast mana package</h2>
              <p>2 members</p>
            </div>
            <div class="panel-heading__actions">
              <label class="kb-chip" for="cut-lab-package-lock-pkg-fast-mana">
                <input id="cut-lab-package-lock-pkg-fast-mana" type="checkbox" data-cut-lab-package-toggle="pkg-fast-mana" />
                <span>Lock package</span>
              </label>
            </div>
          </div>
          <div class="kb-chip-area__chips">
            <span class="kb-chip">Sol Ring</span>
            <span class="kb-chip">Mana Crypt</span>
          </div>
        </div>
      </details>
    `;

    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(document.querySelector<HTMLElement>('.cutlab-combo-badge')?.textContent).toBe('Combo piece');
    expect(document.querySelector<HTMLElement>('.cutlab-package-help p:last-child')?.textContent).toBe(
      "Grouping a card into a package doesn't remove it from the pool — it's still counted and still cuttable unless the package itself is locked. Packages let you lock or unlock several cards together instead of one at a time.",
    );
    expect(document.querySelector<HTMLElement>('[data-cut-lab-package-id="pkg-fast-mana"] .panel-heading p')?.textContent).toBe('2 members');
    expect(document.querySelectorAll('[data-cut-lab-package-id="pkg-fast-mana"] .kb-chip-area__chips .kb-chip')).toHaveLength(2);
  });
});
