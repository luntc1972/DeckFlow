import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabApiWithSyncState {
  syncDecisionState(serializedState: string): void;
}

declare global {
  interface Window {
    DeckFlowCutLab?: CutLabApiWithSyncState;
  }
}

let fetchMock: ReturnType<typeof vi.fn>;

beforeAll(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  document.body.innerHTML = '';
  fetchMock.mockReset();
  vi.restoreAllMocks();
});

const flushWhatifSubmit = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => window.setTimeout(resolve, 0));
};

const buildStateJson = (): string => JSON.stringify({
  commander: 'Commander',
  pool: [
    { name: 'Commander', quantity: 1, typeLine: 'Legendary Creature', isCommander: true, isLocked: true, packageId: null },
    { name: 'Working Card', quantity: 1, typeLine: 'Artifact', isCommander: false, isLocked: false, packageId: null },
    { name: 'Cut Card', quantity: 1, typeLine: 'Instant', isCommander: false, isLocked: false, packageId: null },
  ],
  packages: [],
  decisions: [
    { cardName: 'Cut Card', kind: 'Accepted', round: 'round-1', ordinal: 1 },
  ],
  baselineSnapshot: {
    metrics: [],
  },
  roleFloors: [],
  goals: {
    commanderByTurn: 3,
    engineByTurn: 2,
    representativeLineByTurn: 4,
  },
  intent: {
    primaryPlan: 'Stay lean.',
    secondaryPlan: null,
    bracket: 3,
    playExperience: 'Focused',
    includeSideboard: false,
    includeMaybeboard: false,
  },
});

const buildFixture = (): void => {
  const stateJson = buildStateJson();

  document.body.innerHTML = `
    <form data-cache-key="cut-lab">
      <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
      <textarea name="PrimaryPlan">Stay lean.</textarea>
      <textarea name="SecondaryPlan"></textarea>
      <input type="radio" name="Bracket" value="3" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
      <input type="checkbox" name="IncludeSideboard" value="true" />
      <input type="checkbox" name="IncludeMaybeboard" value="true" />
      <table>
        <tbody>
          <tr data-cut-lab-card="Commander" data-cut-lab-type-line="Legendary Creature" data-cut-lab-role="draw" data-cut-lab-quantity="1" data-cut-lab-commander="true">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Commander" checked disabled /></td>
            <td data-label="Card"><strong>1 × Commander</strong></td>
            <td data-label="Package assignment"><select data-cut-lab-package-card="Commander"><option value="">Unlocked pool</option><option value="__new__">+ New package…</option></select></td>
          </tr>
          <tr data-cut-lab-card="Working Card" data-cut-lab-type-line="Artifact" data-cut-lab-role="ramp" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Working Card" /></td>
            <td data-label="Card"><strong>1 × Working Card</strong></td>
            <td data-label="Package assignment"><select data-cut-lab-package-card="Working Card"><option value="">Unlocked pool</option><option value="__new__">+ New package…</option></select></td>
          </tr>
          <tr data-cut-lab-card="Cut Card" data-cut-lab-type-line="Instant" data-cut-lab-role="interaction" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Cut Card" /></td>
            <td data-label="Card"><strong>1 × Cut Card</strong></td>
            <td data-label="Package assignment"><select data-cut-lab-package-card="Cut Card"><option value="">Unlocked pool</option><option value="__new__">+ New package…</option></select></td>
          </tr>
        </tbody>
      </table>
      <section class="result-panel nested-panel">
        <div class="card-picker__rows">
          <div class="card-picker__row hidden" data-cut-lab-new-package-row>
            <div class="card-picker__input-shell">
              <input class="card-picker__input" type="text" data-cut-lab-new-package-input />
            </div>
            <button type="button" data-cut-lab-new-package-save>+</button>
            <button type="button" data-cut-lab-new-package-cancel>x</button>
          </div>
        </div>
      </section>
    </form>
    <form method="post" action="/cut-lab/whatif" data-cut-lab-whatif-form>
      <input type="hidden" name="__RequestVerificationToken" value="token-123" />
      <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
      <select name="cardOut" data-cut-lab-whatif-card-out>
        <option value="">Choose a card</option>
        <option value="Working Card" selected>Working Card</option>
      </select>
      <select name="cardIn" data-cut-lab-whatif-card-in>
        <option value="">Choose a card</option>
        <option value="Cut Card" selected>Cut Card</option>
      </select>
      <div class="cutlab-whatif__preview hidden" data-cut-lab-whatif-preview>
        <p class="cutlab-whatif__selection hidden" data-cut-lab-whatif-selection></p>
        <table><tbody data-cut-lab-whatif-delta-body></tbody></table>
      </div>
      <button type="submit" name="intent" value="preview" data-cut-lab-whatif-preview-submit>Preview swap</button>
      <button type="submit" name="intent" value="keep" class="hidden" data-cut-lab-whatif-keep-submit>Keep swap</button>
      <button type="button" class="hidden" data-cut-lab-whatif-discard>Discard preview</button>
    </form>
    <section class="result-panel">
      <div class="cutlab-sticky-bar">
        <span class="cutlab-sticky-bar__round" data-cut-lab-sticky-round>Round 1</span>
        <span class="cutlab-sticky-bar__count" data-cut-lab-sticky-remaining>1 to cut</span>
        <span class="cutlab-sticky-bar__accepted" data-cut-lab-sticky-accepted>1 cut so far</span>
      </div>
    </section>
    <section class="result-panel" data-cut-lab-cuts-made-section="true">
      <details class="cutlab-cuts-made" open>
        <summary>Cuts made · 1 card</summary>
      </details>
    </section>
  `;

  document.dispatchEvent(new Event('DOMContentLoaded'));
};

