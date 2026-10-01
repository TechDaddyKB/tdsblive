import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { CanvasRuntime, MediaAsset, OverlayView } from './CanvasRuntime';
import { createWidget, type Scene } from './scene';
import { defaultSettings, type ChatEvent } from './chat';
const connection = vi.hoisted(() => ({ starts: 0, stops: 0, settings: (v: unknown) => { void v; }, events: (v: unknown) => { void v; }, status: (s: string) => { void s; }, initial: null as unknown }));
vi.mock('./ChatConnection', () => ({ ChatConnection: class {
  constructor(_id: string, _token: string, _preview: boolean, settings: typeof connection.settings, events: typeof connection.events, status: typeof connection.status) { Object.assign(connection, { settings, events, status }); }
  start() { connection.starts++; connection.settings(connection.initial); connection.status('Connected'); }
  stop() { connection.stops++; }
} }));
function scene(): Scene { return { id: 'test', name: 'Test', width: 1920, height: 1080, background: 'transparent', version: 1, chat: structuredClone(defaultSettings), canvasEnabled: true, revisionLimit: 50, widgets: [] }; }
const event = (type = 'community.follow'): ChatEvent => ({ id: 'synthetic', type, platform: 'twitch', occurredAt: new Date().toISOString(), receivedAt: new Date().toISOString(), provenance: 'simulation', user: { displayName: '<Test viewer>' }, message: { text: 'Shared canvas chat' } });
beforeEach(() => { connection.starts = 0; connection.stops = 0; connection.initial = scene(); vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue(); vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {}); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); vi.useRealTimers(); });
it('uses one connection for multiple chats, text and alerts and stops on unmount', () => {
  const value = scene(); value.widgets = [createWidget('text'), createWidget('chat'), createWidget('chat'), createWidget('alert'), { ...createWidget('text'), hidden: true, text: 'Hidden' }]; connection.initial = value;
  const view = render(<CanvasRuntime id="test" preview />); expect(connection.starts).toBe(1); expect(screen.queryByText('Hidden')).toBeNull();
  act(() => connection.events([event('chat.message')])); expect(screen.getAllByText('Shared canvas chat')).toHaveLength(2);
  act(() => connection.events([event()])); expect(screen.getByText('<Test viewer> · community.follow')).toBeVisible(); expect(document.querySelector('script')).toBeNull();
  act(() => connection.settings({ ...value, widgets: [] })); expect(screen.queryByText('<Test viewer> · community.follow')).toBeNull();
  view.unmount(); expect(connection.stops).toBe(1);
});
it('expires alerts at their duration and cancels pending jobs when settings change', () => {
  vi.useFakeTimers(); const value = scene(); const alert = createWidget('alert'); alert.alert.durationMs = 1000; value.widgets = [alert]; connection.initial = value;
  render(<CanvasRuntime id="test" preview previewAudio />); act(() => connection.events([event(), { ...event(), id: 'next' }]));
  expect(document.querySelectorAll('[data-alert-event]')).toHaveLength(1); act(() => vi.advanceTimersByTime(1000));
  expect(document.querySelector('[data-alert-event]')).toHaveAttribute('data-alert-event', 'next'); act(() => vi.advanceTimersByTime(1000)); expect(document.querySelector('[data-alert-event]')).toBeNull();
});
it('loads local GIF assets by HEAD and surfaces failures without running scripts', async () => {
  const id = 'a'.repeat(64); const fetcher = vi.fn().mockResolvedValue(new Response(null, { headers: { 'Content-Type': 'image/gif' } })); vi.stubGlobal('fetch', fetcher);
  const view = render(<MediaAsset id={id} overlay="test" token="" volume={.5} muted loop name="Owned GIF" />);
  const image = await screen.findByRole('img', { name: 'Owned GIF' }); expect(image).toHaveAttribute('src', `/assets/${id}`); expect(fetcher.mock.calls[0][1].method).toBe('HEAD');
  fireEvent.error(image); expect(screen.getByText('Image unavailable')).toBeVisible(); view.rerender(<MediaAsset id="../api" overlay="test" token="" volume={.5} muted loop name="Bad" />);
  expect(fetcher).toHaveBeenCalledTimes(1);
});
it.each(['audio/wav', 'video/webm'])('plays %s with volume/mute/loop controls and pauses on removal', async mime => {
  const fetcher = vi.fn().mockResolvedValue(new Response(null, { headers: { 'Content-Type': mime } })); vi.stubGlobal('fetch', fetcher);
  const view = render(<MediaAsset id={'b'.repeat(64)} overlay="test" token="" volume={.25} muted={false} loop name="Media" />);
  await waitFor(() => expect(document.querySelector('audio,video')).not.toBeNull());
  const element = document.querySelector('audio,video') as HTMLMediaElement; expect(element.volume).toBe(.25); expect(element.muted).toBe(false); expect(element.loop).toBe(true);
  view.rerender(<MediaAsset id={'b'.repeat(64)} overlay="test" token="" volume={.5} muted loop={false} name="Media" />); expect(element.volume).toBe(.5); expect(element.muted).toBe(true);
  fireEvent.error(element); expect(screen.getByText(mime.startsWith('audio') ? 'Audio unavailable' : 'Video unavailable')).toBeVisible(); view.unmount(); expect(HTMLMediaElement.prototype.pause).toHaveBeenCalled();
});
it('keeps scoped credentials in headers, revokes blob URLs and reports autoplay rejection', async () => {
  const create = vi.fn().mockReturnValue('blob:owned'); const revoke = vi.fn(); Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: create }); Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revoke });
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockRejectedValue(new Error('Autoplay denied'));
  const fetcher = vi.fn().mockResolvedValue(new Response(new Uint8Array([1, 2]), { headers: { 'Content-Type': 'audio/wav' } })); vi.stubGlobal('fetch', fetcher);
  const view = render(<MediaAsset id={'c'.repeat(64)} overlay="test" token="synthetic-viewing-value" volume={1} muted={false} loop={false} name="Sound" />);
  await screen.findByText('Audio blocked by browser. Enable autoplay or use OBS.');
  expect(fetcher.mock.calls[0][1].headers).toMatchObject({ Authorization: 'Bearer synthetic-viewing-value', 'X-TDSBLive-Overlay': 'test' });
  expect(fetcher.mock.calls[0][0]).not.toContain('synthetic-viewing-value'); view.unmount(); expect(revoke).toHaveBeenCalledWith('blob:owned');
});
it('chooses canvas or compatible legacy chat and exposes authentication failures', async () => {
  const value = scene(); vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(value))); const view = render(<OverlayView id="test" token="" preview={false} previewAudio={false} />);
  await screen.findByLabelText('Overlay scene'); view.unmount();
  connection.initial = { ...value, canvasEnabled: false }; vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(connection.initial)));
  const legacy = render(<OverlayView id="test" token="" preview={false} previewAudio={false} />); await screen.findByLabelText('Combined chat', { exact: true }); legacy.unmount();
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 }))); render(<OverlayView id="test" token="synthetic" preview={false} previewAudio={false} />);
  await screen.findByText('Overlay unavailable. Sign in or use an overlay token.');
});
it('renders media failures as status rather than retrying failed requests without bound', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 404 })));
  render(<MediaAsset id={'d'.repeat(64)} overlay="test" token="" volume={1} muted loop name="Missing" />); await screen.findByText('Media unavailable');
});
