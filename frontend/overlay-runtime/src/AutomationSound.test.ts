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

it('reports browser rejection and refuses non-audio assets', async () => {
  const report = vi.fn();
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('owned', { headers: { 'Content-Type': 'image/png' } })));
  const create = vi.fn();
  await new AutomationSoundPlayer('owned', '', report, create).play(command);
  expect(create).not.toHaveBeenCalled();
  expect(report).toHaveBeenCalledWith(command.executionId, 'failed');
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
