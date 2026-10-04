import { parseHTML } from 'linkedom';
import { createStreamElements } from './StreamElements';

// User code never receives the browser Document/Window. A worker cannot navigate
// a frame, access cookies, parent DOM, storage, or the owning page's credentials.
const virtual = parseHTML('<html><head></head><body></body></html>');
const document = virtual.document;
const listeners = new Map<string, ((value: unknown) => void)[]>();
const pending = new Map<number, { resolve: (value: unknown) => void; reject: (error: Error) => void }>();
let sequence = 0; let config: unknown = {}; let session: unknown = {};
let previous = ''; let initialized = false;
self.addEventListener('unhandledrejection', event => { event.preventDefault(); postMessage({ op: 'error', error: 'Widget asynchronous operation failed' }); });
const dispatch = (type: string, value: unknown) => {
  for (const handler of listeners.get(type) ?? []) { try { handler(value); } catch { postMessage({ op: 'error', error: 'Widget event handler failed' }); } }
  virtual.dispatchEvent(new virtual.CustomEvent(type, { detail: value }));
};
const SBX = Object.freeze({
  enableStreamElements: () => compatibility.enable(),
  on(type: string, handler: (value: unknown) => void) {
    if (typeof type !== 'string' || type.length > 128 || typeof handler !== 'function' || !listeners.has(type) && listeners.size >= 128) throw new Error('Invalid event handler');
    const handlers = listeners.get(type) ?? []; if (handlers.length >= 32) throw new Error('Handler limit reached');
    listeners.set(type, [...handlers, handler]); return () => listeners.set(type, (listeners.get(type) ?? []).filter(h => h !== handler));
  },
  getConfig: () => structuredClone(config), getSession: () => structuredClone(session),
  store: Object.freeze({
    get: () => request('get'), set: (value: unknown) => request('set', value),
  }),
  render: (html: string) => { if (typeof html !== 'string' || html.length > 131072) throw new Error('Render limit exceeded'); document.body.innerHTML = html; },
});
const compatibility = createStreamElements(SBX, dispatch, error => postMessage({ op: 'error', error }));
function request(method: string, value?: unknown): Promise<unknown> {
  if (pending.size >= 8) return Promise.reject(new Error('Storage request limit reached'));
  const id = ++sequence;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject }); postMessage({ op: 'store', id, method, value });
    setTimeout(() => { if (pending.delete(id)) reject(new Error('Storage request timed out')); }, 10000);
  });
}
self.onmessage = (message: MessageEvent) => {
  const data = message.data;
  if (data.op === 'init' && !initialized) {
    initialized = true; config = data.config; session = data.session; document.body.innerHTML = data.html;
    try {
      // The supplied blob program is compiled by the browser only in this
      // network-restricted worker. No eval/Function capability is granted.
      const execute = (self as unknown as { __SBX_RUN?: (api: typeof SBX, dom: typeof document, view: typeof document.defaultView) => void }).__SBX_RUN;
      if (!execute) throw new Error('Widget program unavailable');
      execute(SBX, document, document.defaultView);
      dispatch('sbx:load', { config, session });
      compatibility.load();
      setInterval(() => { const html = document.body.innerHTML; if (html !== previous && html.length <= 131072) { previous = html; postMessage({ op: 'render', html }); } }, 50);
    } catch { postMessage({ op: 'error', error: 'Widget JavaScript failed' }); }
  }
  if (data.op === 'event') { dispatch(data.event.type, data.event); dispatch('sbx:event', data.event); compatibility.event(data.event); }
  if (data.op === 'session') { session = data.session; dispatch('sbx:session', session); compatibility.session(); }
  if (data.op === 'config') { config = data.config; dispatch('sbx:config', config); }
  if (data.op === 'store-result') {
    const result = pending.get(data.id); pending.delete(data.id);
    if (data.error) result?.reject(new Error(data.error)); else result?.resolve(data.value);
  }
  if (data.op === 'dom-event') {
    const element = document.getElementById(data.id);
    if (element) { if ('value' in element) element.value = data.value; element.dispatchEvent(new virtual.Event(data.type, { bubbles: true })); }
  }
};
