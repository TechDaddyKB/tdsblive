import { afterEach, expect, it, vi } from 'vitest';
import { createStreamElements } from './StreamElements';

afterEach(() => vi.useRealTimers());
function fixture(config: Record<string, unknown> = {}) {
  let state: unknown = { existing: 7 };
  const emit = vi.fn(); const warn = vi.fn();
  const api = { getConfig: () => config, getSession: () => ({ preview: true, muted: true }),
    store: { get: async () => structuredClone(state), set: async (value: unknown) => { state = structuredClone(value); } } };
  return { shim: createStreamElements(api, emit, warn), emit, warn, state: () => state, api };
}
it('requires opt-in and exposes local load/session/canonical event envelopes without tokens', async () => {
  const { shim, emit } = fixture({ label: 'Owned' });
  shim.load(); shim.event({ type: 'chat.message' }); expect(emit).not.toHaveBeenCalled();
  const se = shim.enable(); shim.load(); shim.session(); shim.event({ type: 'chat.message', message: { text: 'Owned' } });
  expect(emit.mock.calls).toEqual([
    ['onWidgetLoad', { fieldData: { label: 'Owned' }, session: { data: {} }, isEditorMode: true, muted: true }],
    ['onSessionUpdate', { session: { data: {} }, isEditorMode: true, muted: true }],
    ['onEventReceived', { listener: 'message', event: { type: 'chat.message', message: { text: 'Owned' } } }],
  ]);
  expect(await se.getOverlayStatus()).toEqual({ isEditorMode: true, muted: true });
});
it('serializes keyed writes, preserves SBX state and emits only local store notifications', async () => {
  const { shim, state, emit } = fixture(); const se = shim.enable();
  await Promise.all([se.store.set('a', { count: 1 }), se.store.set('b', { count: 2 })]);
  expect(await se.store.get('a')).toEqual({ count: 1 }); expect(await se.store.get('missing')).toBeNull();
  expect(state()).toEqual({ existing: 7, 'se:a': { count: 1 }, 'se:b': { count: 2 } });
  expect(emit).toHaveBeenCalledWith('onEventReceived', { listener: 'kvstore:update', event: { data: { key: 'customWidget.a', value: { count: 1 } } } });
  await expect(se.store.set('../x', {})).rejects.toThrow('store keys');
  await expect(se.store.set('x', [])).rejects.toThrow('JSON objects');
});
it('propagates permission denial and reports unsupported API names descriptively', async () => {
  const { api, emit, warn } = fixture(); api.store.get = async () => { throw new Error('Storage permission denied'); };
  const se = createStreamElements(api, emit, warn).enable();
  await expect(se.store.get('a')).rejects.toThrow('permission denied');
  expect(() => Reflect.get(se, 'counters')).toThrow('counters is unsupported locally');
  expect(warn).toHaveBeenCalledWith(expect.stringContaining('Use the SBX API'));
  expect(() => Reflect.get(se.store, 'delete')).toThrow('store.delete is unsupported locally');
  expect(() => Reflect.get(se, 'private-property')).toThrow('Unknown API call is unsupported locally');
  expect(warn.mock.lastCall?.[0]).not.toContain('private-property');
});
it('bounds storage backlog', async () => {
  const { api, emit, warn } = fixture(); api.store.get = () => new Promise(() => {});
  const se = createStreamElements(api, emit, warn).enable();
  for (let i = 0; i < 8; i++) void se.store.get('a');
  await expect(se.store.get('a')).rejects.toThrow('request limit');
});
it('bounds the local queue, bypasses chat and resumes without touching production automation', () => {
  vi.useFakeTimers(); const { shim, emit, warn } = fixture({ widgetDuration: 2 }); const se = shim.enable();
  shim.event({ type: 'support', id: 0 }); shim.event({ type: 'support', id: 1 });
  shim.event({ type: 'chat.message' }); expect(emit).toHaveBeenCalledTimes(2);
  se.resumeQueue(); expect(emit).toHaveBeenCalledTimes(3);
  for (let i = 0; i < 101; i++) shim.event({ type: 'support', id: i });
  expect(warn).toHaveBeenCalledOnce(); vi.advanceTimersByTime(2000); expect(emit).toHaveBeenCalledTimes(4);
});
