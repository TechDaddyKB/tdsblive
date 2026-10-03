import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { CustomFrame } from './CustomFrame';
import { createWidget } from './scene';
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
async function setup(permissions: string[] = ['storage']) {
  const widget = createWidget('custom'); widget.custom!.permissions = permissions;
  const fetcher = vi.fn().mockImplementation(async (url: string, init?: RequestInit) => url.includes('CustomWorker') ? new Response('owned worker') : url === '/api/auth/csrf' ? Response.json({ requestToken: 'owned' }) : init?.method === 'PUT' ? new Response(null, { status: 204 }) : Response.json({ count: 3 }));
  vi.stubGlobal('fetch', fetcher);
  const view = render(<CustomFrame widget={widget} overlay="owned" token="owned-viewing-value" preview deliveries={[]} session={{ connected: true, privateValue: 'private-session' }} />);
  const frame = await screen.findByTitle('Custom') as HTMLIFrameElement;
  const channel = /nonce="([a-f0-9]+)"/.exec(frame.srcdoc)![1]; const send = vi.spyOn(frame.contentWindow!, 'postMessage').mockImplementation(() => {});
  const message = async (data: object, source: MessageEventSource | null = frame.contentWindow, origin = 'null') => {
    await act(async () => window.dispatchEvent(new MessageEvent('message', { source, origin, data: { channel, ...data } })));
  };
  return { widget, fetcher, view, frame, channel, send, message };
}
it('keeps tokens outside the opaque document, routes events/session, and stores through mediated scoped endpoints', async () => {
  const { widget, fetcher, view, frame, send, message } = await setup(); expect(frame.srcdoc).not.toContain('owned-viewing-value'); expect(frame).toHaveAttribute('sandbox', 'allow-scripts');
  expect(frame.srcdoc).not.toContain('private-session');
  await message({ op: 'ready' }); expect(send).toHaveBeenCalledWith(expect.objectContaining({ op: 'session', session: { connected: true, preview: true, muted: true } }), '*');
  view.rerender(<CustomFrame widget={widget} overlay="owned" token="owned-viewing-value" preview deliveries={[{ widgetId: widget.id, event: { type: 'future' } }, { widgetId: 'other', event: {} }]} session={{ connected: false }} />);
  expect(send).toHaveBeenCalledWith(expect.objectContaining({ op: 'event', event: { type: 'future' } }), '*');
  await message({ op: 'store', id: 1, method: 'get' }); await waitFor(() => expect(send).toHaveBeenCalledWith(expect.objectContaining({ op: 'store-result', id: 1, value: { count: 3 } }), '*'));
  expect(fetcher).toHaveBeenCalledWith(`/api/overlays/owned/widgets/${widget.id}/store?preview=1`, expect.objectContaining({ headers: expect.objectContaining({ Authorization: 'Bearer owned-viewing-value' }) }));
  await message({ op: 'store', id: 2, method: 'set', value: { count: 4 } }); await waitFor(() => expect(send).toHaveBeenCalledWith(expect.objectContaining({ op: 'store-result', id: 2, value: { count: 4 } }), '*'));
  expect(fetcher.mock.calls.find(c => c[1]?.method === 'PUT')![1].headers).toMatchObject({ 'X-TDSBLive-CSRF': 'owned' });
  await message({ op: 'store', id: 3, method: 'set', value: [] }); expect(send.mock.lastCall![0]).toMatchObject({ op: 'store-result', error: 'Widget storage unavailable' });
  await message({ op: 'error' }); expect(screen.getByText('Custom widget JavaScript failed')).toBeVisible();
});
it('denies storage without a grant and ignores sibling/incorrect-channel/invalid messages', async () => {
  const { fetcher, send, message } = await setup([]); const count = fetcher.mock.calls.length;
  await message({ op: 'store', id: 1, method: 'get' }); expect(send.mock.lastCall![0]).toMatchObject({ error: 'Storage permission denied or busy' });
  await message({ op: 'store', id: 2, method: 'get' }, window); await message({ channel: 'wrong', op: 'store', id: 2, method: 'get' });
  await message({ op: 'store', id: 'bad', method: 'get' }); await message({ op: 'store', id: 3, method: 'fetch' });
  expect(fetcher).toHaveBeenCalledTimes(count);
});
it('loads declared assets through headers as opaque-origin-safe data URLs and reports failed workers', async () => {
  const widget = createWidget('custom'); widget.custom!.assetIds = ['a'.repeat(64)];
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn().mockReturnValue('blob:owned') }); Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() });
  vi.stubGlobal('fetch', vi.fn().mockImplementation(async () => new Response('owned')));
  const view = render(<CustomFrame widget={widget} overlay="owned" token="" preview={false} deliveries={[]} session={{}} />);
  const frame = await screen.findByTitle('Custom') as HTMLIFrameElement; expect(frame.srcdoc).toMatch(/data:text\/plain;charset=utf-8;base64,b3duZWQ=/i); view.unmount();
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 404 })));
  render(<CustomFrame widget={createWidget('custom')} overlay="owned" token="" preview={false} deliveries={[]} session={{}} />); await screen.findByText('Custom widget unavailable');
});
