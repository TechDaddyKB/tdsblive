import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { ChatConnection } from './ChatConnection';
import { defaultSettings } from './chat';

class Socket {
  static OPEN = 1;
  static instances: Socket[] = [];
  readyState = 1;
  onopen: (() => void) | null = null;
  onclose: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onmessage: ((e: { data: string }) => void) | null = null;
  send = vi.fn();
  constructor(public url: URL, public protocols: string[]) { Socket.instances.push(this); }
  close() { this.readyState = 3; this.onclose?.(); }
  receive(value: object) { this.onmessage?.({ data: JSON.stringify(value) }); }
}
const definition = { id: 'combined-chat', name: 'Chat', chat: defaultSettings };
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status });
let connection: ChatConnection;
beforeEach(() => { vi.useFakeTimers(); Socket.instances = []; vi.stubGlobal('WebSocket', Socket); vi.stubGlobal('fetch', vi.fn(async (url: string) => response(url.endsWith('/chat') ? [] : definition))); });
afterEach(() => { connection?.stop(); vi.useRealTimers(); vi.unstubAllGlobals(); });
it('shares one filtered socket, reloads history after subscribe, handles settings, heartbeat and reconnect', async () => {
  const settings = vi.fn(); const events = vi.fn(); const status = vi.fn();
  connection = new ChatConnection('combined-chat', 'private-session-token', true, settings, events, status); await connection.start();
  const socket = Socket.instances[0]; expect(socket.url.pathname).toBe('/ws/overlay/combined-chat'); expect(socket.url.search).toBe('?preview=1'); expect(socket.url.href).not.toContain('private-session-token');
  expect(socket.protocols).toEqual(['tdsblive.overlay.v1', 'private-session-token']); socket.onopen?.();
  expect(socket.send).toHaveBeenCalledWith(JSON.stringify({ op: 'subscribe', types: ['chat.message'] }));
  socket.receive({ op: 'subscribed' }); await vi.advanceTimersByTimeAsync(1); expect(events).toHaveBeenCalledWith([], 'history');
  socket.receive({ op: 'event', event: { id: 'message' } }); expect(events).toHaveBeenCalledWith([{ id: 'message' }], 'socket');
  socket.receive({ op: 'settings', settings: definition }); expect(settings).toHaveBeenCalledWith(definition);
  await vi.advanceTimersByTimeAsync(15000); expect(socket.send).toHaveBeenCalledWith('{"op":"ping"}'); socket.receive({ op: 'pong' });
  socket.close(); await vi.advanceTimersByTimeAsync(1200); expect(Socket.instances).toHaveLength(2); expect(status).toHaveBeenCalledWith('Reconnecting');
  expect(vi.mocked(fetch).mock.calls[0][1]?.headers).toEqual({ Authorization: 'Bearer private-session-token', 'X-TDSBLive-Overlay': 'combined-chat' });
});
it('rejects unavailable or unauthorized definitions without opening a socket and retries with a cap', async () => {
  vi.mocked(fetch).mockResolvedValue(response({}, 401)); const status = vi.fn();
  connection = new ChatConnection('combined-chat', '', false, vi.fn(), vi.fn(), status); await connection.start();
  expect(status).toHaveBeenCalledWith('Sign in or use an overlay token'); expect(Socket.instances).toHaveLength(0);
  vi.mocked(fetch).mockResolvedValue(response({}, 404)); await vi.advanceTimersByTimeAsync(35_000); expect(status).toHaveBeenCalledWith('Overlay unavailable');
  expect(fetch).toHaveBeenCalledTimes(6);
});
it('survives HTTP errors and malformed events and closes stale sockets', async () => {
  const status = vi.fn(); vi.mocked(fetch).mockRejectedValueOnce(new Error('offline'));
  connection = new ChatConnection('combined-chat', '', false, vi.fn(), vi.fn(), status); await connection.start();
  expect(status).toHaveBeenCalledWith('Reconnecting'); await vi.advanceTimersByTimeAsync(1200);
  const socket = Socket.instances[0]; socket.onopen?.(); socket.onmessage?.({ data: '{' }); expect(status).toHaveBeenCalledWith('Invalid event ignored');
  await vi.advanceTimersByTimeAsync(60_000); expect(socket.readyState).toBe(3);
});
it('reports missing history and stops all retries on disposal', async () => {
  const status = vi.fn(); const pending = new ChatConnection('combined-chat', '', false, vi.fn(), vi.fn(), status); connection = pending;
  await pending.start(); const socket = Socket.instances[0];
  vi.mocked(fetch).mockResolvedValueOnce(response({}, 401)); socket.receive({ op: 'subscribed' }); await vi.advanceTimersByTimeAsync(1);
  expect(status).toHaveBeenCalledWith('Sign in or use an overlay token');
  vi.mocked(fetch).mockRejectedValueOnce(new Error()); socket.receive({ op: 'subscribed' }); await vi.advanceTimersByTimeAsync(1); expect(status).toHaveBeenCalledWith('History unavailable');
  socket.onerror?.(); pending.stop(); const count = Socket.instances.length; await vi.advanceTimersByTimeAsync(100_000); expect(Socket.instances).toHaveLength(count);
});

it('subscribes canvas widgets to filtered normalized events without confusing history with live delivery', async () => {
  vi.mocked(fetch).mockImplementation(async (url: string | URL | Request) => response(String(url).endsWith('/chat') ? [{ id: 'old' }] : { ...definition, canvasEnabled: true }));
  const events = vi.fn(); connection = new ChatConnection('canvas', '', false, vi.fn(), events, vi.fn(), true); await connection.start();
  const socket = Socket.instances[0]; socket.onopen?.(); expect(socket.send).toHaveBeenCalledWith(JSON.stringify({ op: 'subscribe', types: ['*'] }));
  socket.receive({ op: 'subscribed' }); await vi.advanceTimersByTimeAsync(1); expect(events).toHaveBeenCalledWith([{ id: 'old' }], 'history');
  socket.receive({ op: 'event', event: { id: 'fresh' } }); expect(events).toHaveBeenCalledWith([{ id: 'fresh' }], 'socket');
});
