import { tmpdir } from 'node:os';
import { describe, expect, it, vi } from 'vitest';
import { resolveTemporaryDirectory } from '../e2e/support/content-kb-e2e-fixture';

describe('resolveTemporaryDirectory', () => {
  it('uses the local temp directory outside WSL without executing a process', () => {
    const exec = vi.fn();

    expect(resolveTemporaryDirectory({}, exec)).toBe(tmpdir());
    expect(exec).not.toHaveBeenCalled();
  });

  it('uses a non-blank override without executing a process', () => {
    const exec = vi.fn();

    expect(resolveTemporaryDirectory({ WSL_DISTRO_NAME: 'Ubuntu', DECKFLOW_E2E_TEMP_DIR: '  /tmp/e2e  ' }, exec, () => true)).toBe('/tmp/e2e');
    expect(exec).not.toHaveBeenCalled();
  });

  it('retries after cmd.exe throws and returns the converted path', () => {
    const exec = vi.fn()
      .mockImplementationOnce(() => { throw new Error('vsock failed'); })
      .mockReturnValueOnce('C:\\Users\\test\\AppData\\Local\\Temp')
      .mockReturnValueOnce('/mnt/c/Users/test/AppData/Local/Temp');

    expect(resolveTemporaryDirectory({ WSL_DISTRO_NAME: 'Ubuntu' }, exec, () => true, () => undefined)).toBe('/mnt/c/Users/test/AppData/Local/Temp');
    expect(exec.mock.calls.filter(([file]) => file === 'cmd.exe')).toHaveLength(2);
  });

  it('retries unexpanded Windows temp output', () => {
    const exec = vi.fn()
      .mockReturnValueOnce('%TEMP%')
      .mockReturnValueOnce('C:\\Temp')
      .mockReturnValueOnce('/mnt/c/Temp');

    expect(resolveTemporaryDirectory({ WSL_DISTRO_NAME: 'Ubuntu' }, exec, () => true, () => undefined)).toBe('/mnt/c/Temp');
  });

  it('throws after three failed attempts and names the override escape hatch', () => {
    const exec = vi.fn(() => { throw new Error('vsock failed'); });

    expect(() => resolveTemporaryDirectory({ WSL_DISTRO_NAME: 'Ubuntu' }, exec, () => true, () => undefined)).toThrow(/DECKFLOW_E2E_TEMP_DIR.*vsock failed/);
    expect(exec.mock.calls.filter(([file]) => file === 'cmd.exe')).toHaveLength(3);
  });
});
