type LocalApi = {
  getConfig(): unknown; getSession(): unknown;
  store: { get(): Promise<unknown>; set(value: unknown): Promise<unknown> };
};
type Dispatch = (type: string, detail: unknown) => void;
const object = (value: unknown): Record<string, unknown> =>
  value !== null && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {};

// This adapter receives only the already permission-filtered SBX data.
export function createStreamElements(api: LocalApi, emit: Dispatch, warn: (message: string) => void) {
  let enabled = false;
  let tail: Promise<unknown> = Promise.resolve();
  let pendingOperations = 0;
  let held = false;
  let timer: ReturnType<typeof setTimeout> | undefined;
  const queue: unknown[] = [];
  const unsupported = (name: string): never => {
    const publicName = ['counters', 'sanitize', 'cheerFilter', 'setField', 'store.delete', 'store.remove'].includes(name) ? name : 'Unknown API call';
    const message = `StreamElements compatibility: ${publicName} is unsupported locally. Use the SBX API or migrate this call.`;
    warn(message); throw new Error(message);
  };
  const keyName = (key: string) => {
    if (typeof key !== 'string' || !/^[a-zA-Z0-9_-]{1,64}$/.test(key)) throw new Error('Local SE store keys must be 1–64 letters, digits, underscores or hyphens.');
    return `se:${key}`;
  };
  const serial = <T>(work: () => Promise<T>) => {
    if (pendingOperations >= 8) return Promise.reject(new Error('Local SE storage request limit reached.'));
    pendingOperations++;
    const result = tail.then(work).finally(() => { pendingOperations--; });
    tail = result.catch(() => undefined); return result;
  };
  const duration = () => Math.min(60, Math.max(0, Number(object(api.getConfig()).widgetDuration) || 0));
  const deliver = (detail: unknown) => {
    const seconds = duration();
    if (seconds > 0) { held = true; timer = setTimeout(resume, seconds * 1000); }
    emit('onEventReceived', detail);
  };
  function resume() {
    if (timer !== undefined) clearTimeout(timer);
    held = false; timer = undefined;
    const next = queue.shift(); if (next !== undefined) deliver(next);
  }
  const status = () => { const session = object(api.getSession()); return { isEditorMode: session.preview === true, muted: session.muted !== false }; };
  const store = Object.freeze({
    get: (key: string) => serial(async () => structuredClone(object(await api.store.get())[keyName(key)] ?? null)),
    set: (key: string, value: unknown) => serial(async () => {
      const name = keyName(key);
      if (value === null || typeof value !== 'object' || Array.isArray(value)) throw new Error('Local SE store values must be JSON objects.');
      const state = object(await api.store.get());
      await api.store.set({ ...state, [name]: structuredClone(value) });
      emit('onEventReceived', { listener: 'kvstore:update', event: { data: { key: `customWidget.${key}`, value: structuredClone(value) } } });
    }),
  });
  const guardedStore = new Proxy(store, { get(target, property) {
    if (Object.hasOwn(target, property)) return Reflect.get(target, property);
    return unsupported(`store.${String(property)}`);
  } });
  const supported = Object.freeze({ store: guardedStore, getOverlayStatus: () => Promise.resolve(status()), resumeQueue: resume });
  const SE_API = new Proxy(supported, { get(target, property) {
    if (Object.hasOwn(target, property)) return Reflect.get(target, property);
    return unsupported(String(property));
  } });
  return {
    enable() { enabled = true; return SE_API; },
    load() { if (enabled) emit('onWidgetLoad', { fieldData: structuredClone(api.getConfig()), session: { data: {} }, ...status() }); },
    session() { if (enabled) emit('onSessionUpdate', { session: { data: {} }, ...status() }); },
    event(value: unknown) {
      if (!enabled) return;
      const event = object(value);
      const type = typeof event.type === 'string' ? event.type : 'unknown';
      const detail = { listener: type === 'chat.message' ? 'message' : type, event: structuredClone(event) };
      if (detail.listener === 'message') emit('onEventReceived', detail);
      else if (!held) deliver(detail);
      else if (queue.length < 100) queue.push(detail);
      else warn('StreamElements compatibility: local queue limit reached; event dropped.');
    },
  };
}