describe('cut-lab what-if enhancement', () => {
  it('preview does not sync decision state', async () => {
    buildFixture();
    const syncSpy = vi.spyOn(window.DeckFlowCutLab!, 'syncDecisionState');
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        cardOut: 'Working Card',
        cardIn: 'Cut Card',
        deltas: [
          {
            label: 'Commander by turn 3',
            before: 57,
            after: 61,
            delta: 4,
            direction: 'Up',
            isMeaningful: true,
            kind: 'CommanderByTurn',
            unit: 'Percent',
          },
        ],
        changedFamilyCount: 1,
        cutLabStateJson: null,
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form[data-cut-lab-whatif-form]');
    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-whatif-preview-submit]');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushWhatifSubmit();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/cut-lab/whatif');
    expect(fetchMock.mock.calls[0][1]).toEqual(expect.objectContaining({
      method: 'POST',
    }));
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({
      cutLabStateJson: expect.any(String),
      cardOut: 'Working Card',
      cardIn: 'Cut Card',
    });
    expect(syncSpy).not.toHaveBeenCalled();
    expect(document.querySelector('[data-cut-lab-whatif-selection]')?.textContent).toContain('Working Card');
    expect(document.querySelectorAll('[data-cut-lab-whatif-delta-body] tr')).toHaveLength(1);
  });

  it('keep syncs decision state on successful commit', async () => {
    buildFixture();
    const committedStateJson = JSON.stringify({
      commander: 'Commander',
      pool: [],
      packages: [],
      decisions: [
        { cardName: 'Working Card', kind: 'Accepted', round: 'whatif-swap', ordinal: 2 },
      ],
      baselineSnapshot: { metrics: [] },
      roleFloors: [],
      goals: {
        commanderByTurn: 3,
        engineByTurn: 2,
        representativeLineByTurn: 4,
      },
      intent: {
        primaryPlan: 'Stay lean.',
        secondaryPlan: null,
        bracket: 3,
        playExperience: 'Focused',
        includeSideboard: false,
        includeMaybeboard: false,
      },
    });
    const syncSpy = vi.spyOn(window.DeckFlowCutLab!, 'syncDecisionState');
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        cardOut: 'Working Card',
        cardIn: 'Cut Card',
        deltas: [],
        changedFamilyCount: 0,
        cutLabStateJson: committedStateJson,
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form[data-cut-lab-whatif-form]');
    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-whatif-keep-submit]');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushWhatifSubmit();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/cut-lab/whatif/commit');
    expect(syncSpy).toHaveBeenCalledOnce();
    expect(syncSpy).toHaveBeenCalledWith(committedStateJson);
    expect(Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).every(input => input.value === committedStateJson)).toBe(true);
    expect(document.querySelector('[data-cut-lab-sticky-accepted]')?.textContent).toBe('1 cut so far');
    expect(document.querySelector('.cutlab-cuts-made__row')?.textContent).toContain('Working Card');
  });
});
