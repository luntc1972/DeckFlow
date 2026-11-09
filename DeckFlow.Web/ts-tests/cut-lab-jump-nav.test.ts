import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

const fixture = (): void => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab"><input name="CutLabStateJson" /></form>
    <div role="tablist">
      <button role="tab" type="button" data-cut-lab-step="1" aria-selected="false" aria-controls="cut-lab-step-panel-1">Process</button>
      <button role="tab" type="button" data-cut-lab-step="2" aria-selected="true" aria-controls="cut-lab-step-panel-2">Plan</button>
    </div>
    <section id="cut-lab-step-panel-1" role="tabpanel">
      <details id="cut-lab-section-role-floors"><summary>Role floors</summary><h2>Role floors</h2><a data-cut-lab-jump-link href="#cut-lab-section-structural">Findings</a>
        <table><tbody><tr data-cut-lab-floor-row="draw" data-cut-lab-floor-count="10"><td><input data-cut-lab-floor="draw" type="number" value="0" /></td></tr></tbody></table>
      </details><span id="original-next"></span>
      <details id="cut-lab-section-structural"><summary>Structural findings</summary><h2>Structural findings</h2></details>
    </section>
    <section id="cut-lab-step-panel-2" role="tabpanel">
      <button type="button" data-cut-lab-reference-open="cut-lab-section-role-floors">Role floors</button>
      <button type="button" data-cut-lab-recalculate>Recalculate analysis</button>
      <a data-cut-lab-jump-link href="#cut-lab-section-tune">Tune quantities</a>
      <a data-cut-lab-jump-link href="#cut-lab-section-structural">Findings</a>
      <details id="cut-lab-section-tune" data-cutlab-mobile-collapse><summary>Tune quantities</summary></details>
    </section>
    <details id="cut-lab-section-constraints"><summary>Constraints</summary><h2>Constraints</h2></details>
    <dialog id="cut-lab-reference"><button type="button" data-cut-lab-reference-close>Close</button><div data-cut-lab-reference-body></div></dialog>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

