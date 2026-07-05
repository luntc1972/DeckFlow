import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

// Why (Phase 82 SRP split): dedicated coverage for busy-indicator.ts's progress-step cycling
// (registerBusyIndicator/showBusyIndicator), extracted from deck-sync.ts. Not previously covered by
// busy-overlay-pageshow.test.ts (which only exercises the pageshow-hide path). Loaded via
// deck-sync.ts's bootstrap — the extracted module attaches no listeners of its own until
// bootstrapDeckSync() calls registerBusyIndicator(), matching how every deck-sync-adjacent Vitest
// test in this suite already drives behavior (no exports under tsconfig's `module: "none"`).
document.body.innerHTML = `
  <div id="busy-indicator" class="busy-indicator hidden">
    <div id="busy-indicator-title"></div>
    <div id="busy-indicator-message"></div>
    <div id="busy-indicator-progress"></div>
  </div>
  <form data-busy-title="Working" data-busy-message="Please wait" data-busy-progress="Step one|Step two|Step three">
    <button type="submit" data-no-busy="false">Go</button>
  </form>
`;

await import('../wwwroot/ts/busy-indicator');
await import('../wwwroot/ts/moxfield-extension-bridge');
await import('../wwwroot/ts/deck-sync');

beforeAll(() => {
  document.dispatchEvent(new Event('DOMContentLoaded'));
});

afterEach(() => {
  vi.useRealTimers();
});

describe('busy indicator progress-step cycling', () => {
  it('shows the first step immediately and cycles on the 4s interval, wrapping back to step 1', () => {
    vi.useFakeTimers();

    const form = document.querySelector('form[data-busy-progress]') as HTMLFormElement;
    const progress = document.getElementById('busy-indicator-progress')!;
    const overlay = document.getElementById('busy-indicator')!;

    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    expect(overlay.classList.contains('hidden')).toBe(false);
    expect(progress.textContent).toBe('Step 1/3: Step one');

    vi.advanceTimersByTime(4000);
    expect(progress.textContent).toBe('Step 2/3: Step two');

    vi.advanceTimersByTime(4000);
    expect(progress.textContent).toBe('Step 3/3: Step three');

    // Why: holdFinalStep defaults to false (no data-busy-hold-final-step attribute here), so the
    // cycle wraps back to step 1 rather than parking on the final step.
    vi.advanceTimersByTime(4000);
    expect(progress.textContent).toBe('Step 1/3: Step one');
  });
});
