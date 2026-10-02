import { afterEach, expect, it, vi } from 'vitest';
import { AutomationSoundPlayer } from './AutomationSound';

const command = { executionId: '11111111-1111-4111-8111-111111111111', assetId: 'a'.repeat(64), volume: .8, timeoutSeconds: 5, duckingVolume: .5 };
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); vi.useRealTimers(); });

it('reports actual play and end separately, deduplicates and releases the asset', async () => {
  const report = vi.fn();
  const fetcher = vi.fn().mockResolvedValue(new Response(new Blob(['owned']), { headers: { 'Content-Type': 'audio/wav' } }));
  vi.stubGlobal('fetch', fetcher);
  vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:owned'), revokeObjectURL: vi.fn() });
  const audio = document.createElement('audio');
  vi.spyOn(audio, 'play').mockResolvedValue(); vi.spyOn(audio, 'pause').mockImplementation(() => {}); vi.spyOn(audio, 'load').mockImplementation(() => {});
  const player = new AutomationSoundPlayer('owned-overlay', 'owned-token', report, () => audio);
  await player.play(command);
  expect(audio.volume).toBe(.8);
  expect(report).toHaveBeenCalledWith(command.executionId, 'started');
  expect(report).not.toHaveBeenCalledWith(command.executionId, 'completed');
  audio.dispatchEvent(new Event('ended'));
  expect(report).toHaveBeenCalledWith(command.executionId, 'completed');
  expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:owned');
  await player.play(command);
  expect(fetcher).toHaveBeenCalledTimes(1);
  expect(fetcher.mock.calls[0][1].headers['X-TDSBLive-Overlay']).toBe('owned-overlay');
  player.stop();
});

it('refuses non-audio assets without constructing an audio element', async () => {
  const report = vi.fn();
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('owned', { headers: { 'Content-Type': 'image/png' } })));
  const create = vi.fn();
  await new AutomationSoundPlayer('owned', '', report, create).play(command);
  expect(create).not.toHaveBeenCalled();
  expect(report).toHaveBeenCalledWith(command.executionId, 'failed');
});

it.each(['reject-play', 'audio-error', 'interrupt', 'stop'])('cleans up %s without reporting successful playback', async operation => {
  const report = vi.fn();
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(new Blob(['owned']), { headers: { 'Content-Type': 'audio/wav' } })));
  vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:owned'), revokeObjectURL: vi.fn() });
  const audio = document.createElement('audio');
  const play = vi.spyOn(audio, 'play').mockResolvedValue();
  const pause = vi.spyOn(audio, 'pause').mockImplementation(() => {});
  vi.spyOn(audio, 'load').mockImplementation(() => {});
  if (operation === 'reject-play') play.mockRejectedValue(new Error('Owned autoplay rejection'));
  const player = new AutomationSoundPlayer('owned', '', report, () => audio);
  await player.play(command);
  if (operation === 'audio-error') audio.dispatchEvent(new Event('error'));
  if (operation === 'interrupt') player.interrupt(command.executionId);
  if (operation === 'stop') player.stop();
  const expected = operation === 'interrupt' || operation === 'stop' ? 'interrupted' : 'failed';
  expect(report).toHaveBeenLastCalledWith(command.executionId, expected);
  expect(report).not.toHaveBeenCalledWith(command.executionId, 'completed');
  expect(pause).toHaveBeenCalledOnce();
  expect(URL.revokeObjectURL).toHaveBeenCalledExactlyOnceWith('blob:owned');
  expect(audio.onended).toBeNull(); expect(audio.onerror).toBeNull();
  player.stop(); expect(pause).toHaveBeenCalledOnce();
});

it('does not start late media after the command was interrupted during fetch', async () => {
  let resolve!: (value: Response) => void;
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(done => { resolve = done; })));
  const report = vi.fn(); const create = vi.fn();
  const player = new AutomationSoundPlayer('owned', '', report, create);
  const loading = player.play(command);
  player.interrupt(command.executionId);
  resolve(new Response(new Blob(['owned']), { headers: { 'Content-Type': 'audio/wav' } }));
  await loading;
  expect(create).not.toHaveBeenCalled();
  expect(report).toHaveBeenCalledExactlyOnceWith(command.executionId, 'interrupted');
});

it.each([{ volume: 2 }, { volume: Number.NaN }, { duckingVolume: -1 }, { timeoutSeconds: 1.5 },
  { assetId: '../owned' }, { executionId: '-'.repeat(36) }])('rejects invalid command fields before requesting an asset: %j', async patch => {
  const fetcher = vi.fn(); const report = vi.fn(); vi.stubGlobal('fetch', fetcher);
  await new AutomationSoundPlayer('owned', '', report).play({ ...command, ...patch });
  expect(fetcher).not.toHaveBeenCalled(); expect(report).not.toHaveBeenCalled();
});

it('bounds stalled fetches and does not report completion on timeout', async () => {
  vi.useFakeTimers();
  const report = vi.fn();
  vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})));
  const player = new AutomationSoundPlayer('owned', '', report);
  void player.play(command);
  await vi.advanceTimersByTimeAsync(5000);
  expect(report).toHaveBeenCalledExactlyOnceWith(command.executionId, 'timeout');
  player.stop();
});
