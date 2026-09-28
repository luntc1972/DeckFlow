import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const fetchStub = vi.fn(() => Promise.resolve({ ok: true, json: () => Promise.resolve({ metricsRevision: '1' }) }));

Object.assign(globalThis, { fetch: fetchStub });
await import('../wwwroot/ts/admin-analytics');

beforeEach(() => {
  vi.useFakeTimers();
  fetchStub.mockClear();
});

afterEach(() => {
  vi.clearAllTimers();
  vi.useRealTimers();
  document.body.innerHTML = '';
});

describe('Admin Analytics poller', () => {
  it('does not bind the legacy class', async () => {
    document.body.innerHTML = '<section class="admin-analytics"></section>';
    document.dispatchEvent(new Event('DOMContentLoaded'));
    await vi.advanceTimersByTimeAsync(15000);
    expect(fetchStub).not.toHaveBeenCalled();
  });

  it('binds the data hook', async () => {
    document.body.innerHTML = '<section data-admin-analytics></section>';
    document.dispatchEvent(new Event('DOMContentLoaded'));
    await vi.advanceTimersByTimeAsync(15000);
    expect(fetchStub).toHaveBeenCalledWith('/Admin/Analytics/status', expect.any(Object));
  });
});