beforeEach(() => {
  sessionStorage.clear();
  vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false, addEventListener: vi.fn() })));
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', { configurable: true, value: vi.fn() });
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: vi.fn(function (this: HTMLDialogElement) { this.open = true; }) });
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: vi.fn(function (this: HTMLDialogElement) { this.open = false; this.dispatchEvent(new Event('close')); }) });
});
afterEach(() => { document.body.innerHTML = ''; sessionStorage.clear(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

const dialog = (): HTMLDialogElement => document.querySelector('#cut-lab-reference')!;
const opener = (): HTMLButtonElement => document.querySelector('[data-cut-lab-reference-open]')!;

describe('Cut Lab reference navigation', () => {
  it('reparents the original section, preserves floor listeners and Plan, and restores position and focus', () => {
    fixture();
    const section = document.getElementById('cut-lab-section-role-floors') as HTMLDetailsElement;
    const parent = section.parentNode;
    const next = section.nextSibling;
    opener().focus(); opener().click();
    expect(dialog().contains(section)).toBe(true);
    expect(section.open).toBe(true);
    expect(dialog().showModal).toHaveBeenCalledOnce();
    expect(document.getElementById('cut-lab-step-panel-2')?.hidden).toBe(false);
    expect(HTMLElement.prototype.scrollIntoView).not.toHaveBeenCalled();
    const input = section.querySelector('input')!;
    input.value = '7'; input.dispatchEvent(new Event('change', { bubbles: true }));
    expect(section.querySelector('tr')?.getAttribute('data-cut-lab-floor-user-set')).toBe('true');
    dialog().close();
    expect(section.parentNode).toBe(parent); expect(section.nextSibling).toBe(next);
    expect(document.activeElement).toBe(opener());
  });

  it('preserves scroll and the original footprint of a focusable reference across open and close', () => {
    fixture();
    const section = document.getElementById('cut-lab-section-constraints') as HTMLDetailsElement;
    section.tabIndex = -1;
    const next = section.nextElementSibling!;
    vi.spyOn(section, 'getBoundingClientRect').mockReturnValue({ height: 55 } as DOMRect);
    const scroll = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    Object.defineProperty(window, 'scrollY', { configurable: true, value: 731 });
    opener().dataset.cutLabReferenceOpen = section.id;
    opener().click();
    expect(next.previousElementSibling?.getAttribute('aria-hidden')).toBe('true');
    expect((next.previousElementSibling as HTMLElement).style.height).toBe('55px');
    expect(section.open).toBe(true);
    expect(window.scrollY).toBe(731);
    dialog().close();
    expect(section.nextElementSibling).toBe(next);
    expect(section.open).toBe(false);
    expect(document.activeElement).toBe(opener());
    expect(window.scrollY).toBe(731);
    expect(scroll).not.toHaveBeenCalled();
  });

  it.each(['button', 'Escape', 'backdrop'])('restores the section on %s dismissal', kind => {
    fixture(); opener().click();
    expect(dialog().open).toBe(true);
    if (kind === 'button') document.querySelector<HTMLButtonElement>('[data-cut-lab-reference-close]')!.click();
    if (kind === 'Escape') { dialog().dispatchEvent(new Event('cancel', { cancelable: true })); if (dialog().open) dialog().close(); }
    if (kind === 'backdrop') dialog().dispatchEvent(new MouseEvent('click', { clientX: -1, clientY: -1, bubbles: true }));
    expect(dialog().open).toBe(false);
    expect(document.getElementById('cut-lab-section-role-floors')?.parentElement?.id).toBe('cut-lab-step-panel-1');
  });

  it('expands and scrolls same-panel links while cross-panel links open the dialog', () => {
    fixture();
    document.querySelector<HTMLAnchorElement>('[href="#cut-lab-section-tune"]')!.click();
    expect((document.getElementById('cut-lab-section-tune') as HTMLDetailsElement).open).toBe(true);
    expect(HTMLElement.prototype.scrollIntoView).toHaveBeenCalledOnce();
    vi.mocked(HTMLElement.prototype.scrollIntoView).mockClear();
    document.querySelector<HTMLAnchorElement>('[href="#cut-lab-section-structural"]')!.click();
    expect(dialog().contains(document.getElementById('cut-lab-section-structural'))).toBe(true);
    expect(HTMLElement.prototype.scrollIntoView).not.toHaveBeenCalled();
  });

  it('follows a cross-panel link inside a reference and returns focus to the original opener', () => {
    fixture();
    const floors = document.getElementById('cut-lab-section-role-floors')!;
    opener().click();
    floors.querySelector<HTMLAnchorElement>('a')!.click();
    expect(dialog().contains(document.getElementById('cut-lab-section-structural'))).toBe(true);
    expect(floors.parentElement?.id).toBe('cut-lab-step-panel-1');
    dialog().close();
    expect(document.activeElement).toBe(opener());
  });

  it('saves open reference and scroll before submitting recalculation', () => {
    fixture();
    vi.spyOn(HTMLFormElement.prototype, 'requestSubmit').mockImplementation(() => {});
    Object.defineProperty(window, 'scrollY', { configurable: true, value: 731 });
    opener().click();
    document.querySelector<HTMLButtonElement>('[data-cut-lab-recalculate]')!.click();
    expect(JSON.parse(sessionStorage.getItem('cut-lab-reference-restore')!)).toEqual({ reference: 'cut-lab-section-role-floors', scrollY: 731, savedAt: expect.any(Number) });
  });

  it('restores scroll and reference after four animation frames, then clears storage', () => {
    vi.spyOn(Date, 'now').mockReturnValue(100_000);
    const frames: FrameRequestCallback[] = [];
    vi.stubGlobal('requestAnimationFrame', vi.fn((callback: FrameRequestCallback) => { frames.push(callback); return frames.length; }));
    const scroll = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    sessionStorage.setItem('cut-lab-reference-restore', JSON.stringify({ reference: 'cut-lab-section-role-floors', scrollY: 731, savedAt: Date.now() - 30_000 }));
    fixture();
    for (let i = 0; i < 3; i++) { frames.shift()!(0); expect(scroll).not.toHaveBeenCalled(); }
    frames.shift()!(0);
    expect(scroll).toHaveBeenCalledWith({ top: 731, behavior: 'instant' });
    expect(dialog().open).toBe(true);
    expect(sessionStorage.getItem('cut-lab-reference-restore')).toBeNull();
  });

  it('discards stale reference restoration without scrolling or opening a dialog', () => {
    vi.spyOn(Date, 'now').mockReturnValue(100_000);
    const frames: FrameRequestCallback[] = [];
    vi.stubGlobal('requestAnimationFrame', vi.fn((callback: FrameRequestCallback) => { frames.push(callback); return frames.length; }));
    const scroll = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    sessionStorage.setItem('cut-lab-reference-restore', JSON.stringify({ reference: 'cut-lab-section-role-floors', scrollY: 731, savedAt: Date.now() - 30_001 }));
    fixture();
    while (frames.length) frames.shift()!(0);
    expect(scroll).not.toHaveBeenCalled();
    expect(dialog().open).toBe(false);
    expect(sessionStorage.getItem('cut-lab-reference-restore')).toBeNull();
  });

  it('discards reference restoration without savedAt without scrolling or opening a dialog', () => {
    vi.spyOn(Date, 'now').mockReturnValue(100_000);
    const frames: FrameRequestCallback[] = [];
    vi.stubGlobal('requestAnimationFrame', vi.fn((callback: FrameRequestCallback) => { frames.push(callback); return frames.length; }));
    const scroll = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    sessionStorage.setItem('cut-lab-reference-restore', JSON.stringify({ reference: 'cut-lab-section-role-floors', scrollY: 731 }));
    fixture();
    while (frames.length) frames.shift()!(0);
    expect(scroll).not.toHaveBeenCalled();
    expect(dialog().open).toBe(false);
    expect(sessionStorage.getItem('cut-lab-reference-restore')).toBeNull();
  });

});
