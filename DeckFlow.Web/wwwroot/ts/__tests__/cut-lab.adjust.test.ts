declare function require(name: string): any;

const { afterEach, beforeAll, describe, expect, it, vi } = require('vitest');

require('../cut-lab');

let fetchMock: any;

beforeAll(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  document.body.innerHTML = '';
  fetchMock.mockReset();
});

const flushAdjustSubmit = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => window.setTimeout(resolve, 0));
};

const buildStateJson = (islandQuantity: number, adjustmentDelta = 0): string => JSON.stringify({
  commander: 'Commander',
  pool: [
    { name: 'Commander', quantity: 1, typeLine: 'Legendary Creature', isCommander: true, isLocked: true, packageId: null },
    { name: 'Island', quantity: islandQuantity, typeLine: 'Basic Land — Island', isCommander: false, isLocked: false, packageId: null },
  ],
  packages: [],
  decisions: [],
  quantityAdjustments: adjustmentDelta === 0 ? [] : [{ name: 'Island', delta: adjustmentDelta, isAddedBasic: false }],
  baselineSnapshot: { metrics: [] },
  roleFloors: [],
  goals: {
    commanderByTurn: 3,
    engineByTurn: 2,
    representativeLineByTurn: 4,
  },
  intent: {
    primaryPlan: 'Hit exactly 100.',
    secondaryPlan: null,
    bracket: 3,
    playExperience: 'Focused',
    includeSideboard: false,
    includeMaybeboard: false,
  },
});

const buildFixture = (): void => {
  const stateJson = buildStateJson(98);

  document.body.innerHTML = `
    <div class="error-banner hidden" role="alert"></div>
    <form data-cache-key="cut-lab">
      <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
      <textarea name="PrimaryPlan">Hit exactly 100.</textarea>
      <textarea name="SecondaryPlan"></textarea>
      <input type="radio" name="Bracket" value="3" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
    </form>
    <form id="cut-lab-export-form">
      <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
      <button type="submit" class="run-button" disabled>Build export</button>
    </form>
    <section class="result-panel">
      <div class="cutlab-sticky-bar">
        <span data-cut-lab-sticky-round>Round 1</span>
        <span data-cut-lab-sticky-remaining>1 to cut</span>
        <span data-cut-lab-sticky-accepted>0 cuts so far</span>
      </div>
      <button type="button" id="cut-lab-step-tab-4" class="is-disabled" disabled aria-disabled="true">Export</button>
    </section>
    <table>
      <tbody>
        <tr data-cut-lab-tuner-row="Island" data-cut-lab-quantity="98" data-cut-lab-legal-max="150">
          <td data-label="Quantity">
            <div class="cutlab-stepper">
              <form method="post" action="/cut-lab/adjust" data-cut-lab-adjust-form>
                <input type="hidden" name="__RequestVerificationToken" value="token-123" />
                <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
                <input type="hidden" name="CardName" value="Island" />
                <input type="hidden" name="Delta" value="1" />
                <input type="hidden" name="IsAddedBasic" value="false" />
                <button type="submit" class="cutlab-stepper-btn" data-cut-lab-adjust data-cut-lab-card="Island" data-cut-lab-delta="1">+</button>
              </form>
            </div>
            <span data-cut-lab-quantity-value>98</span>
          </td>
        </tr>
      </tbody>
    </table>
  `;

  document.dispatchEvent(new Event('DOMContentLoaded'));
};

describe('cut-lab adjust enhancement', () => {
  it('patches sticky count and exact-100 export gates after a successful stepper adjust', async () => {
    buildFixture();
    const nextStateJson = buildStateJson(98, 1);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        cutLabStateJson: nextStateJson,
        cardsRemaining: 0,
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form[data-cut-lab-adjust-form]');
    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-adjust]');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/cut-lab/adjust');
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
      cutLabStateJson: stateJsonFromInputs(),
      cardName: 'Island',
      delta: 1,
      isAddedBasic: false,
    });
    expect(document.querySelector('[data-cut-lab-sticky-remaining]')?.textContent).toBe('0 to cut');
    expect(document.getElementById('cut-lab-step-tab-4')?.hasAttribute('disabled')).toBe(false);
    expect(document.querySelector('#cut-lab-export-form button[type="submit"]')?.hasAttribute('disabled')).toBe(false);
    expect(document.querySelector('[data-cut-lab-quantity-value]')?.textContent).toBe('99');
  });

  it('surfaces the server error and preserves hidden state on a failed adjust', async () => {
    buildFixture();
    const originalState = stateJsonFromInputs();
    fetchMock.mockResolvedValue({
      ok: false,
      json: async () => ({
        message: "Couldn't recalculate this cut — nothing changed. Try again.",
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form[data-cut-lab-adjust-form]');
    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-adjust]');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    expect(document.querySelector('.error-banner')?.textContent).toContain("Couldn't recalculate this cut");
    expect(stateJsonFromInputs()).toBe(originalState);
  });
});

function stateJsonFromInputs(): string[] | string {
  const values = Array.from(document.querySelectorAll('input[name="CutLabStateJson"]')).map((input: any) => input.value);
  return values[0] ?? '';
}
