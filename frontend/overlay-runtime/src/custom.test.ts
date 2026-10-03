import { afterEach, expect, it, vi } from 'vitest';
import { customCsp, defaultCustom, validFrameMessage } from './custom';
import { frameBootstrap, frameDocument } from './CustomFrame';

afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); vi.useRealTimers(); document.body.replaceChildren(); });
it('defaults to blocked networking, frames, forms and remote scripts, with separate audio permission', () => {
  const csp = customCsp(defaultCustom, 'owned');
  expect(csp).toContain("connect-src 'none'"); expect(csp).toContain("media-src 'none'"); expect(csp).toContain("frame-src 'none'");
  expect(csp).toContain("script-src 'nonce-owned' 'unsafe-eval'"); expect(csp).not.toContain("script-src 'unsafe-inline'");
  const allowed = customCsp({ ...defaultCustom, permissions: ['network', 'audio'], networkDomains: ['example.com', '*.bad.com', '127.0.0.1', 'api.local'] }, 'owned');
  expect(allowed).toContain('connect-src https://example.com'); expect(allowed).not.toContain('bad.com'); expect(allowed).not.toContain('127.0.0.1'); expect(allowed).not.toContain('api.local');
});
it('binds messages to the exact opaque frame, capability and bounded operations', () => {
  const frame = {} as Window; const source = { source: frame, origin: 'null', data: { channel: 'owned', op: 'store', id: 1 } };
  expect(validFrameMessage(source as MessageEvent, frame, 'owned')).toBe(true);
  for (const altered of [{ source: {} }, { origin: location.origin }, { data: { channel: 'stale', op: 'store' } }, { data: { channel: 'owned', op: 'fetch' } }, { data: { channel: 'owned', op: 'store', value: 'x'.repeat(40001) } }])
    expect(validFrameMessage({ ...source, ...altered } as MessageEvent, frame, 'owned')).toBe(false);
  expect(validFrameMessage(source as MessageEvent, null, 'owned')).toBe(false);
});
it('escapes embedded source and includes no same-origin or top-navigation grants', () => {
  const html = frameDocument({ ...defaultCustom, javaScript: '</script><script>bad()</script>' }, 'owned worker', 'capability');
  expect(html.match(/<script/g)).toHaveLength(1); expect(html).toContain('\\u003c/script>'); expect(html).not.toContain('allow-same-origin');
});
it('renders only inert permitted markup and mediates DOM events without exposing parent APIs', () => {
  vi.useFakeTimers(); const posts: unknown[] = [];
  const worker = { onmessage: null as ((e: MessageEvent) => void) | null, postMessage: (v: unknown) => posts.push(v), terminate: vi.fn() };
  vi.stubGlobal('Worker', class { constructor() { return worker; } });
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: () => 'blob:worker' });
  frameBootstrap('owned', defaultCustom, 'worker', { asset: 'blob:owned-image' });
  worker.onmessage!({ data: { op: 'render', html: '<script>escape()</script><iframe src="https://bad.com"></iframe><a href="https://bad.com">escape</a><img src="/api/private" onerror="escape()"><img src="asset"><audio src="asset"></audio><button id="click">Click</button>' } } as MessageEvent);
  expect(document.querySelectorAll('script,iframe,a,audio')).toHaveLength(0); expect(document.querySelectorAll('img')).toHaveLength(2);
  expect(document.querySelector('img')).not.toHaveAttribute('src'); expect(document.querySelectorAll('img')[1]).toHaveAttribute('src', 'blob:owned-image');
  expect(document.querySelector('[onerror]')).toBeNull(); document.getElementById('click')!.click();
  expect(posts).toContainEqual({ op: 'dom-event', type: 'click', id: 'click', value: '' });
  for (let i = 0; i < 61; i++) worker.onmessage!({ data: { op: 'render', html: '<div>bounded</div>' } } as MessageEvent);
  expect(worker.terminate).toHaveBeenCalled();
});
