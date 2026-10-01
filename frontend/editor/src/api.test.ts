import { afterEach, expect, it, vi } from 'vitest';
import { api, ApiError } from './api';

afterEach(() => vi.unstubAllGlobals());

it('uses same-origin cookies and CSRF for writes without putting credentials in a URL', async () => {
  const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: 'synthetic-csrf' })))
    .mockResolvedValueOnce(new Response(null, { status: 204 }));
  vi.stubGlobal('fetch', fetch);
  await api.login('synthetic-admin');
  expect(fetch.mock.calls[1][0]).toBe('/api/auth/login');
  expect(fetch.mock.calls[1][1]).toMatchObject({ method: 'POST', credentials: 'same-origin',
    headers: { 'X-TDSBLive-CSRF': 'synthetic-csrf' }, body: JSON.stringify({ credential: 'synthetic-admin' }) });
});

it('reports HTTP failures and refuses writes without a CSRF token', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));
  await expect(api.status()).rejects.toBeInstanceOf(ApiError);
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ requestToken: null }))));
  await expect(api.logout()).rejects.toThrow('request protection');
});

it('loads configuration and saves a restart-required result', async () => {
  const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ displayName: 'Saved' })))
    .mockResolvedValueOnce(new Response(JSON.stringify({ requestToken: 'synthetic-csrf' })))
    .mockResolvedValueOnce(new Response(JSON.stringify({ restartRequired: true })));
  vi.stubGlobal('fetch', fetch);
  const configuration = await api.configuration();
  expect(configuration.displayName).toBe('Saved');
  expect(await api.saveConfiguration(configuration)).toEqual({ restartRequired: true });
});
