import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/admin-harvest';

const fetchStub = vi.fn();
const reload = vi.fn();
const payload = (isActive: boolean, headline = 'Running', tone = 'info') => ({ state: isActive ? 'running' : 'completed', tone, headline, details: ['Batch complete'], isActive });
const respond = (value: ReturnType<typeof payload>) => ({ ok: true, json: async () => value });
const start = async (active = false) => {
  document.body.innerHTML = `<div id="harvest-maintenance-status" data-maintenance-active="${active}"><p data-maintenance-headline>Previous</p><ul data-maintenance-details></ul></div>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
  await vi.advanceTimersByTimeAsync(0);
};
const submit = (operation: string, checked = true) => {
  document.body.innerHTML = `<form data-maintenance-operation="${operation}" data-maintenance-label="Retention drain"><input type="checkbox" name="confirmed" ${checked ? 'checked' : ''}><button type="submit">Original</button></form>`;
  document.dispatchEvent(new Event('DOMContentLoaded'));
  document.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
  return document.querySelector('button')!;
};

beforeEach(() => {
  vi.useFakeTimers();
  fetchStub.mockReset();
  reload.mockReset();
  vi.stubGlobal('fetch', fetchStub);
  const original = window;
  vi.stubGlobal('window', new Proxy(original, { get: (target, key) => key === 'location' ? { reload } : Reflect.get(target, key) }));
});
afterEach(() => {
  vi.clearAllTimers();
  vi.useRealTimers();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
});

describe('maintenance feedback', () => {
  it('disables a confirmed start with its operator label', () => {
    const button = submit('drain');
    expect(button.disabled).toBe(true);
    expect(button.textContent).toBe('Starting Retention drain…');
  });
  it('leaves an unconfirmed start unchanged', () => {
    const button = submit('drain', false);
    expect(button.disabled).toBe(false);
    expect(button.textContent).toBe('Original');
  });
  it('disables stop and preview with immediate feedback', () => {
    for (const [operation, text] of [['stop', 'Stopping…'], ['preview', 'Counting…']]) {
      const button = submit(operation);
      expect(button.disabled).toBe(true);
      expect(button.textContent).toBe(text);
    }
  });
  it('renders server markup as text and defaults unknown tones to info', async () => {
    fetchStub.mockResolvedValue(respond(payload(false, '<img src=x>', 'unknown')));
    await start();
    expect(document.querySelector('[data-maintenance-headline]')!.textContent).toBe('<img src=x>');
    expect(document.querySelector('img')).toBeNull();
    expect(document.querySelector('#harvest-maintenance-status')!.classList.contains('admin-banner--info')).toBe(true);
    expect(document.querySelector('[data-maintenance-details]')!.textContent).toBe('Batch complete');
  });
  it('polls every 5000 ms while active and stops when inactive', async () => {
    fetchStub.mockResolvedValueOnce(respond(payload(true))).mockResolvedValueOnce(respond(payload(true))).mockResolvedValue(respond(payload(false)));
    await start();
    expect(fetchStub).toHaveBeenCalledTimes(1);
    expect(fetchStub.mock.calls[0][0]).toBe('/Admin/Harvest/maintenance/status');
    await vi.advanceTimersByTimeAsync(4999);
    expect(fetchStub).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1);
    expect(fetchStub).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(5000);
    expect(fetchStub).toHaveBeenCalledTimes(3);
    await vi.advanceTimersByTimeAsync(15000);
    expect(fetchStub).toHaveBeenCalledTimes(3);
  });
  it('reloads exactly once when the server-rendered active state ends', async () => {
    fetchStub.mockResolvedValue(respond(payload(false)));
    await start(true);
    expect(document.querySelector('#harvest-maintenance-status')!.getAttribute('data-maintenance-reloading')).toBe('true');
    expect(reload).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(20000);
    expect(reload).toHaveBeenCalledTimes(1);
    expect(fetchStub).toHaveBeenCalledTimes(1);
  });
  it('keeps the last text on failure and continues polling', async () => {
    fetchStub.mockResolvedValueOnce(respond(payload(true, 'Last progress'))).mockRejectedValueOnce(new Error('offline')).mockResolvedValue(respond(payload(true, 'New progress')));
    await start(true);
    await vi.advanceTimersByTimeAsync(5000);
    expect(document.querySelector('[data-maintenance-headline]')!.textContent).toBe('Last progress');
    await vi.advanceTimersByTimeAsync(5000);
    expect(fetchStub).toHaveBeenCalledTimes(3);
    expect(document.querySelector('[data-maintenance-headline]')!.textContent).toBe('New progress');
  });
});
