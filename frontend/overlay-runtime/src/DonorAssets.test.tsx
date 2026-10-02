import { cleanup, renderHook, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { useDonorAsset, useDonorFont } from './DonorAssets';
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });
it('loads scoped media without a credential URL and releases its object URL', async () => {
  const fetcher = vi.fn().mockResolvedValue(new Response(new Uint8Array([1]), { headers: { 'Content-Type': 'image/png' } }));
  vi.stubGlobal('fetch', fetcher);
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:owned-crown') });
  const revoke = vi.fn(); Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revoke });
  const hook = renderHook(() => useDonorAsset('a'.repeat(64), 'image/', 'owned-overlay', 'owned-test-token'));
  await waitFor(() => expect(hook.result.current).toBe('blob:owned-crown'));
  expect(fetcher.mock.calls[0][0]).toBe(`/assets/${'a'.repeat(64)}`);
  expect(fetcher.mock.calls[0][1].headers).toEqual({ Authorization: 'Bearer owned-test-token', 'X-TDSBLive-Overlay': 'owned-overlay' });
  hook.unmount(); expect(revoke).toHaveBeenCalledWith('blob:owned-crown');
});
it('rejects paths and incorrect asset families without displaying them', async () => {
  const fetcher = vi.fn().mockResolvedValue(new Response(null, { headers: { 'Content-Type': 'text/html' } })); vi.stubGlobal('fetch', fetcher);
  const hook = renderHook(({ id }) => useDonorAsset(id, 'font/', 'owned', ''), { initialProps: { id: '../private' } });
  expect(fetcher).not.toHaveBeenCalled();
  hook.rerender({ id: 'b'.repeat(64) }); await waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
  expect(hook.result.current).toBeNull(); expect(fetcher.mock.calls[0][1].method).toBe('HEAD');
});
it('registers a downloaded font and removes it when configuration changes', async () => {
  const add = vi.fn(); const remove = vi.fn();
  Object.defineProperty(document, 'fonts', { configurable: true, value: { add, delete: remove } });
  class OwnedFont {
    constructor(public family: string, public source: string) {}
    load() { return Promise.resolve(this); }
  }
  vi.stubGlobal('FontFace', OwnedFont);
  const hook = renderHook(({ url }) => useDonorFont(url, 'owned-widget'), { initialProps: { url: 'blob:owned-font' as string | null } });
  await waitFor(() => expect(hook.result.current).toBe('tdsblive-donor-ownedwidget'));
  expect(add).toHaveBeenCalledOnce(); expect(add.mock.calls[0][0].source).toBe('url("blob:owned-font")');
  hook.rerender({ url: null }); expect(hook.result.current).toBeNull(); expect(remove).toHaveBeenCalledOnce();
});
it('keeps the fallback font when a downloaded font cannot load', async () => {
  const add = vi.fn(); Object.defineProperty(document, 'fonts', { configurable: true, value: { add, delete: vi.fn() } });
  const load = vi.fn().mockRejectedValue(new Error('Owned invalid font'));
  vi.stubGlobal('FontFace', class { load = load; });
  const hook = renderHook(() => useDonorFont('blob:invalid-font', 'owned-widget'));
  await waitFor(() => expect(load).toHaveBeenCalledOnce()); expect(hook.result.current).toBeNull(); expect(add).not.toHaveBeenCalled();
});
