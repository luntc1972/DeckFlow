import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabApi {
  buildCutLabStateJson(snapshot: unknown): string;
}

let api: CutLabApi;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlowCutLab: CutLabApi }).DeckFlowCutLab;
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
  window.localStorage.clear();
  window.sessionStorage.clear();
});

const renderPortableSessionDom = (): void => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab">
      <select id="cut-lab-input-source" name="DeckInputSource">
        <option value="PublicUrl" selected>URL</option>
        <option value="PasteText">Paste</option>
      </select>
      <input id="cut-lab-deck-url" name="DeckUrl" value="https://www.moxfield.com/decks/current" />
      <textarea id="cut-lab-deck-text" name="DeckText">1 Current Card</textarea>
      <input type="hidden" name="CutLabStateJson" value="" />
      <textarea name="PrimaryPlan">Keep the engine package intact.</textarea>
      <textarea name="SecondaryPlan"></textarea>
      <input type="radio" name="Bracket" value="4" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
      <input data-cut-lab-goal="commander" value="3" />
      <input data-cut-lab-goal="engine" value="2" />
      <input data-cut-lab-goal="representative-line" value="4" />
    </form>
    <details id="cut-lab-section-scenarios" open>
      <summary>Scenarios</summary>
      <div class="cutlab-scenarios">
        <div class="cutlab-scenarios__save-row">
          <input id="cut-lab-scenario-name" type="text" data-cut-lab-scenario-name />
          <button type="button" data-cut-lab-scenario-save>Save scenario</button>
        </div>
        <div class="cutlab-scenarios__portable-row">
          <button type="button" data-cut-lab-session-download>Download session</button>
          <label for="cut-lab-session-file">Load session file</label>
          <input id="cut-lab-session-file" type="file" data-cut-lab-session-file accept=".json,application/json" />
        </div>
        <p data-cut-lab-scenario-status></p>
        <div data-cut-lab-scenario-list></div>
      </div>
    </details>
    <table>
      <tbody>
        <tr
          data-cut-lab-card="Atraxa, Praetors' Voice"
          data-cut-lab-quantity="1"
          data-cut-lab-type-line="Legendary Creature - Angel Horror"
          data-cut-lab-role="payoffs"
          data-cut-lab-commander="true">
          <td data-label="Card"><strong>1 × Atraxa, Praetors' Voice</strong></td>
          <td><input type="checkbox" data-cut-lab-lock-card checked /></td>
          <td>
            <select data-cut-lab-package-card>
              <option value="" selected>Unlocked pool</option>
            </select>
          </td>
        </tr>
        <tr
          data-cut-lab-card="Sol Ring"
          data-cut-lab-quantity="1"
          data-cut-lab-type-line="Artifact"
          data-cut-lab-role="ramp"
          data-cut-lab-commander="false">
          <td data-label="Card"><strong>1 × Sol Ring</strong></td>
          <td><input type="checkbox" data-cut-lab-lock-card /></td>
          <td>
            <select data-cut-lab-package-card>
              <option value="" selected>Unlocked pool</option>
            </select>
          </td>
        </tr>
      </tbody>
    </table>
  `;
};

const readBlobAsText = async (blob: Blob): Promise<string> =>
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error ?? new Error('blob-read-failed'));
    reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : '');
    reader.readAsText(blob);
  });

describe('cut-lab portable session controls', () => {
  it('downloads the current serialized session as a JSON file', async () => {
    renderPortableSessionDom();
    document.dispatchEvent(new Event('DOMContentLoaded'));

    Object.defineProperty(URL, 'createObjectURL', {
      configurable: true,
      value: vi.fn(() => 'blob:cutlab-session'),
    });
    Object.defineProperty(URL, 'revokeObjectURL', {
      configurable: true,
      value: vi.fn(),
    });

    const createObjectURLSpy = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:cutlab-session');
    const revokeObjectURLSpy = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    const anchorClickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    document.querySelector<HTMLElement>('[data-cut-lab-session-download]')?.click();

    expect(createObjectURLSpy).toHaveBeenCalledOnce();
    const [blob] = createObjectURLSpy.mock.calls[0] ?? [];
    expect(blob).toBeInstanceOf(Blob);
    const exportedText = await readBlobAsText(blob as Blob);
    const exportedState = JSON.parse(exportedText) as { pool: Array<{ name: string }> };
    expect(exportedState.pool.map(card => card.name)).toEqual(["Atraxa, Praetors' Voice", 'Sol Ring']);
    expect(anchorClickSpy).toHaveBeenCalledOnce();
    expect(revokeObjectURLSpy).toHaveBeenCalledWith('blob:cutlab-session');
  });

  it('loads a valid session file into CutLabStateJson and submits the main form', async () => {
    renderPortableSessionDom();
    document.dispatchEvent(new Event('DOMContentLoaded'));

    const validStateJson = api.buildCutLabStateJson({
      commander: 'Atraxa, Praetors\' Voice',
      pool: [
        {
          name: 'Atraxa, Praetors\' Voice',
          quantity: 1,
          typeLine: 'Legendary Creature - Angel Horror',
          isCommander: true,
          isLocked: true,
          packageId: null,
        },
      ],
      packages: [],
      decisions: [],
      intent: {
        primaryPlan: 'Keep the engine package intact.',
        secondaryPlan: null,
        bracket: 4,
        playExperience: 'Focused',
        includeSideboard: false,
        includeMaybeboard: false,
      },
      roleFloors: [],
      goals: {
        commanderByTurn: 3,
        engineByTurn: 2,
        representativeLineByTurn: 4,
      },
    });

    const form = document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]');
    const requestSubmitSpy = vi.fn();
    Object.defineProperty(form!, 'requestSubmit', {
      configurable: true,
      value: requestSubmitSpy,
    });

    const fileInput = document.querySelector<HTMLInputElement>('[data-cut-lab-session-file]');
    const file = new File([validStateJson], 'cutlab-session.json', { type: 'application/json' });
    Object.defineProperty(fileInput!, 'files', {
      configurable: true,
      value: [file],
    });

    fileInput?.dispatchEvent(new Event('change', { bubbles: true }));
    await vi.waitFor(() => {
      expect(requestSubmitSpy).toHaveBeenCalledOnce();
    });

    expect(document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')?.value).toBe(validStateJson);
  });

  it('shows an error for a malformed session file and does not submit', async () => {
    renderPortableSessionDom();
    document.dispatchEvent(new Event('DOMContentLoaded'));

    const form = document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]');
    const requestSubmitSpy = vi.fn();
    Object.defineProperty(form!, 'requestSubmit', {
      configurable: true,
      value: requestSubmitSpy,
    });

    const fileInput = document.querySelector<HTMLInputElement>('[data-cut-lab-session-file]');
    const badFile = new File(['{"pool":'], 'broken-session.json', { type: 'application/json' });
    Object.defineProperty(fileInput!, 'files', {
      configurable: true,
      value: [badFile],
    });

    fileInput?.dispatchEvent(new Event('change', { bubbles: true }));

    await vi.waitFor(() => {
      expect(document.querySelector<HTMLElement>('[data-cut-lab-scenario-status]')?.textContent).toBe("That file isn't a Cut Lab session.");
    });
    expect(requestSubmitSpy).not.toHaveBeenCalled();
  });
});
