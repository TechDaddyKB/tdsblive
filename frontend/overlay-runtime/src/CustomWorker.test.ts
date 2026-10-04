import { afterEach, expect, it, vi } from 'vitest';

afterEach(() => { vi.unstubAllGlobals(); vi.useRealTimers(); vi.resetModules(); });
async function worker() {
  vi.useFakeTimers(); const send = vi.fn(); const rejection = vi.fn(); const scope: { addEventListener: typeof rejection; onmessage?: (e: MessageEvent) => void; __SBX_RUN?: (...args: unknown[]) => void } = { addEventListener: rejection };
  vi.stubGlobal('self', scope); vi.stubGlobal('postMessage', send); await import('./CustomWorker');
  return { send, rejection, receive: (data: unknown) => {
    const fixture = data as { op: string; javaScript?: string };
    if (fixture.op === 'init' && fixture.javaScript) {
      // Unit fixtures install a program directly; production programs are
      // compiled in a dedicated blob worker, covered by real-browser tests.
      try { scope.__SBX_RUN = new Function('SBX', 'document', 'window', fixture.javaScript) as (...args: unknown[]) => void; } catch { scope.__SBX_RUN = undefined; }
    }
    scope.onmessage!({ data } as MessageEvent);
  } };
}
it('runs custom code with virtual DOM and lifecycle/event/config/session updates', async () => {
  const { send, receive } = await worker();
  receive({ op: 'init', html: '<div id="value">Initial</div>', config: { label: 'Owned' }, session: { connected: true }, javaScript:
    "window.addEventListener('sbx:load', () => document.getElementById('value').textContent = SBX.getConfig().label); SBX.on('future.available', e => document.getElementById('value').textContent = e.message); SBX.on('sbx:session', s => document.getElementById('value').textContent = String(s.connected)); SBX.on('sbx:config', c => SBX.render('<p>'+c.label+'</p>'));" });
  vi.advanceTimersByTime(50); expect(send).toHaveBeenLastCalledWith({ op: 'render', html: '<div id="value">Owned</div>' });
  receive({ op: 'event', event: { type: 'future.available', message: 'Arbitrary available event' } }); vi.advanceTimersByTime(50);
  expect(send).toHaveBeenLastCalledWith({ op: 'render', html: '<div id="value">Arbitrary available event</div>' });
  receive({ op: 'session', session: { connected: false } }); vi.advanceTimersByTime(50);
  expect(send).toHaveBeenLastCalledWith({ op: 'render', html: '<div id="value">false</div>' });
  receive({ op: 'config', config: { label: 'Changed' } }); vi.advanceTimersByTime(50); expect(send).toHaveBeenLastCalledWith({ op: 'render', html: '<p>Changed</p>' });
});
it('mediates asynchronous storage results and reports code/handler failures safely', async () => {
  const { send, receive } = await worker();
  receive({ op: 'init', html: '<div id="value"></div>', config: {}, session: {}, javaScript:
    "SBX.store.get().then(value => document.getElementById('value').textContent = String(value.count)); SBX.on('bad', () => { throw Error('private-detail'); });" });
  expect(send).toHaveBeenCalledWith({ op: 'store', id: 1, method: 'get', value: undefined });
  receive({ op: 'store-result', id: 1, value: { count: 7 } }); await Promise.resolve(); vi.advanceTimersByTime(50);
  expect(send).toHaveBeenLastCalledWith({ op: 'render', html: '<div id="value">7</div>' });
  receive({ op: 'event', event: { type: 'bad' } }); expect(send).toHaveBeenLastCalledWith({ op: 'error', error: 'Widget event handler failed' });
});
it('reports invalid JavaScript without exposing source or credentials', async () => {
  const { send, receive } = await worker(); receive({ op: 'init', html: '', config: {}, session: {}, javaScript: 'this is invalid JavaScript' });
  expect(send).toHaveBeenCalledWith({ op: 'error', error: 'Widget JavaScript failed' });
});

it('reports rejected custom operations visibly without leaking exception text', async () => {
  const { send, rejection } = await worker(); const event = { preventDefault: vi.fn() };
  expect(rejection).toHaveBeenCalledWith('unhandledrejection', expect.any(Function));
  rejection.mock.calls[0][1](event); expect(event.preventDefault).toHaveBeenCalledOnce(); expect(send).toHaveBeenLastCalledWith({ op: 'error', error: 'Widget asynchronous operation failed' });
});
